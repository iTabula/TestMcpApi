using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Speech;
using KamMobile.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace KamMobile.Services
{
    public class SpeechToTextService : Java.Lang.Object, ISpeechToTextService, IRecognitionListener
    {
        private SpeechRecognizer? _speechRecognizer;
        private Intent? _speechIntent;

        private Action<string>? _onResult;
        private Action<string>? _onError;

        public async Task StartListeningAsync(Action<string> onResult, Action<string> onError)
        {
            _onResult = onResult;
            _onError = onError;

            var permission = await Permissions.RequestAsync<Permissions.Microphone>();
            if (permission != PermissionStatus.Granted)
            {
                _onError?.Invoke("Microphone permission denied");
                return;
            }

            var context = Android.App.Application.Context;

            _speechRecognizer = SpeechRecognizer.CreateSpeechRecognizer(context);
            _speechRecognizer.SetRecognitionListener(this);

            _speechIntent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
            _speechIntent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
            _speechIntent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default);
            _speechIntent.PutExtra(RecognizerIntent.ExtraPartialResults, false);

            _speechRecognizer.StartListening(_speechIntent);
        }

        public Task StopListeningAsync()
        {
            _speechRecognizer?.StopListening();
            _speechRecognizer?.Destroy();
            _speechRecognizer = null;
            return Task.CompletedTask;
        }

        public void OnResults(Bundle results)
        {
            var matches = results.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
            if (matches != null && matches.Count > 0)
                _onResult?.Invoke(matches[0]);
        }

        public void OnError([GeneratedEnum] SpeechRecognizerError error)
        {
            _onError?.Invoke(error.ToString());
        }

        public void OnReadyForSpeech(Bundle? @params) { }
        public void OnBeginningOfSpeech() { }
        public void OnEndOfSpeech() { }
        public void OnBufferReceived(byte[]? buffer) { }
        public void OnEvent(int eventType, Bundle? @params) { }
        public void OnPartialResults(Bundle? partialResults) { }
        public void OnRmsChanged(float rmsdB) { }
    }
}

