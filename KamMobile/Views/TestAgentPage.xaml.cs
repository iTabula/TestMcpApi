using KamMobile.ViewModels;

namespace KamMobile.Views;

public partial class TestAgentPage : ContentPage
{
    private Animation? _micPulseAnimation;
    private Animation? _speakerPulseAnimation;
    private bool _isMicAnimating = false;
    private bool _isSpeakerAnimating = false;

    public TestAgentPage(TestAgentViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Subscribe to property changes for animations
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var viewModel = (TestAgentViewModel)BindingContext;

        if (e.PropertyName == nameof(TestAgentViewModel.IsListening))
        {
            if (viewModel.IsListening)
            {
                StartMicPulseAnimation();
            }
            else
            {
                StopMicPulseAnimation();
            }
        }
        else if (e.PropertyName == nameof(TestAgentViewModel.IsSpeaking))
        {
            if (viewModel.IsSpeaking)
            {
                StartSpeakerPulseAnimation();
            }
            else
            {
                StopSpeakerPulseAnimation();
            }
        }
    }

    private void StartMicPulseAnimation()
    {
        if (_isMicAnimating) return;

        _isMicAnimating = true;
        _micPulseAnimation = new Animation(
            v => MicIcon.Scale = v,
            1.0,
            1.15,
            Easing.SinInOut);

        _micPulseAnimation.Commit(
            this,
            "MicPulse",
            16,
            1500,
            repeat: () => _isMicAnimating);
    }

    private void StopMicPulseAnimation()
    {
        _isMicAnimating = false;
        this.AbortAnimation("MicPulse");
        MicIcon.Scale = 1.0;
    }

    private void StartSpeakerPulseAnimation()
    {
        if (_isSpeakerAnimating) return;

        _isSpeakerAnimating = true;
        _speakerPulseAnimation = new Animation(
            v => SpeakerIcon.Scale = v,
            1.0,
            1.2,
            Easing.SinInOut);

        _speakerPulseAnimation.Commit(
            this,
            "SpeakerPulse",
            16,
            1000,
            repeat: () => _isSpeakerAnimating);
    }

    private void StopSpeakerPulseAnimation()
    {
        _isSpeakerAnimating = false;
        this.AbortAnimation("SpeakerPulse");
        SpeakerIcon.Scale = 1.0;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopMicPulseAnimation();
        StopSpeakerPulseAnimation();
    }
}