using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManager.Models;
using ExpenseManager.Services;

namespace ExpenseManager.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IExpenseService _expenseService;

        public List<Expense> Expenses { get; set; } = new();
        public List<ExpenseStatus> Statuses { get; set; } = new();
        public List<ExpenseCategory> Categories { get; set; } = new();
        public DashboardSummary? Summary { get; set; }
        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CategoryFilter { get; set; }

        public IndexModel(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        public async Task OnGetAsync()
        {
            var (expenses, expError) = await _expenseService.GetAllExpensesAsync(statusId: StatusFilter, categoryId: CategoryFilter);
            var (statuses, statusError) = await _expenseService.GetAllStatusesAsync();
            var (categories, catError) = await _expenseService.GetAllCategoriesAsync();
            var (summary, summaryError) = await _expenseService.GetDashboardSummaryAsync();

            Expenses = expenses;
            Statuses = statuses;
            Categories = categories;
            Summary = summary;

            // Collect the first non-null error
            ErrorMessage = expError ?? statusError ?? catError ?? summaryError;
        }
    }
}
