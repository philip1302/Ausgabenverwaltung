using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Ausgabenverwaltung.Views;

public partial class ReportView : UserControl
{
    /// <summary>
    /// Um wie viel die Kategoriespalte der Kreuztabelle nach rechts
    /// zurueckgeschoben wird, damit sie beim waagerechten Schieben an
    /// ihrem Platz stehen bleibt: genau um die Bildlaufposition.
    ///
    /// EIN gemeinsames Objekt fuer Kopfzeile, alle Kategoriezeilen und
    /// die Summenzeile - und deshalb hier im Code statt als Bindung mit
    /// Konverter in der Ansicht. Eine Bindung je Zeile hat bei jedem
    /// Bildlaufschritt fuer jede sichtbare Zeile ein neues
    /// Transform-Objekt erzeugt und deren Darstellung einzeln fuer
    /// ungueltig erklaert; das flackerte sichtbar. So aendert sich pro
    /// Schritt genau ein Wert, und die ganze Spalte wandert in einem
    /// Stueck.
    ///
    /// Eine Verschiebung veraendert nur die Darstellung, nicht die
    /// Anordnung - die Spalte belegt weiterhin ihren Platz in der Zeile.
    /// </summary>
    public TranslateTransform SpaltenVersatz { get; } = new();

    /// <summary>
    /// Ob waagerecht ueberhaupt geschoben ist. Nur dann bekommt die
    /// stehenbleibende Spalte ihre Trennlinie nach rechts: ungeschoben
    /// gehoert sie sichtbar zur Tabelle, erst darunter wegwandernde
    /// Werte machen die Kante noetig.
    /// </summary>
    public static readonly StyledProperty<bool> SpalteVerschobenProperty =
        AvaloniaProperty.Register<ReportView, bool>(nameof(SpalteVerschoben));

    public bool SpalteVerschoben
    {
        get => GetValue(SpalteVerschobenProperty);
        set => SetValue(SpalteVerschobenProperty, value);
    }

    public ReportView()
    {
        InitializeComponent();

        // Am Offset selbst hoeren und nicht am ScrollChanged-Ereignis:
        // die Verschiebung wird dadurch noch im selben Durchgang gesetzt,
        // in dem der Bildlauf gemeldet wird - Spalte und Werte bewegen
        // sich im selben Bild, ohne Nachziehen um einen Frame.
        Kreuztabelle.PropertyChanged += (_, e) =>
        {
            if (e.Property != ScrollViewer.OffsetProperty)
            {
                return;
            }

            var versatz = Kreuztabelle.Offset.X;

            SpaltenVersatz.X = AufGanzePunkteGerundet(versatz);
            SpalteVerschoben = versatz > 0;
        };
    }

    /// <summary>
    /// Rundet einen Versatz auf ganze Bildschirmpunkte - so, wie es die
    /// Anordnung mit dem geschobenen Inhalt auch tut.
    ///
    /// Ohne diese Rundung zappelt die stehenbleibende Spalte beim
    /// Schieben um ein bis zwei Pixel hin und her: Avalonia setzt den
    /// Inhalt bei <c>UseLayoutRounding</c> (Vorgabe) auf ganze Punkte,
    /// <c>Offset.X</c> bleibt aber ungerundet. Schoeben wir um den
    /// ungerundeten Betrag zurueck, bliebe je Schritt genau diese
    /// Differenz als sichtbarer Rest stehen. Mit derselben Rundung auf
    /// beiden Seiten heben sich Hin- und Rueckweg exakt auf.
    ///
    /// Gerundet wird in Geraetepunkten, nicht in Layoutpunkten: bei
    /// einer Anzeigeskalierung von z. B. 1,5 liegt das Pixelraster
    /// entsprechend feiner. "Von der Null weg" ist dieselbe Regel, die
    /// die Anordnung anwendet - dadurch stimmt das Ergebnis auch fuer
    /// den negativen Weg des Inhalts.
    /// </summary>
    private double AufGanzePunkteGerundet(double versatz)
    {
        var skalierung = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

        // Ohne Fenster (Entwurfsansicht) bleibt der Wert, wie er ist.
        return skalierung <= 0
            ? versatz
            : Math.Round(versatz * skalierung, MidpointRounding.AwayFromZero) / skalierung;
    }

    // Dateiauswahl und Schreiben sind Aufgabe der Oberflaeche; der
    // CSV-Text selbst entsteht in Core (Reports.ReportCsv), und der Text
    // einer Fehlermeldung ebenfalls (Errors.FileErrorText) - Regel 7.
    private async void CsvExportieren_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReportViewModel viewModel)
        {
            return;
        }

        // Der Dateiname wird vor dem Versuch gemerkt: nach einem Fehler
        // soll die Meldung sagen, WELCHE Datei nicht ging, und dann steht
        // das Ziel unter Umstaenden nicht mehr zur Verfuegung.
        var dateiname = viewModel.CsvDateiname;

        try
        {
            dateiname = await ExportiereCsv(viewModel) ?? dateiname;
        }
        catch (Exception ex)
        {
            // Ein fehlgeschlagener Export darf die Anwendung nicht
            // beenden - der Hinweis steht neben der Schaltflaeche.
            //
            // Der mit Abstand haeufigste Fall ist die noch in Excel
            // geoeffnete Zieldatei. Er bekommt deshalb einen eigenen
            // Hinweis statt einer Meldung ueber "Sharing violation".
            AppLog.Current.Exception("Beim CSV-Export", ex);

            viewModel.MeldeExport(
                FileErrorText.ForCsvExport(StorageProblems.Classify(ex), dateiname));
        }
    }

    // Liefert den tatsaechlich gewaehlten Dateinamen, damit eine
    // Fehlermeldung ihn nennen kann - oder NULL, wenn abgebrochen wurde.
    private async Task<string?> ExportiereCsv(ReportViewModel viewModel)
    {
        var fenster = TopLevel.GetTopLevel(this);
        if (fenster is null)
        {
            return null;
        }

        var ziel = await fenster.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Kreuztabelle als CSV speichern",
            SuggestedFileName = viewModel.CsvDateiname,
            DefaultExtension = "csv",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CSV-Datei")
                {
                    Patterns = new[] { "*.csv" },
                },
            },
        });

        if (ziel is null)
        {
            return null;
        }

        var text = viewModel.BaueCsv();

        await using var strom = await ziel.OpenWriteAsync();

        // Eine vorhandene Datei wird ueberschrieben, nicht ueberlagert -
        // sonst blieben Reste einer laengeren Vorgaengerdatei stehen.
        strom.SetLength(0);

        // UTF-8 MIT Signatur: ohne sie zeigt Excel im deutschen
        // Gebietsschema Umlaute falsch an.
        await using var schreiber = new StreamWriter(
            strom, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await schreiber.WriteAsync(text);
        await schreiber.FlushAsync();

        viewModel.MeldeExport($"Gespeichert: {ziel.Name}");

        return ziel.Name;
    }
}
