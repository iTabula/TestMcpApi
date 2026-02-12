using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using KamHttp.Helpers;
using KamHttp.Services;
using KamMobile.Services;

namespace KamMobile.ViewModels;

public class ChatVapiViewModel : INotifyPropertyChanged
{
    private readonly McpSseClient _mcpSseClient;
    private readonly AuthenticationService _authService;
    private string _messageInput = string.Empty;
    private string _statusText = "Initializing VAPI assistant...";
    private bool _isSending = false;
    private bool _isInitialized = false;
    private bool _isInitializing = true;
    private bool _isSpeaking = false;
    private CancellationTokenSource? _speechCts;

    public ChatVapiViewModel(McpSseClient mcpSseClient, AuthenticationService authService)
    {
        _mcpSseClient = mcpSseClient;
        _authService = authService;
        Messages = new ObservableCollection<ChatMessage>();
        SendCommand = new Command(async () => await ExecuteSendAsync(), () => CanSendMessage());
        LogoutCommand = new Command(async () => await ExecuteLogoutAsync());
        StopSpeakingCommand = new Command(StopSpeaking, () => _isSpeaking);
        
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
        return _isInitialized && !_isSending && !_isSpeaking && !string.IsNullOrWhiteSpace(MessageInput);
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
                var userName = _authService.CurrentUser?.FirstName ?? "User";
                Messages.Add(new ChatMessage
                {
                    Text = $"👋 Hello {userName}! I'm your VAPI assistant. Ask me anything!",
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

    public bool IsInitializing
    {
        get => _isInitializing;
        set
        {
            _isInitializing = value;
            OnPropertyChanged();
        }
    }

    public bool IsSpeaking
    {
        get => _isSpeaking;
        set
        {
            _isSpeaking = value;
            OnPropertyChanged();
            ((Command)SendCommand).ChangeCanExecute();
            ((Command)StopSpeakingCommand).ChangeCanExecute();
        }
    }

    public ICommand SendCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand StopSpeakingCommand { get; }

    private async Task ExecuteSendAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageInput) || _isSending || _isSpeaking)
            return;

        var userMessage = MessageInput.Trim();
        MessageInput = string.Empty;
        _isSending = true;
        ((Command)SendCommand).ChangeCanExecute();

        try
        {
            // Add user message to chat
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Messages.Add(new ChatMessage
                {
                    Text = userMessage,
                    IsUser = true,
                    Timestamp = DateTime.Now
                });
            });

            // Build prompt with authenticated user context (matching KamWeb Chat.cshtml.cs pattern)
            var accessToken = _authService.AccessToken ?? string.Empty;
            var userId = _authService.UserId ?? string.Empty;
            var role = _authService.Role ?? string.Empty;

            string prompt = userMessage + $" with user_id = {userId} and user_role = '{role}' and token = '{accessToken}'";

            // Get response from MCP
            var response = await _mcpSseClient.ProcessPromptAsync(prompt);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Messages.Add(new ChatMessage
                {
                    Text = response,
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
            });

            // Speak the response
            await SpeakAnswerAsync(response);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending message: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Messages.Add(new ChatMessage
                {
                    Text = $"Error: {ex.Message}",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
            });
        }
        finally
        {
            _isSending = false;
            ((Command)SendCommand).ChangeCanExecute();
        }
    }

    private async Task SpeakAnswerAsync(string text)
    {
        _isSending = false;
        IsSpeaking = true;
        StatusText = "Speaking response...";

        try
        {
            _speechCts = new CancellationTokenSource();

            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var selectedLocale = locales.FirstOrDefault();

            var speechOptions = new SpeechOptions
            {
                Pitch = 1.0f,
                Volume = 1.0f,
                Locale = selectedLocale
            };

            await TextToSpeech.Default.SpeakAsync(text, speechOptions, _speechCts.Token);

            if (!_speechCts.IsCancellationRequested)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    StatusText = string.Empty;
                    IsSpeaking = false;
                });
            }
        }
        catch (OperationCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = "Speech stopped";
                IsSpeaking = false;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error speaking: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = $"Error speaking: {ex.Message}";
                IsSpeaking = false;
            });
        }
        finally
        {
            _speechCts?.Dispose();
            _speechCts = null;
        }
    }

    private void StopSpeaking()
    {
        _speechCts?.Cancel();
        TextToSpeech.Default.SpeakAsync(string.Empty);
        StatusText = string.Empty;
        IsSpeaking = false;
    }

    private async Task ExecuteLogoutAsync()
    {
        try
        {
            // Stop speaking if in progress
            if (_isSpeaking)
            {
                StopSpeaking();
            }

            await _authService.LogoutAsync();
            await Shell.Current.GoToAsync("//LoginPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logout error: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
