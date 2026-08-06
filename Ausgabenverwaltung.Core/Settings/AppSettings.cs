using Ausgabenverwaltung.Core.Display;

namespace Ausgabenverwaltung.Core.Settings;

/// <summary>
/// Alle dauerhaften Einstellungen der Anwendung - eine Datei, ein Typ
/// (siehe <see cref="AppSettingsStore"/>). Was hier NICHT hineingehoert,
/// sind fachliche Daten: die stehen in der Datenbank und werden mit ihr
/// gesichert. Die Farben der Kategorien sind deshalb eine Spalte in
/// Category und keine Einstellung.
/// </summary>
public sealed record AppSettings
{
    /// <summary>
    /// Zweites, optionales Sicherungsziel: ein blosser Ordnerpfad. Die
    /// Anwendung kennt keine Cloud-Dienste und erkennt auch keine - ob
    /// dahinter OneDrive, ein USB-Stick oder ein Netzlaufwerk steckt, ist
    /// ihr gleichgueltig. NULL = nicht eingerichtet.
    /// </summary>
    public string? ExternalFolderPath { get; init; }

    /// <summary>
    /// Wann zuletzt ERFOLGREICH in das zweite Ziel geschrieben wurde.
    /// Noetig, weil ein nicht erreichbares zweites Ziel beim Start
    /// stillschweigend uebergangen wird: ohne diesen Wert liefe die
    /// externe Sicherung monatelang ins Leere, ohne dass es jemand merkt.
    /// </summary>
    public DateTime? LastExternalBackupUtc { get; init; }

    /// <summary>
    /// Globale Schriftgroesse als Faktor, nicht als Punktzahl (siehe
    /// <see cref="FontScales"/>). Beim Laden auf die naechste bekannte
    /// Stufe gerundet.
    /// </summary>
    public double FontScale { get; init; } = FontScales.DefaultFactor;

    /// <summary>
    /// Breite der Kategoriespalte in den Tabellen, wie der Benutzer sie
    /// zuletzt gezogen hat - in Pixeln fuer die Schriftstufe "Normal"
    /// (siehe <see cref="ColumnWidths"/>). Beim Laden in den erlaubten
    /// Bereich gebracht.
    /// </summary>
    public double CategoryColumnWidth { get; init; } = ColumnWidths.CategoryDefault;

    /// <summary>
    /// Bewusst gewaehlte Themenvariante (UI/UX-Redesign, Verwaltung ▸
    /// Darstellung). Vorgabe "System" entspricht dem bisherigen, einzigen
    /// Verhalten der Anwendung (<c>RequestedThemeVariant="Default"</c>).
    /// </summary>
    public ThemeMode ThemeMode { get; init; } = ThemeMode.System;
}
