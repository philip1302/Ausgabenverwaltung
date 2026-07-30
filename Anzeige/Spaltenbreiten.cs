using System;
using System.ComponentModel;
using Ausgabenverwaltung.Core.Display;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Die eine Stelle, an der die von Hand eingestellten Spaltenbreiten
/// haengen - dasselbe Muster wie <see cref="Skalierung"/> fuer die
/// Schriftgroesse, und aus demselben Grund ein Singleton: die Breite
/// gehoert keiner Ansicht, und die Bindungen in den Ansichten brauchen
/// eine Quelle unabhaengig vom DataContext.
///
/// Zur Zeit ist das genau eine Breite: die Kategoriespalte. Sie gilt
/// bewusst fuer ALLE Tabellen mit einer Kategoriespalte - eine gezogene
/// Breite, die nur in einem Bereich wirkt, waere schwer zu erklaeren.
///
/// Gehalten wird die Breite fuer die Schriftstufe "Normal";
/// <see cref="KategorieSkaliert"/> ist derselbe Wert in der aktuell
/// eingestellten Groesse. Beide melden sich, wenn sich die Breite ODER die
/// Schriftgroesse aendert - die Raster und Mindestbreiten der Ansichten
/// haengen daran.
/// </summary>
public sealed class Spaltenbreiten : INotifyPropertyChanged
{
    public static Spaltenbreiten Aktuell { get; } = new();

    private Spaltenbreiten()
    {
        // Eine Aenderung der Schriftgroesse aendert die angezeigte Breite,
        // ohne die gemerkte anzutasten. Beide Singletons leben so lange wie
        // die Anwendung, deshalb wird hier nichts wieder abgemeldet.
        Skalierung.Aktuell.PropertyChanged += (_, _) => Melde();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Wird beim Start verdrahtet (siehe App.axaml.cs) und schreibt die
    /// Breite in dieselbe Einstellungsdatei, in der auch Schriftgroesse und
    /// Sicherungsziel stehen. Ohne Verdrahtung wirkt das Ziehen nur bis zum
    /// Beenden - so laeuft die Ansicht auch im Entwurfsmodus.
    /// </summary>
    public Action<double>? Sichern { get; set; }

    /// <summary>Breite der Kategoriespalte bei Stufe "Normal", in Pixeln.</summary>
    public double Kategorie { get; private set; } = ColumnWidths.CategoryDefault;

    /// <summary>Dieselbe Breite in der eingestellten Schriftgroesse.</summary>
    public double KategorieSkaliert => Math.Round(Kategorie * Skalierung.Aktuell.Faktor);

    /// <summary>
    /// Uebernimmt eine Breite - beim Start aus der Einstellungsdatei,
    /// danach bei jedem Ziehen am Spaltengriff. Der Wert wird in den
    /// erlaubten Bereich gebracht (siehe <see cref="ColumnWidths"/>).
    /// </summary>
    public void SetzeKategorie(double breite)
    {
        var geprueft = ColumnWidths.NormalizeCategory(breite);
        if (geprueft == Kategorie)
        {
            return;
        }

        Kategorie = geprueft;
        Melde();
    }

    /// <summary>
    /// Merkt die aktuelle Breite dauerhaft. Bewusst getrennt vom Setzen:
    /// waehrend des Ziehens fallen dutzende Zwischenwerte an, und keiner
    /// davon gehoert in eine Datei.
    /// </summary>
    public void SichereKategorie() => Sichern?.Invoke(Kategorie);

    private void Melde()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Kategorie)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KategorieSkaliert)));
    }
}
