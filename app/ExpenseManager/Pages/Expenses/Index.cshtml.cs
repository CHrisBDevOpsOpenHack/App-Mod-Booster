using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManager.Models;
using ExpenseManager.Services;

namespace ExpenseManager.Pages.Expenses
{
    public class ExpensesListModel : PageModel
    {
        private readonly IExpenseService _expenseService;

        public List<Expense> Expenses { get; set; } = new();
        public List<ExpenseStatus> Statuses { get; set; } = new();
        public List<ExpenseCategory> Categories { get; set; } = new();
        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CategoryFilter { get; set; }

        public ExpensesListModel(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        public async Task OnGetAsync()
        {
            var (expenses, err1) = await _expenseService.GetAllExpensesAsync(statusId: StatusFilter, categoryId: CategoryFilter);
            var (statuses, err2) = await _expenseService.GetAllStatusesAsync();
            var (categories, err3) = await _expenseService.GetAllCategoriesAsync();
            Expenses = expenses;
            Statuses = statuses;
            Categories = categories;
            ErrorMessage = err1 ?? err2 ?? err3;
        }
    }
}
