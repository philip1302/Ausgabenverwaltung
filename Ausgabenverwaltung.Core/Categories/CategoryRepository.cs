using System.Data;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Dapper;

namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Anlegen, Umbenennen, Archivieren und Baum-Laden fuer Kategorien.
/// Kategorien werden nie geloescht (Regel 8) - es gibt daher bewusst
/// keine Delete-Methode. Eindeutigkeit von Namen wird von den
/// DB-Constraints durchgesetzt (UNIQUE(ParentId, Name) und
/// UX_Category_RootName); Verstoesse schlagen als SqliteException durch.
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
        var createdUtc = DateTime.UtcNow;

        const string insertSql = """
            INSERT INTO Category (ParentId, Name, SortOrder, IsArchived, CreatedUtc)
            VALUES (@ParentId, @Name, 0, 0, @CreatedUtcText)
            """;

        _connection.Execute(insertSql, new
        {
            ParentId = parentId,
            Name = name,
            CreatedUtcText = IsoDateTime.ToUtcText(createdUtc),
        });

        var id = _connection.ExecuteScalar<long>("SELECT last_insert_rowid()");

        return new Category
        {
            Id = (int)id,
            ParentId = parentId,
            Name = name,
            SortOrder = 0,
            IsArchived = false,
            CreatedUtc = createdUtc,
        };
    }

    public void Rename(int id, string newName)
    {
        const string sql = "UPDATE Category SET Name = @Name WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id, Name = newName });
    }

    public void Archive(int id)
    {
        const string sql = "UPDATE Category SET IsArchived = 1 WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id });
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
}
