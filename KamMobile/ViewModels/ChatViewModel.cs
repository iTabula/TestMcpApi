using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using KamHttp.Helpers;
using CommunityToolkit.Maui.Media;
using System.Globalization; // <-- Add this using directive

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
    private CancellationTokenSource? _recognitionCts;
    private CancellationTokenSource? _speechCts;
    private TaskCompletionSource<string>? _recognitionTcs;

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
            // Request microphone permissions
            var status = await Permissions.RequestAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                StatusText = "Microphone permission denied";
                return;
            }

            IsListening = true;
            ButtonText = "Stop Listening";
            StatusText = "Listening... Speak now";
            QuestionText = "Listening...";

            _recognitionCts = new CancellationTokenSource();
            
            var recognizedText = await RecognizeSpeechAsync(_recognitionCts.Token);

            if (!string.IsNullOrWhiteSpace(recognizedText))
            {
                QuestionText = recognizedText;
                await ProcessQuestionAsync(recognizedText);
            }
            else
            {
                StatusText = "No speech detected. Click to try again.";
                ResetState();
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Listening cancelled";
            ResetState();
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            ResetState();
        }
    }

    private async Task StopListeningAsync()
    {
        _recognitionCts?.Cancel();
        StatusText = "Stopping...";
        
        // Give a moment for the cancellation to process
        await Task.Delay(100);
        
        IsListening = false;
        ButtonText = "Start Conversation";
        StatusText = "Click to start";
    }

    private async Task<string> RecognizeSpeechAsync(CancellationToken cancellationToken)
    {
        var recognitionTcs = new TaskCompletionSource<string>();
        var finalText = string.Empty;

        void OnResultUpdated(object? sender, SpeechToTextRecognitionResultUpdatedEventArgs args)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!string.IsNullOrWhiteSpace(args.RecognitionResult.ToString()))
                {
                    QuestionText = args.RecognitionResult.ToString();
                }
            });
        }

        void OnResultCompleted(object? sender, SpeechToTextRecognitionResultCompletedEventArgs args)
        {
            if (args.RecognitionResult.IsSuccessful)
            {
                recognitionTcs.TrySetResult(args.RecognitionResult.Text ?? string.Empty);
            }
            else
            {
                recognitionTcs.TrySetResult(string.Empty);
            }
        }

        try
        {
            // Subscribe to events
            SpeechToText.Default.RecognitionResultUpdated += OnResultUpdated;
            SpeechToText.Default.RecognitionResultCompleted += OnResultCompleted;

            // Configure options
            var options = new SpeechToTextOptions
            {
                Culture = CultureInfo.GetCultureInfo("en-US"),
                ShouldReportPartialResults = true
            };

            // Start listening
            await SpeechToText.Default.StartListenAsync(options, cancellationToken);

            // Wait for result or cancellation
            using (cancellationToken.Register(() => recognitionTcs.TrySetCanceled()))
            {
                finalText = await recognitionTcs.Task;
            }

            return finalText;
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = $"Recognition error: {ex.Message}";
            return string.Empty;
        }
        finally
        {
            // Always unsubscribe and stop listening
            SpeechToText.Default.RecognitionResultUpdated -= OnResultUpdated;
            SpeechToText.Default.RecognitionResultCompleted -= OnResultCompleted;
            
            try
            {
                await SpeechToText.Default.StopListenAsync(CancellationToken.None);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
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

            // Get available locales from device
            var locales = await TextToSpeech.Default.GetLocalesAsync();

            // Pick a locale (e.g., first supported locale or you can pick specific)
            var selectedLocale = locales.FirstOrDefault();

            var speechOptions = new SpeechOptions
            {
                Pitch = 1.0f,
                Volume = 1.0f,
                Locale = selectedLocale // correct type
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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}