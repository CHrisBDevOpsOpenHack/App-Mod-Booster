using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManager.Models;
using ExpenseManager.Services;

namespace ExpenseManager.Pages.Expenses
{
    public class CreateModel : PageModel
    {
        private readonly IExpenseService _expenseService;

        public List<User> Users { get; set; } = new();
        public List<ExpenseCategory> Categories { get; set; } = new();
        public List<ExpenseStatus> Statuses { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public CreateModel(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        public async Task OnGetAsync()
        {
            await LoadDropdownsAsync();
        }

        public async Task<IActionResult> OnPostAsync(
            int UserId, int CategoryId, int StatusId,
            decimal AmountGBP, DateTime ExpenseDate,
            string? Description, string? ReceiptFile)
        {
            var request = new CreateExpenseRequest
            {
                UserId = UserId,
                CategoryId = CategoryId,
                StatusId = StatusId,
                AmountMinor = (int)(AmountGBP * 100),
                Currency = "GBP",
                ExpenseDate = ExpenseDate,
                Description = Description,
                ReceiptFile = ReceiptFile
            };

            var (expense, error) = await _expenseService.CreateExpenseAsync(request);
            if (error != null)
            {
                ErrorMessage = error;
                await LoadDropdownsAsync();
                return Page();
            }

            return RedirectToPage("/Index");
        }

        private async Task LoadDropdownsAsync()
        {
            var (users, _) = await _expenseService.GetAllUsersAsync();
            var (categories, _) = await _expenseService.GetAllCategoriesAsync();
            var (statuses, _) = await _expenseService.GetAllStatusesAsync();
            Users = users;
            Categories = categories;
            Statuses = statuses;
        }
    }
}
