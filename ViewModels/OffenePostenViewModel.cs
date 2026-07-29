using Ausgabenverwaltung.Core.Expenses;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Offene Posten" - unbeglichene Ausgaben mit fremden Zahlern.
/// Noch ohne Fachfunktion, nur der Rahmen.
/// </summary>
public sealed class OffenePostenViewModel : ViewModelBase
{
    private readonly ExpenseRepository _expenseRepository;

    public OffenePostenViewModel(ExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public string PlatzhalterText => "Offene Posten - hier entsteht spaeter die Liste unbeglichener Ausgaben.";
}
