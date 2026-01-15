using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using KamMobile.Helpers;

namespace KamMobile.ViewModels;

public class ChatViewModel : INotifyPropertyChanged
{
    private readonly McpSseClient _mcpClient;
    private string _questionText = "Waiting for your question...";
    private string _answerText = "Waiting for question...";
    private string _statusText = "Click to start";
    private bool _isListening = false;
    private bool _isProcessing = false;
    private bool _isSpeaking = false;
    private string _buttonText = "Start Conversation";

    public ChatViewModel(McpSseClient mcpClient)
    {
        _mcpClient = mcpClient;
        StartConversationCommand = new Command(async () => await ExecuteStartConversationAsync());
        LogoutCommand = new Command(async () => await ExecuteLogoutAsync());
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

    private async Task ExecuteStartConversationAsync()
    {
        if (!IsListening && !IsProcessing && !IsSpeaking)
        {
            await StartListeningAsync();
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

    private async Task StartListeningAsync()
    {
        try
        {
            // Speech recognition will be handled by platform-specific implementations
            IsListening = true;
            ButtonText = "Stop Listening";
            StatusText = "Listening... Speak now";
            QuestionText = "Listening...";

            // This will be implemented using platform-specific speech recognition
            var recognizedText = await RecognizeSpeechAsync();

            if (!string.IsNullOrWhiteSpace(recognizedText))
            {
                QuestionText = recognizedText;
                await ProcessQuestionAsync(recognizedText);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            ResetState();
        }
    }

    private Task StopListeningAsync()
    {
        IsListening = false;
        ButtonText = "Start Conversation";
        StatusText = "Click to start";
        return Task.CompletedTask;
    }

    private async Task<string> RecognizeSpeechAsync()
    {
        // This is a placeholder - actual implementation will use platform-specific APIs
        // For now, return empty to demonstrate the flow
        await Task.Delay(100);
        
#if ANDROID || IOS
        // Platform-specific speech recognition will be implemented here
        return string.Empty;
#else
        return string.Empty;
#endif
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
            // Use MAUI's built-in TextToSpeech
            await TextToSpeech.Default.SpeakAsync(text);
            
            StatusText = "Done! Click to ask another question.";
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
        // Cancel any ongoing speech
#if ANDROID || IOS
        TextToSpeech.Default.SpeakAsync(string.Empty);
#endif
        StatusText = "Stopped. Click to ask another question.";
        ResetState();
    }

    private void ResetState()
    {
        IsListening = false;
        IsProcessing = false;
        IsSpeaking = false;
        ButtonText = "Start Conversation";
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