using ExpenseManager.Models;

namespace ExpenseManager.Models
{
    public class Expense
    {
        public int ExpenseId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public int AmountMinor { get; set; } // stored in pence
        public decimal AmountGBP => AmountMinor / 100.0m;
        public string Currency { get; set; } = "GBP";
        public DateTime ExpenseDate { get; set; }
        public string? Description { get; set; }
        public string? ReceiptFile { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class User
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int? ManagerId { get; set; }
        public bool IsActive { get; set; }
    }

    public class ExpenseCategory
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class ExpenseStatus
    {
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
    }

    public class CreateExpenseRequest
    {
        public int UserId { get; set; }
        public int CategoryId { get; set; }
        public int StatusId { get; set; }
        public int AmountMinor { get; set; }
        public string Currency { get; set; } = "GBP";
        public DateTime ExpenseDate { get; set; }
        public string? Description { get; set; }
        public string? ReceiptFile { get; set; }
    }

    public class UpdateExpenseRequest
    {
        public int CategoryId { get; set; }
        public int StatusId { get; set; }
        public int AmountMinor { get; set; }
        public string Currency { get; set; } = "GBP";
        public DateTime ExpenseDate { get; set; }
        public string? Description { get; set; }
        public string? ReceiptFile { get; set; }
    }

    public class ApproveRejectRequest
    {
        public int ReviewedBy { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }
    }

    public class DashboardSummary
    {
        public int TotalExpenses { get; set; }
        public int PendingExpenses { get; set; }
        public int ApprovedExpenses { get; set; }
        public int RejectedExpenses { get; set; }
        public decimal TotalAmountGBP { get; set; }
        public decimal PendingAmountGBP { get; set; }
    }
}
