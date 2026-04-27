using Microsoft.Data.SqlClient;
using System.Data;
using ExpenseManager.Models;

namespace ExpenseManager.Services
{
    public interface IExpenseService
    {
        Task<(List<Expense> expenses, string? error)> GetAllExpensesAsync(int? userId = null, int? statusId = null, int? categoryId = null);
        Task<(Expense? expense, string? error)> GetExpenseByIdAsync(int id);
        Task<(Expense? expense, string? error)> CreateExpenseAsync(CreateExpenseRequest request);
        Task<(Expense? expense, string? error)> UpdateExpenseAsync(int id, UpdateExpenseRequest request);
        Task<(bool success, string? error)> DeleteExpenseAsync(int id);
        Task<(Expense? expense, string? error)> ApproveExpenseAsync(int id, int reviewedBy);
        Task<(Expense? expense, string? error)> RejectExpenseAsync(int id, int reviewedBy);
        Task<(List<User> users, string? error)> GetAllUsersAsync();
        Task<(List<ExpenseCategory> categories, string? error)> GetAllCategoriesAsync();
        Task<(List<ExpenseStatus> statuses, string? error)> GetAllStatusesAsync();
        Task<(DashboardSummary summary, string? error)> GetDashboardSummaryAsync();
    }

    public class ExpenseService : IExpenseService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ExpenseService> _logger;

        // Dummy data for fallback when DB is unavailable
        private static readonly List<Expense> _dummyExpenses = new()
        {
            new Expense { ExpenseId = 1, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-7), Description = "Taxi from airport to client site", SubmittedAt = DateTime.Now.AddDays(-7), CreatedAt = DateTime.Now.AddDays(-7) },
            new Expense { ExpenseId = 2, UserId = 1, UserName = "Alice Example", CategoryId = 2, CategoryName = "Meals", StatusId = 3, StatusName = "Approved", AmountMinor = 1425, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-15), Description = "Client lunch meeting", SubmittedAt = DateTime.Now.AddDays(-15), ReviewedByName = "Bob Manager", ReviewedAt = DateTime.Now.AddDays(-14), CreatedAt = DateTime.Now.AddDays(-15) },
            new Expense { ExpenseId = 3, UserId = 1, UserName = "Alice Example", CategoryId = 3, CategoryName = "Supplies", StatusId = 1, StatusName = "Draft", AmountMinor = 799, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-2), Description = "Office stationery", CreatedAt = DateTime.Now.AddDays(-2) },
            new Expense { ExpenseId = 4, UserId = 1, UserName = "Alice Example", CategoryId = 4, CategoryName = "Accommodation", StatusId = 3, StatusName = "Approved", AmountMinor = 12300, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-30), Description = "Hotel during client visit", SubmittedAt = DateTime.Now.AddDays(-29), ReviewedByName = "Bob Manager", ReviewedAt = DateTime.Now.AddDays(-28), CreatedAt = DateTime.Now.AddDays(-30) }
        };

        private static readonly List<User> _dummyUsers = new()
        {
            new User { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true },
            new User { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true }
        };

        private static readonly List<ExpenseCategory> _dummyCategories = new()
        {
            new ExpenseCategory { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new ExpenseCategory { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new ExpenseCategory { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new ExpenseCategory { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new ExpenseCategory { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };

        private static readonly List<ExpenseStatus> _dummyStatuses = new()
        {
            new ExpenseStatus { StatusId = 1, StatusName = "Draft" },
            new ExpenseStatus { StatusId = 2, StatusName = "Submitted" },
            new ExpenseStatus { StatusId = 3, StatusName = "Approved" },
            new ExpenseStatus { StatusId = 4, StatusName = "Rejected" }
        };

        public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private SqlConnection CreateConnection()
        {
            var connStr = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            var clientId = _configuration["AZURE_CLIENT_ID"] ?? _configuration["ManagedIdentityClientId"];

            // Build the full connection string with managed identity auth
            // For production: use Managed Identity; for local dev: use Active Directory Default
            string fullConnStr;
            var env = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

            if (env == "Development" || string.IsNullOrEmpty(clientId))
            {
                // Local development: use Active Directory Default (requires az login)
                fullConnStr = connStr + "Authentication=Active Directory Default;";
            }
            else
            {
                // Production: use User Assigned Managed Identity with explicit client ID
                fullConnStr = connStr + $"Authentication=Active Directory Managed Identity;User Id={clientId};";
            }

            return new SqlConnection(fullConnStr);
        }

        public async Task<(List<Expense> expenses, string? error)> GetAllExpensesAsync(int? userId = null, int? statusId = null, int? categoryId = null)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.GetAllExpenses", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@StatusId", (object?)statusId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);

                var expenses = new List<Expense>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    expenses.Add(MapExpense(reader));
                }
                return (expenses, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all expenses");
                return (_dummyExpenses, BuildErrorMessage(ex, "ExpenseService.cs", nameof(GetAllExpensesAsync)));
            }
        }

        public async Task<(Expense? expense, string? error)> GetExpenseByIdAsync(int id)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.GetExpenseById", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@ExpenseId", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                    return (MapExpense(reader), null);
                return (null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expense {Id}", id);
                var dummy = _dummyExpenses.FirstOrDefault(e => e.ExpenseId == id);
                return (dummy, BuildErrorMessage(ex, "ExpenseService.cs", nameof(GetExpenseByIdAsync)));
            }
        }

        public async Task<(Expense? expense, string? error)> CreateExpenseAsync(CreateExpenseRequest request)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.CreateExpense", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                cmd.Parameters.AddWithValue("@CategoryId", request.CategoryId);
                cmd.Parameters.AddWithValue("@StatusId", request.StatusId);
                cmd.Parameters.AddWithValue("@AmountMinor", request.AmountMinor);
                cmd.Parameters.AddWithValue("@Currency", request.Currency);
                cmd.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
                cmd.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);

                var newId = (int)(await cmd.ExecuteScalarAsync() ?? throw new InvalidOperationException("No ID returned"));
                return await GetExpenseByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating expense");
                return (null, BuildErrorMessage(ex, "ExpenseService.cs", nameof(CreateExpenseAsync)));
            }
        }

        public async Task<(Expense? expense, string? error)> UpdateExpenseAsync(int id, UpdateExpenseRequest request)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.UpdateExpense", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@ExpenseId", id);
                cmd.Parameters.AddWithValue("@CategoryId", request.CategoryId);
                cmd.Parameters.AddWithValue("@StatusId", request.StatusId);
                cmd.Parameters.AddWithValue("@AmountMinor", request.AmountMinor);
                cmd.Parameters.AddWithValue("@Currency", request.Currency);
                cmd.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
                cmd.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                return await GetExpenseByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating expense {Id}", id);
                return (null, BuildErrorMessage(ex, "ExpenseService.cs", nameof(UpdateExpenseAsync)));
            }
        }

        public async Task<(bool success, string? error)> DeleteExpenseAsync(int id)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.DeleteExpense", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@ExpenseId", id);

                await cmd.ExecuteNonQueryAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expense {Id}", id);
                return (false, BuildErrorMessage(ex, "ExpenseService.cs", nameof(DeleteExpenseAsync)));
            }
        }

        public async Task<(Expense? expense, string? error)> ApproveExpenseAsync(int id, int reviewedBy)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.ApproveExpense", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@ExpenseId", id);
                cmd.Parameters.AddWithValue("@ReviewedBy", reviewedBy);

                await cmd.ExecuteNonQueryAsync();
                return await GetExpenseByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving expense {Id}", id);
                return (null, BuildErrorMessage(ex, "ExpenseService.cs", nameof(ApproveExpenseAsync)));
            }
        }

        public async Task<(Expense? expense, string? error)> RejectExpenseAsync(int id, int reviewedBy)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.RejectExpense", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@ExpenseId", id);
                cmd.Parameters.AddWithValue("@ReviewedBy", reviewedBy);

                await cmd.ExecuteNonQueryAsync();
                return await GetExpenseByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting expense {Id}", id);
                return (null, BuildErrorMessage(ex, "ExpenseService.cs", nameof(RejectExpenseAsync)));
            }
        }

        public async Task<(List<User> users, string? error)> GetAllUsersAsync()
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.GetAllUsers", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                var users = new List<User>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    users.Add(new User
                    {
                        UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                        UserName = reader.GetString(reader.GetOrdinal("UserName")),
                        Email = reader.GetString(reader.GetOrdinal("Email")),
                        RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                        RoleName = reader.IsDBNull(reader.GetOrdinal("RoleName")) ? "" : reader.GetString(reader.GetOrdinal("RoleName")),
                        ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                    });
                }
                return (users, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return (_dummyUsers, BuildErrorMessage(ex, "ExpenseService.cs", nameof(GetAllUsersAsync)));
            }
        }

        public async Task<(List<ExpenseCategory> categories, string? error)> GetAllCategoriesAsync()
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.GetAllCategories", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                var categories = new List<ExpenseCategory>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    categories.Add(new ExpenseCategory
                    {
                        CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                        CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                    });
                }
                return (categories, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories");
                return (_dummyCategories, BuildErrorMessage(ex, "ExpenseService.cs", nameof(GetAllCategoriesAsync)));
            }
        }

        public async Task<(List<ExpenseStatus> statuses, string? error)> GetAllStatusesAsync()
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.GetAllStatuses", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                var statuses = new List<ExpenseStatus>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    statuses.Add(new ExpenseStatus
                    {
                        StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                        StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                    });
                }
                return (statuses, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statuses");
                return (_dummyStatuses, BuildErrorMessage(ex, "ExpenseService.cs", nameof(GetAllStatusesAsync)));
            }
        }

        public async Task<(DashboardSummary summary, string? error)> GetDashboardSummaryAsync()
        {
            try
            {
                var (expenses, error) = await GetAllExpensesAsync();
                if (error != null) return (BuildDummySummary(), error);

                var summary = new DashboardSummary
                {
                    TotalExpenses = expenses.Count,
                    PendingExpenses = expenses.Count(e => e.StatusName == "Submitted"),
                    ApprovedExpenses = expenses.Count(e => e.StatusName == "Approved"),
                    RejectedExpenses = expenses.Count(e => e.StatusName == "Rejected"),
                    TotalAmountGBP = expenses.Sum(e => e.AmountGBP),
                    PendingAmountGBP = expenses.Where(e => e.StatusName == "Submitted").Sum(e => e.AmountGBP)
                };
                return (summary, null);
            }
            catch (Exception ex)
            {
                return (BuildDummySummary(), BuildErrorMessage(ex, "ExpenseService.cs", nameof(GetDashboardSummaryAsync)));
            }
        }

        private static DashboardSummary BuildDummySummary() => new()
        {
            TotalExpenses = _dummyExpenses.Count,
            PendingExpenses = _dummyExpenses.Count(e => e.StatusName == "Submitted"),
            ApprovedExpenses = _dummyExpenses.Count(e => e.StatusName == "Approved"),
            RejectedExpenses = _dummyExpenses.Count(e => e.StatusName == "Rejected"),
            TotalAmountGBP = _dummyExpenses.Sum(e => e.AmountGBP),
            PendingAmountGBP = _dummyExpenses.Where(e => e.StatusName == "Submitted").Sum(e => e.AmountGBP)
        };

        private static string BuildErrorMessage(Exception ex, string file, string method)
        {
            var managed_identity_hint = "";
            var msg = ex.Message.ToLower();
            if (msg.Contains("login failed") || msg.Contains("managed identity") || msg.Contains("token") || msg.Contains("authentication"))
            {
                managed_identity_hint = " | MANAGED IDENTITY FIX: 1) Ensure AZURE_CLIENT_ID env var is set on App Service with the client ID of your user-assigned managed identity. 2) Run run-sql-dbrole.py to grant the managed identity db_datareader, db_datawriter and EXECUTE permissions on the Northwind database. 3) For local dev, set ASPNETCORE_ENVIRONMENT=Development and run 'az login' to use Active Directory Default auth.";
            }
            return $"[{file} in {method}] {ex.GetType().Name}: {ex.Message}{managed_identity_hint}";
        }

        private static Expense MapExpense(SqlDataReader reader)
        {
            return new Expense
            {
                ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? "" : reader.GetString(reader.GetOrdinal("UserName")),
                CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                CategoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? "" : reader.GetString(reader.GetOrdinal("CategoryName")),
                StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                StatusName = reader.IsDBNull(reader.GetOrdinal("StatusName")) ? "" : reader.GetString(reader.GetOrdinal("StatusName")),
                AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
                Currency = reader.IsDBNull(reader.GetOrdinal("Currency")) ? "GBP" : reader.GetString(reader.GetOrdinal("Currency")),
                ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
                SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
                ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
                ReviewedByName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName")) ? null : reader.GetString(reader.GetOrdinal("ReviewedByName")),
                ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            };
        }
    }
}
