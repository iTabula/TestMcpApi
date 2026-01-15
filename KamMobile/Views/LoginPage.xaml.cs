using Microsoft.Maui.Controls;
using KamMobile.ViewModels;

namespace KamMobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}