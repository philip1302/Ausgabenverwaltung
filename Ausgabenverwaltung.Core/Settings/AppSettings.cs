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

    /// <summary>
    /// Ob beim Programmstart nachgesehen wird, ob es eine neuere Fassung
    /// gibt (siehe <see cref="Updates.UpdateDownload"/>).
    ///
    /// Vorgabe eingeschaltet - eine Aktualisierung, die erst eingeschaltet
    /// werden muss, findet nicht statt. Abschaltbar ist sie trotzdem, und
    /// zwar aus einem handfesten Grund: es ist der EINZIGE Zeitpunkt, zu
    /// dem diese Anwendung ueberhaupt ins Netz greift. Wer das nicht will,
    /// soll es abstellen koennen, ohne dafuer eine Firewall zu bemuehen.
    /// </summary>
    public bool AutoUpdate { get; init; } = true;

    /// <summary>
    /// Wann zuletzt nachgesehen wurde. Rein zur Anzeige und fuers
    /// Protokoll - die Suche wird davon nicht gesteuert, sie laeuft bei
    /// jedem Start.
    /// </summary>
    public DateTime? LastUpdateCheckUtc { get; init; }

    /// <summary>
    /// Die zuletzt gesehene Fassung der Anwendung ("1.2.0"). Steigt sie
    /// zwischen zwei Starts, erscheint einmalig die Seite "Was ist neu"
    /// (siehe <see cref="Updates.WasIstNeu"/>).
    ///
    /// NULL heisst "erste Ausfuehrung" - dann bleibt es still, denn wer
    /// noch keinen alten Stand kennt, braucht keine Liste der Aenderungen
    /// daran.
    /// </summary>
    public string? LastSeenVersion { get; init; }

    /// <summary>
    /// Zu welcher Fassung <see cref="PendingReleaseNotes"/> gehoert
    /// ("v1.2.0"). Passt der Wert nicht zur laufenden Fassung, wird der
    /// Text nicht gezeigt: er beschriebe dann etwas anderes als das, was
    /// gerade laeuft.
    /// </summary>
    public string? PendingReleaseNotesVersion { get; init; }

    /// <summary>
    /// Der Beschreibungstext der geladenen Veroeffentlichung, abgelegt
    /// beim Bereitlegen und gezeigt nach dem Neustart. Er steht hier und
    /// nicht in der Datenbank, weil er zur Anwendung gehoert und nicht zu
    /// den Buchungen - eine Sicherung braucht ihn nicht.
    /// </summary>
    public string? PendingReleaseNotes { get; init; }

    /// <summary>
    /// Ob die Erfassungsmaske nach dem Speichern Kategorie, Zahler und
    /// Datum stehen laesst (siehe ErfassenViewModel). Fuer das Erfassen
    /// mehrerer Belege am Stueck.
    ///
    /// Vorgabe aus: wer die Maske einmal am Tag benutzt, soll ein leeres
    /// Formular vorfinden. Die Einstellung steht hier und nicht nur im
    /// Speicher, weil das Erfassen mehrerer Belege am Stueck eine
    /// Gewohnheit ist und nicht die Ausnahme eines Nachmittags - sie
    /// jeden Start neu anzuhaken waere genau die Tipparbeit, die sie
    /// sparen soll.
    /// </summary>
    public bool KeepEntryValues { get; init; }
}
