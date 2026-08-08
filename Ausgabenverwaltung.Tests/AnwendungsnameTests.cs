using System.Xml.Linq;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der Anwendungsname, den macOS im Menue oben links zeigt
/// (<c>Avalonia.Application.Name</c>, gesetzt in App.axaml).
///
/// <b>Warum dieser Test die XAML-QUELLE liest und nicht die geladene
/// Anwendung:</b> Um an <c>Application.Current.Name</c> zu kommen, muesste
/// App.axaml geladen werden, und das verlangt eine Fensterplattform -
/// ohne sie scheitert schon das Laden an
/// <c>Unable to locate 'Avalonia.Platform.ICursorFactory'</c>. Dafuer
/// braeuchte es Avalonia.Headless als zusaetzliche Abhaengigkeit der
/// Testbaugruppe, und die traegt sie bewusst nicht (siehe den Kommentar in
/// Ausgabenverwaltung.Tests.csproj: "Es wird KEIN Avalonia-Fenster
/// gestartet"). Fuer eine einzelne Angabe waere das zu viel.
///
/// <b>Warum es diesen Test ueberhaupt gibt:</b> Die Zeile in App.axaml
/// wirkt auf Windows und Linux nichts. Sie sieht auf jedem
/// Entwicklungsrechner wie ein vergessener Wert aus, und genau so wird sie
/// eines Tages entfernt - auffallen wuerde es erst dem naechsten, der die
/// Anwendung auf einem Mac oeffnet. Der Test haelt sie fest, ohne dass
/// dafuer jemand einen Mac braucht.
///
/// Geprueft wird das ATTRIBUT am Wurzelelement und nicht bloss ein
/// Textfund irgendwo in der Datei - sonst genuegte ein Kommentar mit
/// demselben Wortlaut.
/// </summary>
public class AnwendungsnameTests
{
    private const string Erwartet = "Ausgabenverwaltung";

    [Fact]
    public void App_axaml_setzt_den_Anwendungsnamen()
    {
        var wurzel = XDocument.Load(AppAxamlPfad()).Root;

        Assert.NotNull(wurzel);
        Assert.Equal("Application", wurzel!.Name.LocalName);

        var name = wurzel.Attribute("Name")?.Value;

        Assert.True(
            name == Erwartet,
            $"App.axaml muss am <Application>-Element Name=\"{Erwartet}\" tragen "
            + $"(gefunden: {name ?? "gar nichts"}). Ohne diese Angabe steht im "
            + "macOS-Menü der Name des Oberflächen-Baukastens statt der der "
            + "Anwendung. Auf Windows und Linux ist sie wirkungslos — sie ist "
            + "deshalb kein vergessener Wert.");
    }

    /// <summary>
    /// Der Fenstertitel ist NICHT dasselbe und war nie falsch. Steht hier
    /// mit dabei, damit die beiden nicht verwechselt werden: wer den
    /// macOS-Namen sucht und den Fenstertitel findet, haelt das Problem
    /// faelschlich fuer geloest.
    /// </summary>
    [Fact]
    public void Der_Fenstertitel_bleibt_davon_unberuehrt()
    {
        var pfad = Path.Combine(
            Path.GetDirectoryName(AppAxamlPfad())!, "Views", "MainWindow.axaml");

        var titel = XDocument.Load(pfad).Root?.Attribute("Title")?.Value;

        Assert.Equal(Erwartet, titel);
    }

    // Vom Testverzeichnis nach oben, bis App.axaml auftaucht. Ein fester
    // relativer Pfad ("..\..\..\..") haengt daran, wie tief die Baugruppe
    // gerade liegt, und das unterscheidet sich zwischen Debug, Release und
    // Werkzeugen.
    private static string AppAxamlPfad()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);

        while (ordner is not null)
        {
            var kandidat = Path.Combine(ordner.FullName, "App.axaml");
            if (File.Exists(kandidat))
            {
                return kandidat;
            }

            ordner = ordner.Parent;
        }

        throw new FileNotFoundException(
            "App.axaml wurde oberhalb von " + AppContext.BaseDirectory + " nicht gefunden.");
    }
}
