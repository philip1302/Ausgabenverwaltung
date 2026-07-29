using Ausgabenverwaltung.Core.Expenses;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Ausgabenliste" - alle erfassten Ausgaben durchsuchbar.
/// Noch ohne Fachfunktion, nur der Rahmen.
/// </summary>
public sealed class AusgabenlisteViewModel : ViewModelBase
{
    private readonly ExpenseRepository _expenseRepository;

    public AusgabenlisteViewModel(ExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public string PlatzhalterText => "Ausgabenliste - hier entsteht spaeter die vollstaendige Ausgabenliste.";
}
