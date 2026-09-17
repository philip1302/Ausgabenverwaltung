using System;
using Ausgabenverwaltung.Core.Errors;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine anstehende Bandmeldung mitsamt dem, was man damit tun kann.
///
/// Die Meldung selbst kommt aus Core (Regel 7 und 12); hier kommt nur
/// dazu, was die Oberflaeche daran haengt: der eine hervorgehobene Knopf
/// und das, was "Später" bedeutet.
///
/// Genau EIN hervorgehobener Knopf je Band - das ist der Kern. Vorher
/// standen im Aktualisierungsband drei Knoepfe nebeneinander
/// ("Veröffentlichungsseite", "Jetzt beenden", "Schließen"), und der
/// auffaelligste war der, der die Anwendung beendet, auf einem Band, das
/// gleichzeitig beteuerte, es laufe alles weiter. Mehr als eine
/// hervorgehobene Aktion macht aus einer Nachricht eine Wahl.
/// </summary>
public sealed class Bandeintrag
{
    /// <summary>
    /// Woher das Band stammt ("aktualisierung", "sicherung", ...). Dient
    /// dem Ersetzen und Entfernen: derselbe Absender zeigt nie zwei
    /// Baender zugleich, sondern loest sein eigenes ab.
    /// </summary>
    public required string Schluessel { get; init; }

    public required Bandmeldung Meldung { get; init; }

    /// <summary>Beschriftung des hervorgehobenen Knopfes. NULL = das Band hat keine Aktion.</summary>
    public string? AktionText { get; init; }

    /// <summary>Was der Knopf tut, in einem Satz - fuer den Tooltip.</summary>
    public string? AktionTipp { get; init; }

    public Action? Aktion { get; init; }

    /// <summary>
    /// Was beim Schliessen zusaetzlich geschehen soll - etwa die verworfene
    /// Fassung merken, damit dasselbe Band nicht bei jedem Start wiederkommt.
    /// Das Entfernen aus der Liste besorgt <see cref="BaenderViewModel"/>
    /// selbst.
    /// </summary>
    public Action? BeimSchliessen { get; init; }
}
