using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using OpenAI.Chat;
using System.Text.Json;
using ExpenseManager.Models;
using ExpenseManager.Services;

namespace ExpenseManager.Services
{
    public interface IChatService
    {
        Task<string> ChatAsync(string userMessage, List<ChatMessagePair> history);
        bool IsConfigured { get; }
    }

    public class ChatMessagePair
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    public class ChatService : IChatService
    {
        private readonly IConfiguration _configuration;
        private readonly IExpenseService _expenseService;
        private readonly ILogger<ChatService> _logger;

        public bool IsConfigured { get; private set; }

        private AzureOpenAIClient? _openAIClient;
        private string _deploymentName = "gpt-4o";

        public ChatService(IConfiguration configuration, IExpenseService expenseService, ILogger<ChatService> logger)
        {
            _configuration = configuration;
            _expenseService = expenseService;
            _logger = logger;

            var endpoint = _configuration["GenAISettings:OpenAIEndpoint"] ?? _configuration["OpenAI__Endpoint"];
            _deploymentName = _configuration["GenAISettings:OpenAIDeploymentName"] ?? _configuration["OpenAI__DeploymentName"] ?? "gpt-4o";

            if (!string.IsNullOrWhiteSpace(endpoint) && endpoint != "PLACEHOLDER")
            {
                try
                {
                    // Use ManagedIdentityCredential with explicit client ID from config
                    var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
                    Azure.Core.TokenCredential credential;

                    if (!string.IsNullOrEmpty(managedIdentityClientId))
                    {
                        _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                        credential = new ManagedIdentityCredential(managedIdentityClientId);
                    }
                    else
                    {
                        _logger.LogInformation("Using DefaultAzureCredential");
                        credential = new DefaultAzureCredential();
                    }

                    _openAIClient = new AzureOpenAIClient(new Uri(endpoint), credential);
                    IsConfigured = true;
                    _logger.LogInformation("ChatService configured with endpoint: {Endpoint}", endpoint);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to initialize ChatService");
                    IsConfigured = false;
                }
            }
            else
            {
                IsConfigured = false;
                _logger.LogInformation("ChatService not configured - GenAI settings not found");
            }
        }

        public async Task<string> ChatAsync(string userMessage, List<ChatMessagePair> history)
        {
            if (!IsConfigured || _openAIClient == null)
            {
                return "**GenAI services are not deployed yet.**\n\nTo enable the AI assistant:\n1. Run `./deploy-with-chat.sh` to deploy Azure OpenAI and AI Search resources\n2. The script will automatically configure the required environment variables\n3. Restart the application after deployment\n\nThis chat interface will then use GPT-4o to help you manage expenses using natural language.";
            }

            try
            {
                var chatClient = _openAIClient.GetChatClient(_deploymentName);

                // Build function tools for database operations
                var tools = BuildFunctionTools();

                var options = new ChatCompletionOptions();
                foreach (var tool in tools)
                    options.Tools.Add(tool);

                // Build message history
                var messages = new List<ChatMessage>
                {
                    ChatMessage.CreateSystemMessage(GetSystemPrompt())
                };

                foreach (var msg in history.TakeLast(10))
                {
                    if (msg.Role == "user")
                        messages.Add(ChatMessage.CreateUserMessage(msg.Content));
                    else
                        messages.Add(ChatMessage.CreateAssistantMessage(msg.Content));
                }

                messages.Add(ChatMessage.CreateUserMessage(userMessage));

                // Agentic loop - handle function calls
                int maxIterations = 5;
                for (int i = 0; i < maxIterations; i++)
                {
                    var response = await chatClient.CompleteChatAsync(messages, options);
                    var completion = response.Value;

                    if (completion.FinishReason == ChatFinishReason.ToolCalls)
                    {
                        // Add assistant's tool call message
                        messages.Add(ChatMessage.CreateAssistantMessage(completion));

                        // Execute each tool call
                        foreach (var toolCall in completion.ToolCalls)
                        {
                            var result = await ExecuteToolCallAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                            messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
                        }
                        // Continue loop to get final response
                        continue;
                    }

                    // Final text response
                    return completion.Content[0].Text;
                }

                return "I apologize, but I couldn't complete the operation after multiple attempts. Please try again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ChatAsync");
                return $"**Error:** {ex.Message}\n\nPlease check that:\n1. Azure OpenAI is deployed and accessible\n2. The managed identity has the 'Cognitive Services OpenAI User' role\n3. The OpenAI endpoint is correctly configured in app settings";
            }
        }

        private List<ChatTool> BuildFunctionTools()
        {
            return new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    "get_all_expenses",
                    "Retrieves all expenses from the database with optional filters",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "userId": {"type": "integer", "description": "Filter by user ID (optional)"},
                            "statusId": {"type": "integer", "description": "Filter by status ID: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected (optional)"},
                            "categoryId": {"type": "integer", "description": "Filter by category ID (optional)"}
                        }
                    }
                    """)),

                ChatTool.CreateFunctionTool(
                    "get_expense_by_id",
                    "Gets a specific expense by its ID",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "expenseId": {"type": "integer", "description": "The expense ID to retrieve"}
                        },
                        "required": ["expenseId"]
                    }
                    """)),

                ChatTool.CreateFunctionTool(
                    "create_expense",
                    "Creates a new expense in the system",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "userId": {"type": "integer", "description": "ID of the user submitting the expense"},
                            "categoryId": {"type": "integer", "description": "Category ID: 1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other"},
                            "statusId": {"type": "integer", "description": "Status ID: 1=Draft, 2=Submitted"},
                            "amountMinor": {"type": "integer", "description": "Amount in pence (e.g., £25.40 = 2540)"},
                            "expenseDate": {"type": "string", "description": "Date of the expense in YYYY-MM-DD format"},
                            "description": {"type": "string", "description": "Description of the expense"}
                        },
                        "required": ["userId", "categoryId", "statusId", "amountMinor", "expenseDate"]
                    }
                    """)),

                ChatTool.CreateFunctionTool(
                    "approve_expense",
                    "Approves a submitted expense",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "expenseId": {"type": "integer", "description": "The expense ID to approve"},
                            "reviewedBy": {"type": "integer", "description": "The user ID of the manager approving"}
                        },
                        "required": ["expenseId", "reviewedBy"]
                    }
                    """)),

                ChatTool.CreateFunctionTool(
                    "reject_expense",
                    "Rejects a submitted expense",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "expenseId": {"type": "integer", "description": "The expense ID to reject"},
                            "reviewedBy": {"type": "integer", "description": "The user ID of the manager rejecting"}
                        },
                        "required": ["expenseId", "reviewedBy"]
                    }
                    """)),

                ChatTool.CreateFunctionTool(
                    "delete_expense",
                    "Deletes an expense",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "expenseId": {"type": "integer", "description": "The expense ID to delete"}
                        },
                        "required": ["expenseId"]
                    }
                    """)),

                ChatTool.CreateFunctionTool(
                    "get_all_users",
                    "Gets a list of all users in the system",
                    BinaryData.FromString("""{"type": "object", "properties": {}}"}}""")),

                ChatTool.CreateFunctionTool(
                    "get_all_categories",
                    "Gets all expense categories",
                    BinaryData.FromString("""{"type": "object", "properties": {}}""")),

                ChatTool.CreateFunctionTool(
                    "get_dashboard_summary",
                    "Gets summary statistics: total expenses, pending count, approved count, total amount in GBP",
                    BinaryData.FromString("""{"type": "object", "properties": {}}"""))
            };
        }

        private async Task<string> ExecuteToolCallAsync(string functionName, string arguments)
        {
            try
            {
                var args = JsonDocument.Parse(arguments).RootElement;

                switch (functionName)
                {
                    case "get_all_expenses":
                    {
                        int? userId = args.TryGetProperty("userId", out var u) ? u.GetInt32() : null;
                        int? statusId = args.TryGetProperty("statusId", out var s) ? s.GetInt32() : null;
                        int? categoryId = args.TryGetProperty("categoryId", out var c) ? c.GetInt32() : null;
                        var (expenses, error) = await _expenseService.GetAllExpensesAsync(userId, statusId, categoryId);
                        return JsonSerializer.Serialize(new { data = expenses, error });
                    }

                    case "get_expense_by_id":
                    {
                        var id = args.GetProperty("expenseId").GetInt32();
                        var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
                        return JsonSerializer.Serialize(new { data = expense, error });
                    }

                    case "create_expense":
                    {
                        var request = new CreateExpenseRequest
                        {
                            UserId = args.GetProperty("userId").GetInt32(),
                            CategoryId = args.GetProperty("categoryId").GetInt32(),
                            StatusId = args.GetProperty("statusId").GetInt32(),
                            AmountMinor = args.GetProperty("amountMinor").GetInt32(),
                            Currency = "GBP",
                            ExpenseDate = DateTime.Parse(args.GetProperty("expenseDate").GetString()!),
                            Description = args.TryGetProperty("description", out var d) ? d.GetString() : null
                        };
                        var (expense, error) = await _expenseService.CreateExpenseAsync(request);
                        return JsonSerializer.Serialize(new { data = expense, error });
                    }

                    case "approve_expense":
                    {
                        var id = args.GetProperty("expenseId").GetInt32();
                        var reviewedBy = args.GetProperty("reviewedBy").GetInt32();
                        var (expense, error) = await _expenseService.ApproveExpenseAsync(id, reviewedBy);
                        return JsonSerializer.Serialize(new { data = expense, error });
                    }

                    case "reject_expense":
                    {
                        var id = args.GetProperty("expenseId").GetInt32();
                        var reviewedBy = args.GetProperty("reviewedBy").GetInt32();
                        var (expense, error) = await _expenseService.RejectExpenseAsync(id, reviewedBy);
                        return JsonSerializer.Serialize(new { data = expense, error });
                    }

                    case "delete_expense":
                    {
                        var id = args.GetProperty("expenseId").GetInt32();
                        var (success, error) = await _expenseService.DeleteExpenseAsync(id);
                        return JsonSerializer.Serialize(new { data = success, error });
                    }

                    case "get_all_users":
                    {
                        var (users, error) = await _expenseService.GetAllUsersAsync();
                        return JsonSerializer.Serialize(new { data = users, error });
                    }

                    case "get_all_categories":
                    {
                        var (categories, error) = await _expenseService.GetAllCategoriesAsync();
                        return JsonSerializer.Serialize(new { data = categories, error });
                    }

                    case "get_dashboard_summary":
                    {
                        var (summary, error) = await _expenseService.GetDashboardSummaryAsync();
                        return JsonSerializer.Serialize(new { data = summary, error });
                    }

                    default:
                        return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool call {FunctionName}", functionName);
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private static string GetSystemPrompt() => """
            You are an intelligent AI assistant for an Expense Management System used by a UK company.
            
            You have access to real functions that interact with the expense database. Use them to:
            - List, create, update, and manage expense records
            - Approve or reject submitted expenses (manager action)
            - Look up users, categories, and statuses
            - Provide dashboard summaries and insights
            
            Key information:
            - All amounts are stored in pence (minor units). £25.40 = 2540 pence
            - When displaying amounts, always show in GBP format (e.g., £25.40)
            - Status IDs: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected
            - Category IDs: 1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other
            - Default manager user ID is 2 (Bob Manager)
            
            When listing items, use clear formatting with bullet points or numbered lists.
            Be helpful, concise, and professional. Always confirm actions before making changes.
            """;
    }
}
