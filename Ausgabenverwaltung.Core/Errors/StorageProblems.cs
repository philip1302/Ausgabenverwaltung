using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Core.Errors;

/// <summary>
/// Ordnet eine Ausnahme einem <see cref="StorageProblem"/> zu. Die einzige
/// Stelle im Programm, die Fehlernummern von Windows und SQLite kennt -
/// alles davor arbeitet mit Ausnahmen, alles danach mit verstaendlichen
/// Saetzen.
///
/// Reine Zuordnung ohne Seiteneffekte, deshalb vollstaendig pruefbar
/// (Regel 7).
/// </summary>
public static class StorageProblems
{
    // Fehlernummern von Windows, wie sie in den unteren 16 Bit des
    // HResult einer IOException stehen (0x8007xxxx = FACILITY_WIN32).
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorWriteProtect = 19;
    private const int ErrorNotReady = 21;
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;
    private const int ErrorHandleDiskFull = 39;
    private const int ErrorDiskFull = 112;

    // Ergebniscodes von SQLite (SqliteException.SqliteErrorCode).
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;
    private const int SqliteReadOnly = 8;
    private const int SqliteCorrupt = 11;
    private const int SqliteFull = 13;
    private const int SqliteCantOpen = 14;
    private const int SqliteNotADatabase = 26;

    /// <summary>
    /// Wie <see cref="Classify(Exception?)"/>, aber mit dem Wissen, um
    /// welche Datei es ging.
    ///
    /// Der Unterschied betrifft genau einen, dafuer wichtigen Fall: SQLite
    /// meldet eine von einem anderen Programm exklusiv geoeffnete Datei
    /// als SQLITE_CANTOPEN - denselben Code wie fuer eine Datei, die es
    /// gar nicht gibt. Ohne den Blick ins Dateisystem waere beides nicht
    /// zu unterscheiden, und der Anwender bekaeme "Der Datenträger wurde
    /// abgezogen" zu lesen, waehrend die Datei in Wahrheit direkt vor ihm
    /// liegt und nur belegt ist.
    ///
    /// Ob dahinter eine Sperre oder eine entzogene Berechtigung steckt,
    /// laesst sich auch dann nicht sagen. Die Sperre ist bei einer
    /// Desktop-Anwendung der weitaus haeufigere Fall - und ihr Text nennt
    /// die Berechtigung als zweite Moeglichkeit mit.
    /// </summary>
    public static StorageProblem ClassifyForFile(Exception? exception, string filePath)
    {
        var problem = Classify(exception);

        if (problem != StorageProblem.PathNotFound)
        {
            return problem;
        }

        try
        {
            return File.Exists(filePath) ? StorageProblem.DatabaseLocked : problem;
        }
        catch (Exception)
        {
            // Laesst sich nicht einmal das feststellen, bleibt es bei der
            // urspruenglichen Einordnung.
            return problem;
        }
    }

    public static StorageProblem Classify(Exception? exception)
    {
        return exception switch
        {
            null => StorageProblem.Unknown,

            SqliteException sqlite => ClassifySqlite(sqlite),

            // UnauthorizedAccessException kommt auch bei einer
            // schreibgeschuetzten Datei - die Unterscheidung waere fuer den
            // Anwender ohne Wert, beide Male fehlt das Schreibrecht.
            UnauthorizedAccessException => StorageProblem.AccessDenied,

            DirectoryNotFoundException => StorageProblem.PathNotFound,
            FileNotFoundException => StorageProblem.PathNotFound,

            // Die Reihenfolge zaehlt: DirectoryNotFoundException und
            // FileNotFoundException leiten von IOException ab und muessen
            // deshalb VOR diesem Zweig stehen.
            IOException io => ClassifyWin32(io.HResult),

            // Eine Ausnahme kann ihren eigentlichen Grund tiefer tragen -
            // etwa eine IOException in einer AggregateException aus einem
            // Hintergrund-Task.
            _ => exception.InnerException is null
                ? StorageProblem.Unknown
                : Classify(exception.InnerException),
        };
    }

    private static StorageProblem ClassifySqlite(SqliteException exception)
    {
        // Der erweiterte Code traegt bei IO-Fehlern die eigentliche
        // Ursache; der Basiscode reicht fuer alles, was hier unterschieden
        // wird.
        return exception.SqliteErrorCode switch
        {
            SqliteBusy or SqliteLocked => StorageProblem.DatabaseLocked,
            SqliteCorrupt or SqliteNotADatabase => StorageProblem.DatabaseCorrupt,
            SqliteFull => StorageProblem.DiskFull,
            SqliteReadOnly => StorageProblem.ReadOnly,

            // SQLITE_CANTOPEN heisst "Datei laesst sich nicht oeffnen" und
            // kommt sowohl bei fehlender Berechtigung als auch bei einem
            // verschwundenen Ordner. Der genaue Grund steckt dann in einer
            // inneren Ausnahme, sofern es eine gibt.
            SqliteCantOpen => exception.InnerException is null
                ? StorageProblem.PathNotFound
                : Classify(exception.InnerException),

            _ => StorageProblem.Unknown,
        };
    }

    private static StorageProblem ClassifyWin32(int hResult)
    {
        return (hResult & 0xFFFF) switch
        {
            ErrorDiskFull or ErrorHandleDiskFull => StorageProblem.DiskFull,
            ErrorSharingViolation or ErrorLockViolation => StorageProblem.FileInUse,
            ErrorAccessDenied => StorageProblem.AccessDenied,
            ErrorWriteProtect => StorageProblem.ReadOnly,
            ErrorPathNotFound or ErrorFileNotFound or ErrorNotReady => StorageProblem.PathNotFound,
            _ => StorageProblem.Unknown,
        };
    }
}
