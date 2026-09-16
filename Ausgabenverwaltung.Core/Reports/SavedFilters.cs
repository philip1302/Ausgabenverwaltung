using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Die Liste der gespeicherten Filter und die Regeln darauf: anlegen,
/// ersetzen, loeschen, sortieren, und die Pruefung des Namens.
///
/// Steht in Core und nicht in den beiden ViewModels, weil Ausgabenliste
/// und Auswertung sich dieselbe Liste teilen und sich sonst in Kleinig-
/// keiten unterscheiden wuerden - dieselbe Begruendung wie bei
/// <see cref="FilterChips"/>, und pruefbar ist es obendrein (Regel 7).
/// </summary>
public static class SavedFilters
{
    /// <summary>
    /// Mehr Filter passen nicht in eine Liste, die man ueberfliegen
    /// koennen soll. Die Grenze ist keine technische, sondern genau das:
    /// wer dreissig gespeicherte Filter durchsucht, sucht laenger als er
    /// die Haekchen neu gesetzt haette.
    /// </summary>
    public const int MaxCount = 20;

    /// <summary>
    /// Laenger wird der Name in der Liste ohnehin abgeschnitten. Der Wert
    /// ist grosszuegig genug fuer "Auto und Versicherung, dieses Jahr".
    /// </summary>
    public const int MaxNameLength = 40;

    // Sortiert und verglichen wird nach deutschen Regeln: die Liste steht
    // vor einem deutschen Anwender, und "Ärzte" gehoert dort zu "A" und
    // nicht hinter "Z". Fest verdrahtet wie bei den Betraegen (Regel 1)
    // und nicht ueber die Kultur des Rechners, damit die Reihenfolge
    // ueberall dieselbe ist.
    private static readonly StringComparer Vergleich = Kultur.NamensVergleich;

    /// <summary>
    /// Prueft den eingegebenen Namen. NULL = in Ordnung, sonst der Text,
    /// der am Feld steht (Regel 13: Feldfehler gehoeren ans Feld).
    ///
    /// Ein bereits vergebener Name ist ausdruecklich KEIN Fehler - er
    /// ersetzt den vorhandenen Eintrag (siehe <see cref="Save"/>). Das
    /// ist der uebliche Weg, einen Filter nachzubessern: Haekchen
    /// umstellen, denselben Namen noch einmal vergeben.
    /// </summary>
    public static string? Validate(string? name, IReadOnlyList<SavedFilter> existing)
    {
        var getrimmt = name?.Trim() ?? string.Empty;

        if (getrimmt.Length == 0)
        {
            return "Bitte einen Namen vergeben, unter dem der Filter wiederzufinden ist.";
        }

        if (getrimmt.Length > MaxNameLength)
        {
            return $"Der Name ist zu lang — höchstens {MaxNameLength} Zeichen.";
        }

        // Die Obergrenze gilt nur fuer einen NEUEN Namen. Einen
        // vorhandenen Filter zu ueberschreiben muss auch dann noch gehen,
        // wenn die Liste voll ist - sonst waere die letzte freie Stelle
        // eine Falle.
        if (existing.Count >= MaxCount && Find(existing, getrimmt) is null)
        {
            return $"Es sind bereits {MaxCount} Filter gespeichert. "
                 + "Bitte zuerst einen davon löschen.";
        }

        return null;
    }

    /// <summary>
    /// Legt den Filter ab und liefert die neue Liste. Ein Eintrag mit
    /// demselben Namen (Gross-/Kleinschreibung egal) wird ersetzt, sonst
    /// kommt der Filter hinzu. Sortiert wird immer nach Namen.
    ///
    /// Der Name wird dabei getrimmt uebernommen - so, wie er in der Liste
    /// stehen wird, und nicht so, wie er getippt wurde.
    /// </summary>
    public static IReadOnlyList<SavedFilter> Save(
        IReadOnlyList<SavedFilter> existing, SavedFilter filter)
    {
        var neu = filter with { Name = filter.Name.Trim() };

        var liste = existing
            .Where(vorhanden => !Vergleich.Equals(vorhanden.Name, neu.Name))
            .Append(neu)
            .OrderBy(eintrag => eintrag.Name, Vergleich)
            .ToList();

        return liste;
    }

    /// <summary>
    /// Nimmt den Filter mit diesem Namen heraus. Ein unbekannter Name
    /// aendert nichts - geloescht ist geloescht, auch zweimal.
    /// </summary>
    public static IReadOnlyList<SavedFilter> Remove(
        IReadOnlyList<SavedFilter> existing, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return existing;
        }

        var gesucht = name.Trim();

        return existing
            .Where(eintrag => !Vergleich.Equals(eintrag.Name, gesucht))
            .ToList();
    }

    /// <summary>Der Filter mit diesem Namen, oder NULL.</summary>
    public static SavedFilter? Find(IReadOnlyList<SavedFilter> existing, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var gesucht = name.Trim();

        return existing.FirstOrDefault(eintrag => Vergleich.Equals(eintrag.Name, gesucht));
    }

    /// <summary>
    /// Bringt eine eingelesene Liste in denselben Zustand, in dem
    /// <see cref="Save"/> sie hinterlaesst: ohne namenlose Eintraege, ohne
    /// Doppelgaenger, sortiert und auf <see cref="MaxCount"/> gekuerzt.
    ///
    /// Gebraucht beim Laden der Einstellungsdatei - die kann von Hand
    /// bearbeitet worden sein, und eine kaputte Zeile darin darf weder
    /// die Liste noch den Start verderben (siehe
    /// <see cref="Settings.AppSettingsStore"/>).
    /// </summary>
    public static IReadOnlyList<SavedFilter> Normalize(IEnumerable<SavedFilter> gelesen)
    {
        var liste = new List<SavedFilter>();

        foreach (var eintrag in gelesen)
        {
            var name = eintrag.Name?.Trim() ?? string.Empty;

            if (name.Length == 0 || name.Length > MaxNameLength)
            {
                continue;
            }

            // Der erste Eintrag eines Namens gewinnt; ein zweiter waere
            // in der Liste nicht auseinanderzuhalten.
            if (Find(liste, name) is not null)
            {
                continue;
            }

            liste.Add(eintrag with { Name = name });

            if (liste.Count == MaxCount)
            {
                break;
            }
        }

        return liste.OrderBy(eintrag => eintrag.Name, Vergleich).ToList();
    }
}
