namespace Ausgabenverwaltung.Core.Logging;

/// <summary>
/// Dringlichkeit eines Protokolleintrags. Bewusst nur drei Stufen: mehr
/// waeren beim Lesen einer Datei, die ein Mensch nach einem Absturz
/// durchsieht, keine Hilfe.
/// </summary>
public enum LogLevel
{
    /// <summary>Normaler Verlauf: Start, Migration, Sicherung, Erzeugung.</summary>
    Info,

    /// <summary>Etwas hat nicht funktioniert, die Anwendung laeuft weiter.</summary>
    Warning,

    /// <summary>Ausnahme - mit vollstaendigem Aufrufstapel.</summary>
    Error,
}
