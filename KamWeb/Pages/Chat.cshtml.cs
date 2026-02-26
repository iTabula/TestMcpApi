using Azure.Core;
using KamHttp.Helpers;
using KamInfrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Collections.Concurrent;

namespace KamWeb.Pages;

public class ChatModel : PageModel
{
    private static readonly ConcurrentBag<JoinKamRequest> JoinKamRequests = new();
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

    [HttpPost]
    public IActionResult OnPostJoinKam([FromBody] JoinKamRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Phone) ||
            request.LicensedByCaBre is null ||
            request.LicensedByNmls is null ||
            string.IsNullOrWhiteSpace(request.HeardAbout))
        {
            return BadRequest("Missing required information.");
        }

        JoinKamRequests.Add(request);
        _logger.LogInformation("Join KAM request received from {Email}", request.Email);

        return new JsonResult(new { message = "Thanks for your interest! We'll follow up soon." });
    }
}

public class QuestionRequest
{
    public string Question { get; set; } = string.Empty;
}

public class JoinKamRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool? LicensedByCaBre { get; set; }
    public bool? LicensedByNmls { get; set; }
    public string HeardAbout { get; set; } = string.Empty;
}
