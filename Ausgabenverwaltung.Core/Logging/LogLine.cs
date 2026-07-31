using System.Text;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.Logging;

/// <summary>
/// Formt einen einzelnen Protokolleintrag als Text. Reine Zeichenkette,
/// kein Dateizugriff - deshalb vollstaendig pruefbar (Regel 7). Das
/// Schreiben besorgt <see cref="AppLog"/>.
///
/// Aufbau einer Zeile:
/// <c>2026-07-31T14:22:01Z  INFO   Programmstart, Version 1.0.0</c>
///
/// Der Zeitstempel ist UTC im vorgeschriebenen Format (Regel 3). Das ist
/// hier kein Selbstzweck: die Sommerzeitumstellung wuerde in lokaler Zeit
/// eine Stunde doppelt oder gar nicht enthalten, und ausgerechnet in einer
/// Datei, die die Reihenfolge von Ereignissen belegen soll, waere das ein
/// schlechter Tausch gegen die bequemere Lesbarkeit.
/// </summary>
public static class LogLine
{
    // Feste Breite fuer die Stufe, damit die Meldungen untereinander in
    // einer Spalte beginnen und sich die Datei ueberfliegen laesst.
    private const int LevelWidth = 7;

    public static string Format(DateTime utc, LogLevel level, string message)
    {
        var stufe = level.ToString().ToUpperInvariant().PadRight(LevelWidth);

        return $"{IsoDateTime.ToUtcText(utc)}  {stufe}{message}";
    }

    /// <summary>
    /// Eintrag zu einer Ausnahme: eine Kopfzeile mit dem Zusammenhang,
    /// darunter Typ, Meldung und vollstaendiger Aufrufstapel - auch der
    /// aller inneren Ausnahmen. Der Stapel bleibt unveraendert und
    /// uneingerueckt stehen, damit sich der Text aus der Datei heraus
    /// unveraendert weitergeben laesst.
    /// </summary>
    public static string FormatException(DateTime utc, string context, Exception exception)
    {
        var builder = new StringBuilder();

        builder.AppendLine(Format(utc, LogLevel.Error, context));

        // ToString() einer Ausnahme enthaelt bereits Typ, Meldung, Stapel
        // und - eingerueckt darueber - die gesamte Kette innerer
        // Ausnahmen. Von Hand nachgebaut ginge dabei regelmaessig etwas
        // verloren; AggregateException etwa haengt ihre Teilausnahmen nur
        // in dieser Ausgabe an.
        builder.Append(exception.ToString());

        return builder.ToString();
    }
}
