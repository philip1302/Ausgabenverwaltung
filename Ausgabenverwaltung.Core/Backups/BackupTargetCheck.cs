using Ausgabenverwaltung.Core.Errors;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Ergebnis der Schreibprobe eines Sicherungsziels
/// (<see cref="BackupTarget.Check"/>).
/// </summary>
public sealed record BackupTargetCheck
{
    public required bool IsWritable { get; init; }

    /// <summary>Art des Problems - Grundlage des Meldungstextes.</summary>
    public required StorageProblem Problem { get; init; }

    /// <summary>
    /// Der Text der Ausnahme: technisch, fuer Protokoll und aufklappbaren
    /// Bereich. NULL, wenn die Probe bestanden wurde.
    /// </summary>
    public string? Error { get; init; }

    public static BackupTargetCheck Ok() => new()
    {
        IsWritable = true,
        Problem = StorageProblem.Unknown,
    };
}
