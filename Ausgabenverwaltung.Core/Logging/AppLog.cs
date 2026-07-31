using System.Text;

namespace Ausgabenverwaltung.Core.Logging;

/// <summary>
/// Schreibt das Protokoll: eine Datei je Kalendertag im Protokollordner,
/// die letzten <see cref="LogRetention.KeepDays"/> Tage bleiben liegen.
///
/// Zwei Eigenschaften machen diese Klasse aus:
///
/// 1. <b>Sie wirft nie.</b> Jeder Schreibversuch liegt in einem
///    try/catch, das alles schluckt. Ein volles Laufwerk, ein entzogenes
///    Schreibrecht oder ein von aussen geloeschter Ordner duerfen die
///    Anwendung nicht mit hinunterreissen - schon gar nicht die
///    Protokollierung, die ja gerade im Fehlerfall gebraucht wird. Der
///    Preis ist, dass ein nicht geschriebener Eintrag unbemerkt bleibt;
///    ihn irgendwo zu melden, hiesse das Problem nur zu verschieben.
///
/// 2. <b>Sie ist ueber <see cref="Current"/> von ueberall erreichbar.</b>
///    Ausnahmen entstehen an Stellen, an die kein Konstruktorparameter
///    reicht - im globalen Auffangnetz, in einem Hintergrund-Task, in
///    einem Ereignishandler. Bis <see cref="Start"/> gerufen wurde (und
///    falls es scheitert), steht dort eine Ausfuehrung, die nichts tut.
///
/// Was protokolliert wird, steht in <see cref="LogEvents"/> - und
/// ausdruecklich ohne Betraege, Bemerkungen und Personennamen.
/// </summary>
public sealed class AppLog
{
    // Ein Schloss fuer alle Schreibvorgaenge dieser Ausfuehrung. Eintraege
    // entstehen auch aus Hintergrundaufgaben, und zwei gleichzeitige
    // Anhaenge an dieselbe Datei ergaeben ineinander geschobene Zeilen.
    private readonly object _lock = new();

    private readonly string? _folderPath;

    // MIT Signatur: eine Protokolldatei wird im Zweifel mit dem
    // erstbesten Editor geoeffnet, und ohne sie zeigen aeltere Editoren
    // unter Windows die Umlaute falsch an.
    private static readonly Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private AppLog(string? folderPath)
    {
        _folderPath = folderPath;
    }

    /// <summary>
    /// Die Protokollierung dieser Programmausfuehrung. Vor
    /// <see cref="Start"/> eine Ausfuehrung, die alles stillschweigend
    /// verwirft - damit Aufrufer nie auf NULL pruefen muessen.
    /// </summary>
    public static AppLog Current { get; private set; } = new(folderPath: null);

    /// <summary>Der Ordner, in dem geschrieben wird - NULL, solange nicht
    /// protokolliert wird. Fuer den Knopf "Protokollordner oeffnen".</summary>
    public string? FolderPath => _folderPath;

    /// <summary>Ob ueberhaupt geschrieben wird.</summary>
    public bool IsEnabled => _folderPath is not null;

    /// <summary>
    /// Richtet die Protokollierung ein und raeumt dabei die abgelaufenen
    /// Dateien weg. Scheitert das Anlegen des Ordners, bleibt
    /// <see cref="Current"/> die stille Ausfuehrung - der Programmstart
    /// laeuft dann ohne Protokoll weiter statt gar nicht.
    /// </summary>
    public static void Start(string folderPath, DateOnly today)
    {
        try
        {
            Directory.CreateDirectory(folderPath);

            var log = new AppLog(folderPath);
            log.ApplyRetention(today);
            Current = log;
        }
        catch (Exception)
        {
            // Siehe Klassenkommentar: kein Protokoll ist besser als ein
            // Programm, das am Protokoll scheitert.
        }
    }

    /// <summary>Nur fuer Tests: nimmt die Protokollierung zurueck.</summary>
    public static void Stop() => Current = new AppLog(folderPath: null);

    public void Info(string message) => Write(LogLevel.Info, message);

    public void Warning(string message) => Write(LogLevel.Warning, message);

    /// <summary>
    /// Eine Ausnahme mit vollstaendigem Aufrufstapel. <paramref name="context"/>
    /// sagt, WO sie auftrat ("Beim Speichern einer Ausgabe") - der
    /// Aufrufstapel allein beantwortet das nach einer Weile nicht mehr.
    /// </summary>
    public void Exception(string context, Exception exception)
    {
        AppendLines(LogLine.FormatException(DateTime.UtcNow, context, exception));
    }

    /// <summary>
    /// Die Datei, in die gerade geschrieben wird. NULL, wenn nicht
    /// protokolliert wird.
    /// </summary>
    public string? GetCurrentFilePath(DateOnly today)
        => _folderPath is null ? null : Path.Combine(_folderPath, LogFileName.Create(today));

    /// <summary>
    /// Die vorhandenen Protokolldateien, neueste zuerst. Fremde Dateien im
    /// Ordner werden ausgelassen (siehe <see cref="LogFileName.TryParseDate"/>).
    /// </summary>
    public IReadOnlyList<LogFile> ListFiles()
    {
        if (_folderPath is null || !Directory.Exists(_folderPath))
        {
            return [];
        }

        try
        {
            var files = new List<LogFile>();

            foreach (var path in Directory.EnumerateFiles(_folderPath, "*.log"))
            {
                var fileName = Path.GetFileName(path);
                if (LogFileName.TryParseDate(fileName, out var date))
                {
                    files.Add(new LogFile(path, fileName, date));
                }
            }

            return files.OrderByDescending(file => file.Date).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private void Write(LogLevel level, string message)
    {
        AppendLines(LogLine.Format(DateTime.UtcNow, level, message));
    }

    // Jeder Eintrag oeffnet die Datei, haengt an und schliesst wieder.
    // Ein dauerhaft offener Schreiber waere schneller, wuerde aber
    // ausgerechnet den letzten Eintrag vor einem harten Absturz im Puffer
    // behalten - also genau den, auf den es ankommt.
    private void AppendLines(string text)
    {
        if (_folderPath is null)
        {
            return;
        }

        try
        {
            var path = Path.Combine(_folderPath, LogFileName.Create(DateOnly.FromDateTime(DateTime.Now)));

            lock (_lock)
            {
                File.AppendAllText(path, text + Environment.NewLine, FileEncoding);
            }
        }
        catch (Exception)
        {
            // Siehe Klassenkommentar.
        }
    }

    private void ApplyRetention(DateOnly today)
    {
        try
        {
            foreach (var expired in LogRetention.SelectExpired(ListFiles(), today))
            {
                try
                {
                    File.Delete(expired.FullPath);
                }
                catch (Exception)
                {
                    // Eine gesperrte Datei bleibt eben einen Tag laenger
                    // liegen. Kein Grund, den Start aufzuhalten.
                }
            }
        }
        catch (Exception)
        {
        }
    }
}
