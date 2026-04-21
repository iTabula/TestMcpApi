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

            // Build enhanced prompt with strict tool-call context
            string prompt =
                $"User question: {request.Question.Trim()}\n" +
                $"Authenticated context: user_id={userId}, user_role='{role}', token='{accessToken}'.\n" +
                "Important tool-calling rules:\n" +
                "1) For any tool that accepts user_id/user_role/token, ALWAYS pass all three using the authenticated context above.\n" +
                "2) If the question uses words like 'my', 'me', or 'mine', treat it as the logged-in user and fetch only that user's data.\n" +
                "3) For web chat identity/contact/loan questions, prefer agent/user tools instead of phone-call authentication tools.\n" +
                "4) Only use customer-phone authentication tool when an explicit phone number is provided for phone authentication.";

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