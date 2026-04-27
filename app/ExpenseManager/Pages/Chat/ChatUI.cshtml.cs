using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManager.Services;

namespace ExpenseManager.Pages.Chat
{
    public class ChatUIModel : PageModel
    {
        private readonly IChatService _chatService;

        public bool IsConfigured => _chatService.IsConfigured;

        public ChatUIModel(IChatService chatService)
        {
            _chatService = chatService;
        }

        public void OnGet() { }
    }
}
