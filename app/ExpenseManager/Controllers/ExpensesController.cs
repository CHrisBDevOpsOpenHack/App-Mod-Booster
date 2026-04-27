using Microsoft.AspNetCore.Mvc;
using ExpenseManager.Models;
using ExpenseManager.Services;

namespace ExpenseManager.Controllers
{
    /// <summary>
    /// Expense Management API - provides all CRUD operations for expenses, users, categories, and statuses.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ExpensesController : ControllerBase
    {
        private readonly IExpenseService _expenseService;
        private readonly ILogger<ExpensesController> _logger;

        public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
        {
            _expenseService = expenseService;
            _logger = logger;
        }

        /// <summary>Gets all expenses with optional filters</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<Expense>>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] int? userId, [FromQuery] int? statusId, [FromQuery] int? categoryId)
        {
            var (expenses, error) = await _expenseService.GetAllExpensesAsync(userId, statusId, categoryId);
            return Ok(new ApiResponse<List<Expense>> { Success = error == null, Data = expenses, Error = error });
        }

        /// <summary>Gets a specific expense by ID</summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<Expense>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(int id)
        {
            var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null && error == null) return NotFound();
            return Ok(new ApiResponse<Expense> { Success = error == null, Data = expense, Error = error });
        }

        /// <summary>Creates a new expense</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<Expense>), 201)]
        public async Task<IActionResult> Create([FromBody] CreateExpenseRequest request)
        {
            var (expense, error) = await _expenseService.CreateExpenseAsync(request);
            if (error != null) return BadRequest(new ApiResponse<Expense> { Success = false, Error = error });
            return CreatedAtAction(nameof(GetById), new { id = expense!.ExpenseId },
                new ApiResponse<Expense> { Success = true, Data = expense });
        }

        /// <summary>Updates an existing expense</summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse<Expense>), 200)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateExpenseRequest request)
        {
            var (expense, error) = await _expenseService.UpdateExpenseAsync(id, request);
            if (error != null) return BadRequest(new ApiResponse<Expense> { Success = false, Error = error });
            return Ok(new ApiResponse<Expense> { Success = true, Data = expense });
        }

        /// <summary>Deletes an expense</summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        public async Task<IActionResult> Delete(int id)
        {
            var (success, error) = await _expenseService.DeleteExpenseAsync(id);
            if (error != null) return BadRequest(new ApiResponse<bool> { Success = false, Error = error });
            return Ok(new ApiResponse<bool> { Success = true, Data = success });
        }

        /// <summary>Approves an expense (manager action)</summary>
        [HttpPost("{id}/approve")]
        [ProducesResponseType(typeof(ApiResponse<Expense>), 200)]
        public async Task<IActionResult> Approve(int id, [FromBody] ApproveRejectRequest request)
        {
            var (expense, error) = await _expenseService.ApproveExpenseAsync(id, request.ReviewedBy);
            if (error != null) return BadRequest(new ApiResponse<Expense> { Success = false, Error = error });
            return Ok(new ApiResponse<Expense> { Success = true, Data = expense });
        }

        /// <summary>Rejects an expense (manager action)</summary>
        [HttpPost("{id}/reject")]
        [ProducesResponseType(typeof(ApiResponse<Expense>), 200)]
        public async Task<IActionResult> Reject(int id, [FromBody] ApproveRejectRequest request)
        {
            var (expense, error) = await _expenseService.RejectExpenseAsync(id, request.ReviewedBy);
            if (error != null) return BadRequest(new ApiResponse<Expense> { Success = false, Error = error });
            return Ok(new ApiResponse<Expense> { Success = true, Data = expense });
        }
    }

    /// <summary>
    /// Users API
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class UsersController : ControllerBase
    {
        private readonly IExpenseService _expenseService;

        public UsersController(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        /// <summary>Gets all users</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<User>>), 200)]
        public async Task<IActionResult> GetAll()
        {
            var (users, error) = await _expenseService.GetAllUsersAsync();
            return Ok(new ApiResponse<List<User>> { Success = error == null, Data = users, Error = error });
        }
    }

    /// <summary>
    /// Categories API
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CategoriesController : ControllerBase
    {
        private readonly IExpenseService _expenseService;

        public CategoriesController(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        /// <summary>Gets all expense categories</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<ExpenseCategory>>), 200)]
        public async Task<IActionResult> GetAll()
        {
            var (categories, error) = await _expenseService.GetAllCategoriesAsync();
            return Ok(new ApiResponse<List<ExpenseCategory>> { Success = error == null, Data = categories, Error = error });
        }
    }

    /// <summary>
    /// Statuses API
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class StatusesController : ControllerBase
    {
        private readonly IExpenseService _expenseService;

        public StatusesController(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        /// <summary>Gets all expense statuses</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<ExpenseStatus>>), 200)]
        public async Task<IActionResult> GetAll()
        {
            var (statuses, error) = await _expenseService.GetAllStatusesAsync();
            return Ok(new ApiResponse<List<ExpenseStatus>> { Success = error == null, Data = statuses, Error = error });
        }
    }

    /// <summary>
    /// Dashboard API
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class DashboardController : ControllerBase
    {
        private readonly IExpenseService _expenseService;

        public DashboardController(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        /// <summary>Gets dashboard summary statistics</summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(ApiResponse<DashboardSummary>), 200)]
        public async Task<IActionResult> GetSummary()
        {
            var (summary, error) = await _expenseService.GetDashboardSummaryAsync();
            return Ok(new ApiResponse<DashboardSummary> { Success = error == null, Data = summary, Error = error });
        }
    }
}
