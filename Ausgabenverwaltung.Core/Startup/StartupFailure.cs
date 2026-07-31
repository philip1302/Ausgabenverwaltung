namespace Ausgabenverwaltung.Core.Startup;

/// <summary>
/// Ein Startabbruch, fertig formuliert. Das Startfehlerfenster setzt die
/// Teile nur noch an ihren Platz und kennt keinen einzigen Ausnahmetyp
/// (Regel 7).
///
/// Gebaut wird das in <see cref="StartupFailureText"/>.
/// </summary>
public sealed record StartupFailure
{
    /// <summary>Ueberschrift - ein ganzer Satz, kein Schlagwort.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// Der Haupttext: was passiert ist, was das fuer die Daten bedeutet,
    /// was der Anwender tun kann. Ohne technische Begriffe.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Typ, Meldung und vollstaendiger Aufrufstapel fuer den
    /// aufklappbaren Bereich.
    /// </summary>
    public required string Technical { get; init; }

    /// <summary>
    /// Der Ordner, den die Schaltflaeche oeffnet - der Sicherungsordner,
    /// wenn ein Stand zurueckgeholt werden muss, sonst der
    /// Protokollordner. NULL, wenn es nichts zu oeffnen gibt.
    /// </summary>
    public string? FolderPath { get; init; }

    /// <summary>Beschriftung dieser Schaltflaeche.</summary>
    public string? FolderButtonText { get; init; }

    /// <summary>
    /// Ob ein erneuter Versuch ueberhaupt Aussicht hat. Bei einer
    /// gesperrten Datei ja - das andere Fenster ist gleich zu. Bei einer
    /// beschaedigten Datei nein, da muss erst etwas geschehen.
    /// </summary>
    public required bool RetryWorthwhile { get; init; }
}
