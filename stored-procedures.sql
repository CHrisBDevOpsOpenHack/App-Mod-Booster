-- stored-procedures.sql
-- All stored procedures for the Expense Management System
-- Uses CREATE OR ALTER PROCEDURE to be idempotent

SET NOCOUNT ON;
GO

-- =============================================
-- GetAllExpenses: Get all expenses with optional filters
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetAllExpenses
    @UserId     INT = NULL,
    @StatusId   INT = NULL,
    @CategoryId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        ru.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users ru ON e.ReviewedBy = ru.UserId
    WHERE
        (@UserId IS NULL OR e.UserId = @UserId)
        AND (@StatusId IS NULL OR e.StatusId = @StatusId)
        AND (@CategoryId IS NULL OR e.CategoryId = @CategoryId)
    ORDER BY e.CreatedAt DESC;
END
GO

-- =============================================
-- GetExpenseById: Get a single expense by ID
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        ru.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users ru ON e.ReviewedBy = ru.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- CreateExpense: Create a new expense record
-- =============================================
CREATE OR ALTER PROCEDURE dbo.CreateExpense
    @UserId       INT,
    @CategoryId   INT,
    @StatusId     INT,
    @AmountMinor  INT,
    @Currency     NVARCHAR(3) = 'GBP',
    @ExpenseDate  DATE,
    @Description  NVARCHAR(1000) = NULL,
    @ReceiptFile  NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @NewExpenseId INT;
    DECLARE @SubmittedAt DATETIME2 = NULL;

    -- If status is Submitted, set SubmittedAt to now
    IF @StatusId = 2
        SET @SubmittedAt = SYSUTCDATETIME();

    INSERT INTO dbo.Expenses
        (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, SubmittedAt, CreatedAt)
    VALUES
        (@UserId, @CategoryId, @StatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile, @SubmittedAt, SYSUTCDATETIME());

    SET @NewExpenseId = SCOPE_IDENTITY();

    -- Return the new ID so the caller can fetch the full record
    SELECT @NewExpenseId AS NewExpenseId;
END
GO

-- =============================================
-- UpdateExpense: Update an existing expense
-- =============================================
CREATE OR ALTER PROCEDURE dbo.UpdateExpense
    @ExpenseId    INT,
    @CategoryId   INT,
    @StatusId     INT,
    @AmountMinor  INT,
    @Currency     NVARCHAR(3) = 'GBP',
    @ExpenseDate  DATE,
    @Description  NVARCHAR(1000) = NULL,
    @ReceiptFile  NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @SubmittedAt DATETIME2;

    -- Preserve existing SubmittedAt unless transitioning to Submitted status
    SELECT @SubmittedAt = SubmittedAt FROM dbo.Expenses WHERE ExpenseId = @ExpenseId;

    IF @StatusId = 2 AND @SubmittedAt IS NULL
        SET @SubmittedAt = SYSUTCDATETIME();

    UPDATE dbo.Expenses
    SET
        CategoryId   = @CategoryId,
        StatusId     = @StatusId,
        AmountMinor  = @AmountMinor,
        Currency     = @Currency,
        ExpenseDate  = @ExpenseDate,
        Description  = @Description,
        ReceiptFile  = @ReceiptFile,
        SubmittedAt  = @SubmittedAt
    WHERE ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- DeleteExpense: Delete an expense record
-- =============================================
CREATE OR ALTER PROCEDURE dbo.DeleteExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.Expenses WHERE ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- GetExpensesByUser: Get all expenses for a user
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetExpensesByUser
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.GetAllExpenses @UserId = @UserId;
END
GO

-- =============================================
-- GetExpensesByStatus: Get all expenses with a given status
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetExpensesByStatus
    @StatusId INT
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.GetAllExpenses @StatusId = @StatusId;
END
GO

-- =============================================
-- ApproveExpense: Approve a submitted expense
-- =============================================
CREATE OR ALTER PROCEDURE dbo.ApproveExpense
    @ExpenseId  INT,
    @ReviewedBy INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Get the Approved status ID
    DECLARE @ApprovedStatusId INT;
    SELECT @ApprovedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved';

    UPDATE dbo.Expenses
    SET
        StatusId   = @ApprovedStatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- RejectExpense: Reject a submitted expense
-- =============================================
CREATE OR ALTER PROCEDURE dbo.RejectExpense
    @ExpenseId  INT,
    @ReviewedBy INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Get the Rejected status ID
    DECLARE @RejectedStatusId INT;
    SELECT @RejectedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected';

    UPDATE dbo.Expenses
    SET
        StatusId   = @RejectedStatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- GetAllUsers: Get all active users
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetAllUsers
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        u.IsActive
    FROM dbo.Users u
    JOIN dbo.Roles r ON u.RoleId = r.RoleId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END
GO

-- =============================================
-- GetAllCategories: Get all active expense categories
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetAllCategories
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

-- =============================================
-- GetAllStatuses: Get all expense statuses
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetAllStatuses
AS
BEGIN
    SET NOCOUNT ON;
    SELECT StatusId, StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END
GO
