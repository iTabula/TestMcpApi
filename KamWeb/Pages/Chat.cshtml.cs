using KamWeb.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelContextProtocol.Protocol;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KamWeb.Pages
{
    public class ChatModel : PageModel
    {
        public static McpSseClient client = new McpSseClient("https://freemypalestine.com/api/mcp/sse");
        public async Task OnGetAsync()
        {
            // Configure Vapi with your existing assistant
            var vapiPrivateKey = "e3162921-9738-4195-8143-716973bcf9b6";
            var vapiAssistantId = "f6caa8b6-83f7-4b40-a410-39d1988dcf8d";

            client.SetVapiClient(vapiPrivateKey, vapiAssistantId);

            await client.ConnectAsync();
            await client.InitializeAsync();
        }

        [HttpPost]
        public async Task<IActionResult> OnPostGetAnswer([FromBody] QuestionRequest request)
        {
            var answer = await GetAnswerAsync(request.Question);
            return new JsonResult(new { answer });
        }

        private async Task<string> GetAnswerAsync(string question)
        {
            // Dummy answer logic - you can replace this with your actual logic
            if (string.IsNullOrWhiteSpace(question))
            {
                return "I didn't hear your question. Please try again.";
            }

            question = question.ToLower().Trim();

            var response = await client.ProcessPromptAsync(question);

            return response;
        }
    }

    public class QuestionRequest
    {
        public string Question { get; set; } = string.Empty;
    }
}
