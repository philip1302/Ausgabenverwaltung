using System.Data;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Logging;
using Dapper;

namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Anlegen, Umbenennen, Archivieren, Loeschen, Zusammenfuehren und
/// Baum-Laden fuer Kategorien.
///
/// Archivieren bleibt der Normalfall (Regel 8). Geloescht wird nur, was
/// vollstaendig unbenutzt ist (<see cref="Delete"/>); eine benutzte
/// Kategorie kann stattdessen in eine andere ueberfuehrt werden
/// (<see cref="Merge"/>).
///
/// Eindeutigkeit von Namen wird zusaetzlich zu den DB-Constraints
/// (UNIQUE(ParentId, Name) und UX_Category_RootName) hier vorab
/// geprueft, damit Verstoesse als verstaendliche
/// DuplicateCategoryNameException statt als rohe SqliteException
/// durchschlagen. Dasselbe gilt fuer die Fremdschluessel
/// (ON DELETE RESTRICT) und <see cref="CategoryInUseException"/>: die
/// Datenbank bleibt die Absicherung, die verstaendliche Meldung entsteht
/// hier.
/// </summary>
public sealed class CategoryRepository
{
    private readonly IDbConnection _connection;

    public CategoryRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public Category Create(string name, int? parentId)
    {
        if (SiblingNameExists(parentId, name, excludingId: null))
        {
            throw new DuplicateCategoryNameException(name);
        }

        var createdUtc = DateTime.UtcNow;
        var sortOrder = GetNextSortOrder(parentId);

        const string insertSql = """
            INSERT INTO Category (ParentId, Name, SortOrder, IsArchived, CreatedUtc)
            VALUES (@ParentId, @Name, @SortOrder, 0, @CreatedUtcText)
            """;

        _connection.Execute(insertSql, new
        {
            ParentId = parentId,
            Name = name,
            SortOrder = sortOrder,
            CreatedUtcText = IsoDateTime.ToUtcText(createdUtc),
        });

        var id = _connection.ExecuteScalar<long>("SELECT last_insert_rowid()");

        AppLog.Current.Info(LogEvents.CategoryCreated((int)id, parentId));

        return new Category
        {
            Id = (int)id,
            ParentId = parentId,
            Name = name,
            SortOrder = sortOrder,
            IsArchived = false,
            CreatedUtc = createdUtc,
        };
    }

    public void Rename(int id, string newName)
    {
        var parentId = _connection.ExecuteScalar<int?>(
            "SELECT ParentId FROM Category WHERE Id = @Id", new { Id = id });

        if (SiblingNameExists(parentId, newName, excludingId: id))
        {
            throw new DuplicateCategoryNameException(newName);
        }

        const string sql = "UPDATE Category SET Name = @Name WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id, Name = newName });

        AppLog.Current.Info(LogEvents.CategoryRenamed(id));
    }

    /// <summary>
    /// Archiviert den Knoten. Bei includeDescendants = true werden auch
    /// alle Unterkategorien archiviert (die Oberflaeche fragt das bei
    /// Knoten mit Kindern vorher nach, siehe <see cref="GetDescendantIds"/>).
    /// </summary>
    public void Archive(int id, bool includeDescendants)
    {
        var ids = new List<int> { id };
        if (includeDescendants)
        {
            ids.AddRange(GetDescendantIds(id));
        }

        const string sql = "UPDATE Category SET IsArchived = 1 WHERE Id IN @Ids";
        _connection.Execute(sql, new { Ids = ids });

        AppLog.Current.Info(LogEvents.CategoryArchived(id, ids.Count - 1));
    }

    public void Restore(int id)
    {
        const string sql = "UPDATE Category SET IsArchived = 0 WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id });

        AppLog.Current.Info(LogEvents.CategoryRestored(id));
    }

    /// <summary>
    /// Was der Kategorie im Weg steht: zugeordnete Ausgaben,
    /// Unterkategorien, verweisende Vorlagen. Grundlage fuer die
    /// Entscheidung zwischen Loeschen, Archivieren und Zusammenfuehren -
    /// und fuer die Erklaerung, warum das eine gerade nicht geht.
    /// </summary>
    public CategoryUsage GetUsage(int id)
    {
        // Zwei Unterabfragen in einem Durchgang statt zweier Rundreisen.
        // Die Unterkategorien kommen aus dem ohnehin geladenen Baum
        // (siehe GetDescendantIds) und nicht aus rekursivem SQL.
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM Expense
                 WHERE CategoryId = @Id)                     AS ExpenseCount,
                (SELECT COUNT(*) FROM RecurringExpense
                 WHERE CategoryId = @Id)                     AS RecurringExpenseCount
            """;

        var row = _connection.QueryFirst<UsageRow>(sql, new { Id = id });

        return new CategoryUsage
        {
            ExpenseCount = row.ExpenseCount,
            ChildCount = GetDescendantIds(id).Count,
            RecurringExpenseCount = row.RecurringExpenseCount,
        };
    }

    /// <summary>
    /// Loescht die Kategorie endgueltig - nur, wenn sie vollstaendig
    /// unbenutzt ist. Andernfalls fliegt eine
    /// <see cref="CategoryInUseException"/> MIT der Zaehlung, damit die
    /// Oberflaeche benennen kann, was haengt.
    ///
    /// Eine bereits verschwundene Kategorie ist kein Fehler: dann gibt es
    /// schlicht nichts mehr zu tun.
    /// </summary>
    public void Delete(int id)
    {
        var name = _connection.ExecuteScalar<string?>(
            "SELECT Name FROM Category WHERE Id = @Id", new { Id = id });
        if (name is null)
        {
            return;
        }

        var usage = GetUsage(id);
        if (!usage.IsUnused)
        {
            throw new CategoryInUseException(name, usage);
        }

        _connection.Execute("DELETE FROM Category WHERE Id = @Id", new { Id = id });

        AppLog.Current.Info(LogEvents.CategoryDeleted(id));
    }

    /// <summary>
    /// Was <see cref="Merge"/> bewegen wuerde, ohne es zu tun. Prueft
    /// dieselben Bedingungen wie der Vorgang selbst - eine Vorschau, die
    /// gleich darauf am Zusammenfuehren scheitern wuerde, waere
    /// irrefuehrend.
    /// </summary>
    public CategoryMergePreview PreviewMerge(int sourceId, int targetId)
    {
        EnsureMergeAllowed(sourceId, targetId);

        // SumCents bleibt bewusst eine reine Addition ohne Ruecksicht auf
        // Expense.IsIncome: das ist kein Auswertungsergebnis, sondern eine
        // "wie viel Geld haengt an dieser Kategorie"-Vorschau vor dem
        // Verschieben - anders als ReportRepository/ExpenseRepository.Summarize
        // dreht sich hier fuer eine Einnahme nichts um.
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM Expense
                 WHERE CategoryId = @SourceId)               AS ExpenseCount,
                (SELECT COALESCE(SUM(AmountCents), 0) FROM Expense
                 WHERE CategoryId = @SourceId)               AS SumCents,
                (SELECT COUNT(*) FROM RecurringExpense
                 WHERE CategoryId = @SourceId)               AS RecurringExpenseCount
            """;

        var row = _connection.QueryFirst<MergePreviewRow>(sql, new { SourceId = sourceId });

        return new CategoryMergePreview
        {
            ExpenseCount = row.ExpenseCount,
            RecurringExpenseCount = row.RecurringExpenseCount,
            SumCents = row.SumCents,
        };
    }

    /// <summary>
    /// Haengt alle Ausgaben und Vorlagen der Quelle an das Ziel um und
    /// loescht die dann leere Quelle. Nicht rueckgaengig zu machen.
    ///
    /// Alles in EINER Transaktion: bliebe der Vorgang auf halbem Weg
    /// stehen, laegen Buchungen bei der einen und Vorlagen bei der
    /// anderen Kategorie, und die Quelle liesse sich nicht mehr loeschen.
    ///
    /// ModifiedUtc der bewegten Zeilen wird mitgezogen - die Buchungen
    /// haben sich geaendert, auch wenn Betrag und Datum gleich bleiben.
    /// CreatedUtc bleibt unberuehrt: erfasst wurden sie damals.
    /// </summary>
    public void Merge(int sourceId, int targetId)
    {
        EnsureMergeAllowed(sourceId, targetId);

        var nowUtcText = IsoDateTime.ToUtcText(DateTime.UtcNow);
        var parameters = new
        {
            SourceId = sourceId,
            TargetId = targetId,
            NowUtcText = nowUtcText,
        };

        using var transaction = _connection.BeginTransaction();

        const string moveExpensesSql = """
            UPDATE Expense
            SET CategoryId  = @TargetId,
                ModifiedUtc = @NowUtcText
            WHERE CategoryId = @SourceId
            """;
        var expenseCount = _connection.Execute(moveExpensesSql, parameters, transaction);

        const string moveRecurringSql = """
            UPDATE RecurringExpense
            SET CategoryId  = @TargetId,
                ModifiedUtc = @NowUtcText
            WHERE CategoryId = @SourceId
            """;
        var recurringExpenseCount = _connection.Execute(moveRecurringSql, parameters, transaction);

        const string deleteSql = "DELETE FROM Category WHERE Id = @SourceId";
        _connection.Execute(deleteSql, parameters, transaction);

        transaction.Commit();

        // Die Zeilenzahlen der beiden UPDATE-Anweisungen sind hier schon
        // bekannt - eine zusaetzliche Zaehlabfrage fuer das Protokoll waere
        // ueberfluessig.
        AppLog.Current.Info(LogEvents.CategoryMerged(sourceId, targetId, expenseCount, recurringExpenseCount));
    }

    /// <summary>
    /// Die Bedingungen des Zusammenfuehrens. Die beiden Faelle, die die
    /// Oberflaeche erklaeren muss, bekommen eine eigene Ausnahme; der
    /// Rest kann ueber die Oberflaeche gar nicht erst entstehen und
    /// bleibt eine schlichte Zusicherung.
    /// </summary>
    private void EnsureMergeAllowed(int sourceId, int targetId)
    {
        if (sourceId == targetId)
        {
            throw new InvalidOperationException(
                "Quelle und Ziel des Zusammenfuehrens sind dieselbe Kategorie.");
        }

        var tree = GetTree();

        var source = FindNode(tree, sourceId)
            ?? throw new InvalidOperationException(
                $"Die zusammenzufuehrende Kategorie (Id {sourceId}) gibt es nicht.");
        var target = FindNode(tree, targetId)
            ?? throw new InvalidOperationException(
                $"Die Zielkategorie (Id {targetId}) gibt es nicht.");

        if (source.Children.Count > 0)
        {
            var alleNachfahren = new List<int>();
            CollectIds(source.Children, alleNachfahren);
            throw new CategoryHasChildrenException(source.Category.Name, alleNachfahren.Count);
        }

        // Nur Blattknoten koennen Ziel sein - Ausgaben lassen sich auch
        // sonst nirgends anders zuordnen (siehe GetSelectableLeaves).
        // Ueber die Oberflaeche ist das nicht auswaehlbar; die Zusicherung
        // steht hier, damit kein anderer Weg daran vorbeikommt.
        if (target.Children.Count > 0)
        {
            throw new InvalidOperationException(
                $"Die Zielkategorie \"{target.Category.Name}\" hat Unterkategorien " +
                "und kann deshalb keine Ausgaben aufnehmen.");
        }
    }

    /// <summary>
    /// Setzt die eigene Farbe oder nimmt sie zurueck (NULL = keine
    /// eigene, die Kategorie erbt dann wieder). Werte ausserhalb der
    /// Palette werden gar nicht erst gespeichert - sonst stuende in der
    /// Datenbank ein Wert, den die Anzeige spaeter ohnehin verwirft
    /// (siehe <see cref="CategoryColors"/>).
    /// </summary>
    public void SetColor(int id, string? colorHex)
    {
        var value = CategoryColorPalette.IsKnown(colorHex) ? colorHex : null;

        const string sql = "UPDATE Category SET Color = @Color WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id, Color = value });

        AppLog.Current.Info(LogEvents.CategoryColorChanged(id));
    }

    /// <summary>
    /// Die aufgeloeste Farbe je Kategorie-Id - einmal geladen fuer alle
    /// Ansichten, die Kategorien einfaerben (Ausgabenliste, Report,
    /// Erfassung).
    /// </summary>
    public IReadOnlyDictionary<int, string> GetResolvedColors()
        => CategoryColors.Resolve(GetTree());

    /// <summary>
    /// Alle Nachfahren-Ids eines Knotens (ohne den Knoten selbst), fuer
    /// die Nachfrage vor dem Archivieren und fuer die Archivierungs-
    /// Kaskade. Ueber den bereits geladenen Baum ermittelt statt per
    /// rekursivem SQL, weil GetTree() ohnehin fuer die Anzeige gebraucht wird.
    /// </summary>
    public IReadOnlyList<int> GetDescendantIds(int id)
    {
        var node = FindNode(GetTree(), id);
        if (node is null)
        {
            return Array.Empty<int>();
        }

        var result = new List<int>();
        CollectIds(node.Children, result);
        return result;
    }

    public void MoveUp(int id) => Reorder(id, delta: -1);

    public void MoveDown(int id) => Reorder(id, delta: +1);

    /// <summary>
    /// Vertauscht die SortOrder mit dem vorherigen (delta -1) bzw.
    /// naechsten (delta +1) Geschwisterknoten in der Anzeigereihenfolge.
    /// Steht der Knoten bereits am Anfang/Ende, passiert nichts.
    /// </summary>
    private void Reorder(int id, int delta)
    {
        var current = _connection.QueryFirstOrDefault<CurrentNodeRow>(
            "SELECT ParentId, SortOrder FROM Category WHERE Id = @Id", new { Id = id });
        if (current is null)
        {
            return;
        }

        const string siblingsSql = """
            SELECT Id, SortOrder
            FROM Category
            WHERE ParentId IS @ParentId
            ORDER BY SortOrder, Name
            """;
        var siblings = _connection.Query<SiblingRow>(siblingsSql, new { current.ParentId }).ToList();

        var index = siblings.FindIndex(s => s.Id == id);
        var neighbourIndex = index + delta;
        if (index < 0 || neighbourIndex < 0 || neighbourIndex >= siblings.Count)
        {
            return;
        }

        var self = siblings[index];
        var neighbour = siblings[neighbourIndex];

        const string updateSql = "UPDATE Category SET SortOrder = @SortOrder WHERE Id = @Id";
        _connection.Execute(updateSql, new { Id = self.Id, SortOrder = neighbour.SortOrder });
        _connection.Execute(updateSql, new { Id = neighbour.Id, SortOrder = self.SortOrder });

        AppLog.Current.Info(LogEvents.CategoryMoved(id, delta));
    }

    /// <summary>
    /// Anzahl der direkt zugeordneten Ausgaben je Kategorie, fuer die
    /// Anzeige im Baum vor dem Archivieren (Kategorien ohne Eintrag fehlen
    /// im Ergebnis).
    /// </summary>
    public IReadOnlyDictionary<int, int> GetExpenseCounts()
    {
        const string sql = """
            SELECT CategoryId, COUNT(*) AS Anzahl
            FROM Expense
            GROUP BY CategoryId
            """;

        return _connection.Query<CategoryExpenseCountRow>(sql)
            .ToDictionary(row => row.CategoryId, row => row.Anzahl);
    }

    // IS statt = , damit auch Oberkategorien (ParentId NULL) korrekt
    // verglichen werden - zwei NULL-Werte sind mit = nie gleich.
    private bool SiblingNameExists(int? parentId, string name, int? excludingId)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM Category
            WHERE ParentId IS @ParentId
              AND Name = @Name
              AND (@ExcludingId IS NULL OR Id <> @ExcludingId)
            """;

        var count = _connection.ExecuteScalar<int>(
            sql, new { ParentId = parentId, Name = name, ExcludingId = excludingId });
        return count > 0;
    }

    private int GetNextSortOrder(int? parentId)
    {
        const string sql = """
            SELECT COALESCE(MAX(SortOrder), -1) + 1
            FROM Category
            WHERE ParentId IS @ParentId
            """;

        return _connection.ExecuteScalar<int>(sql, new { ParentId = parentId });
    }

    private static CategoryNode? FindNode(IReadOnlyList<CategoryNode> nodes, int id)
    {
        foreach (var node in nodes)
        {
            if (node.Category.Id == id)
            {
                return node;
            }

            var found = FindNode(node.Children, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static void CollectIds(IReadOnlyList<CategoryNode> nodes, List<int> result)
    {
        foreach (var node in nodes)
        {
            result.Add(node.Category.Id);
            CollectIds(node.Children, result);
        }
    }

    public IReadOnlyList<CategoryNode> GetTree()
    {
        const string sql = """
            SELECT Id, ParentId, Name, SortOrder, IsArchived, Color, CreatedUtc
            FROM Category
            ORDER BY ParentId, SortOrder, Name
            """;

        var rows = _connection.Query<CategoryRow>(sql);

        var nodesById = new Dictionary<int, CategoryNode>();
        foreach (var row in rows)
        {
            nodesById[row.Id] = new CategoryNode(new Category
            {
                Id = row.Id,
                ParentId = row.ParentId,
                Name = row.Name,
                SortOrder = row.SortOrder,
                IsArchived = row.IsArchived,
                Color = row.Color,
                CreatedUtc = IsoDateTime.ParseUtc(row.CreatedUtc),
            });
        }

        var roots = new List<CategoryNode>();
        foreach (var node in nodesById.Values)
        {
            if (node.Category.ParentId is int parentId)
            {
                nodesById[parentId].Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }

    /// <summary>
    /// Fuer die Erfassungsmaske waehlbare Kategorien: nur Blattknoten
    /// (Kategorien ohne Kinder), nur nicht-archivierte, mit vollem Pfad
    /// ab der Wurzel ("Pferde › Hufschmied"). Ob ein Vorfahre archiviert
    /// ist, spielt keine Rolle - archiviert wird pro Zeile, nicht als
    /// Kaskade ueber den Ast (Regel 8 archiviert einzelne Kategorien).
    /// </summary>
    public IReadOnlyList<CategoryOption> GetSelectableLeaves()
    {
        var tree = GetTree();
        var colors = CategoryColors.Resolve(tree);

        var leaves = new List<CategoryOption>();
        CollectLeaves(tree, parentPath: null, colors, leaves);
        return leaves;
    }

    private static void CollectLeaves(
        IReadOnlyList<CategoryNode> nodes,
        string? parentPath,
        IReadOnlyDictionary<int, string> colors,
        List<CategoryOption> leaves)
    {
        foreach (var node in nodes)
        {
            var path = CategoryPaths.Append(parentPath, node.Category.Name);

            if (node.Children.Count == 0)
            {
                if (!node.Category.IsArchived)
                {
                    leaves.Add(new CategoryOption
                    {
                        Id = node.Category.Id,
                        FullPath = path,
                        Color = colors[node.Category.Id],
                    });
                }
            }
            else
            {
                CollectLeaves(node.Children, path, colors, leaves);
            }
        }
    }

    // CreatedUtc wird als reiner TEXT gelesen statt ueber automatische
    // Dapper/DateTime-Konvertierung, damit das Parsen des Formats
    // 'YYYY-MM-DDTHH:MM:SSZ' zentral ueber IsoDateTime laeuft.
    private sealed class CategoryRow
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsArchived { get; set; }
        public string? Color { get; set; }
        public string CreatedUtc { get; set; } = string.Empty;
    }

    private sealed class CurrentNodeRow
    {
        public int? ParentId { get; set; }
        public int SortOrder { get; set; }
    }

    private sealed class SiblingRow
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
    }

    private sealed class CategoryExpenseCountRow
    {
        public int CategoryId { get; set; }
        public int Anzahl { get; set; }
    }

    private sealed class UsageRow
    {
        public int ExpenseCount { get; set; }
        public int RecurringExpenseCount { get; set; }
    }

    private sealed class MergePreviewRow
    {
        public int ExpenseCount { get; set; }
        public int RecurringExpenseCount { get; set; }
        public long SumCents { get; set; }
    }
}
