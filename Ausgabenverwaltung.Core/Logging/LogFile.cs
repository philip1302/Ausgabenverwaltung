namespace Ausgabenverwaltung.Core.Logging;

/// <summary>
/// Eine vorhandene Protokolldatei: wo sie liegt und zu welchem Tag sie
/// gehoert. Der Tag stammt aus dem Dateinamen (<see cref="LogFileName"/>),
/// nicht aus dem Zeitstempel des Dateisystems - der aendert sich beim
/// Kopieren des Ordners und taugt deshalb nicht als Grundlage fuer die
/// Aufbewahrung.
/// </summary>
public sealed record LogFile(string FullPath, string FileName, DateOnly Date);
