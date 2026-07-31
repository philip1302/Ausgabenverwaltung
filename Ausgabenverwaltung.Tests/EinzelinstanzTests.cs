using Ausgabenverwaltung.Core.Startup;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der zweite Programmstart bei laufender Instanz: er darf kein zweites
/// Fenster oeffnen, sondern muss das vorhandene nach vorn holen.
/// </summary>
public class EinzelinstanzTests
{
    // Jeder Test bekommt einen eigenen Pfad, sonst blockieren sich
    // gleichzeitig laufende Tests gegenseitig ueber dasselbe Mutex.
    private static string FrischerPfad()
        => Path.Combine(Path.GetTempPath(), $"ausgabenverwaltung-{Guid.NewGuid():N}", "ausgaben.db");

    [Fact]
    public void Die_erste_Ausfuehrung_bekommt_den_Platz()
    {
        using var erste = SingleInstance.Acquire(FrischerPfad());

        Assert.True(erste.IsFirstInstance);
    }

    [Fact]
    public void Eine_zweite_Ausfuehrung_erkennt_die_erste()
    {
        var pfad = FrischerPfad();

        using var erste = SingleInstance.Acquire(pfad);
        using var zweite = SingleInstance.Acquire(pfad);

        Assert.True(erste.IsFirstInstance);
        Assert.False(zweite.IsFirstInstance);
    }

    [Fact]
    public void Nach_dem_Beenden_der_ersten_ist_der_Platz_wieder_frei()
    {
        var pfad = FrischerPfad();

        using (var erste = SingleInstance.Acquire(pfad))
        {
            Assert.True(erste.IsFirstInstance);
        }

        using var naechste = SingleInstance.Acquire(pfad);

        Assert.True(naechste.IsFirstInstance);
    }

    [Fact]
    public void Verschiedene_Datenbanken_blockieren_einander_nicht()
    {
        // Zwei Anwender an einem Rechner haben verschiedene Datenbanken -
        // sie duerfen einander nicht aussperren. Genau deshalb leitet sich
        // der Name aus dem Datenbankpfad ab und nicht aus dem
        // Programmnamen.
        using var eine = SingleInstance.Acquire(FrischerPfad());
        using var andere = SingleInstance.Acquire(FrischerPfad());

        Assert.True(eine.IsFirstInstance);
        Assert.True(andere.IsFirstInstance);
    }

    [Fact]
    public void Gross_und_Kleinschreibung_des_Pfads_meint_dieselbe_Datei()
    {
        // Unter Windows sind "C:\Daten\ausgaben.db" und
        // "c:\daten\Ausgaben.db" dieselbe Datei - zwei Ausfuehrungen
        // sollen sich daran nicht vorbeimogeln.
        var pfad = FrischerPfad();

        using var erste = SingleInstance.Acquire(pfad.ToLowerInvariant());
        using var zweite = SingleInstance.Acquire(pfad.ToUpperInvariant());

        Assert.True(erste.IsFirstInstance);
        Assert.False(zweite.IsFirstInstance);
    }

    [Fact]
    public void Die_zweite_Ausfuehrung_holt_die_erste_nach_vorn()
    {
        var pfad = FrischerPfad();

        using var erste = SingleInstance.Acquire(pfad);

        // Der Horcher laeuft in einem eigenen Thread; das Ereignis
        // signalisiert, dass die Nachricht angekommen ist.
        using var angekommen = new ManualResetEventSlim(false);
        erste.StartListening(() => angekommen.Set());

        using var zweite = SingleInstance.Acquire(pfad);
        Assert.False(zweite.IsFirstInstance);

        Assert.True(zweite.SignalExistingInstance());
        Assert.True(angekommen.Wait(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void Auch_ein_dritter_Start_wird_noch_gemeldet()
    {
        // Der Horcher muss nach der ersten Nachricht weiterhorchen -
        // sonst kaeme beim dritten Start wieder nichts an, und der
        // Anwender staende vor einem Programm, das nicht aufgeht.
        var pfad = FrischerPfad();

        using var erste = SingleInstance.Acquire(pfad);

        var anzahl = 0;
        using var zweimal = new CountdownEvent(2);

        erste.StartListening(() =>
        {
            Interlocked.Increment(ref anzahl);
            zweimal.Signal();
        });

        using (var zweite = SingleInstance.Acquire(pfad))
        {
            Assert.True(zweite.SignalExistingInstance());
        }

        using (var dritte = SingleInstance.Acquire(pfad))
        {
            Assert.True(dritte.SignalExistingInstance());
        }

        Assert.True(zweimal.Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal(2, anzahl);
    }

    [Fact]
    public void Ohne_horchende_Gegenstelle_scheitert_die_Nachricht_ohne_Wurf()
    {
        // Die laufende Ausfuehrung haengt oder stammt aus einer aelteren
        // Programmversion. Dann muss SignalExistingInstance false liefern,
        // damit die Oberflaeche das Fenster "laeuft bereits" zeigen kann,
        // statt wortlos zu verschwinden.
        var pfad = FrischerPfad();

        using var erste = SingleInstance.Acquire(pfad);
        // Bewusst KEIN StartListening.

        using var zweite = SingleInstance.Acquire(pfad);

        Assert.False(zweite.SignalExistingInstance());
    }

    [Fact]
    public void Die_Meldung_zur_laufenden_Instanz_erklaert_die_Lage()
    {
        var fehler = StartupFailureText.AlreadyRunning(@"C:\Logs");

        Assert.Contains("läuft bereits", fehler.Title);
        Assert.Contains("Taskleiste", fehler.Message);
        Assert.Contains("nichts verändert", fehler.Message);
        Assert.True(fehler.RetryWorthwhile);

        // Keine technischen Begriffe im Haupttext - die stehen im
        // aufklappbaren Bereich.
        Assert.DoesNotContain("Mutex", fehler.Message);
        Assert.DoesNotContain("Pipe", fehler.Message);
        Assert.Contains("Mutex", fehler.Technical);
    }
}
