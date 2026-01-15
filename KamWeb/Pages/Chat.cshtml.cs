using Azure.Core;
using KamWeb.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamWeb.Pages;

public class ChatModel : PageModel
{
    private readonly McpSseClient _mcpClient;
    private readonly ILogger<ChatModel> _logger;

    public ChatModel(McpSseClient mcpClient, ILogger<ChatModel> logger)
    {
        _mcpClient = mcpClient;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        // No initialization needed here - handled by background service
        var answer = await _mcpClient.ProcessPromptAsync("OTP code is 1234");
    }

    [HttpPost]
    public async Task<IActionResult> OnPostGetAnswer([FromBody] QuestionRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return new JsonResult(new { answer = "I didn't hear your question. Please try again." });
            }

            _logger.LogInformation("Processing question: {Question}", request.Question);
            
            var answer = await _mcpClient.ProcessPromptAsync(request.Question.Trim());
            
            _logger.LogInformation("Answer generated successfully");
            
            return new JsonResult(new { answer });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing question");
            return new JsonResult(new { answer = "I'm sorry, I encountered an error processing your question. Please try again." });
        }
    }
}

public class QuestionRequest
{
    public string Question { get; set; } = string.Empty;
}
