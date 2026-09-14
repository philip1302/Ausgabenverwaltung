namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Die Rechnungen des Jahresrueckblicks. Bewusst Funktionen und keine
/// Felder auf <see cref="ReviewCategoryChange"/>: die Faelle, in denen es
/// kein Ergebnis gibt (Vorjahr null, Gesamtsumme null), sollen an genau
/// einer Stelle behandelt werden und nicht an jeder Aufrufstelle neu.
/// </summary>
public static class ReviewMath
{
    /// <summary>
    /// Die Veraenderung als Anteil des Vorjahreswerts, z. B. 0,34 fuer
    /// 34 Prozent mehr.
    ///
    /// <b>NULL, wenn es keinen Vorjahreswert gibt.</b> Es gibt keine
    /// unendliche Prozentzahl und keinen Ersatzwert - eine Kategorie, die
    /// im Vorjahr nicht vorkam, ist neu, und genau das sagt der Rueckblick
    /// dann auch (<see cref="ReviewFindingKind.NeuHinzugekommen"/>).
    /// </summary>
    public static decimal? RelativeChange(long previousCents, long currentCents)
    {
        if (previousCents == 0)
        {
            return null;
        }

        // Cent durch Cent - das Verhaeltnis ist dasselbe wie in Euro, und
        // es ist keine Geldgroesse, sondern ein Anteil (Regel 1 betrifft
        // nur Betraege).
        return (decimal)(currentCents - previousCents) / previousCents;
    }

    /// <summary>
    /// Anteil an einer Gesamtsumme, z. B. 0,18 fuer 18 Prozent. Ohne
    /// Gesamtsumme null statt einer Division durch null.
    /// </summary>
    public static decimal Share(long partCents, long totalCents) =>
        totalCents == 0 ? 0m : (decimal)partCents / totalCents;

    /// <summary>
    /// Die Rangzahl einer Veraenderung: der Betrag des Unterschieds,
    /// gedaempft mit der Wurzel aus seinem Verhaeltnis zum Vorjahreswert.
    ///
    /// Der absolute Betrag bleibt damit der Hauptmassstab - 1.000 Euro
    /// mehr sind eine groessere Nachricht als 100 Euro mehr, egal worauf.
    /// Das Verhaeltnis entscheidet nur zwischen aehnlich grossen
    /// Unterschieden: dieselben 200 Euro wiegen auf 200 Euro Grundlage
    /// schwerer als auf 2.000. Der Deckel
    /// (<see cref="ReviewThresholds.RelativDeckel"/>) haelt Kategorien mit
    /// winzigem Vorjahreswert davon ab, allein durch ihr Verhaeltnis zu
    /// gewinnen; das Mindestvolumen im Nenner ist zugleich die
    /// Absicherung gegen die Division durch null.
    ///
    /// Das Ergebnis ist eine RANGZAHL und kein Geldbetrag. Es wird nie
    /// ueber EuroText ausgegeben - deshalb ist der Zwischenschritt ueber
    /// <c>double</c> fuer die Wurzel mit Regel 1 vertraeglich.
    /// </summary>
    public static long Score(long previousCents, long currentCents)
    {
        var delta = Math.Abs(currentCents - previousCents);
        if (delta == 0)
        {
            return 0;
        }

        var grundlage = Math.Max(previousCents, ReviewThresholds.MindestVolumenCents);
        var verhaeltnis = Math.Min(ReviewThresholds.RelativDeckel, (double)delta / grundlage);

        return (long)Math.Round(delta * Math.Sqrt(verhaeltnis));
    }
}
