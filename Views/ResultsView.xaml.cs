using System.Windows.Controls;

namespace eodh.Views;

public partial class ResultsView : UserControl
{
    public ResultsView()
    {
        InitializeComponent();
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem != null)
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }
}
