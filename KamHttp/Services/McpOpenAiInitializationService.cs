using KamHttp.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KamHttp.Services;

public class McpOpenAiInitializationService : BackgroundService, IHostedService
{
    private readonly McpOpenAiClient _mcpOpenAiClient;
    private readonly ILogger<McpOpenAiInitializationService> _logger;

    public McpOpenAiInitializationService(
        McpOpenAiClient mcpOpenAiClient,
        ILogger<McpOpenAiInitializationService> logger)
    {
        _mcpOpenAiClient = mcpOpenAiClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting OpenAI MCP client initialization...");

            // Connect to MCP SSE server
            await _mcpOpenAiClient.ConnectAsync();

            // Initialize MCP session and discover tools
            await _mcpOpenAiClient.InitializeAsync();

            _logger.LogInformation("OpenAI MCP client initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize OpenAI MCP client");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping OpenAI MCP client...");
        await _mcpOpenAiClient.DisconnectAsync();
        await base.StopAsync(stoppingToken);
    }
}