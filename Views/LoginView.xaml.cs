using System.Windows.Controls;
using eodh.ViewModels;

namespace eodh.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is LoginViewModel vm)
                vm.SetPasswordBox(ApiTokenBox);
        };
    }
}
