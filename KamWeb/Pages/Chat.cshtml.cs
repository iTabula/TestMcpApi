using Azure.Core;
using KamHttp.Helpers;
using KamInfrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

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

            string AccessToken = User.FindFirst(ClaimTypes.Authentication)?.Value ?? string.Empty;
            string UserId = User.FindFirst(ClaimTypes.PrimarySid)?.Value ?? string.Empty; 
            string Role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty; 

            string prompt = request.Question.Trim() + $" with user_id = {UserId} and user_role = '{Role}' and token = '{AccessToken}'";
            var answer = await _mcpClient.ProcessPromptAsync(prompt);
            
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
