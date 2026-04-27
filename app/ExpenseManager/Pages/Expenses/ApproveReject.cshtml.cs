using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManager.Models;
using ExpenseManager.Services;

namespace ExpenseManager.Pages.Expenses
{
    public class ApproveModel : PageModel
    {
        private readonly IExpenseService _expenseService;

        public Expense? Expense { get; set; }
        public string? ErrorMessage { get; set; }

        public ApproveModel(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null && error == null) return NotFound();
            Expense = expense;
            ErrorMessage = error;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id, int reviewerId)
        {
            var (expense, error) = await _expenseService.ApproveExpenseAsync(id, reviewerId);
            if (error != null)
            {
                ErrorMessage = error;
                return Page();
            }
            return RedirectToPage("/Index");
        }
    }

    public class RejectModel : PageModel
    {
        private readonly IExpenseService _expenseService;

        public Expense? Expense { get; set; }
        public string? ErrorMessage { get; set; }

        public RejectModel(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null && error == null) return NotFound();
            Expense = expense;
            ErrorMessage = error;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id, int reviewerId)
        {
            var (expense, error) = await _expenseService.RejectExpenseAsync(id, reviewerId);
            if (error != null)
            {
                ErrorMessage = error;
                return Page();
            }
            return RedirectToPage("/Index");
        }
    }
}
