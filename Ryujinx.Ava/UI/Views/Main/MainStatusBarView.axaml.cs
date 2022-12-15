using Avalonia.Controls;
using Avalonia.Input;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Ui.Common.Configuration;
using System;

namespace Ryujinx.Ava.UI.Views.Main;

public partial class MainStatusBarView : UserControl
{
    public MainWindow Window;

    public MainStatusBarView()
    {
        InitializeComponent();
    }

    private void VsyncStatus_PointerReleased(object sender, PointerReleasedEventArgs e)
    {
        Window.AppHost.Device.EnableDeviceVsync = !Window.AppHost.Device.EnableDeviceVsync;

        Logger.Info?.Print(LogClass.Application, $"VSync toggled to: {Window.AppHost.Device.EnableDeviceVsync}");
    }

    private void DockedStatus_PointerReleased(object sender, PointerReleasedEventArgs e)
    {
        ConfigurationState.Instance.System.EnableDockedMode.Value = !ConfigurationState.Instance.System.EnableDockedMode.Value;
    }

    private void AspectRatioStatus_PointerReleased(object sender, PointerReleasedEventArgs e)
    {
        AspectRatio aspectRatio = ConfigurationState.Instance.Graphics.AspectRatio.Value;

        ConfigurationState.Instance.Graphics.AspectRatio.Value = (int)aspectRatio + 1 > Enum.GetNames(typeof(AspectRatio)).Length - 1 ? AspectRatio.Fixed4x3 : aspectRatio + 1;
    }
}