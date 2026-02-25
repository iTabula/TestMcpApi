using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using KamHttp.Helpers;
using KamHttp.Services;
using KamMobile.Interfaces;
using KamMobile.Services;

namespace KamMobile.ViewModels;

public class ChatVapiViewModel : INotifyPropertyChanged
{
    private readonly McpSseClient _mcpSseClient;
    private readonly AuthenticationService _authService;
    private readonly ISpeechToTextService _speechService;
    private string _messageInput = string.Empty;
    private string _statusText = "Initializing VAPI assistant...";
    private bool _isSending = false;
    private bool _isInitialized = false;
    private bool _isInitializing = true;
    private bool _isSpeaking = false;
    private bool _isListening = false;
    private CancellationTokenSource? _speechCts;

    public ChatVapiViewModel(McpSseClient mcpSseClient, AuthenticationService authService, ISpeechToTextService speechService)
    {
        _mcpSseClient = mcpSseClient;
        _authService = authService;
        _speechService = speechService;
        Messages = new ObservableCollection<ChatMessage>();
        SendCommand = new Command(async () => await ExecuteSendAsync(), () => CanSendMessage());
        LogoutCommand = new Command(async () => await ExecuteLogoutAsync());
        StopSpeakingCommand = new Command(async () => await ExecuteStopSpeakingAsync(), () => _isSpeaking);
        ToggleListeningCommand = new Command(async () => await ExecuteToggleListeningAsync(), () => CanToggleListening());

        Messages.Add(new ChatMessage
        {
            Text = "Please wait while I initialize...",
            IsUser = false,
            Timestamp = DateTime.Now
        });

        Task.Run(async () => await WaitForInitializationAsync());
    }

    private bool CanSendMessage()
    {
        return _isInitialized && !_isSending && !_isSpeaking && !_isListening && !string.IsNullOrWhiteSpace(MessageInput);
    }

    private bool CanToggleListening()
    {
        return _isInitialized && !_isSending && !_isSpeaking;
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

            // Set initialization flags BEFORE UI updates - use backing fields to avoid 
            // triggering PropertyChanged on background thread
            _isInitialized = true;
            _isInitializing = false;
            System.Diagnostics.Debug.WriteLine("ChatVapiViewModel initialization successful");

            // Update UI - if this fails, the initialization state is still correct
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // Trigger property change notifications now that we're on main thread
                    OnPropertyChanged(nameof(IsInitializing));
                    OnPropertyChanged(nameof(IsStatusVisible));
                    OnPropertyChanged(nameof(AreButtonsEnabled));
                    
                    StatusText = string.Empty;
                    Messages.Clear();
                    var userName = _authService.CurrentUser?.FirstName ?? "User";
                    Messages.Add(new ChatMessage
                    {
                        Text = $"Hello {userName}! I'm your VAPI assistant. Ask me anything!",
                        IsUser = false,
                        Timestamp = DateTime.Now
                    });
                    ((Command)SendCommand).ChangeCanExecute();
                    ((Command)ToggleListeningCommand).ChangeCanExecute();
                });
                System.Diagnostics.Debug.WriteLine("ChatVapiViewModel UI updated successfully");
            }
            catch (Exception uiEx)
            {
                // Log UI update failure but don't fail initialization
                System.Diagnostics.Debug.WriteLine($"UI update failed (initialization still successful): {uiEx.Message}");

                // Try a simpler UI update without clearing/rebuilding
                try
                {
                    await Task.Delay(100); // Brief delay to let main thread catch up
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        OnPropertyChanged(nameof(IsInitializing));
                        OnPropertyChanged(nameof(IsStatusVisible));
                        OnPropertyChanged(nameof(AreButtonsEnabled));
                        StatusText = string.Empty;
                        ((Command)SendCommand).ChangeCanExecute();
                        ((Command)ToggleListeningCommand).ChangeCanExecute();
                    });
                }
                catch
                {
                    // If even this fails, just ensure commands are updated on next interaction
                    System.Diagnostics.Debug.WriteLine("Retry UI update also failed, but initialization is complete");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Initialization failed: {ex.Message}");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _isInitializing = false;
                OnPropertyChanged(nameof(IsInitializing));
                OnPropertyChanged(nameof(IsStatusVisible));
                StatusText = "Failed to initialize";
                Messages.Clear();
                Messages.Add(new ChatMessage
                {
                    Text = $"Failed to initialize: {ex.Message}\n\nPlease restart the app.",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
                ((Command)ToggleListeningCommand).ChangeCanExecute();
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
            OnPropertyChanged(nameof(IsStatusVisible));
        }
    }

    public bool IsInitializing
    {
        get => _isInitializing;
        set
        {
            _isInitializing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStatusVisible));
            OnPropertyChanged(nameof(AreButtonsEnabled));
            ((Command)ToggleListeningCommand).ChangeCanExecute();
        }
    }

    public bool IsSpeaking
    {
        get => _isSpeaking;
        set
        {
            _isSpeaking = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStatusVisible));
            OnPropertyChanged(nameof(AreButtonsEnabled));
            ((Command)SendCommand).ChangeCanExecute();
            ((Command)StopSpeakingCommand).ChangeCanExecute();
            ((Command)ToggleListeningCommand).ChangeCanExecute();
        }
    }

    public bool IsListening
    {
        get => _isListening;
        set
        {
            _isListening = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MicButtonText));
            OnPropertyChanged(nameof(AreButtonsEnabled));
            ((Command)SendCommand).ChangeCanExecute();
            ((Command)ToggleListeningCommand).ChangeCanExecute();
        }
    }

    public bool IsStatusVisible => !_isInitializing && !string.IsNullOrWhiteSpace(StatusText);

    public bool AreButtonsEnabled => !_isInitializing && !_isSpeaking && !_isListening && !_isSending;

    public string MicButtonText => IsListening ? "Listening..." : "Tap to Speak";

    public ICommand SendCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand StopSpeakingCommand { get; }
    public ICommand ToggleListeningCommand { get; }

    private async Task ExecuteSendAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageInput) || _isSending || _isSpeaking || _isListening)
            return;

        var userMessage = MessageInput.Trim();
        MessageInput = string.Empty;
        
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _isSending = true;
            StatusText = "Thinking...";
            OnPropertyChanged(nameof(IsStatusVisible));
            OnPropertyChanged(nameof(AreButtonsEnabled));
            ((Command)SendCommand).ChangeCanExecute();
            ((Command)ToggleListeningCommand).ChangeCanExecute();
        });

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
                StatusText = string.Empty;
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
                StatusText = string.Empty;
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _isSending = false;
                OnPropertyChanged(nameof(IsStatusVisible));
                OnPropertyChanged(nameof(AreButtonsEnabled));
                ((Command)SendCommand).ChangeCanExecute();
                ((Command)ToggleListeningCommand).ChangeCanExecute();
            });
        }
    }

    private async Task ExecuteToggleListeningAsync()
    {
        if (IsListening)
        {
            await ExecuteStopListeningAsync();
            return;
        }

        await ExecuteStartListeningAsync();
    }

    private async Task ExecuteStartListeningAsync()
    {
        if (_isListening || _isSending || _isSpeaking || !_isInitialized)
            return;

        IsListening = true;
        StatusText = "Listening... Speak now";

        await _speechService.StartListeningAsync(
            result =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    IsListening = false;

                    if (string.IsNullOrWhiteSpace(result))
                    {
                        StatusText = "No speech detected";
                        return;
                    }

                    MessageInput = result.Trim();
                    StatusText = string.Empty;
                    await ExecuteSendAsync();
                });
            },
            error =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsListening = false;
                    StatusText = $"Speech error: {error}";
                });
            });
    }

    private async Task ExecuteStopListeningAsync()
    {
        if (!_isListening)
            return;

        StatusText = "Stopping...";
        await _speechService.StopListeningAsync();
        IsListening = false;
        StatusText = string.Empty;
    }

    private async Task SpeakAnswerAsync(string text)
    {
        try
        {
            _isSending = false;
            IsSpeaking = true;
            StatusText = "Speaking response...";
            OnPropertyChanged(nameof(AreButtonsEnabled));

            _speechCts = new CancellationTokenSource();

            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var selectedLocale = locales.FirstOrDefault();

            var speechOptions = new SpeechOptions
            {
                Pitch = 1.0f,
                Volume = 1.0f,
                Locale = selectedLocale
            };

            System.Diagnostics.Debug.WriteLine($"Starting to speak: {text.Substring(0, Math.Min(50, text.Length))}...");
            await TextToSpeech.Default.SpeakAsync(text, speechOptions, _speechCts.Token);
            System.Diagnostics.Debug.WriteLine("Speech completed normally");

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
            System.Diagnostics.Debug.WriteLine("Speech was cancelled");
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

    private async Task ExecuteStopSpeakingAsync()
    {
        System.Diagnostics.Debug.WriteLine("Stop speaking requested");
        
        try
        {
            _speechCts?.Cancel();
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = "Speech stopped";
                IsSpeaking = false;
            });

            // Give a moment for the cancellation to process
            await Task.Delay(100);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = string.Empty;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping speech: {ex.Message}");
        }
    }

    private async Task ExecuteLogoutAsync()
    {
        try
        {
            // Stop speaking if in progress
            if (_isSpeaking)
            {
                await ExecuteStopSpeakingAsync();
            }

            if (_isListening)
            {
                await ExecuteStopListeningAsync();
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
