using Ausgabenverwaltung.Core.Expenses;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Erfassen" - Erfassung neuer Ausgaben. Noch ohne Fachfunktion,
/// nur der Rahmen; das Repository wird bereits per DI verdrahtet, damit
/// spaeter keine Konstruktoraenderung an den Aufrufstellen noetig ist.
/// </summary>
public sealed class ErfassenViewModel : ViewModelBase
{
    private readonly ExpenseRepository _expenseRepository;

    public ErfassenViewModel(ExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public string PlatzhalterText => "Erfassen - hier entsteht spaeter die Erfassung neuer Ausgaben.";
}
