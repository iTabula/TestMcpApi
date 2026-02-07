using Microsoft.Extensions.DependencyInjection;
using KamHttp.Helpers;

namespace KamMobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }

        protected override void OnStart()
        {
            base.OnStart();
            
            // Initialize McpOpenAiClient in background after app has started
            Task.Run(async () =>
            {
                try
                {
                    var mcpOpenAiClient = IPlatformApplication.Current?.Services.GetService<KamHttp.Helpers.McpOpenAiClient>();

                    if (mcpOpenAiClient != null)
                    {
                        System.Diagnostics.Debug.WriteLine("Initializing McpOpenAiClient...");
                        
                        // Connect to MCP SSE server
                        await mcpOpenAiClient.ConnectAsync();

                        // Initialize MCP session and discover tools
                        await mcpOpenAiClient.InitializeAsync();
                        
                        System.Diagnostics.Debug.WriteLine("McpOpenAiClient initialized successfully");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize McpOpenAiClient: {ex.Message}");
                }
            });
        }
    }
}