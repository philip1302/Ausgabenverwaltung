using System;
using System.Diagnostics;
using Ausgabenverwaltung.Core.Logging;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Oeffnet einen Ordner im Dateimanager des Betriebssystems.
///
/// Reine Bedienmechanik, deshalb hier und nicht in Core (Regel 7 betrifft
/// Fachlogik). Zusammengefasst an einer Stelle, weil inzwischen drei
/// Stellen denselben Handgriff brauchen: der Fehlerdialog, das
/// Startfehlerfenster und der Sicherungsbereich.
///
/// Wirft nie. Ein nicht geoeffneter Ordner ist ein Aergernis - dass die
/// Anwendung deswegen abstuerzt, waere um ein Vielfaches schlimmer,
/// besonders im Fehlerdialog, wo dieser Aufruf ohnehin schon die Folge
/// eines Fehlers ist.
/// </summary>
public static class Ordner
{
    /// <summary>
    /// Liefert <c>true</c>, wenn der Dateimanager angestossen werden
    /// konnte. Bei <c>false</c> gehoert dem Anwender der Pfad im Klartext
    /// gezeigt, damit er ihn von Hand aufrufen kann.
    /// </summary>
    public static bool Oeffne(string? pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad))
        {
            return false;
        }

        try
        {
            // UseShellExecute laesst das Betriebssystem den Ordner in
            // seinem Dateimanager oeffnen - unter Windows der Explorer.
            Process.Start(new ProcessStartInfo
            {
                FileName = pfad,
                UseShellExecute = true,
            });

            return true;
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception($"Beim Oeffnen des Ordners '{pfad}'", ex);
            return false;
        }
    }
}
