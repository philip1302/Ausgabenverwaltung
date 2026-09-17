namespace Ausgabenverwaltung.Core.Errors;

/// <summary>
/// Wie dringend ein Band ist. Die Reihenfolge der Werte ist die
/// Rangfolge: steht mehr als eine Meldung an, gewinnt die groessere
/// (siehe <see cref="Bandauswahl"/>).
///
/// Bewusst dieselben vier Rollen wie die Banner-Farbtokens der
/// Oberflaeche (ErfolgFlaeche, WarnungFlaeche, FehlerFlaeche,
/// HinweisFlaeche) - so gibt es genau EINE Einteilung und nicht eine
/// fachliche neben einer farblichen.
/// </summary>
public enum Bandrang
{
    /// <summary>Etwas ist da, das man wissen darf. Nichts ist schiefgegangen.</summary>
    Hinweis = 0,

    /// <summary>Etwas hat geklappt. Die kurzlebigste Art von Meldung.</summary>
    Erfolg = 1,

    /// <summary>Etwas braucht eine Entscheidung, aber nichts ist verloren.</summary>
    Warnung = 2,

    /// <summary>Etwas ist misslungen.</summary>
    Fehler = 3,
}

/// <summary>
/// Eine Meldung in der Gestalt, die ein Band tragen kann: eine
/// Ueberschrift, die man im Vorbeigehen erfasst, eine Zeile darunter, und
/// - falls noetig - der lange Text hinter einem Knopf.
///
/// Der Grund fuer die Dreiteilung: ein Band ist eine Statusleiste, kein
/// Absatz. Vorher stand der vollstaendige Text im Band selbst, mit
/// Leerzeilen als Absatztrenner; ueber die Fensterbreite wurden daraus
/// vier bis sechs Zeilen, in denen die eigentliche Nachricht ("es liegt
/// eine neue Fassung bereit") dieselbe Groesse und Farbe hatte wie die
/// Beruhigung ueber den Verbleib der Daten. Wer das ueberfliegt, nimmt
/// nichts davon mit.
///
/// Regel 12 bleibt dabei vollstaendig gewahrt - was ist passiert, was
/// bedeutet das fuer die Daten, was kann ich tun. Die drei Dinge stehen
/// nur nicht mehr alle im Band: <see cref="Titel"/> und
/// <see cref="Kurz"/> tragen das Was und das Naechste,
/// <see cref="Details"/> traegt die Erklaerung samt Aussage ueber die
/// Daten. Damit daraus kein Schlupfloch wird, prueft
/// <c>BandmeldungTests</c>, dass jede laengere Meldung ihre Details
/// mitbringt - eine Kurzfassung ohne Langfassung gibt es nicht.
/// </summary>
/// <param name="Rang">Dringlichkeit; entscheidet Farbe und Vorrang.</param>
/// <param name="Titel">
/// Die Aussage in einer Zeile, ohne Schlusspunkt - eine Ueberschrift,
/// kein Satz. "Fassung 1.7.0 ist bereit", nicht "Die neue Fassung 1.7.0
/// wurde geladen und geprueft."
/// </param>
/// <param name="Kurz">
/// Genau ein Satz darunter: was als Naechstes geschieht oder zu tun ist.
/// Hoechstens zwei Zeilen im Band - deshalb die Laengengrenze im Test.
/// </param>
/// <param name="Details">
/// Der vollstaendige Text fuer den Dialog dahinter. <c>null</c>, wenn die
/// Sache in einer Zeile erschoepft ist.
/// </param>
public sealed record Bandmeldung(
    Bandrang Rang,
    string Titel,
    string Kurz,
    string? Details = null)
{
    /// <summary>Ob es etwas aufzuschlagen gibt (Knopf "Details").</summary>
    public bool HatDetails => !string.IsNullOrWhiteSpace(Details);

    /// <summary>
    /// Titel, Kurzzeile und Details hintereinander - fuer das Protokoll,
    /// fuer Tests und ueberall dort, wo der ganze Text am Stueck gebraucht
    /// wird.
    /// </summary>
    public string Vollstaendig =>
        HatDetails ? $"{Titel}\n\n{Kurz}\n\n{Details}" : $"{Titel}\n\n{Kurz}";
}
