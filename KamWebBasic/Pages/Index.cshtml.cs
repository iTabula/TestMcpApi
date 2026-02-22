using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Azure.Core;
using KamHttp.Helpers;
using KamInfrastructure.Models;
using System.Security.Claims;

namespace KamWebBasic.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
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

                string prompt = request.Question.Trim();
                var answer = "Your question was: " + prompt;

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
}

    public class QuestionRequest
    {
        public string Question { get; set; } = string.Empty;
    }
