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
/// <param name="PageTitle">Seitentitel im Kopf jedes Bereichs (UI/UX-Redesign,
/// neu gegenueber den urspruenglichen drei Stufen).</param>
/// <param name="Kpi">Reine Anzeigegroesse fuer KPI-Zahlen auf der Startseite,
/// ohne eigene Bedeutungsebene - folgt derselben Skalierung wie die uebrigen
/// Stufen.</param>
public sealed record FontSizeSet(double Small, double Normal, double Heading, double PageTitle, double Kpi);
