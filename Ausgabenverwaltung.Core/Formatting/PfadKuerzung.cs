namespace Ausgabenverwaltung.Core.Formatting;

/// <summary>
/// Kuerzt einen Ordner- oder Dateipfad fuer die Anzeige in einer Zeile.
///
/// Rohe Pfade als Dauerinhalt sind Rauschen: sie sind lang, sie brechen
/// um, und sie verdraengen die Zeilen, auf die es ankommt. Gebraucht wird
/// meist nur der letzte Teil ("in welchem Ordner?"), der Rest steht im
/// Hilfetext daneben und im ⋯-Menue.
///
/// Gekuerzt wird in der Mitte und an Trennzeichen, nie mittendrin in
/// einem Ordnernamen: Anfang und Ende eines Pfades tragen die Aussage,
/// die Ebenen dazwischen nicht.
/// </summary>
public static class PfadKuerzung
{
    /// <summary>Ab dieser Laenge lohnt das Kuerzen ueberhaupt.</summary>
    public const int Hoechstlaenge = 48;

    private const string Auslassung = "…";

    public static string Kuerze(string? pfad, int hoechstlaenge = Hoechstlaenge)
    {
        // Nichts zu zeigen: ein Pfad aus Leerzeichen ist kein Pfad und
        // soll keine leere Zeile Platz belegen.
        if (string.IsNullOrWhiteSpace(pfad))
        {
            return string.Empty;
        }

        if (pfad.Length <= hoechstlaenge)
        {
            return pfad;
        }

        var teile = pfad.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        // Ein Pfad ohne Trennzeichen (oder mit nur einem Teil) laesst sich
        // nicht an Ebenen kuerzen - dann bleibt nur das Ende, denn dort
        // steht der Name.
        if (teile.Length < 3)
        {
            return Auslassung + pfad[^(hoechstlaenge - 1)..];
        }

        var trenner = pfad.Contains('\\') ? '\\' : '/';
        var anfang = teile[0];
        var ende = teile[^1];

        var gekuerzt = $"{anfang}{trenner}{Auslassung}{trenner}{ende}";

        // Reicht das nicht, ist der letzte Ordnername selbst zu lang -
        // dann zaehlt er, nicht der Anfang.
        return gekuerzt.Length <= hoechstlaenge
            ? gekuerzt
            : Auslassung + trenner + ende;
    }
}
