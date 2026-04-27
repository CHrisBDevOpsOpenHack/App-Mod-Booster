using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManager.Models;
using ExpenseManager.Services;

namespace ExpenseManager.Pages.Expenses
{
    public class EditModel : PageModel
    {
        private readonly IExpenseService _expenseService;

        public Expense? Expense { get; set; }
        public List<ExpenseCategory> Categories { get; set; } = new();
        public List<ExpenseStatus> Statuses { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public EditModel(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null && error == null) return NotFound();
            Expense = expense;
            ErrorMessage = error;
            await LoadDropdownsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int ExpenseId, int CategoryId, int StatusId,
            decimal AmountGBP, DateTime ExpenseDate, string? Description, string? ReceiptFile)
        {
            var request = new UpdateExpenseRequest
            {
                CategoryId = CategoryId,
                StatusId = StatusId,
                AmountMinor = (int)(AmountGBP * 100),
                Currency = "GBP",
                ExpenseDate = ExpenseDate,
                Description = Description,
                ReceiptFile = ReceiptFile
            };

            var (expense, error) = await _expenseService.UpdateExpenseAsync(ExpenseId, request);
            if (error != null)
            {
                ErrorMessage = error;
                var (exp, _) = await _expenseService.GetExpenseByIdAsync(ExpenseId);
                Expense = exp;
                await LoadDropdownsAsync();
                return Page();
            }

            return RedirectToPage("/Index");
        }

        private async Task LoadDropdownsAsync()
        {
            var (cats, _) = await _expenseService.GetAllCategoriesAsync();
            var (statuses, _) = await _expenseService.GetAllStatusesAsync();
            Categories = cats;
            Statuses = statuses;
        }
    }
}
