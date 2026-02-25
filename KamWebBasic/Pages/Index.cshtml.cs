using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Azure.Core;
using KamHttp.Helpers;
using KamInfrastructure.Models;
using System.Security.Claims;
using System.Collections.Concurrent;

namespace KamWebBasic.Pages
{
    public class IndexModel : PageModel
    {
        private static readonly ConcurrentBag<JoinKamRequest> JoinKamRequests = new();
        private readonly ILogger<IndexModel> _logger;
        private readonly McpSseClient _mcpClient;

        public IndexModel(ILogger<IndexModel> logger, McpSseClient mcpClient)
        {
            _logger = logger;
            _mcpClient = mcpClient;
        }

        public void OnGetAsync()
        {

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

                // Use VAPI agent to process the question
                var answer = await _mcpClient.ProcessPromptAsync(request.Question.Trim());

                _logger.LogInformation("Answer generated successfully from VAPI agent");

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
