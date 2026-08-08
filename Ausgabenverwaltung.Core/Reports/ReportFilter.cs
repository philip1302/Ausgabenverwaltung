namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Stellschrauben fuer eine Auswertung. Bewusst kein fertiger
/// Einzelreport, sondern ein Filtermodell, aus dem sich verschiedene
/// Auswertungen zusammensetzen lassen (siehe ReportRepository.Evaluate).
///
/// Durchgaengige Regel fuer alle Listenfilter hier: <b>eine leere Liste
/// schraenkt nicht ein</b>. Das gilt fuer Kategorien, Ausschluesse und
/// Zahler gleichermassen und ebenso fuer <see cref="SettlementStatus.Alle"/>
/// - so bedeutet "nichts angehakt" ueberall dasselbe, naemlich "alles".
/// </summary>
public sealed class ReportFilter
{
    /// <summary>Zeitraumanfang, einschliesslich.</summary>
    public required DateOnly From { get; init; }

    /// <summary>Zeitraumende, ausschliesslich.</summary>
    public required DateOnly To { get; init; }

    /// <summary>
    /// Gewaehlte Kategorie-Aeste. Jeder Eintrag umfasst den Knoten selbst
    /// und alle Unterkategorien (rekursiv). Leer = keine Einschraenkung.
    ///
    /// Mehrere Aeste sind ausdruecklich erlaubt ("Haushalt UND Auto"):
    /// die Bedingungen der einzelnen Aeste sind ODER-verknuepft.
    /// </summary>
    public IReadOnlyList<int> CategoryRootIds { get; init; } = [];

    /// <summary>
    /// Ausgenommene Kategorien. Wie bei <see cref="CategoryRootIds"/> wirkt
    /// jeder Eintrag auf den ganzen Unter-Ast. Leer = nichts ausgenommen.
    ///
    /// Der Ausschluss sticht die Auswahl: "Haushalt, aber ohne Restaurant"
    /// nimmt auch alles unterhalb von Restaurant heraus. Ein Ausschluss
    /// ausserhalb jedes gewaehlten Astes ist wirkungslos, aber kein Fehler.
    /// </summary>
    public IReadOnlyList<int> ExcludedCategoryIds { get; init; } = [];

    public ReportGrouping Grouping { get; init; } = ReportGrouping.Month;

    /// <summary>
    /// Wirkt zusaetzlich zu <see cref="PayerIds"/> - beide Bedingungen
    /// muessen erfuellt sein. Die Oberflaeche waehlt Personen inzwischen
    /// einzeln aus und laesst diesen Wert dabei auf
    /// <see cref="PayerScope.All"/>; gebraucht wird er noch fuer
    /// <see cref="PayerScope.SelfAndOpen"/> ("Meine Kosten"), das sich
    /// aus einzelnen Personen nicht zusammensetzen laesst.
    /// </summary>
    public PayerScope PayerScope { get; init; } = PayerScope.All;

    /// <summary>
    /// Gewaehlte Zahler. Leer = keine Einschraenkung. Mehrere Eintraege
    /// sind ODER-verknuepft.
    /// </summary>
    public IReadOnlyList<int> PayerIds { get; init; } = [];

    /// <summary>
    /// Einschraenkung auf offene bzw. beglichene Posten, kombinierbar.
    /// Beachtet Regel 4 (siehe <see cref="SettlementStatus"/>).
    /// </summary>
    public SettlementStatus Status { get; init; } = SettlementStatus.Alle;

    /// <summary>
    /// Optionale Volltextsuche. Sie greift in DREI Feldern: Bemerkung
    /// (Note), Zahlername und vollem Kategoriepfad - wer "Strom" sucht,
    /// meint die Kategorie mindestens so oft wie das Wort in einer
    /// Bemerkung. Die drei Teile sind ODER-verknuepft, ein Treffer in
    /// einem genuegt. NULL = kein Filter.
    ///
    /// Beim Kategoriepfad zaehlt der GANZE Pfad und nicht nur der Name der
    /// gebuchten Kategorie: "Wohnen" findet also auch die Buchung unter
    /// "Wohnen > Nebenkosten > Strom".
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// Optionale Einschraenkung auf EINEN Buchungstyp: <c>true</c> nur
    /// Einnahmen, <c>false</c> nur Ausgaben. NULL schraenkt nicht ein -
    /// dieselbe Regel wie bei allen uebrigen Filtern dieser Klasse.
    ///
    /// Gebraucht fuer die Spruenge aus den beiden Monatskacheln der
    /// Startseite, die genau eine der beiden Haelften zeigen.
    /// </summary>
    public bool? IsIncome { get; init; }

    /// <summary>
    /// Optionale Einschraenkung auf die aus EINER Vorlage erzeugten
    /// Buchungen - fuer den Sprung "zeig mir, was diese Vorlage bisher
    /// gebucht hat" aus der Vorlagenverwaltung. NULL = keine
    /// Einschraenkung.
    /// </summary>
    public int? RecurringExpenseId { get; init; }

    /// <summary>
    /// Optionale Einschraenkung auf genau EINE Buchung - fuer den Sprung
    /// aus den Uebersichtslisten ("Letzte Buchungen") auf die Zeile, die
    /// dort angeklickt wurde. NULL = keine Einschraenkung.
    ///
    /// Wirkt wie jeder andere Filter zusaetzlich: liegt die Buchung
    /// ausserhalb des eingestellten Zeitraums, bleibt die Liste leer. Wer
    /// hierher springt, muss den Zeitraum deshalb mit oeffnen.
    /// </summary>
    public int? ExpenseId { get; init; }
}
