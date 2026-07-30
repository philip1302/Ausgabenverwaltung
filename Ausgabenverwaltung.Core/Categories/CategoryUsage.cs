namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Was einer Kategorie im Weg steht, wenn sie geloescht werden soll.
/// Drei Zahlen statt eines blossen "ist in Verwendung": die Oberflaeche
/// soll benennen koennen, WAS haengt ("18 Ausgaben, 2 Unterkategorien,
/// 1 Vorlage") - sonst bleibt dem Anwender nur Raten.
///
/// Archivierte Unterkategorien und Vorlagen zaehlen mit: sie stehen als
/// Zeile in der Datenbank und wuerden am Fremdschluessel scheitern
/// (ON DELETE RESTRICT), egal wie sie in der Anzeige aussehen.
/// </summary>
public sealed record CategoryUsage
{
    /// <summary>Direkt zugeordnete Ausgaben - nicht die des ganzen Astes.</summary>
    public required int ExpenseCount { get; init; }

    /// <summary>Alle Nachfahren, nicht nur die direkten Kinder.</summary>
    public required int ChildCount { get; init; }

    /// <summary>Vorlagen wiederkehrender Buchungen, die darauf verweisen.</summary>
    public required int RecurringExpenseCount { get; init; }

    /// <summary>
    /// Nur eine vollstaendig unbenutzte Kategorie darf geloescht werden
    /// (Regel 8). Alles andere wird archiviert oder zusammengefuehrt.
    /// </summary>
    public bool IsUnused =>
        ExpenseCount == 0 && ChildCount == 0 && RecurringExpenseCount == 0;
}
