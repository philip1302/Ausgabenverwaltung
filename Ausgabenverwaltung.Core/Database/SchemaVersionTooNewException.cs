namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Wird beim Start geworfen, wenn die Datenbank eine neuere SchemaVersion
/// hat als der Code kennt (z. B. nach einem Downgrade der Anwendung). Die
/// Anwendung darf in diesem Fall nicht weiterlaufen, da unbekannte
/// Schema-Aenderungen zu stillem Datenverlust fuehren koennten.
/// </summary>
public sealed class SchemaVersionTooNewException : Exception
{
    public int ActualVersion { get; }
    public int ExpectedVersion { get; }

    public SchemaVersionTooNewException(int actualVersion, int expectedVersion)
        : base(
            $"Die Datenbank hat Schema-Version {actualVersion}, diese Programmversion " +
            $"kennt aber nur bis Version {expectedVersion}. Bitte zuerst die Anwendung " +
            "aktualisieren, bevor diese Datenbank geoeffnet wird.")
    {
        ActualVersion = actualVersion;
        ExpectedVersion = expectedVersion;
    }
}
