using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using KamHttp.Helpers;

namespace KamMobile.ViewModels;

public class ChatAIViewModel : INotifyPropertyChanged
{
    private readonly McpOpenAiClient _mcpOpenAiClient;
    private string _messageInput = string.Empty;
    private string _statusText = "Initializing AI assistant...";
    private bool _isSending = false;
    private bool _isInitialized = false;
    private bool _isInitializing = true;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    public ChatAIViewModel(McpOpenAiClient mcpOpenAiClient)
    {
        _mcpOpenAiClient = mcpOpenAiClient;
        Messages = new ObservableCollection<ChatMessage>();
        SendCommand = new Command(async () => await ExecuteSendAsync(), () => CanSendMessage());
        LogoutCommand = new Command(async () => await ExecuteLogoutAsync());
        
        // Add welcome message
        Messages.Add(new ChatMessage
        {
            Text = "👋 Please wait while I initialize...",
            IsUser = false,
            Timestamp = DateTime.Now
        });

        // Initialize client
        Task.Run(async () => await InitializeClientAsync());
    }

    private bool CanSendMessage()
    {
        return _isInitialized && !_isSending && !string.IsNullOrWhiteSpace(MessageInput);
    }

    private async Task InitializeClientAsync()
    {
        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            System.Diagnostics.Debug.WriteLine("Starting McpOpenAiClient initialization...");
            
            // Connect to MCP SSE server
            await _mcpOpenAiClient.ConnectAsync();
            System.Diagnostics.Debug.WriteLine("Connected to SSE server");

            // Initialize MCP session and discover tools
            await _mcpOpenAiClient.InitializeAsync();
            System.Diagnostics.Debug.WriteLine("MCP session initialized");

            _isInitialized = true;
            IsInitializing = false;
            
            // Update UI on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = string.Empty;
                Messages.Clear();
                Messages.Add(new ChatMessage
                {
                    Text = "👋 Hello! I'm your AI assistant. Ask me anything!",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
                ((Command)SendCommand).ChangeCanExecute();
            });
            
            System.Diagnostics.Debug.WriteLine("McpOpenAiClient initialized successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize McpOpenAiClient: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsInitializing = false;
                StatusText = "Failed to initialize. Please restart the app.";
                Messages.Clear();
                Messages.Add(new ChatMessage
                {
                    Text = $"⚠️ Failed to initialize: {ex.Message}\n\nPlease check your connection and restart the app.",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
            });
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public ObservableCollection<ChatMessage> Messages { get; }

    public string MessageInput
    {
        get => _messageInput;
        set
        {
            _messageInput = value;
            OnPropertyChanged();
            ((Command)SendCommand).ChangeCanExecute();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public bool IsSending
    {
        get => _isSending;
        set
        {
            _isSending = value;
            OnPropertyChanged();
            ((Command)SendCommand).ChangeCanExecute();
        }
    }

    public bool IsInitializing
    {
        get => _isInitializing;
        set
        {
            _isInitializing = value;
            OnPropertyChanged();
        }
    }

    public ICommand SendCommand { get; }
    public ICommand LogoutCommand { get; }

    private async Task ExecuteSendAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageInput) || !_isInitialized)
            return;

        var userMessage = MessageInput.Trim();
        MessageInput = string.Empty;

        // Add user message
        Messages.Add(new ChatMessage
        {
            Text = userMessage,
            IsUser = true,
            Timestamp = DateTime.Now
        });

        IsSending = true;
        StatusText = "AI is thinking...";

        try
        {
            // Get user information (you can extend this with actual claims if needed)
            string userId = "mobile_user";
            string role = "user";
            string accessToken = string.Empty;

            // Build enhanced prompt with user context (matching web implementation)
            string prompt = $"{userMessage} with user_id = {userId} and user_role = '{role}' and token = '{accessToken}'";

            System.Diagnostics.Debug.WriteLine($"Sending prompt: {userMessage}");
            
            // Process with OpenAI MCP client
            var answer = await _mcpOpenAiClient.ProcessPromptAsync(prompt);
            
            System.Diagnostics.Debug.WriteLine($"Received answer: {answer}");

            // Add AI response
            Messages.Add(new ChatMessage
            {
                Text = answer,
                IsUser = false,
                Timestamp = DateTime.Now
            });

            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ExecuteSendAsync: {ex.Message}");
            Messages.Add(new ChatMessage
            {
                Text = $"I'm sorry, I encountered an error: {ex.Message}",
                IsUser = false,
                Timestamp = DateTime.Now
            });
            StatusText = "Error occurred";
        }
        finally
        {
            IsSending = false;
        }
    }

    private async Task ExecuteLogoutAsync()
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ChatMessage
{
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }
}