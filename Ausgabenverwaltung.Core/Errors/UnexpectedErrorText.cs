using System.Text;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.Errors;

/// <summary>
/// Baut aus einer nicht behandelten Ausnahme den <see cref="ErrorReport"/>
/// fuer den Fehlerdialog.
///
/// Der Aufbau ist immer derselbe, und zwar aus einem Grund: ein Anwender,
/// der so einen Dialog sieht, ist erschrocken. Er soll in dieser
/// Reihenfolge lesen - was passiert ist, dass seine Daten in Ordnung sind,
/// was er jetzt tun kann. Die technischen Angaben stehen vollstaendig zur
/// Verfuegung, aber weggeklappt.
///
/// Reine Textbildung, deshalb vollstaendig pruefbar (Regel 7).
/// </summary>
public static class UnexpectedErrorText
{
    /// <summary>
    /// <paramref name="context"/> beschreibt in Anwendersprache, wobei es
    /// passiert ist ("beim Aufbau der Ansicht", "im Hintergrund") - nicht
    /// den Namen einer Methode.
    /// </summary>
    public static ErrorReport Describe(
        string context,
        Exception exception,
        string? logFolderPath,
        string version)
    {
        var problem = StorageProblems.Classify(exception);

        // Ein Speicher- oder Datenbankproblem wiegt schwerer als ein
        // Fehler in der Anzeige: dort ist offen, ob die naechste Aenderung
        // ankommt.
        var recommendation = problem == StorageProblem.Unknown
            ? ErrorRecommendation.Continue
            : ErrorRecommendation.Restart;

        return new ErrorReport
        {
            Title = "Es ist ein unerwarteter Fehler aufgetreten.",
            Message = BaueMeldung(context, problem, recommendation, logFolderPath),
            Technical = BaueTechnik(context, exception, version, logFolderPath),
            LogFolderPath = logFolderPath,
            Recommendation = recommendation,
        };
    }

    private static string BaueMeldung(
        string context,
        StorageProblem problem,
        ErrorRecommendation recommendation,
        string? logFolderPath)
    {
        var text = new StringBuilder();

        // 1. Was passiert ist.
        text.Append("Bei einem Schritt ")
            .Append(context)
            .AppendLine(" ist etwas schiefgegangen, womit die Anwendung nicht gerechnet hat.");
        text.AppendLine();

        // 2. Was das fuer die Daten bedeutet. Der wichtigste Absatz.
        if (problem == StorageProblem.Unknown)
        {
            text.AppendLine(
                "Ihre erfassten Ausgaben sind davon nicht betroffen. Es wurde nichts "
                + "gespeichert und nichts verändert — in der Datei steht alles so wie "
                + "vor diesem Fehler.");
        }
        else
        {
            text.AppendLine(
                "Der Fehler hing mit dem Speichern zusammen. Angefangene Änderungen "
                + "wurden vollständig zurückgenommen, halb Gespeichertes gibt es nicht. "
                + "Ob die nächste Änderung ankommt, lässt sich allerdings nicht "
                + "zusichern.");
            text.AppendLine();
            text.AppendLine(DatabaseErrorText.WriteFailed(problem).Split("\n\n")[0]);
        }

        text.AppendLine();

        // 3. Was der Anwender tun kann.
        if (logFolderPath is not null)
        {
            text.AppendLine(
                "Die technischen Einzelheiten stehen unten zum Aufklappen und Kopieren "
                + "bereit. Dasselbe steht im Protokoll:");
            text.AppendLine(logFolderPath);
        }
        else
        {
            text.AppendLine(
                "Die technischen Einzelheiten stehen unten zum Aufklappen und Kopieren "
                + "bereit. Ein Protokoll wird gerade nicht geschrieben — bitte kopieren "
                + "Sie den Text von dort, falls er noch gebraucht wird.");
        }

        text.AppendLine();

        text.Append(recommendation == ErrorRecommendation.Continue
            ? "Sie können weiterarbeiten. Tritt derselbe Fehler wieder auf, hilft ein "
              + "Neustart der Anwendung."
            : "Es empfiehlt sich, die Anwendung zu beenden und neu zu starten. "
              + "Weiterarbeiten ist möglich, aber die nächste Speicherung kann "
              + "wieder scheitern.");

        return text.ToString();
    }

    private static string BaueTechnik(
        string context, Exception exception, string version, string? logFolderPath)
    {
        var text = new StringBuilder();

        text.AppendLine($"Zeitpunkt:   {IsoDateTime.ToUtcText(DateTime.UtcNow)}");
        text.AppendLine($"Version:     {version}");
        text.AppendLine($"Zusammenhang: {context}");

        if (logFolderPath is not null)
        {
            text.AppendLine($"Protokoll:   {logFolderPath}");
        }

        text.AppendLine();

        // ToString() der Ausnahme: Typ, Meldung, Aufrufstapel und die
        // gesamte Kette innerer Ausnahmen - siehe LogLine.FormatException.
        text.Append(exception.ToString());

        return text.ToString();
    }
}
