using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Media;
using KamHttp.Helpers;
using KamHttp.Services;
using KamMobile.Models;
using KamMobile.Services;
using KamMobile.ViewModels;
using KamMobile.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;

namespace KamMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
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

        // Register McpSseClient as singleton (one instance for the entire application)
        builder.Services.AddSingleton<McpSseClient>(sp =>
        {
            var mcpConfig = builder.Configuration.GetSection("Mcp").Get<McpConfiguration>();
            var vapiConfig = builder.Configuration.GetSection("Vapi").Get<VapiConfiguration>();

            var client = new McpSseClient(mcpConfig?.SseEndpoint ?? "https://freemypalestine.com/api/mcp/sse");

            if (vapiConfig != null)
            {
                client.SetVapiClient(vapiConfig.PrivateApiKey, vapiConfig.AssistantId);
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

        // Register Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ChatPage>();

        return builder.Build();
    }
}
