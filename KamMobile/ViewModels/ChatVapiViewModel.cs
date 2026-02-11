using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using KamHttp.Helpers;
using KamHttp.Services;

namespace KamMobile.ViewModels;

public class ChatVapiViewModel : INotifyPropertyChanged
{
    private readonly McpSseClient _mcpSseClient;
    private string _messageInput = string.Empty;
    private string _statusText = "Initializing VAPI assistant...";
    private bool _isSending = false;
    private bool _isInitialized = false;
    private bool _isInitializing = true;

    public ChatVapiViewModel(McpSseClient mcpSseClient)
    {
        _mcpSseClient = mcpSseClient;
        Messages = new ObservableCollection<ChatMessage>();
        SendCommand = new Command(async () => await ExecuteSendAsync(), () => CanSendMessage());
        LogoutCommand = new Command(async () => await ExecuteLogoutAsync());
        
        Messages.Add(new ChatMessage
        {
            Text = "👋 Please wait while I initialize...",
            IsUser = false,
            Timestamp = DateTime.Now
        });

        Task.Run(async () => await WaitForInitializationAsync());
    }

    private bool CanSendMessage()
    {
        return _isInitialized && !_isSending && !string.IsNullOrWhiteSpace(MessageInput);
    }

    private async Task WaitForInitializationAsync()
    {
        try
        {
            // Connect to the MCP SSE server first
            await _mcpSseClient.ConnectAsync();
            
            // Initialize and load tools
            await _mcpSseClient.InitializeAsync();

            // Verify tools were loaded
            if (_mcpSseClient.ToolCount == 0)
            {
                throw new TimeoutException("No tools were loaded after initialization");
            }

            _isInitialized = true;
            IsInitializing = false;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = string.Empty;
                Messages.Clear();
                Messages.Add(new ChatMessage
                {
                    Text = "👋 Hello! I'm your VAPI assistant. Ask me anything!",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
                ((Command)SendCommand).ChangeCanExecute();
            });

            System.Diagnostics.Debug.WriteLine("ChatVapiViewModel ready to process messages");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Initialization failed: {ex.Message}");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsInitializing = false;
                StatusText = "Failed to initialize";
                Messages.Clear();
                Messages.Add(new ChatMessage
                {
                    Text = $"⚠️ Failed to initialize: {ex.Message}\n\nPlease restart the app.",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
            });
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

        Messages.Add(new ChatMessage
        {
            Text = userMessage,
            IsUser = true,
            Timestamp = DateTime.Now
        });

        IsSending = true;
        StatusText = "VAPI is thinking...";

        try
        {
            string userId = "2";
            string role = "Admin";
            string accessToken = string.Empty;

            string prompt = $"{userMessage} with user_id = {userId} and user_role = '{role}' and token = '{accessToken}'";

            System.Diagnostics.Debug.WriteLine($"Sending prompt: {userMessage}");
            
            var answer = await _mcpSseClient.ProcessPromptAsync(prompt);
            
            System.Diagnostics.Debug.WriteLine($"Received answer: {answer}");

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
        // await _initService.DisconnectAsync(); // Removed: method does not exist
        await Shell.Current.GoToAsync("//LoginPage");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}