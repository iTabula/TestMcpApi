namespace KamMobile.Services;

public interface ISpeechRecognitionService
{
    Task<string> RecognizeSpeechAsync(
        Action<string> onPartialResult,
        CancellationToken cancellationToken);
}