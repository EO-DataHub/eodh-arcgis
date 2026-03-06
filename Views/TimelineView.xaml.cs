using System.Windows;
using System.Windows.Controls;
using eodh.ViewModels;

namespace eodh.Views;

public partial class TimelineView : UserControl
{
    private const double ItemWidth = 68; // 64px width + 4px margin

    public TimelineView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TimelineViewModel oldVm)
            oldVm.ScrollRequested -= ScrollToIndex;

        if (e.NewValue is TimelineViewModel newVm)
            newVm.ScrollRequested += ScrollToIndex;
    }

    private void ScrollToIndex(int index)
    {
        var offset = index * ItemWidth;
        var viewportCenter = TimelineScroller.ViewportWidth / 2;
        TimelineScroller.ScrollToHorizontalOffset(offset - viewportCenter + ItemWidth / 2);
    }
}
