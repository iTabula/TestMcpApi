using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Media;
using KamHttp.Helpers;
using KamHttp.Services;
using KamHttp.Interfaces;
using KamMobile.Models;
using KamMobile.Services;
using KamMobile.ViewModels;
using KamMobile.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Net.Http.Headers;

namespace KamMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        // Debug: List available embedded resources
        var assembly = typeof(MauiProgram).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();
        System.Diagnostics.Debug.WriteLine("=== Available Embedded Resources ===");
        foreach (var resourceName in resourceNames.Where(r => r.Contains("appsettings", StringComparison.OrdinalIgnoreCase)))
        {
            System.Diagnostics.Debug.WriteLine($"  - {resourceName}");
        }
        System.Diagnostics.Debug.WriteLine("=====================================");

        // Try to load appsettings.json from embedded resource
        Stream? configStream = null;
        
        // First try: KamMobile.appsettings.json
        configStream = assembly.GetManifestResourceStream("KamMobile.appsettings.json");
        if (configStream != null)
        {
            System.Diagnostics.Debug.WriteLine("✓ Loaded: KamMobile.appsettings.json");
        }
        else
        {
            // Second try: appsettings.json (root namespace)
            configStream = assembly.GetManifestResourceStream("appsettings.json");
            if (configStream != null)
            {
                System.Diagnostics.Debug.WriteLine("✓ Loaded: appsettings.json");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("✗ FAILED: Could not find appsettings.json");
                System.Diagnostics.Debug.WriteLine("  Ensure appsettings.json is set as EmbeddedResource in .csproj");
            }
        }
        
        if (configStream != null)
        {
            builder.Configuration.AddJsonStream(configStream);
            System.Diagnostics.Debug.WriteLine("✓ Configuration loaded from embedded resource");
        }
        
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Configure options
        builder.Services.Configure<VapiConfiguration>(builder.Configuration.GetSection("Vapi"));
        builder.Services.Configure<McpConfiguration>(builder.Configuration.GetSection("Mcp"));
        builder.Services.Configure<OpenAIConfiguration>(builder.Configuration.GetSection("OpenAI"));

        // Get the base URL from configuration
        var webApiBaseUrl = builder.Configuration.GetValue<string>("WebApi:BaseUrl");

        // Register HttpClient for API communication (matches KamWeb pattern)
        builder.Services.AddHttpClient("MyWebApi",
            client =>
            {
                client.BaseAddress = new Uri(webApiBaseUrl ?? "https://mcpauth.kamfr.com/");
                client.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/vnd.github.v3+json");
                client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, "KAM.Mobile");
                client.Timeout = TimeSpan.FromSeconds(120);
            });

        // Register factory and services (matches KamWeb pattern)
        builder.Services.AddSingleton<IFactoryHttpClient, FactoryHttpClient>();
        builder.Services.AddScoped<IUserService, UserService>();

        // Register McpSseClient as singleton with logger
        builder.Services.AddSingleton<McpSseClient>(sp =>
        {
            var mcpConfig = builder.Configuration.GetSection("Mcp").Get<McpConfiguration>();
            var vapiConfig = builder.Configuration.GetSection("Vapi").Get<VapiConfiguration>();
            var logger = sp.GetRequiredService<ILogger<McpSseClient>>();
            
            // DIAGNOSTIC LOGGING - Match KamWeb behavior
            logger.LogInformation("=== McpSseClient Configuration ===");
            logger.LogInformation($"MCP SseEndpoint: {mcpConfig?.SseEndpoint ?? "NULL"}");
            logger.LogInformation($"Vapi Config Present: {(vapiConfig != null ? "YES" : "NO")}");
            if (vapiConfig != null)
            {
                logger.LogInformation($"Vapi PrivateApiKey: {(string.IsNullOrEmpty(vapiConfig.PrivateApiKey) ? "EMPTY" : "SET")}");
                logger.LogInformation($"Vapi AssistantId: {vapiConfig.AssistantId ?? "NULL"}");
            }
            logger.LogInformation("===================================");
            
            var client = new McpSseClient(mcpConfig?.SseEndpoint ?? "https://freemypalestine.com/api/mcp/sse", logger);

            // Match KamWeb pattern: configure Vapi if config exists
            if (vapiConfig != null)
            {
                logger.LogInformation("✓ Configuring Vapi client with assistant: {AssistantId}", vapiConfig.AssistantId);
                client.SetVapiClient(vapiConfig.PrivateApiKey, vapiConfig.AssistantId);
            }
            else
            {
                logger.LogWarning("✗ Vapi configuration is NULL - configuration may not be loading correctly");
            }

            // Initialize immediately during registration
            Task.Run(async () =>
            {
                try
                {
                    await client.ConnectAsync();
                    await client.InitializeAsync();
                    logger.LogInformation("✓ MCP SSE Client initialized successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "✗ Failed to initialize MCP SSE Client");
                }
            });

            return client;
        });

        // Register McpOpenAiClient as singleton
        builder.Services.AddSingleton<McpOpenAiClient>(sp =>
        {
            var mcpConfig = builder.Configuration.GetSection("Mcp").Get<McpConfiguration>();
            var openAiConfig = builder.Configuration.GetSection("OpenAI").Get<OpenAIConfiguration>();
            var logger = sp.GetRequiredService<ILogger<McpOpenAiClient>>();
            
            var client = new McpOpenAiClient(mcpConfig?.SseEndpoint ?? "https://freemypalestine.com/api/mcp/sse", logger);
            
            if (openAiConfig != null && !string.IsNullOrEmpty(openAiConfig.ApiKey))
            {
                client.SetOpenAiClient(openAiConfig.ApiKey, openAiConfig.Model ?? "gpt-4o");
            }
            
            return client;
        });

        // Register Speech Recognition Service (platform-specific)
#if ANDROID
        builder.Services.AddSingleton<ISpeechRecognitionService, Platforms.Android.Services.AndroidSpeechRecognitionService>();
#endif

        builder.Services.AddSingleton<AuthenticationService>();

        // Register ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<ChatViewModel>();
        builder.Services.AddTransient<ChatVapiViewModel>();
        builder.Services.AddTransient<ChatAIViewModel>();

        // Register Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<ChatVapiPage>();
        builder.Services.AddTransient<ChatAIPage>();

        return builder.Build();
    }
}
