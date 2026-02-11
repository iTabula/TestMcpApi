using Microsoft.Extensions.DependencyInjection;
using KamHttp.Helpers;
using KamHttp.Services;

namespace KamMobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new AppShell();
        }

        protected override async void OnStart()
        {
            base.OnStart();
            
            try
            {
                // Initialize McpSseClient using the existing McpInitializationService
                // Call StartAsync() to start the background service
                var initService = IPlatformApplication.Current!.Services.GetService<McpInitializationService>();
                if (initService != null)
                {
                    await initService.StartAsync(CancellationToken.None);
                    System.Diagnostics.Debug.WriteLine("McpSseClient (VAPI) initialized successfully");
                }

                // Initialize McpOpenAiClient
                var mcpOpenAiClient = IPlatformApplication.Current!.Services.GetService<McpOpenAiClient>();
                if (mcpOpenAiClient != null)
                {
                    await mcpOpenAiClient.ConnectAsync();
                    await mcpOpenAiClient.InitializeAsync();
                    System.Diagnostics.Debug.WriteLine("McpOpenAiClient initialized successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Initialization error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}