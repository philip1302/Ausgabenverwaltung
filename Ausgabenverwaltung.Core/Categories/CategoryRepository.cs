using System.Data;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Dapper;

namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Anlegen, Umbenennen, Archivieren und Baum-Laden fuer Kategorien.
/// Kategorien werden nie geloescht (Regel 8) - es gibt daher bewusst
/// keine Delete-Methode. Eindeutigkeit von Namen wird zusaetzlich zu den
/// DB-Constraints (UNIQUE(ParentId, Name) und UX_Category_RootName) hier
/// vorab geprueft, damit Verstoesse als verstaendliche
/// DuplicateCategoryNameException statt als rohe SqliteException
/// durchschlagen.
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
    }

    public void Restore(int id)
    {
        const string sql = "UPDATE Category SET IsArchived = 0 WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id });
    }

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
            SELECT Id, ParentId, Name, SortOrder, IsArchived, CreatedUtc
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
        var leaves = new List<CategoryOption>();
        CollectLeaves(GetTree(), parentPath: null, leaves);
        return leaves;
    }

    private static void CollectLeaves(IReadOnlyList<CategoryNode> nodes, string? parentPath, List<CategoryOption> leaves)
    {
        foreach (var node in nodes)
        {
            var path = parentPath is null ? node.Category.Name : $"{parentPath} › {node.Category.Name}";

            if (node.Children.Count == 0)
            {
                if (!node.Category.IsArchived)
                {
                    leaves.Add(new CategoryOption { Id = node.Category.Id, FullPath = path });
                }
            }
            else
            {
                CollectLeaves(node.Children, path, leaves);
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
}
