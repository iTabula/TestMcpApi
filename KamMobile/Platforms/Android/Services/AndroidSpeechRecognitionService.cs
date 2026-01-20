using Android.Content;
using Android.OS;
using Android.Speech;
using KamMobile.Services;

namespace KamMobile.Platforms.Android.Services;

public class AndroidSpeechRecognitionService : ISpeechRecognitionService
{
    private readonly SpeechRecognitionListener _listener;
    private SpeechRecognizer? _speechRecognizer;
    private TaskCompletionSource<string>? _tcs;
    private Action<string>? _onPartialResult;

    public AndroidSpeechRecognitionService()
    {
        _listener = new SpeechRecognitionListener();
    }

    public async Task<string> RecognizeSpeechAsync(
        Action<string> onPartialResult,
        CancellationToken cancellationToken)
    {
        _tcs = new TaskCompletionSource<string>();
        _onPartialResult = onPartialResult;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var context = Platform.CurrentActivity ?? throw new InvalidOperationException("Activity is null");
            _speechRecognizer = SpeechRecognizer.CreateSpeechRecognizer(context);

            _listener.PartialResults += OnPartialResults;
            _listener.Results += OnResults;
            _listener.Error += OnError;

            _speechRecognizer.SetRecognitionListener(_listener);

            var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(RecognizerIntent.ExtraLanguage, "en-US");
            intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
            intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);

            _speechRecognizer.StartListening(intent);
        });

        using (cancellationToken.Register(() =>
        {
            StopListening();
            _tcs?.TrySetCanceled();
        }))
        {
            return await _tcs.Task;
        }
    }

    private void OnPartialResults(object? sender, Bundle? bundle)
    {
        if (bundle == null) return;

        var matches = bundle.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
        if (matches != null && matches.Count > 0)
        {
            var text = matches[0] ?? string.Empty;
            _onPartialResult?.Invoke(text);
        }
    }

    private void OnResults(object? sender, Bundle? bundle)
    {
        if (bundle == null)
        {
            _tcs?.TrySetResult(string.Empty);
            return;
        }

        var matches = bundle.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
        if (matches != null && matches.Count > 0)
        {
            var text = matches[0] ?? string.Empty;
            _tcs?.TrySetResult(text);
        }
        else
        {
            _tcs?.TrySetResult(string.Empty);
        }

        Cleanup();
    }

    private void OnError(object? sender, SpeechRecognizerError error)
    {
        _tcs?.TrySetResult(string.Empty);
        Cleanup();
    }

    private void StopListening()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _speechRecognizer?.StopListening();
            Cleanup();
        });
    }

    private void Cleanup()
    {
        if (_listener != null)
        {
            _listener.PartialResults -= OnPartialResults;
            _listener.Results -= OnResults;
            _listener.Error -= OnError;
        }

        _speechRecognizer?.Destroy();
        _speechRecognizer = null;
    }

    private class SpeechRecognitionListener : Java.Lang.Object, IRecognitionListener
    {
        public event EventHandler<Bundle?>? PartialResults;
        public event EventHandler<Bundle?>? Results;
        public event EventHandler<SpeechRecognizerError>? Error;

        public void OnBeginningOfSpeech() { }
        public void OnBufferReceived(byte[]? buffer) { }
        public void OnEndOfSpeech() { }
        public void OnEvent(int eventType, Bundle? @params) { }
        public void OnReadyForSpeech(Bundle? @params) { }
        public void OnRmsChanged(float rmsdB) { }

        public void OnPartialResults(Bundle? partialResults)
        {
            PartialResults?.Invoke(this, partialResults);
        }

        public void OnResults(Bundle? results)
        {
            Results?.Invoke(this, results);
        }

        public void OnError(SpeechRecognizerError error)
        {
            Error?.Invoke(this, error);
        }
    }
}