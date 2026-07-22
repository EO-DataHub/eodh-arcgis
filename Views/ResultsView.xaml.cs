using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using eodh.Models;
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
            _viewModel.PageChanged -= ResultsView_PageChanged;

        _viewModel = e.NewValue as ResultsViewModel;
        if (_viewModel != null)
            _viewModel.PageChanged += ResultsView_PageChanged;
    }

    private void ResultsView_PageChanged(List<StacItem> items)
    {
        if (items.Count == 0) return;

        Dispatcher.BeginInvoke(
            new Action(() => ResultsList.ScrollIntoView(ResultsList.Items[0])),
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
