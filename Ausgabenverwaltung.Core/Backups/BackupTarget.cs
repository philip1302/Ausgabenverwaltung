namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Pruefung eines Sicherungsziels.
/// </summary>
public static class BackupTarget
{
    /// <summary>
    /// Schreibt einmal testweise eine winzige Datei in den Ordner und
    /// loescht sie wieder. Liefert NULL, wenn das geklappt hat, sonst den
    /// Fehlertext.
    ///
    /// Wird beim Auswaehlen des zweiten Ziels aufgerufen: fehlende
    /// Berechtigungen sollen sofort auffallen, im Moment der Entscheidung -
    /// und nicht Wochen spaeter still bei einem Programmstart. Dass der
    /// Ordner existiert, sagt fuer sich genommen nichts darueber aus, ob
    /// hineingeschrieben werden darf.
    /// </summary>
    public static string? TestWritable(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return "Es wurde kein Ordner angegeben.";
        }

        var probePath = Path.Combine(folderPath, $"schreibprobe_{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(folderPath);
            File.WriteAllText(probePath, "Schreibprobe der Ausgabenverwaltung.");
            File.Delete(probePath);
            return null;
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

            return ex.Message;
        }
    }
}
