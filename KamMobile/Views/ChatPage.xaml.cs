using KamMobile.ViewModels;

namespace KamMobile.Views;

public partial class ChatPage : ContentPage
{
    private Animation? _pulseAnimation;
    private bool _isAnimating = false;

    public ChatPage(ViewModels.ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Subscribe to property changes to start/stop animation
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.ChatViewModel.IsListening))
        {
            var viewModel = (ViewModels.ChatViewModel)BindingContext;
            if (viewModel.IsListening)
            {
                StartPulseAnimation();
            }
            else
            {
                StopPulseAnimation();
            }
        }
    }

    private void StartPulseAnimation()
    {
        if (_isAnimating)
            return;

        _isAnimating = true;

        // Create animation exactly like the web app:
        // 0%, 100%: scale(1), opacity(1)
        // 50%: scale(1.15), opacity(0.8)
        // Duration: 1.5s, infinite

        _pulseAnimation = new Animation();

        // Scale animation: 1 -> 1.15 -> 1
        var scaleUp = new Animation(v => MicrophoneFrame.Scale = v, 1.0, 1.15, Easing.SinInOut);
        var scaleDown = new Animation(v => MicrophoneFrame.Scale = v, 1.15, 1.0, Easing.SinInOut);

        // Opacity animation: 1 -> 0.8 -> 1
        var fadeOut = new Animation(v => MicrophoneFrame.Opacity = v, 1.0, 0.8, Easing.SinInOut);
        var fadeIn = new Animation(v => MicrophoneFrame.Opacity = v, 0.8, 1.0, Easing.SinInOut);

        // Add animations to parent (0 to 0.5 for up/out, 0.5 to 1.0 for down/in)
        _pulseAnimation.Add(0, 0.5, scaleUp);
        _pulseAnimation.Add(0.5, 1.0, scaleDown);
        _pulseAnimation.Add(0, 0.5, fadeOut);
        _pulseAnimation.Add(0.5, 1.0, fadeIn);

        // Start the animation with 1.5 second duration, repeating infinitely
        _pulseAnimation.Commit(
            this,
            "PulseMicrophone",
            length: 1500, // 1.5 seconds
            repeat: () => _isAnimating // Keep repeating while _isAnimating is true
        );
    }

    private void StopPulseAnimation()
    {
        if (!_isAnimating)
            return;

        _isAnimating = false;
        this.AbortAnimation("PulseMicrophone");

        // Reset to default values
        MicrophoneFrame.Scale = 1.0;
        MicrophoneFrame.Opacity = 1.0;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopPulseAnimation();
        
        if (BindingContext is ViewModels.ChatViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}