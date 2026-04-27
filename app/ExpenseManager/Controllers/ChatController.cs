using Microsoft.AspNetCore.Mvc;
using ExpenseManager.Services;

namespace ExpenseManager.Controllers
{
    /// <summary>
    /// Chat API - Azure OpenAI powered conversational interface for expense management
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        /// <summary>Sends a message to the AI assistant and gets a response</summary>
        [HttpPost]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Message cannot be empty" });

            try
            {
                var reply = await _chatService.ChatAsync(request.Message, request.History ?? new());
                return Ok(new { reply, isConfigured = _chatService.IsConfigured });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Chat endpoint");
                return Ok(new { reply = $"Error: {ex.Message}", isConfigured = _chatService.IsConfigured });
            }
        }

        /// <summary>Checks if GenAI services are configured</summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new { isConfigured = _chatService.IsConfigured });
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatMessagePair>? History { get; set; }
    }
}
