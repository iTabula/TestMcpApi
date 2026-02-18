using KamHttp.Helpers;
using KamMobile.Interfaces;
using KamMobile.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace KamMobile.ViewModels;

public class ChatViewModel : INotifyPropertyChanged
{
    private readonly McpSseClient _mcpClient;
    private readonly ISpeechToTextService _speechService;
    private readonly ILogger<ChatViewModel> _logger;
    private string _questionText = "Waiting for your question...";
    private string _answerText = "Waiting for question...";
    private string _statusText = "Click to start";
    private bool _isListening = false;
    private bool _isProcessing = false;
    private bool _isSpeaking = false;
    private string _buttonText = "Start Conversation";
    private CancellationTokenSource? _recognitionCts;
    private CancellationTokenSource? _speechCts;

    public ChatViewModel(McpSseClient mcpClient, ISpeechToTextService speechService, ILogger<ChatViewModel> logger)
    {
        _mcpClient = mcpClient;
        _speechService = speechService;
        _logger = logger;
        StartConversationCommand = new Command(async () => await ExecuteStartConversationAsync());
        LogoutCommand = new Command(async () => await ExecuteLogoutAsync());
        SendMessageCommand = new Command(async () => await SendMessageAsync());
    }

    private string _currentMessage = string.Empty;
    public ObservableCollection<string> Messages { get; } = new();

    public string CurrentMessage
    {
        get => _currentMessage;
        set => SetProperty(ref _currentMessage, value);
    }

    public string QuestionText
    {
        get => _questionText;
        set
        {
            _questionText = value;
            OnPropertyChanged();
        }
    }

    public string AnswerText
    {
        get => _answerText;
        set
        {
            _answerText = value;
            OnPropertyChanged();
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

    public bool IsListening
    {
        get => _isListening;
        set
        {
            _isListening = value;
            OnPropertyChanged();
        }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            _isProcessing = value;
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
        }
    }

    public string ButtonText
    {
        get => _buttonText;
        set
        {
            _buttonText = value;
            OnPropertyChanged();
        }
    }

    public ICommand StartConversationCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand SendMessageCommand { get; }

    private async Task ExecuteStartConversationAsync()
    {
        if (!IsListening && !IsProcessing && !IsSpeaking)
        {
            //await StartListeningAsync();
        }
        else if (IsListening)
        {
            await StopListeningAsync();
        }
        else if (IsSpeaking)
        {
            StopSpeaking();
        }
    }

    //private async Task StartListeningAsync()
    //{
    //    try
    //    {
    //        // Request microphone permissions
    //        var status = await Permissions.RequestAsync<Permissions.Microphone>();
    //        if (status != PermissionStatus.Granted)
    //        {
    //            StatusText = "Microphone permission denied";
    //            return;
    //        }

    //        IsListening = true;
    //        ButtonText = "Stop Listening";
    //        StatusText = "Listening... Speak now";
    //        QuestionText = "Listening...";

    //        _recognitionCts = new CancellationTokenSource();
            
    //        var recognizedText = await _speechService.StartListeningAsync(
    //            partialText =>
    //            {
    //                MainThread.BeginInvokeOnMainThread(() =>
    //                {
    //                    if (!string.IsNullOrWhiteSpace(partialText))
    //                    {
    //                        QuestionText = partialText;
    //                    }
    //                });
    //            },
    //            _recognitionCts.Token);

    //        if (!string.IsNullOrWhiteSpace(recognizedText))
    //        {
    //            QuestionText = recognizedText;
    //            await ProcessQuestionAsync(recognizedText);
    //        }
    //        else
    //        {
    //            StatusText = "No speech detected. Click to try again.";
    //            ResetState();
    //        }
    //    }
    //    catch (OperationCanceledException)
    //    {
    //        StatusText = "Listening cancelled";
    //        ResetState();
    //    }
    //    catch (Exception ex)
    //    {
    //        StatusText = $"Error: {ex.Message}";
    //        ResetState();
    //    }
    //}

    private async Task StopListeningAsync()
    {
        _recognitionCts?.Cancel();
        StatusText = "Stopping...";
        
        // Give the cancellation time to process
        await Task.Delay(100);
        
        IsListening = false;
        ButtonText = "Start Conversation";
        StatusText = "Click to start";
    }

    private async Task ProcessQuestionAsync(string question)
    {
        IsListening = false;
        IsProcessing = true;
        ButtonText = "Processing...";
        StatusText = "Processing your question...";
        AnswerText = "Thinking...";

        try
        {
            var answer = await _mcpClient.ProcessPromptAsync(question.Trim());
            AnswerText = answer;
            await SpeakAnswerAsync(answer);
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AnswerText = "Sorry, I encountered an error processing your question.";
            ResetState();
        }
    }

    private async Task SpeakAnswerAsync(string text)
    {
        IsProcessing = false;
        IsSpeaking = true;
        StatusText = "Speaking answer...";
        ButtonText = "Stop Speaking";

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
                StatusText = "Done! Click to ask another question.";
                ResetState();
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Stopped. Click to ask another question.";
            ResetState();
        }
        catch (Exception ex)
        {
            StatusText = $"Error speaking: {ex.Message}";
            ResetState();
        }
    }

    private void StopSpeaking()
    {
        _speechCts?.Cancel();
        TextToSpeech.Default.SpeakAsync(string.Empty);
        StatusText = "Stopped. Click to ask another question.";
        ResetState();
    }

    private void ResetState()
    {
        IsListening = false;
        IsProcessing = false;
        IsSpeaking = false;
        ButtonText = "Start Conversation";
        _recognitionCts?.Dispose();
        _recognitionCts = null;
        _speechCts?.Dispose();
        _speechCts = null;
    }

    private async Task ExecuteLogoutAsync()
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage))
            return;

        var userMessage = CurrentMessage.Trim();
        Messages.Add($"You: {userMessage}");
        CurrentMessage = string.Empty;

        try
        {
            var response = await _mcpClient.ProcessPromptAsync(userMessage);
            Messages.Add($"Agent: {response}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            Messages.Add("Agent: Sorry, I couldn't process your message.");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Add this helper method to your ChatViewModel class
    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}