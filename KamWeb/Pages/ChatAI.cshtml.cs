using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KamHttp.Helpers;
using System.Security.Claims;

namespace KamWeb.Pages;

public class ChatAIModel : PageModel
{
    private readonly ILogger<ChatAIModel> _logger;
    private readonly IConfiguration _configuration;
    private readonly McpOpenAiClient _mcpOpenAiClient;

    public ChatAIModel(ILogger<ChatAIModel> logger, IConfiguration configuration, McpOpenAiClient mcpOpenAiClient)
    {
        _logger = logger;
        _configuration = configuration;
        _mcpOpenAiClient = mcpOpenAiClient;
    }

    public void OnGet()
    {
        // No initialization needed - handled by background service
    }

    [HttpPost]
    public async Task<IActionResult> OnPostGetAnswer([FromBody] QuestionRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return new JsonResult(new { answer = "I didn't receive your question. Please try again." });
            }

            _logger.LogInformation("Processing question with OpenAI MCP: {Question}", request.Question);

            // Get user information from claims
            string accessToken = User.FindFirst(ClaimTypes.Authentication)?.Value ?? string.Empty;
            string userId = User.FindFirst(ClaimTypes.PrimarySid)?.Value ?? string.Empty;
            string role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

            // Build enhanced prompt with user context
            string prompt = $"{request.Question.Trim()} with user_id = {userId} and user_role = '{role}' and token = '{accessToken}'";

            // Process with OpenAI MCP client
            var answer = await _mcpOpenAiClient.ProcessPromptAsync(prompt);

            _logger.LogInformation("Answer generated successfully using OpenAI with MCP");

            return new JsonResult(new { answer });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing question with OpenAI MCP");
            return new JsonResult(new { answer = $"I'm sorry, I encountered an error: {ex.Message}" });
        }
    }
}