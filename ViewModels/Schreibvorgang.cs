using System;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Rahmen um jeden schreibenden Zugriff auf die Datenbank.
///
/// Er tut drei Dinge, und zwar ueberall gleich:
/// die Ausnahme ins Protokoll schreiben, sie einordnen und daraus einen
/// verstaendlichen Satz machen. Der Aufrufer bekommt NULL, wenn es
/// geklappt hat, sonst den Text - und entscheidet selbst, wo er ihn
/// hinstellt.
///
/// Das Entscheidende steht dabei nicht hier, sondern in dem, was NICHT
/// passiert: der Aufrufer raeumt sein Formular erst, wenn NULL
/// zurueckkommt. Ein Schreibfehler laesst die Eingaben also stehen.
///
/// Keine Fachlogik (Regel 7) - eingeordnet wird in
/// <see cref="StorageProblems"/>, formuliert in
/// <see cref="DatabaseErrorText"/>, beides in Core und beides pruefbar.
/// </summary>
internal static class Schreibvorgang
{
    /// <summary>
    /// <paramref name="zusammenhang"/> beschreibt fuer das Protokoll, was
    /// gerade versucht wurde ("Beim Speichern einer Ausgabe").
    /// </summary>
    public static string? Versuche(string zusammenhang, Action aktion)
    {
        try
        {
            aktion();
            return null;
        }
        catch (Exception ex)
        {
            return Beschreibe(zusammenhang, ex);
        }
    }

    /// <summary>
    /// Dasselbe fuer eine bereits gefangene Ausnahme - fuer Aufrufer, die
    /// vorher noch eigene Faelle abfangen (etwa einen Namenskonflikt) und
    /// erst danach beim allgemeinen Schreibfehler landen.
    /// </summary>
    public static string Beschreibe(string zusammenhang, Exception ausnahme)
    {
        AppLog.Current.Exception(zusammenhang, ausnahme);
        return DatabaseErrorText.WriteFailed(StorageProblems.Classify(ausnahme));
    }
}
