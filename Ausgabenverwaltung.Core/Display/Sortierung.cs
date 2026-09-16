namespace Ausgabenverwaltung.Core.Display;

/// <summary>
/// Das Verhalten einer sortierbaren Tabellenueberschrift - einmal, fuer
/// jede Liste. Zwei Dinge gehoeren dazu und standen vorher Wort fuer Wort
/// gleich in der Ausgabenliste und in den offenen Posten, nur mit
/// verschiedenen Aufzaehlungstypen daneben:
///
/// 1. Ein Klick auf die Spalte, auf die schon sortiert wird, DREHT die
///    Richtung um; ein Klick auf eine andere stellt auf diese Spalte um
///    und faengt aufsteigend an (<see cref="NaechsteRichtung"/>).
/// 2. Die Ueberschrift der aktiven Spalte traegt ein Dreieck in
///    Sortierrichtung, alle uebrigen nichts (<see cref="KopfText"/>).
///
/// Warum in Core und nicht bei den ViewModels: die Regel "gleiche Spalte
/// dreht um" ist pruefbares Verhalten und kein Anzeigekram (Regel 7), und
/// sie ist die Stelle, an der ein Fehler still bleibt - eine falsch
/// herum gedrehte Sortierung sieht aus wie eine Sortierung.
///
/// Der Typparameter ist die Spaltenaufzaehlung der jeweiligen Liste
/// (<see cref="Expenses.ExpenseSortColumn"/>,
/// <see cref="OpenItems.OpenItemsSortColumn"/>). Generisch und nicht je
/// Liste abgeschrieben, damit eine neue sortierbare Liste nichts
/// mitzubringen hat ausser ihrer Aufzaehlung.
/// </summary>
public static class Sortierung
{
    /// <summary>Dreieck fuer aufsteigend - kleinster Wert oben.</summary>
    public const string PfeilAufsteigend = "▲";

    /// <summary>Dreieck fuer absteigend - groesster Wert oben.</summary>
    public const string PfeilAbsteigend = "▼";

    /// <summary>
    /// Wandelt den Kommandoparameter einer Spaltenueberschrift in den
    /// Aufzaehlungswert um. Unbekannter Text liefert NULL, der Aufrufer
    /// laesst die Sortierung dann stehen.
    ///
    /// Vorher stand dafuer in jeder Liste ein switch, der jeden
    /// Spaltennamen noch einmal von Hand auf seinen Aufzaehlungswert
    /// abbildete - und der still auf die bisherige Sortierung zurueckfiel,
    /// wenn ein Name nicht dabei war. Eine neue Spalte, im XAML angelegt
    /// und im switch vergessen, hatte deshalb keinen Fehler zur Folge,
    /// sondern eine Ueberschrift, auf die man klicken kann und bei der
    /// nichts geschieht. Die Namen der Aufzaehlungswerte SIND die
    /// Kommandoparameter (siehe die Ansichten), also braucht es die
    /// Abbildung gar nicht.
    ///
    /// Gelesen wird ueber <see cref="Aufzaehlung.NachName{T}(string?)"/>,
    /// damit hier dieselbe Strenge gilt wie beim Lesen der
    /// Einstellungsdatei: nur Namen, keine Zahlen.
    /// </summary>
    public static TSpalte? Spalte<TSpalte>(string? kommandoParameter)
        where TSpalte : struct, Enum
        => Aufzaehlung.NachName<TSpalte>(kommandoParameter);

    /// <summary>
    /// Was ein Klick auf <paramref name="geklickt"/> aus der aktuellen
    /// Sortierung macht.
    ///
    /// Dieselbe Spalte dreht die Richtung um. Eine andere Spalte faengt
    /// aufsteigend an - und zwar unabhaengig davon, wie herum die vorige
    /// Spalte stand: die Richtung gehoert zur Spalte, nicht zur Tabelle,
    /// und eine frisch gewaehlte Spalte absteigend zu beginnen waere fuer
    /// den Anwender nicht vorhersagbar.
    /// </summary>
    public static (TSpalte Spalte, bool Aufsteigend) NaechsteRichtung<TSpalte>(
        TSpalte geklickt,
        TSpalte aktuelleSpalte,
        bool aktuellAufsteigend)
        where TSpalte : struct, Enum
        => EqualityComparer<TSpalte>.Default.Equals(geklickt, aktuelleSpalte)
            ? (aktuelleSpalte, !aktuellAufsteigend)
            : (geklickt, true);

    /// <summary>
    /// Die Beschriftung einer Spaltenueberschrift: mit Dreieck, wenn nach
    /// ihr sortiert wird, sonst nur die Bezeichnung. Das Dreieck ist ein
    /// ZUSATZ zum Text und nie sein Ersatz - dieselbe Ueberlegung wie bei
    /// den Kategoriefarben (Regel 10).
    /// </summary>
    public static string KopfText<TSpalte>(
        string bezeichnung,
        TSpalte spalte,
        TSpalte aktuelleSpalte,
        bool aufsteigend)
        where TSpalte : struct, Enum
        => EqualityComparer<TSpalte>.Default.Equals(spalte, aktuelleSpalte)
            ? $"{bezeichnung} {(aufsteigend ? PfeilAufsteigend : PfeilAbsteigend)}"
            : bezeichnung;
}
