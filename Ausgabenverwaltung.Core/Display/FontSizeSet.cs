namespace Ausgabenverwaltung.Core.Display;

/// <summary>
/// Die drei Schriftgroessen, aus denen die gesamte Oberflaeche besteht.
/// Mehr Groessen gibt es bewusst nicht - jede weitere waere eine, die
/// beim Umschalten der Stufe vergessen werden koennte.
/// </summary>
/// <param name="Small">Nebentexte: Feldbeschriftungen ueber den Filtern,
/// Fehlermeldungen, Marker.</param>
/// <param name="Normal">Alles Uebrige - Tabellen, Formulare, Schaltflaechen.</param>
/// <param name="Heading">Ueberschriften von Dialogen und Gruppen.</param>
public sealed record FontSizeSet(double Small, double Normal, double Heading);
