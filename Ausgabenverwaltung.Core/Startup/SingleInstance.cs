using System.Globalization;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using Ausgabenverwaltung.Core.Logging;

namespace Ausgabenverwaltung.Core.Startup;

/// <summary>
/// Sorgt dafuer, dass die Anwendung nur einmal laeuft - und dass ein
/// zweiter Start das vorhandene Fenster nach vorn holt, statt kommentarlos
/// zu verschwinden.
///
/// Zwei Bausteine, weil ein einzelner beides nicht kann:
///
/// 1. Ein benanntes <see cref="Mutex"/> beantwortet die Frage "laeuft schon
///    eine?". Das Betriebssystem gibt es beim Beenden des Prozesses von
///    selbst frei, auch bei einem Absturz - eine Sperrdatei auf der Platte
///    bliebe dagegen liegen und muesste beim naechsten Start muehsam als
///    verwaist erkannt werden.
///
/// 2. Eine benannte Pipe traegt die Nachricht "komm nach vorn" von der
///    zweiten zur ersten Ausfuehrung. Das Mutex allein kann das nicht: es
///    sagt nur, DASS jemand da ist, nicht wo dessen Fenster steckt.
///
/// Der Name beider Bausteine leitet sich aus dem PFAD DER DATENBANK ab,
/// nicht aus dem Programmnamen. Das ist die genauere Frage: es geht nicht
/// darum, dass die Anwendung zweimal laeuft, sondern dass zwei
/// Ausfuehrungen dieselbe Datei beschreiben. Zwei Anwender an einem
/// Rechner haben verschiedene Datenbanken und duerfen einander deshalb
/// nicht blockieren.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    // "Local\" und nicht "Global\": der Namensraum gilt dann je
    // Anmeldesitzung. Fuer eine Anwendung, die im eigenen
    // Anwendungsdatenordner arbeitet, ist das die richtige Grenze. Der
    // seltene Fall zweier gleichzeitiger Sitzungen desselben Anwenders
    // (etwa Konsole und Remotedesktop) faellt damit durch dieses Netz -
    // ihn faengt die Dateisperre von SQLite und die zugehoerige Meldung.
    private const string Scope = "Local\\";

    // Wie lange die zweite Ausfuehrung auf die erste wartet. Kurz genug,
    // dass niemand vor einem stehenden Fenster wartet, lang genug fuer
    // eine beschaeftigte erste Ausfuehrung.
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(3);

    // Welche Plaetze DIESER Prozess bereits belegt hat.
    //
    // Das Mutex allein reicht dafuer nicht: es ist wiedereintrittsfaehig.
    // Ein zweiter Acquire aus demselben Thread bekaeme es anstandslos noch
    // einmal und hielte sich fuer die erste Ausfuehrung. In der fertigen
    // Anwendung faellt das nicht auf - dort ruft jeder Prozess genau
    // einmal auf -, aber eine Zusage, die nur unter dieser Annahme gilt,
    // ist keine. Und pruefen liesse sie sich so auch nicht.
    private static readonly HashSet<string> BelegteSchluessel = new(StringComparer.Ordinal);

    private readonly Mutex? _mutex;
    private readonly string _pipeName;
    private readonly string? _key;
    private readonly CancellationTokenSource _listenerStop = new();

    private bool _mutexHeld;
    private bool _disposed;

    private SingleInstance(
        Mutex? mutex, bool isFirstInstance, string pipeName, bool mutexHeld, string? key)
    {
        _mutex = mutex;
        _pipeName = pipeName;
        _mutexHeld = mutexHeld;
        _key = key;
        IsFirstInstance = isFirstInstance;
    }

    /// <summary>
    /// Ob dies die einzige laufende Ausfuehrung ist. Bei <c>false</c>
    /// laeuft bereits eine - dann gehoert
    /// <see cref="SignalExistingInstance"/> aufgerufen und dieser Start
    /// beendet.
    /// </summary>
    public bool IsFirstInstance { get; }

    /// <summary>
    /// Belegt den Platz fuer die angegebene Datenbankdatei.
    ///
    /// Scheitert das - fehlende Rechte, ein Betriebssystem ohne benannte
    /// Mutexe -, gilt der Start als erste Ausfuehrung. Eine Anwendung, die
    /// wegen ihrer Doppelstart-Erkennung gar nicht erst startet, waere die
    /// schlechtere von beiden Moeglichkeiten.
    /// </summary>
    public static SingleInstance Acquire(string databaseFilePath)
    {
        var key = BuildKey(databaseFilePath);
        var pipeName = "ausgabenverwaltung-" + key;

        // Erst im eigenen Prozess nachsehen (siehe BelegteSchluessel).
        lock (BelegteSchluessel)
        {
            if (BelegteSchluessel.Contains(key))
            {
                return new SingleInstance(
                    mutex: null, isFirstInstance: false, pipeName, mutexHeld: false, key: null);
            }
        }

        try
        {
            var mutex = new Mutex(initiallyOwned: true, Scope + "ausgabenverwaltung-" + key, out var createdNew);

            if (createdNew)
            {
                return Belege(mutex, pipeName, key);
            }

            // Das Mutex gibt es bereits - es kann aber von einer
            // abgestuerzten Ausfuehrung verwaist sein. WaitOne(0) meldet
            // das ueber AbandonedMutexException und uebergibt es dabei an
            // uns; dann sind wir die erste Ausfuehrung.
            try
            {
                if (mutex.WaitOne(TimeSpan.Zero))
                {
                    return Belege(mutex, pipeName, key);
                }
            }
            catch (AbandonedMutexException)
            {
                return Belege(mutex, pipeName, key);
            }

            return new SingleInstance(
                mutex, isFirstInstance: false, pipeName, mutexHeld: false, key: null);
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Pruefen auf eine bereits laufende Instanz", ex);

            // Siehe oben: lieber starten als wegen der Pruefung nicht
            // starten.
            return new SingleInstance(
                mutex: null, isFirstInstance: true, pipeName, mutexHeld: false, key: null);
        }
    }

    private static SingleInstance Belege(Mutex mutex, string pipeName, string key)
    {
        lock (BelegteSchluessel)
        {
            BelegteSchluessel.Add(key);
        }

        return new SingleInstance(mutex, isFirstInstance: true, pipeName, mutexHeld: true, key);
    }

    /// <summary>
    /// Sagt der laufenden Ausfuehrung, sie moege ihr Fenster nach vorn
    /// holen. Liefert <c>false</c>, wenn die Nachricht nicht ankam - dann
    /// gehoert dem Anwender trotzdem etwas gesagt, statt ihn vor einem
    /// Bildschirm ohne Reaktion stehen zu lassen.
    /// </summary>
    public bool SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", _pipeName, PipeDirection.Out, PipeOptions.None);

            client.Connect((int)SignalTimeout.TotalMilliseconds);
            client.WriteByte(1);
            client.Flush();

            return true;
        }
        catch (Exception ex)
        {
            // Die laufende Ausfuehrung haengt, ist gerade am Beenden oder
            // stammt aus einer aelteren Programmversion ohne diese Pipe.
            AppLog.Current.Exception("Beim Benachrichtigen der laufenden Instanz", ex);
            return false;
        }
    }

    /// <summary>
    /// Nimmt Nachrichten spaeterer Startversuche entgegen und ruft
    /// <paramref name="onActivate"/> auf. Der Aufruf kommt aus einem
    /// Hintergrund-Thread - wer daraufhin ein Fenster anfasst, muss selbst
    /// auf den Oberflaechen-Thread wechseln.
    /// </summary>
    public void StartListening(Action onActivate)
    {
        if (!IsFirstInstance)
        {
            return;
        }

        // Das Abbruchmerkmal wird EINMAL hier abgegriffen und nicht in der
        // Schleife immer wieder aus der Quelle geholt: Dispose kann
        // waehrenddessen laufen, und ein Zugriff auf .Token nach dem
        // Aufraeumen wirft - auf einem Hintergrund-Thread, wo niemand ihn
        // faengt. Das hat den Prozess mitgenommen, ausgerechnet beim
        // Beenden.
        var abbruch = _listenerStop.Token;

        // Eigener Thread statt Task.Run: die Schleife wartet die ganze
        // Programmlaufzeit ueber und wuerde einen Arbeitsthread des
        // ThreadPools dauerhaft belegen.
        var thread = new Thread(() => Listen(onActivate, abbruch))
        {
            IsBackground = true,
            Name = "Einzelinstanz-Horcher",
        };

        thread.Start();
    }

    private void Listen(Action onActivate, CancellationToken abbruch)
    {
        // Die Pipe wird EINMAL angelegt und ueber die ganze Laufzeit
        // behalten; zwischen zwei Nachrichten macht Disconnect sie wieder
        // aufnahmebereit.
        //
        // Frueher entstand je Nachricht eine neue Pipe. Unter Windows ging
        // das gut, unter Unix nicht: dort bildet .NET benannte Pipes auf
        // Unix-Domain-Sockets ab. Verbindet sich der naechste Start,
        // waehrend die vorige Nachricht noch verarbeitet wird, wartet er
        // in der Annahmeschlange des ALTEN Sockets - und die wird beim
        // Neuanlegen verworfen. Die Nachricht war weg, ohne dass es jemand
        // bemerkte: SignalExistingInstance meldete trotzdem Erfolg. Der
        // dritte Start holte das Fenster damit nicht mehr nach vorn.
        //
        // Behaelt man dieselbe Pipe, bleibt die Annahmeschlange bestehen
        // und die wartende Verbindung wird im naechsten Durchlauf bedient.
        NamedPipeServerStream? server = null;

        try
        {
            while (!abbruch.IsCancellationRequested)
            {
                try
                {
                    // Beim ersten Durchlauf - und nach einem Fehler, der
                    // die Pipe unbrauchbar gemacht haben koennte.
                    server ??= new NamedPipeServerStream(
                        _pipeName, PipeDirection.In, maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    server.WaitForConnectionAsync(abbruch).GetAwaiter().GetResult();

                    if (abbruch.IsCancellationRequested)
                    {
                        return;
                    }

                    server.ReadByte();

                    // Erst trennen, dann melden: onActivate holt ein
                    // Fenster nach vorn und braucht dafuer merklich Zeit.
                    // Waehrenddessen muss die Pipe schon wieder
                    // aufnahmebereit sein, sonst geht genau die Nachricht
                    // verloren, die in dieser Zeit eintrifft.
                    server.Disconnect();

                    AppLog.Current.Info(LogEvents.SecondInstanceRejected());
                    onActivate();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    // Beim Beenden geraeumt, waehrend hier noch gewartet
                    // wurde. Kein Fehler, sondern das Ende.
                    return;
                }
                catch (Exception ex)
                {
                    // Diese Schleife laeuft neben allem anderen her. Sie darf
                    // die Anwendung unter keinen Umstaenden mitreissen - ein
                    // nicht nach vorn geholtes Fenster ist ein Aergernis, ein
                    // Absturz deswegen waere absurd.
                    AppLog.Current.Exception("Im Horcher fuer weitere Programmstarts", ex);

                    // In welchem Zustand die Pipe nach dem Fehler ist,
                    // laesst sich nicht sagen - deshalb wegwerfen und im
                    // naechsten Durchlauf neu anlegen.
                    server?.Dispose();
                    server = null;

                    // Kurz durchatmen, damit ein dauerhafter Fehler nicht in
                    // eine Endlosschleife mit voller Last laeuft.
                    if (abbruch.WaitHandle.WaitOne(TimeSpan.FromSeconds(1)))
                    {
                        return;
                    }
                }
            }
        }
        finally
        {
            server?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_key is not null)
        {
            lock (BelegteSchluessel)
            {
                BelegteSchluessel.Remove(_key);
            }
        }

        try
        {
            _listenerStop.Cancel();
        }
        catch (Exception)
        {
        }

        try
        {
            if (_mutexHeld)
            {
                _mutex?.ReleaseMutex();
                _mutexHeld = false;
            }
        }
        catch (Exception)
        {
            // Wird der Mutex nicht sauber freigegeben, gilt er beim
            // naechsten Start als verwaist - Acquire faengt das ab.
        }

        _mutex?.Dispose();

        // Die Abbruchquelle wird bewusst NICHT geraeumt. Der Horcher-
        // Thread haelt ihr Wartehandle unter Umstaenden noch fest, und ein
        // Zugriff darauf nach dem Raeumen wirft dort, wo niemand faengt.
        // Eine abgebrochene, liegen gelassene Quelle kostet nichts - sie
        // haelt keine unverwalteten Betriebsmittel, solange sie ohne
        // Zeitgeber benutzt wird, und die Speicherbereinigung holt sie
        // zusammen mit dieser Ausfuehrung ab.
    }

    /// <summary>
    /// Der Erkennungsname aus dem Datenbankpfad. Nicht der Pfad selbst:
    /// er enthaelt Trennzeichen, die in Mutex- und Pipe-Namen nicht
    /// vorkommen duerfen, und waere ausserdem laenger als erlaubt. Der
    /// Hash dient nur der Unterscheidung, nicht der Geheimhaltung.
    /// </summary>
    private static string BuildKey(string databaseFilePath)
    {
        // Gross-/Kleinschreibung vereinheitlicht: unter Windows sind
        // "C:\Daten\ausgaben.db" und "c:\daten\Ausgaben.db" dieselbe Datei,
        // und zwei Ausfuehrungen sollen sich nicht daran vorbeimogeln.
        var normalized = databaseFilePath.Trim().ToLowerInvariant();

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));

        var builder = new StringBuilder(16);
        foreach (var b in hash.AsSpan(0, 8))
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
