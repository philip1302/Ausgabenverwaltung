namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Die veraenderlichen Teile der Datensicherung. Das erste Ziel steht fest
/// und taucht hier deshalb nicht auf.
/// </summary>
public sealed record BackupSettings
{
    /// <summary>
    /// Zweites, optionales Ziel: ein blosser Ordnerpfad. Die Anwendung
    /// kennt keine Cloud-Dienste und erkennt auch keine - ob dahinter
    /// OneDrive, ein USB-Stick oder ein Netzlaufwerk steckt, ist ihr
    /// gleichgueltig. NULL = nicht eingerichtet.
    /// </summary>
    public string? ExternalFolderPath { get; init; }

    /// <summary>
    /// Wann zuletzt ERFOLGREICH in das zweite Ziel geschrieben wurde.
    /// Noetig, weil ein nicht erreichbares zweites Ziel beim Start
    /// stillschweigend uebergangen wird: ohne diesen Wert liefe die
    /// externe Sicherung monatelang ins Leere, ohne dass es jemand merkt.
    /// </summary>
    public DateTime? LastExternalBackupUtc { get; init; }
}
