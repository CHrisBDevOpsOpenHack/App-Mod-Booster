using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManager.Models;
using ExpenseManager.Services;

namespace ExpenseManager.Pages.Expenses
{
    public class DetailsModel : PageModel
    {
        private readonly IExpenseService _expenseService;

        public Expense? Expense { get; set; }
        public string? ErrorMessage { get; set; }

        public DetailsModel(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
            Expense = expense;
            ErrorMessage = error;
            if (expense == null && error == null) return NotFound();
            return Page();
        }
    }
}
