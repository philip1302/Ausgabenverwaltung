namespace Ausgabenverwaltung.Core.Updates;

/// <summary>
/// Liest eine Versionsangabe aus den beiden Formen, in denen sie hier
/// vorkommt, und macht daraus einen vergleichbaren Wert.
///
/// Die eigene Version steht als Informationsversion in der Baugruppe und
/// traegt dort IMMER den Git-Stand hinter einem Pluszeichen:
/// "1.1.0+c63496bec93093cc2c9f4fc0cf62ecc2abdfb345". Das haengt die
/// .NET-Werkzeugkette von sich aus an, sobald das Projekt in einem
/// Git-Verzeichnis liegt. Wer das Pluszeichen nicht abschneidet,
/// vergleicht Versionen mit einem Hash daran und bekommt nie eine
/// Uebereinstimmung.
///
/// Auf der Gegenseite steht der Name einer Release-Marke, und der beginnt
/// bei GitHub ueblicherweise mit einem "v": "v1.1.0".
///
/// Reine Textverarbeitung, deshalb vollstaendig pruefbar (Regel 7).
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// Liefert <c>false</c> statt zu werfen, wenn nichts Brauchbares
    /// dasteht. Eine unlesbare Versionsangabe ist kein Fehlerfall, der
    /// jemanden aufhalten sollte - sie bedeutet schlicht "damit laesst
    /// sich nicht vergleichen", und der Aufrufer sieht dann von einer
    /// Aktualisierung ab.
    /// </summary>
    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0);

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var kern = text.Trim();

        // Fuehrendes "v" der Release-Marke.
        if (kern.StartsWith('v') || kern.StartsWith('V'))
        {
            kern = kern[1..];
        }

        // Alles ab dem Pluszeichen ist Bauzusatz (Git-Stand), nicht Teil
        // der Version.
        var plus = kern.IndexOf('+');
        if (plus >= 0)
        {
            kern = kern[..plus];
        }

        // Ein Vorabzusatz ("1.2.0-beta.1") gehoert ebenfalls nicht in den
        // Zahlenvergleich. Solche Staende kommen ueber die Releases-Liste
        // ohnehin nicht durch (Vorabversionen werden dort verworfen),
        // aber ein Text, der hier hereinkommt, soll trotzdem lesbar
        // bleiben statt am Bindestrich zu scheitern.
        var strich = kern.IndexOf('-');
        if (strich >= 0)
        {
            kern = kern[..strich];
        }

        kern = kern.Trim();

        if (kern.Length == 0)
        {
            return false;
        }

        // Version.TryParse nimmt "1.1" und "1.1.0" und "1.1.0.0"; eine
        // einzelne Zahl ("1") lehnt es ab. Das ist hier richtig so: eine
        // Marke, die nur "v1" heisst, sagt nichts darueber, welcher Stand
        // gemeint ist.
        return Version.TryParse(kern, out var gelesen) && Setze(gelesen, out version);
    }

    /// <summary>
    /// Vergleicht zwei Versionsangaben und sagt, ob die zweite echt neuer
    /// ist. Laesst sich eine von beiden nicht lesen, lautet die Antwort
    /// <c>false</c> - im Zweifel wird nicht aktualisiert.
    /// </summary>
    public static bool IstNeuer(string? aktuell, string? angeboten)
    {
        if (!TryParse(aktuell, out var a) || !TryParse(angeboten, out var b))
        {
            return false;
        }

        return b > a;
    }

    /// <summary>
    /// Die Versionsangabe ohne Bauzusatz, fuer Anzeige und Protokoll:
    /// "1.1.0+c63496b..." wird zu "1.1.0". Laesst sie sich nicht lesen,
    /// kommt der Ausgangstext unveraendert zurueck - eine Anzeige ist
    /// kein Grund, etwas zu verschweigen.
    /// </summary>
    public static string Anzeigetext(string? text)
    {
        if (TryParse(text, out var version))
        {
            return version.ToString();
        }

        return string.IsNullOrWhiteSpace(text) ? "unbekannt" : text.Trim();
    }

    // Vereinheitlicht auf drei Stellen: "1.1" und "1.1.0" und "1.1.0.0"
    // sollen als derselbe Stand gelten. Ohne das waere "1.1" kleiner als
    // "1.1.0" (Version fuellt fehlende Stellen mit -1 statt mit 0) und
    // ein Anwender auf "1.1" bekaeme "1.1.0" als vermeintlich neuer
    // angeboten.
    private static bool Setze(Version gelesen, out Version version)
    {
        version = new Version(
            gelesen.Major,
            gelesen.Minor,
            gelesen.Build < 0 ? 0 : gelesen.Build);

        return true;
    }

    /// <summary>
    /// Formt eine Version zur Release-Marke, wie sie im Projekt vergeben
    /// wird ("1.1.0" -> "v1.1.0"). Nur fuer Anzeige und Protokoll.
    /// </summary>
    public static string ZuMarke(Version version) => "v" + version.ToString(3);
}
