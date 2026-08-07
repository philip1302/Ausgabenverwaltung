namespace Ausgabenverwaltung.Core.Updates;

/// <summary>
/// Eine Veroeffentlichung, wie sie die GitHub-Schnittstelle beschreibt -
/// heruntergebrochen auf das, was fuer die Entscheidung "aktualisieren
/// oder nicht" gebraucht wird. Alles andere aus der Antwort (Autor,
/// Beschreibungstext, Zeitpunkte) bleibt bewusst aussen vor - was der
/// Anwender nach einer Aktualisierung zu lesen bekommt, steht in der
/// eingebetteten Aenderungsliste und nicht auf GitHub (siehe
/// <see cref="Changelog"/>).
/// </summary>
public sealed record ReleaseInfo
{
    /// <summary>Der Name der Marke, z. B. "v1.1.0".</summary>
    public required string TagName { get; init; }

    /// <summary>Die daraus gelesene Version, z. B. 1.1.0.</summary>
    public required Version Version { get; init; }

    /// <summary>Die Seite zum Nachlesen - fuer den Fall, dass nicht
    /// automatisch aktualisiert werden kann und nur ein Hinweis
    /// bleibt.</summary>
    public required string HtmlUrl { get; init; }

    /// <summary>
    /// Die anhaengenden Dateien. Welche davon zur laufenden Plattform
    /// passt, entscheidet <see cref="UpdatePlatform"/>.
    /// </summary>
    public required IReadOnlyList<ReleaseAsset> Assets { get; init; }
}

/// <summary>
/// Eine einzelne Datei an einer Veroeffentlichung.
/// </summary>
public sealed record ReleaseAsset
{
    public required string Name { get; init; }

    public required string DownloadUrl { get; init; }

    /// <summary>Groesse in Byte, wie GitHub sie meldet. Wird nach dem
    /// Laden gegengeprueft.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>
    /// Die SHA256-Pruefsumme als reine Hexziffern, ohne das
    /// "sha256:"-Vorzeichen, das die Schnittstelle voranstellt.
    /// <c>null</c>, wenn GitHub keine mitliefert - dann wird die Datei
    /// NICHT uebernommen (siehe <see cref="UpdateDownload"/>): eine
    /// Programmdatei ohne Pruefmoeglichkeit auszutauschen waere genau die
    /// Art von Zutrauen, die man einem Selbstaustausch nicht mitgeben
    /// sollte.
    /// </summary>
    public string? Sha256 { get; init; }
}
