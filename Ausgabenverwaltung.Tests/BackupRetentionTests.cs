using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.Tests;

public class BackupRetentionTests
{
    // Hilfsmittel: eine Sicherung an einem bestimmten Tag, 18:42 Uhr.
    private static BackupFile Sicherung(int jahr, int monat, int tag, int stunde = 18, int minute = 42)
    {
        var zeitpunkt = new DateTime(jahr, monat, tag, stunde, minute, 0);
        var name = BackupFileName.Create(zeitpunkt);
        return new BackupFile($@"C:\Backups\{name}", name, zeitpunkt, 1024);
    }

    private static IReadOnlyList<string> Namen(IEnumerable<BackupFile> sicherungen)
        => sicherungen.Select(sicherung => sicherung.FileName).OrderBy(name => name).ToList();

    [Fact]
    public void Weniger_als_zehn_Sicherungen_werden_nie_geloescht()
    {
        var sicherungen = Enumerable.Range(1, 9)
            .Select(tag => Sicherung(2026, 7, tag))
            .ToList();

        Assert.Empty(BackupRetention.SelectExpired(sicherungen));
    }

    [Fact]
    public void Die_letzten_zehn_bleiben_erhalten()
    {
        // 20 Sicherungen im selben Monat: die zehn juengsten bleiben ueber
        // Regel 1, die aelteste des Monats faellt weg - denn "letzte des
        // Monats" ist hier die juengste, die ohnehin schon bleibt.
        var sicherungen = Enumerable.Range(1, 20)
            .Select(tag => Sicherung(2026, 7, tag))
            .ToList();

        var geloescht = BackupRetention.SelectExpired(sicherungen);

        Assert.Equal(10, geloescht.Count);
        Assert.All(geloescht, sicherung => Assert.True(sicherung.Timestamp.Day <= 10));
    }

    [Fact]
    public void Die_letzte_Sicherung_jedes_Monats_bleibt_dauerhaft()
    {
        // Drei Monate mit je drei Sicherungen (9 Stueck) plus zehn
        // Sicherungen im aktuellen Monat. Regel 1 deckt nur den aktuellen
        // Monat ab; aus den drei alten Monaten muss je genau die letzte
        // ueberleben.
        var sicherungen = new List<BackupFile>();

        foreach (var monat in new[] { 3, 4, 5 })
        {
            sicherungen.Add(Sicherung(2026, monat, 4));
            sicherungen.Add(Sicherung(2026, monat, 14));
            sicherungen.Add(Sicherung(2026, monat, 24));
        }

        for (var tag = 1; tag <= 10; tag++)
        {
            sicherungen.Add(Sicherung(2026, 7, tag));
        }

        var behalten = sicherungen
            .Except(BackupRetention.SelectExpired(sicherungen))
            .ToList();

        Assert.Equal(
            Namen(new[]
            {
                Sicherung(2026, 3, 24),
                Sicherung(2026, 4, 24),
                Sicherung(2026, 5, 24),
            }.Concat(Enumerable.Range(1, 10).Select(tag => Sicherung(2026, 7, tag)))),
            Namen(behalten));
    }

    [Fact]
    public void Mehrere_Sicherungen_am_selben_Tag_zaehlen_einzeln()
    {
        // Ein Tag mit vielen Laeufen ("Sicherung jetzt") darf die
        // Monatsstaende nicht verdraengen: die letzte des Vormonats bleibt.
        var sicherungen = new List<BackupFile>
        {
            Sicherung(2026, 6, 5),
            Sicherung(2026, 6, 28),
        };

        for (var stunde = 8; stunde < 20; stunde++)
        {
            sicherungen.Add(Sicherung(2026, 7, 30, stunde));
        }

        var geloescht = BackupRetention.SelectExpired(sicherungen);

        Assert.Contains(geloescht, sicherung => sicherung.Timestamp == new DateTime(2026, 6, 5, 18, 42, 0));
        Assert.DoesNotContain(geloescht, sicherung => sicherung.Timestamp == new DateTime(2026, 6, 28, 18, 42, 0));
    }

    [Fact]
    public void Reihenfolge_der_Eingabe_spielt_keine_Rolle()
    {
        var sicherungen = Enumerable.Range(1, 20)
            .Select(tag => Sicherung(2026, 7, tag))
            .ToList();

        var vorwaerts = Namen(BackupRetention.SelectExpired(sicherungen));
        var rueckwaerts = Namen(BackupRetention.SelectExpired(Enumerable.Reverse(sicherungen)));

        Assert.Equal(vorwaerts, rueckwaerts);
    }
}
