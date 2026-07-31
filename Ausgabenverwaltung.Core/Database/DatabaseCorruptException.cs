namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Wird beim Start geworfen, wenn die Integritaetspruefung
/// (<see cref="DatabaseHealth.QuickCheck"/>) Schaeden findet.
///
/// Die Anwendung laeuft dann bewusst NICHT weiter. Auf einer beschaedigten
/// Datei weiterzuschreiben vergroessert den Schaden und ueberschreibt
/// womoeglich noch die Seiten, aus denen sich sonst etwas haette retten
/// lassen. Ein Start, der erklaert, wie die letzte Sicherung
/// zurueckgespielt wird, ist die bessere Antwort.
/// </summary>
public sealed class DatabaseCorruptException : Exception
{
    /// <summary>Die Befunde von SQLite - technisch, fuer Protokoll und
    /// aufklappbaren Bereich.</summary>
    public string Findings { get; }

    public string DatabaseFilePath { get; }

    public DatabaseCorruptException(string databaseFilePath, string findings)
        : base($"Die Datenbank '{databaseFilePath}' ist beschaedigt: {findings}")
    {
        DatabaseFilePath = databaseFilePath;
        Findings = findings;
    }
}
