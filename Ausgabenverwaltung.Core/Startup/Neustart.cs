using System.Globalization;
using Ausgabenverwaltung.Core.Logging;

namespace Ausgabenverwaltung.Core.Startup;

/// <summary>
/// Der Neustart der Anwendung durch sich selbst - und das Warten des
/// Nachfolgers auf den Vorgaenger.
///
/// Steht hier und nicht bei der Aktualisierung, weil es inzwischen ZWEI
/// Anlaesse gibt, sich selbst neu zu starten: eine uebernommene Fassung
/// (Updates.UpdateInstaller) und eine wiederhergestellte Sicherung
/// (Backups.BackupRestore). Beide brauchen dasselbe Verabreden mit dem
/// Nachfolger, und zwei Fassungen desselben Aufrufmerkmals liefen
/// unweigerlich auseinander.
///
/// Das Warten ist der eigentliche Kunstgriff: der Vorgaenger endet erst,
/// waehrend der Nachfolger schon laeuft. Ohne das Warten wuerde
/// <see cref="SingleInstance"/> den Neustart als Doppelstart abweisen und
/// die Anwendung waere schlicht weg - und im Fall der Wiederherstellung
/// haette der Nachfolger die Datenbank angefasst, die der Vorgaenger noch
/// offen haelt.
/// </summary>
public static class Neustart
{
    /// <summary>
    /// Aufrufmerkmal fuer den neu gestarteten Prozess: warte, bis der
    /// Vorgaenger wirklich beendet ist. Dahinter steht seine Kennung.
    /// </summary>
    public const string WarteMerkmal = "--warte-auf-prozess";

    /// <summary>
    /// Wie lange der neue Prozess auf das Ende des alten wartet. Grosszuegig
    /// bemessen: laenger zu warten kostet nur Sekunden, zu frueh
    /// aufzugeben kostet den Start.
    /// </summary>
    private static readonly TimeSpan WarteHoechstdauer = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Startet die eigene Programmdatei erneut. Der eigene Prozess muss
    /// sich unmittelbar danach beenden - der neue wartet darauf.
    ///
    /// <c>false</c>, wenn sich kein neuer Prozess starten liess. Dann darf
    /// der Aufrufer sich NICHT beenden, sonst ist die Anwendung weg.
    /// </summary>
    public static bool StarteSichSelbst()
    {
        var eigenerPfad = Environment.ProcessPath;
        if (string.IsNullOrEmpty(eigenerPfad))
        {
            return false;
        }

        return Starte(eigenerPfad, Path.GetDirectoryName(eigenerPfad) ?? string.Empty);
    }

    /// <summary>
    /// Startet die angegebene Programmdatei mit dem Warte-Merkmal auf den
    /// eigenen Prozess. Wirft nie - ein gescheiterter Start wird gemeldet,
    /// nicht geworfen, weil der Aufrufer daraufhin gerade NICHT enden darf.
    /// </summary>
    public static bool Starte(string ausfuehrbareDatei, string arbeitsordner)
    {
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ausfuehrbareDatei,
                UseShellExecute = false,
                WorkingDirectory = arbeitsordner,
            };

            start.ArgumentList.Add(WarteMerkmal);
            start.ArgumentList.Add(
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

            return System.Diagnostics.Process.Start(start) is not null;
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Starten der Anwendung", ex);
            return false;
        }
    }

    /// <summary>
    /// Wartet auf das Ende des Vorgaengerprozesses, dessen Kennung als
    /// <see cref="WarteMerkmal"/>-Argument hereinkam.
    ///
    /// Kehrt auch zurueck, wenn der Prozess laengst weg ist oder die
    /// Kennung unbrauchbar war - gewartet wird auf ein Ende, nicht auf
    /// eine Bestaetigung.
    /// </summary>
    public static void WarteAufVorgaenger(string[] argumente)
    {
        try
        {
            var index = Array.IndexOf(argumente, WarteMerkmal);
            if (index < 0 || index + 1 >= argumente.Length)
            {
                return;
            }

            if (!int.TryParse(
                    argumente[index + 1], CultureInfo.InvariantCulture, out var pid))
            {
                return;
            }

            using var vorgaenger = System.Diagnostics.Process.GetProcessById(pid);
            vorgaenger.WaitForExit((int)WarteHoechstdauer.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // GetProcessById wirft, wenn es den Prozess nicht mehr gibt -
            // genau der Zustand, auf den gewartet wurde.
        }
        catch (Exception)
        {
            // Auch sonst gilt: nicht warten zu koennen ist kein Grund,
            // den Start abzubrechen.
        }
    }
}
