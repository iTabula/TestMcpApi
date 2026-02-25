using KamMobile.ViewModels;

namespace KamMobile.Views;

public partial class ChatVapiPage : ContentPage
{
    public ChatVapiPage(ChatVapiViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}