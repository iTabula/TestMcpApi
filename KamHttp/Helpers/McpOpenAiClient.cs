using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace KamHttp.Helpers
{
    public class McpOpenAiClient
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
        private ChatClient? _openAiClient;
        private readonly List<ChatMessage> _conversationHistory = [];

        private readonly ILogger<McpOpenAiClient>? _logger;
        private string? _sessionId;

        public McpOpenAiClient(string sseEndpoint, ILogger<McpOpenAiClient>? logger = null)
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

        public void SetOpenAiClient(string apiKey, string model = "gpt-4o")
        {
            _openAiClient = new ChatClient(model: model, apiKey: apiKey);
            _logger?.LogInformation($"[Using OpenAI model: {model}]");
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
                        name = "mcp-openai-client",
                        version = "1.0.0"
                    }
                });

                // Extract session ID if provided
                if (initResponse.RootElement.TryGetProperty("result", out var results) &&
                    results.TryGetProperty("sessionId", out var sessionIdProp))
                {
                    _sessionId = sessionIdProp.GetString();
                    _logger?.LogInformation($"Session ID: {_sessionId}");
                }
                
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

                _logger?.LogInformation("Ready to chat! OpenAI will intelligently select and execute MCP tools.");
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

            if (_openAiClient == null)
            {
                return "OpenAI client not configured.";
            }

            try
            {
                _logger?.LogInformation("[Processing with OpenAI...]");

                // Build chat messages
                var messages = new List<ChatMessage>
                {
                    new UserChatMessage(prompt)
                };

                // Convert MCP tools to OpenAI function definitions
                var options = new ChatCompletionOptions();
                foreach (var mcpTool in _tools)
                {
                    // Serialize to JSON and then create BinaryData to ensure proper formatting
                    var schemaObject = ConvertMcpSchemaToOpenAiSchema(mcpTool.InputSchema);
                    var functionParams = BinaryData.FromString(JsonSerializer.Serialize(schemaObject));
                    
                    options.Tools.Add(ChatTool.CreateFunctionTool(
                        functionName: mcpTool.Name,
                        functionDescription: mcpTool.Description,
                        functionParameters: functionParams
                    ));
                }

                // Conversation loop to handle tool calls
                int maxIterations = 5;
                int iteration = 0;

                while (iteration < maxIterations)
                {
                    var completionResult = await _openAiClient.CompleteChatAsync(messages, options);
                    var completion = completionResult.Value;

                    // Check if we have a text response
                    if (completion.FinishReason == ChatFinishReason.Stop &&
                        completion.Content.Count > 0 &&
                        !string.IsNullOrEmpty(completion.Content[0].Text))
                    {
                        var answer = completion.Content[0].Text;
                        _logger?.LogInformation("[OpenAI response generated]");
                        return CleanupResponse(answer);
                    }

                    // Check if OpenAI wants to call tools
                    if (completion.FinishReason == ChatFinishReason.ToolCalls)
                    {
                        // Add assistant's tool call request to messages
                        messages.Add(new AssistantChatMessage(completion));

                        // Handle each tool call
                        foreach (var toolCall in completion.ToolCalls)
                        {
                            _logger?.LogInformation($"[Tool call: {toolCall.FunctionName}]");

                            string toolResult;
                            try
                            {
                                // Call the MCP tool
                                var mcpResponse = await SendJsonRpcRequestAsync("tools/call", new
                                {
                                    name = toolCall.FunctionName,
                                    arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.FunctionArguments.ToString())
                                });

                                if (mcpResponse.RootElement.TryGetProperty("result", out var result) &&
                                    result.TryGetProperty("content", out var contentArray) &&
                                    contentArray.GetArrayLength() > 0)
                                {
                                    var firstContent = contentArray[0];
                                    if (firstContent.TryGetProperty("text", out var text))
                                    {
                                        toolResult = text.GetString() ?? "No response from tool.";
                                    }
                                    else
                                    {
                                        toolResult = JsonSerializer.Serialize(firstContent);
                                    }
                                }
                                else
                                {
                                    toolResult = "No response from tool.";
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Error calling MCP tool");
                                toolResult = JsonSerializer.Serialize(new
                                {
                                    error = "Failed to execute tool",
                                    message = ex.Message
                                });
                            }

                            messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                        }

                        iteration++;
                        continue;
                    }

                    // Unexpected finish reason
                    _logger?.LogWarning($"Unexpected finish reason: {completion.FinishReason}");
                    break;
                }

                return "I couldn't generate a proper answer. Please try again.";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OpenAI processing failed");
                return $"Error: {ex.Message}";
            }
        }

        private string CleanupResponse(string response)
        {
            return response
                .Replace("Hi, this is Ava your KAM AI Agent. How can I help you today? You can ask questions like How do I join KAM, what does KAM do.", "")
                .Replace("Hi, this is Ava, your KAM AI Agent.", "")
                .Replace("Let me check that for you.", "")
                .Replace("By the way, may I get your name, phone number, and email in case you'd like more insights or help joining KAM?", "")
                .Trim();
        }

        public async Task DisconnectAsync()
        {
            _cts?.Cancel();
            if (_sseTask != null)
            {
                await _sseTask;
            }
            _httpClient.Dispose();
            _logger?.LogInformation("Disconnected from MCP server");
        }

        private object ConvertMcpSchemaToOpenAiSchema(McpInputSchema? inputSchema)
        {
            if (inputSchema == null)
            {
                return new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                };
            }

            var openAiSchema = new Dictionary<string, object>
            {
                ["type"] = inputSchema.Type ?? "object"
            };

            if (inputSchema.Properties != null && inputSchema.Properties.Count > 0)
            {
                var properties = new Dictionary<string, object>();
                
                foreach (var prop in inputSchema.Properties)
                {
                    var propertyDef = new Dictionary<string, object>();
                    
                    // Convert Type array to string or array based on count
                    if (prop.Value.Type.Count == 1)
                    {
                        propertyDef["type"] = prop.Value.Type[0];
                    }
                    else if (prop.Value.Type.Count > 1)
                    {
                        propertyDef["type"] = prop.Value.Type.ToArray();
                    }
                    
                    if (!string.IsNullOrEmpty(prop.Value.Description))
                    {
                        propertyDef["description"] = prop.Value.Description;
                    }
                    
                    if (!string.IsNullOrEmpty(prop.Value.Format))
                    {
                        propertyDef["format"] = prop.Value.Format;
                    }
                    
                    if (prop.Value.Default.HasValue)
                    {
                        propertyDef["default"] = JsonSerializer.Deserialize<object>(prop.Value.Default.Value.GetRawText());
                    }
                    
                    properties[prop.Key] = propertyDef;
                }
                
                openAiSchema["properties"] = properties;
            }
            else
            {
                openAiSchema["properties"] = new { };
            }

            if (inputSchema.Required != null && inputSchema.Required.Count > 0)
            {
                openAiSchema["required"] = inputSchema.Required.ToArray();
            }

            return openAiSchema;
        }
    }
}