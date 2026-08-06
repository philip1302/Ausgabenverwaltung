namespace Ausgabenverwaltung.Core.Updates;

/// <summary>
/// Warum eine Aktualisierung unterbleibt. Nur fuer das Protokoll - dem
/// Anwender wird davon nichts gezeigt, denn "es gibt nichts Neues" ist
/// keine Nachricht.
/// </summary>
public enum UpdateGrund
{
    /// <summary>Es gibt etwas Neueres, und es liegt eine passende Datei
    /// dafuer bereit.</summary>
    Verfuegbar,

    /// <summary>Der laufende Stand ist der neueste.</summary>
    Aktuell,

    /// <summary>Fuer diese Plattform wird nichts veroeffentlicht.</summary>
    PlattformOhneVeroeffentlichung,

    /// <summary>Die neuere Veroeffentlichung bringt keine Datei fuer
    /// diese Plattform mit.</summary>
    KeinPassendesAsset,

    /// <summary>Die Datei traegt keine brauchbare Pruefsumme. Ohne sie
    /// wird nicht ausgetauscht.</summary>
    OhnePruefsumme,

    /// <summary>Die Liste war leer oder unlesbar - auch ein nicht
    /// erreichbares GitHub landet hier.</summary>
    NichtsGefunden,
}

/// <summary>
/// Das Ergebnis der Frage "gibt es etwas Neueres, und koennen wir es
/// nehmen?".
/// </summary>
public sealed record UpdateDecision
{
    public required UpdateGrund Grund { get; init; }

    /// <summary>Nur bei <see cref="UpdateGrund.Verfuegbar"/> gesetzt.</summary>
    public ReleaseInfo? Release { get; init; }

    /// <summary>Nur bei <see cref="UpdateGrund.Verfuegbar"/> gesetzt.</summary>
    public ReleaseAsset? Asset { get; init; }

    public bool IstVerfuegbar => Grund == UpdateGrund.Verfuegbar;

    /// <summary>
    /// Die Adresse zum Nachlesen, wenn nicht automatisch aktualisiert
    /// werden kann - auch dann, wenn die Datei fuer diese Plattform
    /// fehlt, gibt es ja eine Seite dazu.
    /// </summary>
    public string? HtmlUrl => Release?.HtmlUrl;

    internal static UpdateDecision Ohne(UpdateGrund grund, ReleaseInfo? release = null)
        => new() { Grund = grund, Release = release };
}

/// <summary>
/// Entscheidet allein aus Werten, ob aktualisiert wird - ohne Netz, ohne
/// Platte, ohne Uhrzeit. Genau deshalb laesst sich der interessante Teil
/// dieser Funktion pruefen, waehrend das Laden selbst
/// (<see cref="UpdateDownload"/>) nur noch ausfuehrt, was hier
/// beschlossen wurde (Regel 7).
/// </summary>
public static class UpdateEntscheidung
{
    /// <param name="aktuelleVersion">
    /// Die Informationsversion der laufenden Baugruppe, mit oder ohne
    /// Git-Zusatz hinter dem Pluszeichen.
    /// </param>
    /// <param name="releases">
    /// Die gelesene Liste, ueblicherweise aus
    /// <see cref="GitHubReleases.Lies"/>.
    /// </param>
    /// <param name="assetName">
    /// Der fuer diese Plattform erwartete Dateiname, oder <c>null</c>,
    /// wenn fuer sie nichts veroeffentlicht wird.
    /// </param>
    public static UpdateDecision Treffe(
        string? aktuelleVersion,
        IReadOnlyList<ReleaseInfo> releases,
        string? assetName)
    {
        if (assetName is null)
        {
            return UpdateDecision.Ohne(UpdateGrund.PlattformOhneVeroeffentlichung);
        }

        if (releases.Count == 0)
        {
            return UpdateDecision.Ohne(UpdateGrund.NichtsGefunden);
        }

        // Die Liste kommt sortiert herein, aber darauf verlassen wir uns
        // nicht: eine von Hand zusammengestellte Liste (Tests, spaetere
        // Aufrufer) waere es womoeglich nicht.
        var neuester = releases.MaxBy(release => release.Version);

        if (neuester is null || !AppVersion.IstNeuer(aktuelleVersion, neuester.TagName))
        {
            return UpdateDecision.Ohne(UpdateGrund.Aktuell);
        }

        var asset = UpdatePlatform.FindeAsset(neuester, assetName);
        if (asset is null)
        {
            return UpdateDecision.Ohne(UpdateGrund.KeinPassendesAsset, neuester);
        }

        // Ohne Pruefsumme wird nichts uebernommen. Der Anwender bekommt
        // in diesem Fall den Hinweis mit dem Link - herunterladen darf er
        // von Hand, was das Programm sich selbst nicht zutraut.
        if (string.IsNullOrEmpty(asset.Sha256))
        {
            return UpdateDecision.Ohne(UpdateGrund.OhnePruefsumme, neuester);
        }

        return new UpdateDecision
        {
            Grund = UpdateGrund.Verfuegbar,
            Release = neuester,
            Asset = asset,
        };
    }
}
