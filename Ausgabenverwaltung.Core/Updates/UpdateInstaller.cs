using System.Globalization;
using System.Runtime.InteropServices;
using Ausgabenverwaltung.Core.Logging;

namespace Ausgabenverwaltung.Core.Updates;

/// <summary>
/// Der Austausch selbst - und zwar GANZ FRUEH beim Programmstart, bevor
/// Protokollordner, Datenbank, Einzelinstanz-Sperre oder Fenster
/// existieren.
///
/// Der Zeitpunkt ist der eigentliche Kunstgriff. Mitten im Betrieb waere
/// der Austausch heikel: die Datenbankverbindung steht, ein halb
/// getauschter Zustand bei einem Stromausfall waere schwer zu ordnen, und
/// unter Windows laesst sich eine laufende Programmdatei ohnehin nicht
/// ueberschreiben. Beim Start dagegen haengt nichts daran - schlaegt der
/// Austausch fehl, wird er verworfen und die Anwendung startet ganz
/// normal in der alten Fassung weiter.
///
/// Windows kann eine LAUFENDE Programmdatei zwar nicht ueberschreiben,
/// aber sehr wohl umbenennen. Genau darauf beruht der Ablauf: die alte
/// Datei wird zur Seite benannt, die neue nimmt ihren Platz ein, und der
/// neue Prozess startet. Unter macOS gilt dasselbe fuer das Bundle -
/// Unix benennt ueber den Inode um, das laufende Programm stoert das
/// nicht.
/// </summary>
public static class UpdateInstaller
{
    /// <summary>
    /// Das Ergebnis eines Uebernahmeversuchs.
    /// </summary>
    public enum Ergebnis
    {
        /// <summary>Nichts lag bereit - der Normalfall.</summary>
        NichtsVorbereitet,

        /// <summary>Getauscht; der Aufrufer muss den neuen Prozess starten
        /// und sich sofort beenden.</summary>
        Uebernommen,

        /// <summary>Es lag etwas bereit, liess sich aber nicht uebernehmen.
        /// Es wurde verworfen, der Start laeuft normal weiter.</summary>
        Gescheitert,
    }

    /// <summary>
    /// Was ausgetauscht wird: unter Windows die Programmdatei selbst,
    /// unter macOS das ganze .app-Bundle, in dem sie steckt.
    ///
    /// <c>null</c>, wenn sich das nicht bestimmen laesst (etwa weil die
    /// Anwendung nicht als eigenstaendige Datei laeuft) - dann findet
    /// keine Aktualisierung statt.
    /// </summary>
    public static string? ZielPfad()
    {
        try
        {
            var eigenerPfad = Environment.ProcessPath;
            if (string.IsNullOrEmpty(eigenerPfad))
            {
                return null;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return eigenerPfad;
            }

            // Unter macOS liegt die ausfuehrbare Datei in
            // <Name>.app/Contents/MacOS/<Name>. Ausgetauscht wird nicht
            // sie allein, sondern das Bundle darum - sonst blieben
            // Info.plist und Symbol auf dem alten Stand.
            var bundle = SucheBundle(eigenerPfad);
            return bundle ?? eigenerPfad;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? SucheBundle(string ausfuehrbareDatei)
    {
        var ordner = Directory.GetParent(ausfuehrbareDatei);

        // Hoechstens drei Ebenen hoch: MacOS -> Contents -> <Name>.app
        for (var i = 0; i < 3 && ordner is not null; i++)
        {
            if (ordner.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                return ordner.FullName;
            }

            ordner = ordner.Parent;
        }

        return null;
    }

    /// <summary>
    /// Entfernt die Reste eines vorangegangenen Austauschs: die
    /// beiseitegelegte alte Fassung und, was ein abgebrochenes Laden
    /// liegen liess (siehe <see cref="UpdateStaging.RestEndungen"/>).
    ///
    /// Beim ersten Start nach einem Austausch ist die alte Fassung noch
    /// einen Augenblick gesperrt, weil der Vorgaenger gerade erst endete -
    /// deshalb raeumt <see cref="UpdateStaging.LoescheHartnaeckig"/>
    /// dahinter mit mehreren Versuchen. Ein einziger traf genau in diese
    /// Sperre und liess die Datei sichtbar liegen, oft ueber Wochen.
    ///
    /// Die vorbereitete Fassung selbst (<c>.neu</c>) fasst diese Methode
    /// NICHT an - sie ist der Grund, warum ueberhaupt neu gestartet wurde.
    /// </summary>
    public static void RaeumeAlteAuf(string zielPfad)
        => UpdateStaging.RaeumeReste(zielPfad);

    /// <summary>
    /// Uebernimmt eine vorbereitete Fassung, falls eine bereitliegt und
    /// sie der Pruefung standhaelt.
    ///
    /// Wirft nie. Jeder Fehlschlag endet mit
    /// <see cref="Ergebnis.Gescheitert"/>, verworfener Vorbereitung und
    /// einem Protokolleintrag - die Anwendung startet dann in der alten
    /// Fassung weiter. Das ist immer die bessere von beiden
    /// Moeglichkeiten: eine veraltete Anwendung ist ein Aergernis, eine
    /// nicht mehr startende ein Schaden.
    /// </summary>
    public static Ergebnis TryUebernehmen(string zielPfad)
    {
        var neu = UpdateStaging.NeuPfad(zielPfad);

        try
        {
            var zettel = UpdateStaging.LiesZettel(zielPfad);

            // Ohne Begleitzettel gilt eine daliegende Datei als
            // unvollstaendig - etwa als Rest eines abgebrochenen
            // Downloads.
            if (zettel is null)
            {
                if (File.Exists(neu) || Directory.Exists(neu))
                {
                    UpdateStaging.Verwirf(zielPfad);
                }

                return Ergebnis.NichtsVorbereitet;
            }

            if (!File.Exists(neu) && !Directory.Exists(neu))
            {
                UpdateStaging.Verwirf(zielPfad);
                return Ergebnis.NichtsVorbereitet;
            }

            // Die Pruefsumme wird hier ein ZWEITES Mal geprueft, obwohl
            // sie beim Herunterladen schon stimmte. Zwischen damals und
            // jetzt liegt mindestens ein Programmende, womoeglich ein
            // Absturz oder ein Virenscanner - und was gleich ausgefuehrt
            // wird, prueft man unmittelbar davor.
            if (!UpdateStaging.PasstZumZettel(neu, zettel))
            {
                AppLog.Current.Warning(LogEvents.UpdateVerworfen(zettel.Version));
                UpdateStaging.Verwirf(zielPfad);
                return Ergebnis.Gescheitert;
            }

            var alt = UpdateStaging.AltPfad(zielPfad);

            // Ein Rest vom vorletzten Mal wuerde das Umbenennen scheitern
            // lassen.
            UpdateStaging.LoescheHartnaeckig(alt);

            // Der eigentliche Austausch: zwei Umbenennungen innerhalb
            // desselben Ordners. Zwischen ihnen liegt der einzige
            // heikle Augenblick - faellt hier der Strom aus, fehlt die
            // Programmdatei. Deshalb der Umweg ueber ".alt" statt eines
            // Loeschens: die alte Fassung ist bis zuletzt vorhanden und
            // liesse sich von Hand zurueckbenennen.
            //
            // Die REIHENFOLGE des Versteckens gehoert zum Austausch dazu.
            // Sie ist so gewaehlt, dass nie zwei sichtbare Programmdateien
            // nebeneinander liegen:
            //
            //   1. exe      -> exe.alt
            //   2. exe.alt  verstecken
            //   3. exe.neu  -> exe          (kommt versteckt aus dem Laden)
            //   4. exe      sichtbar machen
            //
            // Andersherum - erst die neue zeigen, dann die alte
            // verstecken - staende genau dazwischen zweimal fast derselbe
            // Name im Ordner, und das ist der Zustand, den der ganze
            // Aufwand hier abschafft. So entsteht stattdessen ein
            // Augenblick, in dem GAR KEINE Programmdatei zu sehen ist. Das
            // ist die bessere der beiden Moeglichkeiten: es dauert zwei
            // Umbenennungen, liegt im Start vor dem ersten Fenster, und
            // "kurz nichts" versteht jeder - "drei fast gleiche" niemand.
            Bewege(zielPfad, alt);
            UpdateStaging.Verstecke(alt);

            try
            {
                Bewege(neu, zielPfad);
            }
            catch (Exception)
            {
                // Der zweite Schritt ging schief - die alte Fassung
                // zurueckholen, sonst startet gar nichts mehr. Und sie
                // wieder sichtbar machen: eine versteckte Programmdatei
                // waere schlimmer als ein Rest zu viel.
                Bewege(alt, zielPfad);
                UpdateStaging.Zeige(zielPfad);
                throw;
            }

            UpdateStaging.Zeige(zielPfad);

            UpdateStaging.LoescheStill(UpdateStaging.ZettelPfad(zielPfad));

            AppLog.Current.Info(LogEvents.UpdateUebernommen(zettel.Version));

            return Ergebnis.Uebernommen;
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Uebernehmen einer Aktualisierung", ex);
            UpdateStaging.Verwirf(zielPfad);

            return Ergebnis.Gescheitert;
        }
    }

    // File.Move kann keine Verzeichnisse, Directory.Move keine Dateien -
    // und unter macOS ist das Ziel ein Verzeichnis (.app-Bundle).
    private static void Bewege(string von, string nach)
    {
        if (Directory.Exists(von))
        {
            Directory.Move(von, nach);
        }
        else
        {
            File.Move(von, nach);
        }
    }

    /// <summary>
    /// Startet die soeben uebernommene Fassung. Der eigene Prozess muss
    /// sich unmittelbar danach beenden - der neue wartet darauf.
    ///
    /// Das Starten selbst und das Verabreden mit dem Nachfolger stehen in
    /// <see cref="Startup.Neustart"/>; hier bleibt nur, was der
    /// Aktualisierung eigen ist: unter macOS ist das Ziel das Bundle,
    /// starten muss man die Datei darin.
    /// </summary>
    public static bool StarteNeuenProzess(string zielPfad)
        => Startup.Neustart.Starte(
            AusfuehrbareDatei(zielPfad), Path.GetDirectoryName(zielPfad) ?? string.Empty);

    /// <summary>
    /// Unter macOS ist das Ziel das Bundle, gestartet werden muss aber
    /// die Datei darin.
    /// </summary>
    private static string AusfuehrbareDatei(string zielPfad)
    {
        if (!zielPfad.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            return zielPfad;
        }

        var name = Path.GetFileNameWithoutExtension(zielPfad);
        return Path.Combine(zielPfad, "Contents", "MacOS", name);
    }

}
