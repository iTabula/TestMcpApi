using System;
using System.Collections.Generic;
using System.Text;

namespace KamMobile.Interfaces
{
    public interface ISpeechToTextService
    {
        Task StartListeningAsync(Action<string> onResult, Action<string> onError);
        Task StopListeningAsync();
    }
}
