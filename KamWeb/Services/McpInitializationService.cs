using KamWeb.Helpers;

namespace KamWeb.Services;

public class McpInitializationService : BackgroundService
{
    private readonly McpSseClient _mcpClient;
    private readonly ILogger<McpInitializationService> _logger;

    public McpInitializationService(
        McpSseClient mcpClient, 
        ILogger<McpInitializationService> logger)
    {
        _mcpClient = mcpClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Initializing MCP SSE Client...");
            
            await _mcpClient.ConnectAsync();
            await _mcpClient.InitializeAsync();
            
            _logger.LogInformation("MCP SSE Client initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MCP SSE Client");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Disconnecting MCP SSE Client...");
        await _mcpClient.DisconnectAsync();
        await base.StopAsync(cancellationToken);
    }
}