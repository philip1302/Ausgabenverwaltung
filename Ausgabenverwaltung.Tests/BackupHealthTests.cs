using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der Gesamtzustand der Datensicherung.
///
/// Diese Ampel ist der Kern des Bereichs "Datensicherung": sie
/// beantwortet die einzige Frage, die die Seite stellt. Deshalb steht sie
/// in Core und wird hier gegen alle Faelle gehalten - besonders gegen die
/// beiden, die eine je Ziel getrennte Anzeige frueher falsch dargestellt
/// hat.
/// </summary>
public class BackupHealthTests
{
    private static readonly DateTime Jetzt = new(2026, 8, 7, 10, 0, 0, DateTimeKind.Utc);

    private static BackupResult Lauf(BackupOutcome ziel1, BackupOutcome ziel2)
        => new() { Primary = ziel1, External = ziel2 };

    [Fact]
    public void Beide_Ziele_aktuell_ist_gesichert()
    {
        var zustand = BackupHealth.Evaluate(
            Lauf(BackupOutcome.Succeeded, BackupOutcome.Succeeded),
            backupCount: 3,
            lastLocalBackup: DateTime.Now,
            externalConfigured: true,
            lastExternalBackupUtc: Jetzt,
            Jetzt);

        Assert.Equal(BackupHealthLevel.Gesichert, zustand.Level);
        Assert.False(zustand.PrimaryFailed);
        Assert.False(zustand.ExternalFailed);
        Assert.False(zustand.ExternalStale);
    }

    [Fact]
    public void Ohne_zweites_Ziel_ist_der_Zustand_eingeschraenkt()
    {
        var zustand = BackupHealth.Evaluate(
            Lauf(BackupOutcome.Succeeded, BackupOutcome.NotConfigured),
            backupCount: 3,
            lastLocalBackup: DateTime.Now,
            externalConfigured: false,
            lastExternalBackupUtc: null,
            Jetzt);

        Assert.Equal(BackupHealthLevel.Eingeschraenkt, zustand.Level);

        // "Fehlt" und "veraltet" sind zwei verschiedene Aussagen - ohne
        // Ziel gibt es nichts, das veralten koennte.
        Assert.False(zustand.ExternalStale);
        Assert.False(zustand.ExternalFailed);
    }

    [Fact]
    public void Ein_nicht_erreichbares_zweites_Ziel_ist_eingeschraenkt_und_kein_Ausfall()
    {
        var zustand = BackupHealth.Evaluate(
            Lauf(BackupOutcome.Succeeded, BackupOutcome.Failed),
            backupCount: 3,
            lastLocalBackup: DateTime.Now,
            externalConfigured: true,
            lastExternalBackupUtc: Jetzt,
            Jetzt);

        Assert.Equal(BackupHealthLevel.Eingeschraenkt, zustand.Level);
        Assert.True(zustand.ExternalFailed);
    }

    [Fact]
    public void Ein_zu_lange_nicht_beschriebenes_zweites_Ziel_ist_eingeschraenkt()
    {
        var zustand = BackupHealth.Evaluate(
            Lauf(BackupOutcome.Succeeded, BackupOutcome.Skipped),
            backupCount: 3,
            lastLocalBackup: DateTime.Now,
            externalConfigured: true,
            lastExternalBackupUtc: Jetzt.AddDays(-(ExternalBackupAge.StaleAfterDays + 1)),
            Jetzt);

        Assert.Equal(BackupHealthLevel.Eingeschraenkt, zustand.Level);
        Assert.True(zustand.ExternalStale);
    }

    // ================= Die beiden Grenzfaelle =================

    [Fact]
    public void Ziel_1_fehlgeschlagen_wiegt_schwerer_als_ein_aktuelles_Ziel_2()
    {
        // Genau der Fall, den die alte Anzeige verschwieg: oben stand
        // "Externe Sicherung aktuell", waehrend es fuer heute ueberhaupt
        // keine neue Sicherung gab.
        var zustand = BackupHealth.Evaluate(
            Lauf(BackupOutcome.Failed, BackupOutcome.Failed),
            backupCount: 3,
            lastLocalBackup: DateTime.Now.AddDays(-1),
            externalConfigured: true,
            lastExternalBackupUtc: Jetzt,
            Jetzt);

        Assert.Equal(BackupHealthLevel.NichtGesichert, zustand.Level);
        Assert.True(zustand.PrimaryFailed);
    }

    [Fact]
    public void Ohne_jede_Sicherung_ist_der_Zustand_nicht_gesichert_auch_ohne_Fehler()
    {
        // Der erste Start: nichts ist schiefgegangen, es gibt nur noch
        // nichts. Fuer die Frage "bin ich abgesichert?" ist das dasselbe.
        var zustand = BackupHealth.Evaluate(
            lastRun: null,
            backupCount: 0,
            lastLocalBackup: null,
            externalConfigured: false,
            lastExternalBackupUtc: null,
            Jetzt);

        Assert.Equal(BackupHealthLevel.NichtGesichert, zustand.Level);
        Assert.False(zustand.PrimaryFailed);
    }

    [Fact]
    public void Ohne_Lauf_in_dieser_Sitzung_zaehlen_die_vorhandenen_Dateien()
    {
        // Beim Start wurde uebersprungen ("heute bereits gesichert") -
        // das ist kein Grund zur Beunruhigung, die Sicherung ist ja da.
        var zustand = BackupHealth.Evaluate(
            BackupResult.SkippedToday(),
            backupCount: 12,
            lastLocalBackup: DateTime.Now,
            externalConfigured: true,
            lastExternalBackupUtc: Jetzt.AddDays(-1),
            Jetzt);

        Assert.Equal(BackupHealthLevel.Gesichert, zustand.Level);
    }

    // ================= Texte =================

    [Theory]
    [InlineData(BackupHealthLevel.Gesichert)]
    [InlineData(BackupHealthLevel.Eingeschraenkt)]
    [InlineData(BackupHealthLevel.NichtGesichert)]
    public void Jeder_Zustand_hat_eine_Ueberschrift_und_eine_Erklaerung(BackupHealthLevel stufe)
    {
        Assert.NotEmpty(BackupHealthText.Ueberschrift(stufe));

        // Die Grundsaetze selbst prueft MeldungsGrundsaetzeTests; hier
        // geht es nur darum, dass kein Zustand ohne Text dasteht.
        Assert.NotEmpty(BackupHealthText.Erklaerung(stufe));
    }

    [Fact]
    public void Die_Zeitangabe_fasst_beide_Ziele_in_einem_Satz_zusammen()
    {
        var jetztLokal = new DateTime(2026, 8, 7, 10, 0, 0, DateTimeKind.Local);

        var zustand = BackupHealth.Evaluate(
            Lauf(BackupOutcome.Succeeded, BackupOutcome.Succeeded),
            backupCount: 1,
            lastLocalBackup: new DateTime(2026, 8, 7, 8, 14, 0, DateTimeKind.Local),
            externalConfigured: true,
            lastExternalBackupUtc: Jetzt.AddDays(-2),
            Jetzt);

        var text = BackupHealthText.Zeitangabe(zustand, jetztLokal, Jetzt);

        Assert.Contains("heute 08:14", text);
        Assert.Contains("vor 2 Tagen", text);
    }

    [Fact]
    public void Ohne_zweites_Ziel_sagt_die_Zeitangabe_genau_das()
    {
        var jetztLokal = new DateTime(2026, 8, 7, 10, 0, 0, DateTimeKind.Local);

        var zustand = BackupHealth.Evaluate(
            lastRun: null,
            backupCount: 0,
            lastLocalBackup: null,
            externalConfigured: false,
            lastExternalBackupUtc: null,
            Jetzt);

        var text = BackupHealthText.Zeitangabe(zustand, jetztLokal, Jetzt);

        Assert.Contains("noch keine Sicherung", text);
        Assert.Contains("nicht eingerichtet", text);
    }
}
