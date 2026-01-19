using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace KamWeb.Pages
{
    public class MainModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MainModel> _logger;

        // Default base address for TestMcpApi (adjust via appsettings if different)
        private readonly string _defaultMcpApiBase = "https://localhost:44352";

        public MainModel(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<MainModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public class AskRequest
        {
            public string? Question { get; set; }
        }

        public class AskResponse
        {
            public bool Success { get; set; }
            public string? Answer { get; set; }
            public string? Error { get; set; }
        }

        public async Task<IActionResult> OnPostAskAsync([FromBody] AskRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Question))
                return new JsonResult(new AskResponse { Success = false, Error = "Empty question." });

            string question = request.Question.Trim();
            string baseUrl = _configuration.GetValue<string>("McpApiBaseUrl") ?? _defaultMcpApiBase;
            baseUrl = baseUrl.TrimEnd('/');

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            // First try: POST to MCP messages endpoint (if available)
            try
            {
                var mcpUrl = $"{baseUrl}/api/mcp/messages";
                _logger.LogDebug("Attempting MCP messages POST to {url}", mcpUrl);

                // Attempt a generic payload — adjust if your MCP message schema differs
                var payload = new { input = question, message = question, prompt = question };
                using var resp = await client.PostAsJsonAsync(mcpUrl, payload);
                if (resp.IsSuccessStatusCode)
                {
                    string text = await resp.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return new JsonResult(new AskResponse { Success = true, Answer = text });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "MCP messages POST failed, falling back to REST heuristics");
            }

            // Fallback: heuristics -> call specific MCP tool endpoints (loans/reals/lenders/thirdparties)
            try
            {
                string lower = question.ToLowerInvariant();

                if (lower.Contains("top agents") || lower.Contains("top agent"))
                {
                    var url = $"{baseUrl}/loans/top-agents?top=5";
                    return await GetStringResult(url);
                }

                if (lower.Contains("top lenders") || lower.Contains("top lender"))
                {
                    var url = $"{baseUrl}/lenders/top?top=5";
                    return await GetStringResult(url);
                }

                if (lower.Contains("top cities") || lower.Contains("top city"))
                {
                    var url = $"{baseUrl}/loans/top-cities?top=5";
                    return await GetStringResult(url);
                }

                // LTV by address: "ltv for 123 Main St" or "ltv 123 Main St"
                if (lower.Contains("ltv"))
                {
                    var addr = ExtractAddress(question);
                    if (!string.IsNullOrEmpty(addr))
                    {
                        var url = $"{baseUrl}/loans/ltv-by-address/{WebUtility.UrlEncode(addr)}";
                        return await GetStringResult(url);
                    }
                    return new JsonResult(new AskResponse { Success = true, Answer = "Please include the property address to check LTV." });
                }

                // Real transaction by id: "real transaction 12345" or "realtrans 12345"
                if (lower.Contains("real transaction") || lower.Contains("realtrans"))
                {
                    var id = ExtractFirstNumberOrToken(question);
                    if (!string.IsNullOrEmpty(id))
                    {
                        var url = $"{baseUrl}/real-transaction/{WebUtility.UrlEncode(id)}";
                        return await GetStringResult(url);
                    }
                    return new JsonResult(new AskResponse { Success = true, Answer = "Please include the RealTransID to look up." });
                }

                // Agent / lender by loan or address
                if (lower.Contains("agent by loan") || lower.Contains("agent for loan") || lower.Contains("agent by id"))
                {
                    var id = ExtractFirstNumberOrToken(question);
                    if (!string.IsNullOrEmpty(id))
                    {
                        var url = $"{baseUrl}/loans/agent-by-id/{WebUtility.UrlEncode(id)}";
                        return await GetStringResult(url);
                    }
                    return new JsonResult(new AskResponse { Success = true, Answer = "Please include the loan id." });
                }

                if (lower.Contains("lender for") && lower.Contains("address") || lower.Contains("lender for") && question.Contains(" at "))
                {
                    var addr = ExtractAddress(question);
                    if (!string.IsNullOrEmpty(addr))
                    {
                        var url = $"{baseUrl}/reals/lender-by-address/{WebUtility.UrlEncode(addr)}";
                        return await GetStringResult(url);
                    }
                }

                // Generic fallback: try top agents
                {
                    var url = $"{baseUrl}/loans/top-agents?top=5";
                    return await GetStringResult(url);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when calling MCP tool endpoints");
                return new JsonResult(new AskResponse { Success = false, Error = "Error contacting MCP tools: " + ex.Message });
            }
        }

        private async Task<IActionResult> GetStringResult(string url)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                using var resp = await client.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    var text = await resp.Content.ReadAsStringAsync();
                    return new JsonResult(new AskResponse { Success = true, Answer = text });
                }
                else
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    return new JsonResult(new AskResponse { Success = false, Error = $"MCP tool returned {(int)resp.StatusCode}: {err}" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new AskResponse { Success = false, Error = ex.Message });
            }
        }

        // Very small helpers to extract tokens (naive).
        private static string? ExtractAddress(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            // Try to find text after "address", "at", "for"
            var markers = new[] { "address", " at ", " for ", " of " };
            foreach (var marker in markers)
            {
                var idx = input.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var candidate = input.Substring(idx + marker.Length).Trim();
                    // cut trailing question mark
                    candidate = candidate.Trim('?', '\"', '\'').Trim();
                    if (candidate.Length > 0) return candidate;
                }
            }

            // fallback: take last phrase
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return string.Join(' ', parts.Skip(Math.Max(0, parts.Length - 4)));
            return input;
        }

        private static string? ExtractFirstNumberOrToken(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var tokens = input.Split(new[] { ' ', ',', '.', '/', '\\', ':' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var t in tokens)
            {
                if (t.Any(char.IsDigit)) return t;
            }
            // fallback to last token
            return tokens.LastOrDefault();
        }
    }
}