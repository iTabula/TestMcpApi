using KamMobile.Helpers;
using KamMobile.Models;
using KamMobile.Services;
using KamMobile.ViewModels;
using KamMobile.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace KamMobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Add configuration
            var a = builder.Configuration.GetSection("Mcp");
            var b = builder.Configuration.GetSection("Vapi");

            // Register services
            builder.Services.AddSingleton<AuthenticationService>();

            // Register McpSseClient
            builder.Services.AddSingleton<McpSseClient>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var mcpConfig = configuration.GetSection("Mcp").Get<McpConfiguration>();
                var vapiConfig = configuration.GetSection("Vapi").Get<VapiConfiguration>();

                var client = new McpSseClient(
                    mcpConfig?.SseEndpoint ?? "https://mcp.kamfr.com/api/mcp/sse");

                if (vapiConfig != null)
                {
                    client.SetVapiClient(vapiConfig.PrivateApiKey, vapiConfig.AssistantId);
                }

                // Initialize on background thread
                Task.Run(async () =>
                {
                    try
                    {
                        await client.ConnectAsync();
                        await client.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error initializing MCP client: {ex.Message}");
                    }
                });

                return client;
            });

            // Register ViewModels
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<ChatViewModel>();

            // Register Views
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<ChatPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
