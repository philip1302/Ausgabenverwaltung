using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Pruefung eines Sicherungsziels.
/// </summary>
public static class BackupTarget
{
    /// <summary>
    /// Schreibt einmal testweise eine winzige Datei in den Ordner und
    /// loescht sie wieder.
    ///
    /// Wird beim Auswaehlen des zweiten Ziels aufgerufen: fehlende
    /// Berechtigungen sollen sofort auffallen, im Moment der Entscheidung -
    /// und nicht Wochen spaeter still bei einem Programmstart. Dass der
    /// Ordner existiert, sagt fuer sich genommen nichts darueber aus, ob
    /// hineingeschrieben werden darf.
    /// </summary>
    public static BackupTargetCheck Check(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return new BackupTargetCheck
            {
                IsWritable = false,
                Problem = StorageProblem.PathNotFound,
                Error = "Es wurde kein Ordner angegeben.",
            };
        }

        var probePath = Path.Combine(folderPath, $"schreibprobe_{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(folderPath);
            File.WriteAllText(probePath, "Schreibprobe der Ausgabenverwaltung.");
            File.Delete(probePath);

            return BackupTargetCheck.Ok();
        }
        catch (Exception ex)
        {
            // Der Aufraeumversuch darf den urspruenglichen Fehler nicht
            // ueberdecken - deshalb steht er hier und nicht in einem
            // finally, das erneut werfen koennte.
            try
            {
                File.Delete(probePath);
            }
            catch (Exception)
            {
            }

            AppLog.Current.Exception($"Bei der Schreibprobe in '{folderPath}'", ex);

            return new BackupTargetCheck
            {
                IsWritable = false,
                Problem = StorageProblems.Classify(ex),
                Error = ex.Message,
            };
        }
    }
}
