using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Verwaltung" - Personen, Kategorien und Vorlagen fuer
/// wiederkehrende Buchungen pflegen. Noch ohne Fachfunktion, nur der
/// Rahmen.
/// </summary>
public sealed class VerwaltungViewModel : ViewModelBase
{
    private readonly PersonRepository _personRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly RecurringExpenseRepository _recurringExpenseRepository;

    public VerwaltungViewModel(
        PersonRepository personRepository,
        CategoryRepository categoryRepository,
        RecurringExpenseRepository recurringExpenseRepository)
    {
        _personRepository = personRepository;
        _categoryRepository = categoryRepository;
        _recurringExpenseRepository = recurringExpenseRepository;
    }

    public string PlatzhalterText =>
        "Verwaltung - hier entsteht spaeter die Pflege von Personen, Kategorien und Vorlagen.";
}
