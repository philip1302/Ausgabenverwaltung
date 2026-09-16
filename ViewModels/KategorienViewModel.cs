using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Verwaltung -> Kategorien": Baumansicht mit Anlegen,
/// Umbenennen, Archivieren/Wiederherstellen, Verschieben, Loeschen und
/// Zusammenfuehren. Die eigentliche Fachlogik (Duplikat-Pruefung,
/// Kaskade, Sortierung, Verwendungspruefung, Umhaengen in einer
/// Transaktion) steckt komplett in <see cref="CategoryRepository"/>
/// (Regel 7) - hier wird nur der DB-Baum in UI-Knoten
/// (<see cref="KategorieKnoten"/>) uebersetzt, auf Anwenderaktionen
/// reagiert und der Text der Nachfragen gebaut.
/// </summary>
public sealed partial class KategorienViewModel : ViewModelBase
{
    private readonly CategoryRepository _categoryRepository;
    private readonly BackupService _backupService;
    private readonly IMessenger _messenger;

    public ObservableCollection<KategorieKnoten> Wurzelknoten { get; } = new();

    /// <summary>
    /// Es gibt ueberhaupt keine Kategorie - auch keine archivierte. Das
    /// ist der allererste Start: das Schema saet keine Kategorien
    /// (docs/schema_v4.sql), und ohne Kategorie laesst sich nichts
    /// erfassen. Der Leerzustand nennt deshalb nicht nur das Fehlen,
    /// sondern den naechsten Schritt.
    /// </summary>
    [ObservableProperty]
    private bool _nochKeineKategorie;

    /// <summary>
    /// Es gibt Kategorien, aber alle sind archiviert und damit
    /// ausgeblendet. Andere Lage als <see cref="NochKeineKategorie"/>
    /// und deshalb ein anderer Satz: hier fehlt nichts, es ist nur
    /// nichts zu sehen - wie "kein Treffer trotz Daten" im Report.
    /// </summary>
    [ObservableProperty]
    private bool _nurArchivierteVorhanden;

    /// <summary>
    /// Die waehlbaren Farben: die feste Palette aus Core und zusaetzlich
    /// "keine eigene Farbe". Kein freier Farbwaehler - siehe
    /// <see cref="CategoryColorPalette"/>.
    /// </summary>
    public IReadOnlyList<FarbOption> Farboptionen { get; } =
        new[] { FarbOption.Keine() }
            .Concat(CategoryColorPalette.Colors.Select(FarbOption.Aus))
            .ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KnotenBestaetigtAusgewaehlt))]
    [NotifyPropertyChangedFor(nameof(AusgewaehlterKnotenIstArchiviert))]
    [NotifyCanExecuteChangedFor(nameof(NeueUnterkategorieCommand))]
    private KategorieKnoten? _ausgewaehlterKnoten;

    [ObservableProperty]
    private bool _archivierteAnzeigen;

    /// <summary>
    /// "19 Kategorien, 4 Ebenen" im Kopf der Seite (UI/UX-Redesign,
    /// Abschnitt 5.5, mockups/06-verwaltung-kategorien.png) - reine
    /// Anzeigezusammenfassung, wird bei jedem Baumaufbau neu berechnet.
    /// </summary>
    [ObservableProperty]
    private string _uebersichtText = string.Empty;

    /// <summary>
    /// Ein Schreibfehler bei einem Vorgang am Baum - archivieren,
    /// loeschen, umsortieren, einfaerben. Als Band ueber dem Baum. Der
    /// Baum selbst bleibt dabei unveraendert stehen: was nicht
    /// geschrieben wurde, wird auch nicht angezeigt.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SchreibFehlerSichtbar))]
    private string? _schreibFehlerText;

    public bool SchreibFehlerSichtbar => SchreibFehlerText is not null;

    [RelayCommand]
    private void SchreibFehlerSchliessen() => SchreibFehlerText = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArchivierungAnfrageAktiv))]
    [NotifyPropertyChangedFor(nameof(ArchivierungAnfrageText))]
    private KategorieKnoten? _archivierungAnfrageKnoten;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArchivierungAnfrageText))]
    private int _archivierungAnfrageAnzahlUnterkategorien;

    /// <summary>Ob ein bereits gespeicherter (nicht gerade erst per
    /// "Neue ..." angelegter, noch unbestaetigter) Knoten ausgewaehlt
    /// ist - Voraussetzung fuer Umbenennen/Archivieren/Verschieben.</summary>
    public bool KnotenBestaetigtAusgewaehlt => AusgewaehlterKnoten is { IstNeuUndUnbestaetigt: false };

    public bool AusgewaehlterKnotenIstArchiviert => AusgewaehlterKnoten?.IsArchived ?? false;

    public bool ArchivierungAnfrageAktiv => ArchivierungAnfrageKnoten is not null;

    public string ArchivierungAnfrageText => ArchivierungAnfrageKnoten is null
        ? string.Empty
        : $"\"{ArchivierungAnfrageKnoten.Name}\" hat {ArchivierungAnfrageAnzahlUnterkategorien} " +
          (ArchivierungAnfrageAnzahlUnterkategorien == 1 ? "Unterkategorie" : "Unterkategorien") +
          ". Sollen diese mit archiviert werden?";

    public KategorienViewModel(CategoryRepository categoryRepository, BackupService backupService, IMessenger messenger)
    {
        _categoryRepository = categoryRepository;
        _backupService = backupService;
        _messenger = messenger;
        LadeBaum();
    }

    partial void OnArchivierteAnzeigenChanged(bool value) => LadeBaum();

    /// <summary>
    /// Knopf des Leerzustands "alles archiviert". Setzt denselben
    /// Schalter, den die Werkzeugleiste traegt - der Leerzustand bietet
    /// keinen zweiten Weg an, sondern den vorhandenen.
    /// </summary>
    [RelayCommand]
    private void ArchivierteEinblenden() => ArchivierteAnzeigen = true;

    [RelayCommand]
    private void NeueOberkategorie()
    {
        var knoten = new KategorieKnoten(id: null, parentId: null, name: string.Empty, isArchived: false, ausgabenAnzahl: 0);
        Wurzelknoten.Add(knoten);

        // Der Leerzustand muss JETZT weichen und nicht erst beim naechsten
        // LadeBaum: die neue Zeile wartet auf ihren Namen, und das
        // Eingabefeld dafuer steckt im Baum. Bliebe der Leerzustand
        // stehen, haette der Anwender auf "Kategorie anlegen" gedrueckt
        // und saehe unveraendert denselben Satz.
        NochKeineKategorie = false;
        NurArchivierteVorhanden = false;

        AusgewaehlterKnoten = knoten;
        StarteBearbeitung(knoten);
    }

    [RelayCommand(CanExecute = nameof(KnotenBestaetigtAusgewaehlt))]
    private void NeueUnterkategorie()
    {
        var eltern = AusgewaehlterKnoten;
        if (eltern is null)
        {
            return;
        }

        var knoten = new KategorieKnoten(id: null, parentId: eltern.Id, name: string.Empty, isArchived: false, ausgabenAnzahl: 0)
        {
            Eltern = eltern,
        };

        eltern.IsExpanded = true;
        eltern.Children.Add(knoten);
        AusgewaehlterKnoten = knoten;
        StarteBearbeitung(knoten);
    }

    [RelayCommand]
    private void BearbeitenStarten(KategorieKnoten? knoten)
    {
        if (knoten is null || knoten.WirdBearbeitet)
        {
            return;
        }

        StarteBearbeitung(knoten);
    }

    [RelayCommand]
    private void BearbeitenUebernehmen(KategorieKnoten? knoten)
    {
        if (knoten is null || !knoten.WirdBearbeitet)
        {
            return;
        }

        var name = knoten.BearbeitungsText.Trim();
        if (name.Length == 0)
        {
            knoten.BearbeitungsFehler = "Bitte einen Namen eingeben.";
            return;
        }

        try
        {
            if (knoten.IstNeuUndUnbestaetigt)
            {
                var erstellt = _categoryRepository.Create(name, knoten.ParentId);
                knoten.UebernehmeErstellteId(erstellt.Id);
            }
            else
            {
                _categoryRepository.Rename(knoten.Id!.Value, name);
            }
        }
        catch (DuplicateCategoryNameException)
        {
            knoten.BearbeitungsFehler =
                $"Es gibt hier bereits eine Kategorie namens „{name}“. "
                + "Bitte einen anderen Namen wählen — zwei gleichnamige Kategorien "
                + "unter demselben Elternteil wären in den Auswertungen nicht "
                + "auseinanderzuhalten.";
            return;
        }
        catch (Exception ex)
        {
            // Schreibfehler: der Name bleibt im Bearbeitungsfeld stehen,
            // damit er nicht noch einmal getippt werden muss.
            knoten.BearbeitungsFehler = Schreibvorgang.Beschreibe(
                "Beim Speichern eines Kategorienamens", ex);
            return;
        }

        knoten.Name = name;
        knoten.WirdBearbeitet = false;
        knoten.BearbeitungsFehler = null;
        LadeBaum();
    }

    [RelayCommand]
    private void BearbeitenAbbrechen(KategorieKnoten? knoten)
    {
        if (knoten is null || !knoten.WirdBearbeitet)
        {
            return;
        }

        if (knoten.IstNeuUndUnbestaetigt)
        {
            EntferneKnoten(knoten);
        }
        else
        {
            knoten.WirdBearbeitet = false;
            knoten.BearbeitungsFehler = null;
        }
    }

    [RelayCommand]
    private void Archivieren(KategorieKnoten? knoten)
    {
        if (knoten?.Id is not int id)
        {
            return;
        }

        var unterkategorien = _categoryRepository.GetDescendantIds(id);
        if (unterkategorien.Count > 0)
        {
            ArchivierungAnfrageKnoten = knoten;
            ArchivierungAnfrageAnzahlUnterkategorien = unterkategorien.Count;
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Archivieren einer Kategorie",
            () => _categoryRepository.Archive(id, includeDescendants: false));

        LadeBaum();
    }

    [RelayCommand]
    private void ArchivierungNurDieseBestaetigen()
    {
        if (ArchivierungAnfrageKnoten?.Id is not int id)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Archivieren einer Kategorie ohne ihre Unterkategorien",
            () => _categoryRepository.Archive(id, includeDescendants: false));

        ArchivierungAnfrageKnoten = null;
        LadeBaum();
    }

    [RelayCommand]
    private void ArchivierungMitUnterkategorienBestaetigen()
    {
        if (ArchivierungAnfrageKnoten?.Id is not int id)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Archivieren einer Kategorie samt Unterkategorien",
            () => _categoryRepository.Archive(id, includeDescendants: true));

        ArchivierungAnfrageKnoten = null;
        LadeBaum();
    }

    [RelayCommand]
    private void ArchivierungAbbrechen()
    {
        ArchivierungAnfrageKnoten = null;
    }

    // ================= Loeschen =================
    //
    // Regel 8: Archivieren bleibt der Normalfall. Geloescht wird nur, was
    // vollstaendig unbenutzt ist; alles andere fuehrt in eine Erklaerung
    // mit den beiden moeglichen Wegen (archivieren oder zusammenfuehren).

    /// <summary>Die Sicherheitsabfrage vor dem Loeschen einer unbenutzten
    /// Kategorie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoeschAnfrageAktiv))]
    [NotifyPropertyChangedFor(nameof(LoeschAnfrageText))]
    private KategorieKnoten? _loeschAnfrageKnoten;

    public bool LoeschAnfrageAktiv => LoeschAnfrageKnoten is not null;

    public string LoeschAnfrageText => LoeschAnfrageKnoten is null
        ? string.Empty
        : $"Die Kategorie \"{LoeschAnfrageKnoten.Name}\" wird nirgends verwendet und kann " +
          "endgültig gelöscht werden. Das lässt sich nicht rückgängig machen.";

    /// <summary>Die Erklaerung, warum eine benutzte Kategorie nicht
    /// geloescht werden kann.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoeschHindernisAktiv))]
    private KategorieKnoten? _loeschHindernisKnoten;

    [ObservableProperty]
    private string _loeschHindernisText = string.Empty;

    /// <summary>
    /// Ob von hier aus zusammengefuehrt werden kann. Eine Quelle MIT
    /// Unterkategorien scheidet aus - was mit denen geschehen soll, ist
    /// bei jeder einzelnen eine eigene Entscheidung (siehe
    /// <see cref="CategoryHasChildrenException"/>).
    /// </summary>
    [ObservableProperty]
    private bool _zusammenfuehrenMoeglich;

    public bool LoeschHindernisAktiv => LoeschHindernisKnoten is not null;

    [RelayCommand]
    private void Loeschen(KategorieKnoten? knoten)
    {
        if (knoten?.Id is not int id)
        {
            return;
        }

        SchliesseNachfragen();

        var verwendung = _categoryRepository.GetUsage(id);
        if (verwendung.IsUnused)
        {
            LoeschAnfrageKnoten = knoten;
            return;
        }

        LoeschHindernisKnoten = knoten;
        LoeschHindernisText =
            $"\"{knoten.Name}\" wird verwendet: {BeschreibeVerwendung(verwendung)}. " +
            "Löschen ist deshalb nicht möglich.";
        ZusammenfuehrenMoeglich = verwendung.ChildCount == 0;
    }

    [RelayCommand]
    private void LoeschenBestaetigen()
    {
        if (LoeschAnfrageKnoten?.Id is not int id)
        {
            return;
        }

        // Zwischen Nachfrage und Bestaetigung kann die Kategorie benutzt
        // worden sein. Das Repository prueft erneut - hier faellt die
        // Antwort darauf nur wieder in die Erklaerung zurueck, statt die
        // Ausnahme durchschlagen zu lassen.
        try
        {
            _categoryRepository.Delete(id);
        }
        catch (CategoryInUseException ex)
        {
            var knoten = LoeschAnfrageKnoten;
            LoeschAnfrageKnoten = null;
            LoeschHindernisKnoten = knoten;
            LoeschHindernisText =
                $"„{ex.Name}“ wird inzwischen verwendet: {BeschreibeVerwendung(ex.Usage)}. " +
                "Löschen ist deshalb nicht möglich. Es wurde nichts verändert.";
            ZusammenfuehrenMoeglich = ex.Usage.ChildCount == 0;
            return;
        }
        catch (Exception ex)
        {
            // Schreibfehler statt Verwendungshindernis: die Nachfrage
            // bleibt offen, damit sich der Versuch wiederholen laesst.
            SchreibFehlerText = Schreibvorgang.Beschreibe(
                "Beim Loeschen einer Kategorie", ex);
            return;
        }

        LoeschAnfrageKnoten = null;
        AusgewaehlterKnoten = null;
        LadeBaum();
    }

    /// <summary>
    /// Der empfohlene Weg aus der Erklaerung heraus. Fuehrt in den
    /// gewoehnlichen Archivierungs-Ablauf - inklusive der Nachfrage nach
    /// den Unterkategorien, falls es welche gibt.
    /// </summary>
    [RelayCommand]
    private void HindernisArchivieren()
    {
        var knoten = LoeschHindernisKnoten;
        SchliesseNachfragen();

        if (knoten is not null)
        {
            Archivieren(knoten);
        }
    }

    [RelayCommand]
    private void LoeschenAbbrechen() => SchliesseNachfragen();

    // ================= Zusammenfuehren =================

    /// <summary>Die Kategorie, die aufgeloest werden soll.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZusammenfuehrenAktiv))]
    [NotifyPropertyChangedFor(nameof(ZusammenfuehrenTitel))]
    private KategorieKnoten? _zusammenfuehrenQuelle;

    public bool ZusammenfuehrenAktiv => ZusammenfuehrenQuelle is not null;

    public string ZusammenfuehrenTitel => ZusammenfuehrenQuelle is null
        ? string.Empty
        : $"\"{ZusammenfuehrenQuelle.Name}\" zusammenführen";

    /// <summary>Derselbe Baum wie sonst, nur sind hier ausschliesslich
    /// Blattknoten waehlbar (siehe <see cref="KategorieZielKnoten"/>).</summary>
    public ObservableCollection<KategorieZielKnoten> ZielWurzeln { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZielText))]
    [NotifyPropertyChangedFor(nameof(ZusammenfuehrenBereit))]
    private KategorieZielKnoten? _zusammenfuehrenZiel;

    public string ZielText => ZusammenfuehrenZiel?.FullPath ?? "Zielkategorie wählen…";

    public bool ZusammenfuehrenBereit => ZusammenfuehrenZiel is not null;

    /// <summary>Was der Vorgang bewegen wuerde - die einzige Gelegenheit,
    /// einen Irrtum vor einem nicht umkehrbaren Schritt zu bemerken.</summary>
    [ObservableProperty]
    private string _zusammenfuehrenVorschauText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZusammenfuehrenFehlerSichtbar))]
    private string? _zusammenfuehrenFehler;

    public bool ZusammenfuehrenFehlerSichtbar => !string.IsNullOrEmpty(ZusammenfuehrenFehler);

    [RelayCommand]
    private void ZusammenfuehrenStarten()
    {
        var quelle = LoeschHindernisKnoten;
        if (quelle?.Id is not int quelleId || !ZusammenfuehrenMoeglich)
        {
            return;
        }

        SchliesseNachfragen();

        ZusammenfuehrenQuelle = quelle;
        BaueZielbaum(quelleId);
    }

    /// <summary>
    /// Uebernimmt die gewaehlte Zielkategorie und holt die Vorschau. Nur
    /// waehlbare Knoten kommen hier an - die uebrigen sind in der Ansicht
    /// nicht anklickbar.
    /// </summary>
    [RelayCommand]
    private void ZielWaehlen(KategorieZielKnoten? ziel)
    {
        if (ziel is null || !ziel.IstWaehlbar || ZusammenfuehrenQuelle?.Id is not int quelleId)
        {
            return;
        }

        ZusammenfuehrenZiel = ziel;
        ZusammenfuehrenFehler = null;

        var vorschau = _categoryRepository.PreviewMerge(quelleId, ziel.Id);

        ZusammenfuehrenVorschauText =
            $"{Zaehle(vorschau.ExpenseCount, "Ausgabe", "Ausgaben")} und " +
            $"{Zaehle(vorschau.RecurringExpenseCount, "Vorlage", "Vorlagen")} " +
            $"werden nach \"{ziel.FullPath}\" umgehängt. " +
            $"Betroffene Summe: {EuroText.Format(vorschau.SumCents)}. " +
            $"Danach wird \"{ZusammenfuehrenQuelle.Name}\" gelöscht. " +
            "Das lässt sich nicht rückgängig machen. " +
            "Vorher wird automatisch eine Sicherung angelegt.";
    }

    [RelayCommand]
    private void ZusammenfuehrenBestaetigen()
    {
        if (ZusammenfuehrenQuelle?.Id is not int quelleId || ZusammenfuehrenZiel is not { } ziel)
        {
            return;
        }

        // Erst sichern, dann bewegen. Scheitert die Sicherung, wird auch
        // nicht zusammengefuehrt: ein nicht umkehrbarer Schritt ohne Netz
        // ist genau das, was die Sicherung verhindern soll.
        // Lokale Zeit wie bei jeder Sicherung - der Dateiname soll zum
        // Kalendertag des Anwenders passen.
        var sicherung = _backupService.RunNow(DateTime.Now);
        if (sicherung.NeedsAttention)
        {
            ZusammenfuehrenFehler =
                "Vor dem Zusammenführen wird automatisch gesichert, weil sich der "
                + "Vorgang nicht rückgängig machen lässt. Genau diese Sicherung ist "
                + "fehlgeschlagen.\n\n"
                + FileErrorText.ForBackup(sicherung.PrimaryProblem) + "\n\n"
                + "Zusammengeführt wurde deshalb nichts — es ist alles unverändert.";
            return;
        }

        try
        {
            _categoryRepository.Merge(quelleId, ziel.Id);
        }
        catch (CategoryHasChildrenException ex)
        {
            // Zwischen Vorschau und Bestaetigung kann eine Unterkategorie
            // entstanden sein.
            ZusammenfuehrenFehler = ex.Message;
            return;
        }
        catch (Exception ex)
        {
            // Merge laeuft vollstaendig in EINER Transaktion (Regel 8) -
            // ein Fehler dabei hinterlaesst keine halb umgehaengten
            // Ausgaben. Die Auswahl bleibt stehen, der Versuch laesst sich
            // wiederholen; die Sicherung von eben liegt bereits.
            ZusammenfuehrenFehler = Schreibvorgang.Beschreibe(
                "Beim Zusammenfuehren von Kategorien", ex);
            return;
        }

        SchliesseNachfragen();
        AusgewaehlterKnoten = null;
        LadeBaum();

        // Der Merge haengt Ausgaben auf eine andere Kategorie um - andere
        // Farbe/Pfad ueberall, wo diese Ausgaben angezeigt werden (Regel 14).
        _messenger.Send(new BuchungenGeaendertNachricht());
    }

    [RelayCommand]
    private void ZusammenfuehrenAbbrechen() => SchliesseNachfragen();

    /// <summary>
    /// Raeumt alle Nachfragen und Baender weg. Sie schliessen einander
    /// aus: es geht immer um dieselbe Kategorie, und zwei gleichzeitig
    /// offene Nachfragen dazu waeren nicht zu beantworten.
    /// </summary>
    private void SchliesseNachfragen()
    {
        LoeschAnfrageKnoten = null;
        LoeschHindernisKnoten = null;
        LoeschHindernisText = string.Empty;
        ZusammenfuehrenMoeglich = false;

        ZusammenfuehrenQuelle = null;
        ZusammenfuehrenZiel = null;
        ZusammenfuehrenVorschauText = string.Empty;
        ZusammenfuehrenFehler = null;
        ZielWurzeln.Clear();

        // Auch der Schreibfehler von vorhin: er gehoerte zu dem Vorgang,
        // der gerade weggeraeumt wird.
        SchreibFehlerText = null;
    }

    /// <summary>
    /// Zaehlt auf, was der Kategorie im Weg steht - ohne die Posten mit
    /// der Zahl Null, die nur Rauschen waeren.
    /// </summary>
    private static string BeschreibeVerwendung(CategoryUsage verwendung)
    {
        var teile = new List<string>();

        if (verwendung.ExpenseCount > 0)
        {
            teile.Add(Zaehle(verwendung.ExpenseCount, "Ausgabe", "Ausgaben"));
        }

        if (verwendung.ChildCount > 0)
        {
            teile.Add(Zaehle(verwendung.ChildCount, "Unterkategorie", "Unterkategorien"));
        }

        if (verwendung.RecurringExpenseCount > 0)
        {
            teile.Add(Zaehle(verwendung.RecurringExpenseCount, "Vorlage", "Vorlagen"));
        }

        return string.Join(", ", teile);
    }

    private static string Zaehle(int anzahl, string einzahl, string mehrzahl) =>
        $"{anzahl} {(anzahl == 1 ? einzahl : mehrzahl)}";

    /// <summary>
    /// Baut den Baum der Zielauswahl. Alle Kategorien bleiben sichtbar,
    /// waehlbar sind aber nur nicht-archivierte Blattknoten ausser der
    /// Quelle selbst - genau die Kategorien, denen sich eine Ausgabe auch
    /// sonst zuordnen laesst.
    /// </summary>
    private void BaueZielbaum(int quelleId)
    {
        ZielWurzeln.Clear();

        foreach (var knoten in BaueZielknoten(_categoryRepository.GetTree(), parentPath: null, quelleId))
        {
            ZielWurzeln.Add(knoten);
        }
    }

    private static List<KategorieZielKnoten> BaueZielknoten(
        IReadOnlyList<CategoryNode> nodes, string? parentPath, int quelleId)
    {
        var ergebnis = new List<KategorieZielKnoten>();

        foreach (var node in nodes)
        {
            var pfad = CategoryPaths.Append(parentPath, node.Category.Name);

            var waehlbar =
                node.Children.Count == 0 &&
                !node.Category.IsArchived &&
                node.Category.Id != quelleId;

            var knoten = new KategorieZielKnoten(
                node.Category.Id, node.Category.Name, pfad, node.Category.IsArchived, waehlbar);

            foreach (var kind in BaueZielknoten(node.Children, pfad, quelleId))
            {
                knoten.Children.Add(kind);
            }

            ergebnis.Add(knoten);
        }

        return ergebnis;
    }

    /// <summary>
    /// Merkt sich, fuer welche Kategorie die Farbwahl geoeffnet wurde.
    ///
    /// Die laufende Baumauswahl taugt dafuer nicht: das Aufklappfenster
    /// der Farbwahl nimmt den Fokus, und der TreeView gibt dabei seine
    /// Auswahl her (dieselbe Eigenheit ist in AusgabenlisteView bereits
    /// vermerkt). Haengte die Farbwahl daran, stuenden ihre Eintraege
    /// im Moment des Oeffnens grau da und ein Klick liefe ins Leere.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FarbwahlHinweis))]
    private KategorieKnoten? _farbwahlKnoten;

    /// <summary>Ueberschrift der Farbwahl: welche Kategorie und welche
    /// Farbe sie gerade hat.</summary>
    public string FarbwahlHinweis => FarbwahlKnoten is { } knoten
        ? $"{knoten.Name} — {knoten.FarbHinweis}"
        : string.Empty;

    /// <summary>
    /// Wird beim Oeffnen der Farbwahl aufgerufen und haelt den Knoten
    /// fest, um den es geht - direkt die Zeile, in der "Farbe…" gewaehlt
    /// wurde (UI/UX-Redesign, Abschnitt 5.5: Aktionen sitzen jetzt in der
    /// Zeile selbst statt an einer Werkzeugleiste mit vorheriger
    /// Baumauswahl zu haengen).
    /// </summary>
    public void OeffneFarbwahlFuer(KategorieKnoten knoten) => FarbwahlKnoten = knoten;

    /// <summary>
    /// Setzt die Farbe der Kategorie, fuer die die Farbwahl geoeffnet
    /// wurde, oder nimmt sie zurueck. Danach wird der Baum neu geladen:
    /// die Farbe wirkt auf den ganzen Ast darunter, und der soll sich
    /// sofort mit umfaerben.
    ///
    /// Ohne CanExecute: die Eintraege der Farbwahl sind immer benutzbar,
    /// geoeffnet wird sie ohnehin nur mit ausgewaehlter Kategorie.
    /// </summary>
    [RelayCommand]
    private void FarbeSetzen(FarbOption? option)
    {
        var knoten = FarbwahlKnoten ?? AusgewaehlterKnoten;
        if (option is null || knoten?.Id is not int id)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Setzen einer Kategoriefarbe",
            () => _categoryRepository.SetColor(id, option.Hex));

        FarbwahlKnoten = null;
        LadeBaum();
    }

    [RelayCommand]
    private void Wiederherstellen(KategorieKnoten? knoten)
    {
        if (knoten?.Id is not int id)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Wiederherstellen einer Kategorie",
            () => _categoryRepository.Restore(id));

        LadeBaum();
    }

    [RelayCommand]
    private void NachObenVerschieben(KategorieKnoten? knoten)
    {
        if (knoten?.Id is not int id)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Verschieben einer Kategorie nach oben",
            () => _categoryRepository.MoveUp(id));

        LadeBaum();
    }

    [RelayCommand]
    private void NachUntenVerschieben(KategorieKnoten? knoten)
    {
        if (knoten?.Id is not int id)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Verschieben einer Kategorie nach unten",
            () => _categoryRepository.MoveDown(id));

        LadeBaum();
    }

    private static void StarteBearbeitung(KategorieKnoten knoten)
    {
        knoten.BearbeitungsText = knoten.Name;
        knoten.BearbeitungsFehler = null;
        knoten.WirdBearbeitet = true;
    }

    private void EntferneKnoten(KategorieKnoten knoten)
    {
        var geschwister = knoten.Eltern?.Children ?? Wurzelknoten;
        geschwister.Remove(knoten);

        if (ReferenceEquals(AusgewaehlterKnoten, knoten))
        {
            AusgewaehlterKnoten = knoten.Eltern;
        }
    }

    private void LadeBaum()
    {
        var ausgewaehlteId = AusgewaehlterKnoten?.Id;

        var baum = _categoryRepository.GetTree();
        var anzahlen = _categoryRepository.GetExpenseCounts();

        // Aufgeloeste Farben fuer den ganzen Baum: der Punkt vor einem
        // Namen zeigt die Farbe, die tatsaechlich gilt - eigene oder
        // geerbte.
        var farben = CategoryColors.Resolve(baum);

        Wurzelknoten.Clear();
        foreach (var knoten in BaueKnoten(baum, anzahlen, farben, eltern: null))
        {
            Wurzelknoten.Add(knoten);
        }

        AusgewaehlterKnoten = ausgewaehlteId is int id ? FindeKnoten(Wurzelknoten, id) : null;

        var anzahlGesamt = 0;
        var maxEbene = 0;
        ZaehleBaum(Wurzelknoten, ebene: 1, ref anzahlGesamt, ref maxEbene);

        // Gezaehlt wird zweimal: sichtbar (Wurzelknoten, ohne die vom
        // Schalter ausgeblendeten archivierten) und roh (baum, mit
        // allen). Erst der Vergleich unterscheidet "es gibt noch
        // nichts" von "es ist nur gerade nichts zu sehen".
        var anzahlMitArchivierten = ZaehleRoh(baum);
        NochKeineKategorie = anzahlMitArchivierten == 0;
        NurArchivierteVorhanden = anzahlGesamt == 0 && anzahlMitArchivierten > 0;

        UebersichtText =
            $"{anzahlGesamt} {(anzahlGesamt == 1 ? "Kategorie" : "Kategorien")}, " +
            $"{maxEbene} {(maxEbene == 1 ? "Ebene" : "Ebenen")}";
    }

    /// <summary>
    /// Zaehlt den Baum, wie er aus der Datenbank kommt - einschliesslich
    /// der archivierten Kategorien, die der Schalter ausblendet.
    /// </summary>
    private static int ZaehleRoh(IEnumerable<CategoryNode> knoten) =>
        knoten.Sum(k => 1 + ZaehleRoh(k.Children));

    private static void ZaehleBaum(IEnumerable<KategorieKnoten> knoten, int ebene, ref int anzahl, ref int maxEbene)
    {
        foreach (var kandidat in knoten)
        {
            anzahl++;
            maxEbene = Math.Max(maxEbene, ebene);
            ZaehleBaum(kandidat.Children, ebene + 1, ref anzahl, ref maxEbene);
        }
    }

    private List<KategorieKnoten> BaueKnoten(
        IReadOnlyList<CategoryNode> nodes,
        IReadOnlyDictionary<int, int> anzahlen,
        IReadOnlyDictionary<int, string> farben,
        KategorieKnoten? eltern)
    {
        var ergebnis = new List<KategorieKnoten>();

        foreach (var node in nodes)
        {
            var anzahl = anzahlen.TryGetValue(node.Category.Id, out var count) ? count : 0;
            var knoten = new KategorieKnoten(
                node.Category.Id, node.Category.ParentId, node.Category.Name, node.Category.IsArchived, anzahl)
            {
                Eltern = eltern,
                EigeneFarbe = node.Category.Color,
                Farbe = Farbpinsel.Fuer(CategoryColors.Of(farben, node.Category.Id)),
            };

            var kinder = BaueKnoten(node.Children, anzahlen, farben, knoten);

            // Ein komplett archivierter Ast (Knoten selbst archiviert und
            // keine sichtbaren Kinder uebrig) wird ausgeblendet, solange
            // der Schalter "Archivierte anzeigen" aus ist.
            if (node.Category.IsArchived && !ArchivierteAnzeigen && kinder.Count == 0)
            {
                continue;
            }

            foreach (var kind in kinder)
            {
                knoten.Children.Add(kind);
            }

            ergebnis.Add(knoten);
        }

        return ergebnis;
    }

    private static KategorieKnoten? FindeKnoten(IEnumerable<KategorieKnoten> knoten, int id)
    {
        foreach (var kandidat in knoten)
        {
            if (kandidat.Id == id)
            {
                return kandidat;
            }

            var gefunden = FindeKnoten(kandidat.Children, id);
            if (gefunden is not null)
            {
                return gefunden;
            }
        }

        return null;
    }
}
