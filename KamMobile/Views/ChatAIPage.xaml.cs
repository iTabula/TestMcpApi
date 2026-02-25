using KamMobile.ViewModels;

namespace KamMobile.Views;

public partial class ChatAIPage : ContentPage
{
    public ChatAIPage(ChatAIViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}