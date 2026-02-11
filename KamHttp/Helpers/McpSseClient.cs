using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace KamHttp.Helpers
{
    public class McpSseClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _sseEndpoint;
        private readonly string _baseUrl;
        private List<McpTool> _tools = [];
        private CancellationTokenSource? _cts;
        private Task? _sseTask;
        private string? _messageEndpoint;
        private readonly Dictionary<string, TaskCompletionSource<JsonDocument>> _pendingRequests = [];
        private readonly SemaphoreSlim _endpointReadySemaphore = new(0, 1);
        private int _requestId = 0;
        private HttpClient? _vapiClient;
        private string? _vapiAssistantId;
        private readonly List<VapiMessage> _conversationHistory = [];

        private readonly ILogger<McpSseClient>? _logger;
        public int ToolCount => _tools.Count;

        public McpSseClient(string sseEndpoint, ILogger<McpSseClient>? logger = null)
        {
            _sseEndpoint = sseEndpoint;
            var uri = new Uri(sseEndpoint);
            _baseUrl = $"{uri.Scheme}://{uri.Host}";
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            _logger = logger;
        }

        public void SetVapiClient(string privateApiKey, string assistantId)
        {
            _vapiClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.vapi.ai")
            };
            _vapiClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {privateApiKey}");
            //_vapiClient.DefaultRequestHeaders.Add("Khaled", $"1234");
            _vapiAssistantId = assistantId;
            _logger?.LogInformation($"[Using Vapi assistant: {assistantId}]");
        }

        public async Task ConnectAsync()
        {
            _cts = new CancellationTokenSource();

            _logger?.LogInformation("Connecting to MCP SSE server...");

            // Start SSE stream listener
            _sseTask = Task.Run(async () => await ListenToSseStreamAsync(_cts.Token));

            // Wait for endpoint to be provided by server
            var endpointReceived = await _endpointReadySemaphore.WaitAsync(TimeSpan.FromSeconds(10));

            if (!endpointReceived || string.IsNullOrEmpty(_messageEndpoint))
            {
                throw new TimeoutException("Did not receive message endpoint from SSE server");
            }

            _logger?.LogInformation($"Connected to SSE stream. Message endpoint: {_messageEndpoint}");
        }

        private async Task ListenToSseStreamAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, _sseEndpoint);
                request.Headers.Add("Accept", "text/event-stream");
                request.Headers.Add("Cache-Control", "no-cache");

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                string? line;
                var eventData = new StringBuilder();
                string? eventType = null;

                while (!cancellationToken.IsCancellationRequested && (line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        // Empty line means end of event
                        if (eventData.Length > 0)
                        {
                            await ProcessSseEventAsync(eventType, eventData.ToString().Trim());
                            eventData.Clear();
                            eventType = null;
                        }
                        continue;
                    }

                    if (line.StartsWith("event:"))
                    {
                        eventType = line.Substring(6).Trim();
                    }
                    else if (line.StartsWith("data:"))
                    {
                        eventData.AppendLine(line.Substring(5).Trim());
                    }
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "SSE stream error");
            }
        }

        private async Task ProcessSseEventAsync(string? eventType, string data)
        {
            try
            {
                // Handle endpoint event
                if (eventType == "endpoint")
                {
                    _messageEndpoint = data.StartsWith("http") ? data : _baseUrl + data;
                    _logger?.LogInformation($"[Received message endpoint: {_messageEndpoint}]");
                    _endpointReadySemaphore.Release();
                    return;
                }

                // Handle message events (JSON-RPC responses)
                if (eventType == "message" || string.IsNullOrEmpty(eventType))
                {
                    var jsonDoc = JsonDocument.Parse(data);

                    // Handle JSON-RPC 2.0 response
                    if (jsonDoc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        var id = idProp.ToString();
                        if (_pendingRequests.TryGetValue(id, out var tcs))
                        {
                            tcs.SetResult(jsonDoc);
                            _pendingRequests.Remove(id);
                        }
                    }
                    // Handle notifications or other messages
                    else if (jsonDoc.RootElement.TryGetProperty("method", out var methodProp))
                    {
                        _logger?.LogInformation($"[Server notification: {methodProp.GetString()}]");
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON, possibly just a plain text event
                _logger?.LogInformation($"[Event {eventType}: {data}]");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing SSE event");
            }

            await Task.CompletedTask;
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Send initialize request
                var initResponse = await SendJsonRpcRequestAsync("initialize", new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "mcp-dotnet-client",
                        version = "1.0.0"
                    }
                });

                _logger?.LogInformation("MCP session initialized.");

                // List available tools
                var toolsResponse = await SendJsonRpcRequestAsync("tools/list", new { });

                if (toolsResponse.RootElement.TryGetProperty("result", out var result) &&
                    result.TryGetProperty("tools", out var toolsArray))
                {
                    _tools = JsonSerializer.Deserialize<List<McpTool>>(toolsArray.GetRawText()) ?? [];

                    _logger?.LogInformation($"Found {_tools.Count} tools:");
                    foreach (var tool in _tools.Take(10))
                    {
                        _logger?.LogInformation($"  - {tool.Name}: {tool.Description}");
                    }
                    if (_tools.Count > 10)
                    {
                        _logger?.LogInformation($"  ... and {_tools.Count - 10} more tools");
                    }
                    _logger?.LogInformation("");
                }

                _logger?.LogInformation("Ready to chat! Vapi will intelligently select and execute MCP tools.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Initialization error");
            }
        }

        private async Task<JsonDocument> SendJsonRpcRequestAsync(string method, object parameters)
        {
            if (string.IsNullOrEmpty(_messageEndpoint))
            {
                throw new InvalidOperationException("Message endpoint not yet established");
            }

            var id = (++_requestId).ToString();
            var tcs = new TaskCompletionSource<JsonDocument>();
            _pendingRequests[id] = tcs;

            var request = new
            {
                jsonrpc = "2.0",
                id,
                method,
                @params = parameters
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger?.LogInformation($"[Sending to {_messageEndpoint}: {method}]");

            // Post the request to the message endpoint (not SSE endpoint)
            var response = await _httpClient.PostAsync(_messageEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Request failed: {response.StatusCode} - {errorContent}");
            }

            // Wait for response via SSE stream (with timeout)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            _pendingRequests.Remove(id);
            throw new TimeoutException("Request timed out waiting for SSE response");
        }

        public async Task<string> ProcessPromptAsync(string prompt)
        {
            if (_tools.Count == 0)
            {
                return "No tools available from MCP server.";
            }

            if (_vapiClient == null || string.IsNullOrEmpty(_vapiAssistantId))
            {
                return "Vapi client not configured.";
            }

            try
            {
                _logger?.LogInformation("[Sending to Vapi for intelligent processing...]");

                // Send message to Vapi assistant
                var chatRequest = new
                {
                    assistantId = _vapiAssistantId,
                    input = prompt
                };

                var response = await _vapiClient.PostAsJsonAsync("/chat", chatRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger?.LogError($"[Vapi Error: {error}]");

                    // Fallback to simple tool matching
                    return await ProcessPromptWithSimpleMatchingAsync(prompt);
                }


                PromptResponse result = await response.Content.ReadFromJsonAsync<PromptResponse>();
                string json = JsonSerializer.Serialize(result);

                if (result == null)
                {
                    return "No response from Vapi.";
                }

                //"content": "Hi, this is Ava, your KAM AI Agent. Let me check that for you.  \n\n
                //The most popular title company at KAM is **First American Title**.  \n\n
                //By the way, may I get your name, phone number, and email in case you'd like more insights or help joining KAM?"
                // Handle Vapi response (this will depend on your assistant's configuration)
                string message = "No response content.";
                if (result.output != null && result.output.Count > 0)
                {
                    Output output = result.output.Where(x => x.role == "tool").FirstOrDefault();
                    bool isToolOutputInvalid = false;

                    if (output == null)
                    {
                        output = result.output.Where(x => x.content.Length > 10).FirstOrDefault();
                    }
                    message = output.content;

                    if (message.StartsWith("[") && message.EndsWith("]"))
                    {
                        List<ContentMessages> text_messages = JsonSerializer.Deserialize<List<ContentMessages>>(message);
                        if (text_messages != null && text_messages.Count > 0)
                        {
                            string combinedText = string.Join(" ", text_messages.Select(tm => tm.text));

                            if (IsInvalidToolOutput(combinedText))
                            {
                                _logger?.LogInformation("[Tool output contains invalid/debug content, ignoring...]");
                                isToolOutputInvalid = true;
                                message = "";
                            }
                            else
                            {
                                message = combinedText;
                            }
                        }
                    }
                    message = message
                        .Replace("Hi, this is Ava your KAM AI Agent. How can I help you today? You can ask questions like How do I join KAM, what does KAM do.", "")
                        .Replace("Hi, this is Ava, your KAM AI Agent.", "")
                        .Replace("Let me check that for you.", "")  
                        .Replace("By the way, may I get your name, phone number, and email in case you'd like more insights or help joining KAM?", "")
                        .Replace("\n", "")
                        .Trim(); // Remove markdown bold for console display"

                    if (string.IsNullOrEmpty(message) && isToolOutputInvalid)
                    {
                        return "No response content.";
                    }
                    return message ?? "No response content.";
                }

                return message;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Vapi processing failed");
                _logger?.LogInformation("[Falling back to simple tool matching...]");
                return await ProcessPromptWithSimpleMatchingAsync(prompt);
            }
        }

        private bool IsInvalidToolOutput(string content)
        {
            // Check if content contains patterns that indicate it's debug/technical output
            // that should be ignored (like tokens, secret codes, etc.)
            var contentLower = content.ToLowerInvariant();

            var invalidPatterns = new[]
            {
                "secret code",
                "user_role",
                "token =",
                "eyj", // JWT token prefix
                "bearer",
                "authorization"
            };

            // If any of these patterns exist, consider it invalid
            return invalidPatterns.Any(pattern => contentLower.Contains(pattern));
        }

        private async Task<string> ProcessPromptWithSimpleMatchingAsync(string prompt)
        {
            var bestTool = FindBestMatchingTool(prompt);

            if (bestTool == null)
            {
                return "I couldn't find a suitable tool to answer your question.";
            }

            _logger?.LogInformation($"[Using tool: {bestTool.Name}]");

            try
            {
                var parameters = ExtractParameters(prompt, bestTool);

                var response = await SendJsonRpcRequestAsync("tools/call", new
                {
                    name = bestTool.Name,
                    arguments = parameters
                });

                if (response.RootElement.TryGetProperty("result", out var result) &&
                    result.TryGetProperty("content", out var contentArray) &&
                    contentArray.GetArrayLength() > 0)
                {
                    var firstContent = contentArray[0];
                    if (firstContent.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? "No text content.";
                    }
                }

                return "No response from tool.";
            }
            catch (Exception ex)
            {
                return $"Error calling tool: {ex.Message}";
            }
        }

        private McpTool? FindBestMatchingTool(string prompt)
        {
            var promptLower = prompt.ToLowerInvariant();
            var scoredTools = new List<(McpTool Tool, int Score)>();

            foreach (var tool in _tools)
            {
                int score = 0;

                if (promptLower.Contains(tool.Name.ToLowerInvariant()))
                {
                    score += 50;
                }

                var descriptionWords = tool.Description.ToLowerInvariant().Split(' ');
                var promptWords = promptLower.Split(' ');

                score += promptWords.Count(pw => descriptionWords.Contains(pw)) * 10;

                if (tool.InputSchema?.Properties != null)
                {
                    foreach (var param in tool.InputSchema.Properties.Keys)
                    {
                        if (promptLower.Contains(param.ToLowerInvariant()))
                        {
                            score += 20;
                        }
                    }
                }

                if (score > 0)
                {
                    scoredTools.Add((tool, score));
                }
            }

            return scoredTools
                .OrderByDescending(st => st.Score)
                .FirstOrDefault().Tool;
        }

        private Dictionary<string, object> ExtractParameters(string prompt, McpTool tool)
        {
            var parameters = new Dictionary<string, object>();

            if (tool.InputSchema?.Properties == null)
            {
                return parameters;
            }

            var promptLower = prompt.ToLowerInvariant();

            foreach (var (paramName, paramSchema) in tool.InputSchema.Properties)
            {
                var pattern = $"{paramName.ToLowerInvariant()}[:\\s]+[\"']?([^\"']+)[\"']?";
                var match = System.Text.RegularExpressions.Regex.Match(promptLower, pattern);

                if (match.Success)
                {
                    parameters[paramName] = match.Groups[1].Value.Trim();
                }
                else if (paramSchema.GetPrimaryType() == "string")
                {
                    parameters[paramName] = prompt;
                }
            }

            return parameters;
        }

        public async Task DisconnectAsync()
        {
            _cts?.Cancel();
            if (_sseTask != null)
            {
                await _sseTask;
            }
            _httpClient.Dispose();
            _vapiClient?.Dispose();
        }
    }

    // Vapi Models
    public class VapiMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    // MCP Protocol Models
    public class McpTool
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("inputSchema")]
        public McpInputSchema? InputSchema { get; set; }
    }

    public class McpInputSchema
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("properties")]
        public Dictionary<string, McpPropertySchema>? Properties { get; set; }

        [JsonPropertyName("required")]
        public List<string>? Required { get; set; }
    }

    public class McpPropertySchema
    {
        [JsonPropertyName("type")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public List<string> Type { get; set; } = [];

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("default")]
        public JsonElement? Default { get; set; }
    }
    public class Cost
    {
        public double cost { get; set; }
        public string type { get; set; }
        public Model model { get; set; }
        public int promptTokens { get; set; }
        public int completionTokens { get; set; }
    }

    public class Input
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class Model
    {
        public string model { get; set; }
        public string provider { get; set; }
    }

    public class Output
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class PromptResponse
    {
        public string id { get; set; }
        public string orgId { get; set; }
        public List<Input> input { get; set; }
        public List<Output> output { get; set; }
        public string assistantId { get; set; }
        public List<object> messages { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
        public double cost { get; set; }
        public List<Cost> costs { get; set; }
    }


    public class ContentMessages
    {
        public string type { get; set; }
        public string text { get; set; }
    }
    // Extension methods for McpPropertySchema
    public static class McpPropertySchemaExtensions
    {
        public static bool IsNullable(this McpPropertySchema schema)
        {
            return schema.Type.Contains("null");
        }

        public static string GetPrimaryType(this McpPropertySchema schema)
        {
            return schema.Type.FirstOrDefault(t => t != "null") ?? "string";
        }

        public static object? GetDefaultValue(this McpPropertySchema schema)
        {
            if (schema.Default == null || schema.Default.Value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var primaryType = schema.GetPrimaryType();
            return primaryType switch
            {
                "integer" => schema.Default.Value.GetInt32(),
                "number" => schema.Default.Value.GetDouble(),
                "boolean" => schema.Default.Value.GetBoolean(),
                _ => schema.Default.Value.GetString()
            };
        }
    }

    // Custom converter to handle type being either a string or array of strings
    public class StringOrArrayConverter : JsonConverter<List<string>>
    {
        public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return [reader.GetString() ?? "string"];
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        break;
                    }
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        list.Add(reader.GetString() ?? "");
                    }
                }
                return list;
            }
            return ["string"];
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            if (value.Count == 1)
            {
                writer.WriteStringValue(value[0]);
            }
            else
            {
                writer.WriteStartArray();
                foreach (var item in value)
                {
                    writer.WriteStringValue(item);
                }
                writer.WriteEndArray();
            }
        }
    }
}
