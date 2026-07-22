using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using eodh.ViewModels;

namespace eodh.Views;

public partial class ResultsView : UserControl
{
    private ResultsViewModel? _viewModel;

    public ResultsView()
    {
        InitializeComponent();
        DataContextChanged += ResultsView_DataContextChanged;
    }

    private void ResultsView_DataContextChanged(
        object sender,
        System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.SelectionRevealRequested -= ResultsView_SelectionRevealRequested;

        _viewModel = e.NewValue as ResultsViewModel;
        if (_viewModel != null)
            _viewModel.SelectionRevealRequested += ResultsView_SelectionRevealRequested;
    }

    private void ResultsView_SelectionRevealRequested(ResultItemViewModel result)
    {
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                ResultsList.UpdateLayout();
                ResultsList.ScrollIntoView(result);
            }),
            DispatcherPriority.Loaded);
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem != null)
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    private void CommercialHelpLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
