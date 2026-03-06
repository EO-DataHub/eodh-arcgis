using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace eodh
{
    internal class Module1 : Module
    {
        private static Module1? _this;

        public static Module1 Current => _this ??= (Module1)FrameworkApplication.FindModule("eodh_Module");

        protected override bool CanUnload()
        {
            return true;
        }
    }

    /// <summary>
    /// Button to show/activate the main EODH dockpane.
    /// </summary>
    internal class ShowMainDockpaneButton : Button
    {
        protected override void OnClick()
        {
            ViewModels.MainDockpaneViewModel.Show();
        }
    }
}
