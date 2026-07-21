using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
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

    private void OpenLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
