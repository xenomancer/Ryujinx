using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Ncm;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Input;
<<<<<<< HEAD
=======
using Ryujinx.Ava.Modules.Updater;
>>>>>>> 66aac324 (Fix Namespace Case)
using Ryujinx.Ava.UI.Applet;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gpu;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.Input.SDL2;
using Ryujinx.Modules;
using Ryujinx.Ui.App.Common;
using Ryujinx.Ui.Common;
using Ryujinx.Ui.Common.Configuration;
using Ryujinx.Ui.Common.Helper;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using InputManager = Ryujinx.Input.HLE.InputManager;
using Path = System.IO.Path;
using UserId = LibHac.Fs.UserId;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class MainWindow : StyleableWindow
    {
        public const int HotKeyPressDelayMs = 500;
        
        internal static MainWindowViewModel MainWindowViewModel { get; private set; }
        private bool _canUpdate;
        private bool _isClosing;
        private bool _isLoading;

        private Control _mainViewContent;

        private UserChannelPersistence _userChannelPersistence;
        private static bool _deferLoad;
        private static string _launchPath;
        private static bool _startFullscreen;
        private string _currentEmulatedGamePath;
        internal readonly AvaHostUiHandler UiHandler;
        private AutoResetEvent _rendererWaitEvent;

        public VirtualFileSystem VirtualFileSystem { get; private set; }
        public ContentManager ContentManager { get; private set; }
        public AccountManager AccountManager { get; private set; }

        public LibHacHorizonManager LibHacHorizonManager { get; private set; }

        public InputManager InputManager { get; private set; }

        internal RendererHost RendererControl { get; private set; }
        internal MainWindowViewModel ViewModel { get; private set; }
        public SettingsWindow SettingsWindow { get; set; }
        
        public bool CanUpdate
        {
            get => _canUpdate;
            set
            {
                _canUpdate = value;

                Dispatcher.UIThread.InvokeAsync(() => MenuBarView.UpdateMenuItem.IsEnabled = _canUpdate);
            }
        }

        public static bool ShowKeyErrorOnLoad { get; set; }
        public ApplicationLibrary ApplicationLibrary { get; set; }

        public MainWindow()
        {
            ViewModel = new MainWindowViewModel();

            MainWindowViewModel = ViewModel;

            DataContext = ViewModel;

            InitializeComponent();
            Load();

            UiHandler = new AvaHostUiHandler(this);

            Title = $"Ryujinx {Program.Version}";

            // NOTE: Height of MenuBar and StatusBar is not usable here, since it would still be 0 at this point.
            double barHeight = MenuBar.MinHeight + StatusBarView.StatusBar.MinHeight;
            Height = ((Height - barHeight) / Program.WindowScaleFactor) + barHeight;
            Width /= Program.WindowScaleFactor;

            if (Program.PreviewerDetached)
            {
                Initialize();

                ViewModel.Initialize();

                InputManager = new InputManager(new AvaloniaKeyboardDriver(this), new SDL2GamepadDriver());

                LoadGameList();
            }

            _rendererWaitEvent = new AutoResetEvent(false);
            ApplicationLibrary.ApplicationCountUpdated += ApplicationLibrary_ApplicationCountUpdated;
            ApplicationLibrary.ApplicationAdded += ApplicationLibrary_ApplicationAdded;
        }

        public void LoadGameList()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;

            LoadApplications();

            _isLoading = false;
        }

        private void Update_StatusBar(object sender, StatusUpdatedEventArgs args)
        {
            if (ViewModel.ShowMenuAndStatusBar && !ViewModel.ShowLoadProgress)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (args.VSyncEnabled)
                    {
                        ViewModel.VsyncColor = new SolidColorBrush(Color.Parse("#ff2eeac9"));
                    }
                    else
                    {
                        ViewModel.VsyncColor = new SolidColorBrush(Color.Parse("#ffff4554"));
                    }

                    ViewModel.DockedStatusText = args.DockedMode;
                    ViewModel.AspectRatioStatusText = args.AspectRatio;
                    ViewModel.GameStatusText = args.GameStatus;
                    ViewModel.VolumeStatusText = args.VolumeStatus;
                    ViewModel.FifoStatusText = args.FifoStatus;
                    ViewModel.GpuNameText = args.GpuName;
                    ViewModel.BackendText = args.GpuBackend;

                    ViewModel.ShowStatusSeparator = true;
                });
            }
        }

        protected override void HandleScalingChanged(double scale)
        {
            Program.DesktopScaleFactor = scale;
            base.HandleScalingChanged(scale);
        }
        
        public void AddApplication(ApplicationData applicationData)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ViewModel.Applications.Add(applicationData);
            });
        }
        
        public void ToggleFavorite()
        {
            ApplicationData selection = ViewModel.SelectedApplication;
            if (selection != null)
            {
                selection.Favorite = !selection.Favorite;

                ApplicationLibrary.LoadAndSaveMetaData(selection.TitleId, appMetadata =>
                {
                    appMetadata.Favorite = selection.Favorite;
                });

                ViewModel.RefreshView();
            }
        }
        
        private void ApplicationLibrary_ApplicationAdded(object sender, ApplicationAddedEventArgs e)
        {
            AddApplication(e.AppData);
        }

        private void ApplicationLibrary_ApplicationCountUpdated(object sender, ApplicationCountUpdatedEventArgs e)
        {
            LocaleManager.Instance.UpdateDynamicValue("StatusBarGamesLoaded", e.NumAppsLoaded, e.NumAppsFound);

            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.StatusBarProgressValue   = e.NumAppsLoaded;
                ViewModel.StatusBarProgressMaximum = e.NumAppsFound;

                if (e.NumAppsFound == 0)
                {
                    StatusBarView.LoadProgressBar.IsVisible = false;
                }

                if (e.NumAppsLoaded == e.NumAppsFound)
                {
                    StatusBarView.LoadProgressBar.IsVisible = false;
                }
            });
        }

        public void Application_Opened(object sender, ApplicationOpenedEventArgs args)
        {
            if (args.Application != null)
            {
                ViewModel.SelectedIcon = args.Application.Icon;

                string path = new FileInfo(args.Application.Path).FullName;

                LoadApplication(path);
            }

            args.Handled = true;
        }
        
        public async void OpenAmiiboWindow()
        {
            if (!ViewModel.IsAmiiboRequested)
            {
                return;
            }

            if (ViewModel.AppHost.Device.System.SearchingForAmiibo(out int deviceId))
            {
                string      titleId = ViewModel.AppHost.Device.Application.TitleIdText.ToUpper();
                AmiiboWindow window = new(ViewModel.ShowAll, ViewModel.LastScannedAmiiboId, titleId);

                await window.ShowDialog(this);

                if (window.IsScanned)
                {
                    ViewModel.ShowAll             = window.ViewModel.ShowAllAmiibo;
                    ViewModel.LastScannedAmiiboId = window.ScannedAmiibo.GetId();

                    ViewModel.AppHost.Device.System.ScanAmiibo(deviceId, ViewModel.LastScannedAmiiboId, window.ViewModel.UseRandomUuid);
                }
            }
        }

        public async Task PerformanceCheck()
        {
            if (ConfigurationState.Instance.Logger.EnableTrace.Value)
            {
                string mainMessage = LocaleManager.Instance["DialogPerformanceCheckLoggingEnabledMessage"];
                string secondaryMessage = LocaleManager.Instance["DialogPerformanceCheckLoggingEnabledConfirmMessage"];

                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(mainMessage, secondaryMessage,
                    LocaleManager.Instance["InputDialogYes"], LocaleManager.Instance["InputDialogNo"],
                    LocaleManager.Instance["RyujinxConfirm"]);

                if (result != UserResult.Yes)
                {
                    ConfigurationState.Instance.Logger.EnableTrace.Value = false;

                    SaveConfig();
                }
            }

            if (!string.IsNullOrWhiteSpace(ConfigurationState.Instance.Graphics.ShadersDumpPath.Value))
            {
                string mainMessage = LocaleManager.Instance["DialogPerformanceCheckShaderDumpEnabledMessage"];
                string secondaryMessage =
                    LocaleManager.Instance["DialogPerformanceCheckShaderDumpEnabledConfirmMessage"];

                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(mainMessage, secondaryMessage,
                    LocaleManager.Instance["InputDialogYes"], LocaleManager.Instance["InputDialogNo"],
                    LocaleManager.Instance["RyujinxConfirm"]);

                if (result != UserResult.Yes)
                {
                    ConfigurationState.Instance.Graphics.ShadersDumpPath.Value = "";

                    SaveConfig();
                }
            }
        }

        internal static void DeferLoadApplication(string launchPathArg, bool startFullscreenArg)
        {
            _deferLoad = true;
            _launchPath = launchPathArg;
            _startFullscreen = startFullscreenArg;
        }

        public async void LoadApplication(string path, bool startFullscreen = false, string titleName = "")
        {
            if (ViewModel.AppHost != null)
            {
                await ContentDialogHelper.CreateInfoDialog(
                    LocaleManager.Instance["DialogLoadAppGameAlreadyLoadedMessage"],
                    LocaleManager.Instance["DialogLoadAppGameAlreadyLoadedSubMessage"],
                    LocaleManager.Instance["InputDialogOk"],
                    "",
                    LocaleManager.Instance["RyujinxInfo"]);

                return;
            }

#if RELEASE
            await PerformanceCheck();
#endif

            Logger.RestartTime();

            if (ViewModel.SelectedIcon == null)
            {
                ViewModel.SelectedIcon = ApplicationLibrary.GetApplicationIcon(path);
            }

            PrepareLoadScreen();

            _mainViewContent = MainContent.Content as Control;

            RendererControl = new RendererHost(ConfigurationState.Instance.Logger.GraphicsDebugLevel);
            if (ConfigurationState.Instance.Graphics.GraphicsBackend.Value == GraphicsBackend.OpenGl)
            {
                RendererControl.CreateOpenGL();
            }
            else
            {
                RendererControl.CreateVulkan();
            }

            ViewModel.AppHost = new AppHost(RendererControl, InputManager, path, VirtualFileSystem, ContentManager, AccountManager, _userChannelPersistence, this);

            Dispatcher.UIThread.Post(async () =>
            {
                if (!await ViewModel.AppHost.LoadGuestApplication())
                {
                    ViewModel.AppHost.DisposeContext();
                    ViewModel.AppHost = null;

                    return;
                }

                CanUpdate = false;
                ViewModel.LoadHeading = string.IsNullOrWhiteSpace(titleName) ? string.Format(LocaleManager.Instance["LoadingHeading"], ViewModel.AppHost.Device.Application.TitleName) : titleName;
                ViewModel.TitleName   = string.IsNullOrWhiteSpace(titleName) ? ViewModel.AppHost.Device.Application.TitleName : titleName;

                SwitchToGameControl(startFullscreen);

                _currentEmulatedGamePath = path;

                Thread gameThread = new(InitializeGame)
                {
                    Name = "GUI.WindowThread"
                };
                gameThread.Start();
            });
        }

        private void InitializeGame()
        {
            RendererControl.RendererInitialized += GlRenderer_Created;

            ViewModel.AppHost.StatusUpdatedEvent += Update_StatusBar;
            ViewModel.AppHost.AppExit += AppHost_AppExit;

            _rendererWaitEvent.WaitOne();

            ViewModel.AppHost?.Start();

            ViewModel.AppHost.DisposeContext();
        }


        private void HandleRelaunch()
        {
            if (_userChannelPersistence.PreviousIndex != -1 && _userChannelPersistence.ShouldRestart)
            {
                _userChannelPersistence.ShouldRestart = false;

                Dispatcher.UIThread.Post(() =>
                {
                    LoadApplication(_currentEmulatedGamePath);
                });
            }
            else
            {
                // otherwise, clear state.
                _userChannelPersistence = new UserChannelPersistence();
                _currentEmulatedGamePath = null;
            }
        }

        public void SwitchToGameControl(bool startFullscreen = false)
        {
            ViewModel.ShowLoadProgress = false;
            ViewModel.ShowContent = true;
            ViewModel.IsLoadingIndeterminate = false;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainContent.Content = RendererControl;

                if (startFullscreen && WindowState != WindowState.FullScreen)
                {
                    ToggleFullscreen();
                }

                RendererControl.Focus();
            });
        }

        public void ShowLoading(bool startFullscreen = false)
        {
            ViewModel.ShowContent = false;
            ViewModel.ShowLoadProgress = true;
            ViewModel.IsLoadingIndeterminate = true;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (startFullscreen && WindowState != WindowState.FullScreen)
                {
                    ToggleFullscreen();
                }
            });
        }

        private void GlRenderer_Created(object sender, EventArgs e)
        {
            ShowLoading();

            _rendererWaitEvent.Set();
        }

        private void AppHost_AppExit(object sender, EventArgs e)
        {
            if (_isClosing)
            {
                return;
            }

            ViewModel.IsGameRunning = false;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ViewModel.ShowMenuAndStatusBar = true;
                ViewModel.ShowContent = true;
                ViewModel.ShowLoadProgress = false;
                ViewModel.IsLoadingIndeterminate = false;
                CanUpdate = true;
                Cursor = Cursor.Default;

                if (MainContent.Content != _mainViewContent)
                {
                    MainContent.Content = _mainViewContent;
                }

                ViewModel.AppHost = null;

                HandleRelaunch();
            });

            RendererControl.RendererInitialized -= GlRenderer_Created;
            RendererControl = null;

            ViewModel.SelectedIcon = null;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Title = $"Ryujinx {Program.Version}";
            });
        }
        
        public void ToggleFullscreen()
        {
            if (Environment.TickCount64 - ViewModel.LastFullscreenToggle < HotKeyPressDelayMs)
            {
                return;
            }

            ViewModel.LastFullscreenToggle = Environment.TickCount64;

            if (WindowState == WindowState.FullScreen)
            {
                WindowState = WindowState.Normal;

                if (ViewModel.IsGameRunning)
                {
                    ViewModel.ShowMenuAndStatusBar = true;
                }
            }
            else
            {
                WindowState = WindowState.FullScreen;

                if (ViewModel.IsGameRunning)
                {
                    ViewModel.ShowMenuAndStatusBar = false;
                }
            }

            ViewModel.IsFullScreen = WindowState == WindowState.FullScreen;
        }

        protected override void HandleWindowStateChanged(WindowState state)
        {
            WindowState = state;

            if (state != WindowState.Minimized)
            {
                Renderer.Start();
            }
        }

        private void Initialize()
        {
            _userChannelPersistence = new UserChannelPersistence();
            VirtualFileSystem = VirtualFileSystem.CreateInstance();
            LibHacHorizonManager = new LibHacHorizonManager();
            ContentManager = new ContentManager(VirtualFileSystem);

            LibHacHorizonManager.InitializeFsServer(VirtualFileSystem);
            LibHacHorizonManager.InitializeArpServer();
            LibHacHorizonManager.InitializeBcatServer();
            LibHacHorizonManager.InitializeSystemClients();

            ApplicationLibrary = new ApplicationLibrary(VirtualFileSystem);

            // Save data created before we supported extra data in directory save data will not work properly if
            // given empty extra data. Luckily some of that extra data can be created using the data from the
            // save data indexer, which should be enough to check access permissions for user saves.
            // Every single save data's extra data will be checked and fixed if needed each time the emulator is opened.
            // Consider removing this at some point in the future when we don't need to worry about old saves.
            VirtualFileSystem.FixExtraData(LibHacHorizonManager.RyujinxClient);

            AccountManager = new AccountManager(LibHacHorizonManager.RyujinxClient, CommandLineState.Profile);

            VirtualFileSystem.ReloadKeySet();

            ApplicationHelper.Initialize(VirtualFileSystem, AccountManager, LibHacHorizonManager.RyujinxClient, this);

            RefreshFirmwareStatus();
        }

        protected void CheckLaunchState()
        {
            if (ShowKeyErrorOnLoad)
            {
                ShowKeyErrorOnLoad = false;

                Dispatcher.UIThread.Post(async () => await
                    UserErrorDialog.ShowUserErrorDialog(UserError.NoKeys, this));
            }

            if (_deferLoad)
            {
                _deferLoad = false;

                LoadApplication(_launchPath, _startFullscreen);
            }

            if (ConfigurationState.Instance.CheckUpdatesOnStart.Value && Updater.CanUpdate(false, this))
            {
                Updater.BeginParse(this, false).ContinueWith(task =>
                {
                    Logger.Error?.Print(LogClass.Application, $"Updater Error: {task.Exception}");
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        public void RefreshFirmwareStatus()
        {
            SystemVersion version = null;
            try
            {
                version = ContentManager.GetCurrentFirmwareVersion();
            }
            catch (Exception) { }

            bool hasApplet = false;

            if (version != null)
            {
                LocaleManager.Instance.UpdateDynamicValue("StatusBarSystemVersion",
                    version.VersionString);

                hasApplet = version.Major > 3;
            }
            else
            {
                LocaleManager.Instance.UpdateDynamicValue("StatusBarSystemVersion", "0.0");
            }

            ViewModel.IsAppletMenuActive = hasApplet;
        }

        private void Load()
        {
            StatusBarView.VolumeStatus.Click += VolumeStatus_CheckedChanged;

            GameGrid.ApplicationOpened += Application_Opened;

            GameGrid.DataContext = ViewModel;

            GameList.ApplicationOpened += Application_Opened;

            GameList.DataContext = ViewModel;

            LoadHotKeys();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            CheckLaunchState();
        }

        public static void UpdateGraphicsConfig()
        {
            GraphicsConfig.ResScale                   = ConfigurationState.Instance.Graphics.ResScale == -1 ? ConfigurationState.Instance.Graphics.ResScaleCustom : ConfigurationState.Instance.Graphics.ResScale;
            GraphicsConfig.MaxAnisotropy              = ConfigurationState.Instance.Graphics.MaxAnisotropy;
            GraphicsConfig.ShadersDumpPath            = ConfigurationState.Instance.Graphics.ShadersDumpPath;
            GraphicsConfig.EnableShaderCache          = ConfigurationState.Instance.Graphics.EnableShaderCache;
            GraphicsConfig.EnableTextureRecompression = ConfigurationState.Instance.Graphics.EnableTextureRecompression;
            GraphicsConfig.EnableMacroHLE             = ConfigurationState.Instance.Graphics.EnableMacroHLE;
        }

        public void LoadHotKeys()
        {
            HotKeyManager.SetHotKey(FullscreenHotKey,  new KeyGesture(Key.Enter, KeyModifiers.Alt));
            HotKeyManager.SetHotKey(FullscreenHotKey2, new KeyGesture(Key.F11));
            HotKeyManager.SetHotKey(DockToggleHotKey,  new KeyGesture(Key.F9));
            HotKeyManager.SetHotKey(ExitHotKey,        new KeyGesture(Key.Escape));
        }

        public static void SaveConfig()
        {
            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
        }

        public void UpdateGameMetadata(string titleId)
        {
            ApplicationLibrary.LoadAndSaveMetaData(titleId, appMetadata =>
            {
                if (DateTime.TryParse(appMetadata.LastPlayed, out DateTime lastPlayedDateTime))
                {
                    double sessionTimePlayed = DateTime.UtcNow.Subtract(lastPlayedDateTime).TotalSeconds;

                    appMetadata.TimePlayed += Math.Round(sessionTimePlayed, MidpointRounding.AwayFromZero);
                }
            });
        }

        private void PrepareLoadScreen()
        {
            using MemoryStream stream = new MemoryStream(ViewModel.SelectedIcon);
            using var gameIconBmp = SixLabors.ImageSharp.Image.Load<Bgra32>(stream);

            var dominantColor = IconColorPicker.GetFilteredColor(gameIconBmp).ToPixel<Bgra32>();

            const int ColorDivisor = 4;

            Color progressFgColor = Color.FromRgb(dominantColor.R, dominantColor.G, dominantColor.B);
            Color progressBgColor = Color.FromRgb(
                (byte)(dominantColor.R / ColorDivisor),
                (byte)(dominantColor.G / ColorDivisor),
                (byte)(dominantColor.B / ColorDivisor));

            ViewModel.ProgressBarForegroundColor = new SolidColorBrush(progressFgColor);
            ViewModel.ProgressBarBackgroundColor = new SolidColorBrush(progressBgColor);
        }

        private void VolumeStatus_CheckedChanged(object sender, RoutedEventArgs e)
        {
            var volumeSplitButton = sender as ToggleSplitButton;
            if (ViewModel.IsGameRunning)
            {
                if (!volumeSplitButton.IsChecked)
                {
                    ViewModel.AppHost.Device.SetVolume(ConfigurationState.Instance.System.AudioVolume);
                }
                else
                {
                    ViewModel.AppHost.Device.SetVolume(0);
                }

                ViewModel.Volume = ViewModel.AppHost.Device.GetVolume();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isClosing && ViewModel.AppHost != null && ConfigurationState.Instance.ShowConfirmExit)
            {
                e.Cancel = true;

                ConfirmExit();

                return;
            }

            _isClosing = true;

            if (ViewModel.AppHost != null)
            {
                ViewModel.AppHost.AppExit -= AppHost_AppExit;
                ViewModel.AppHost.AppExit += (sender, e) =>
                {
                    ViewModel.AppHost = null;

                    Dispatcher.UIThread.Post(() =>
                    {
                        MainContent = null;

                        Close();
                    });
                };
                ViewModel.AppHost?.Stop();

                e.Cancel = true;

                return;
            }

            ApplicationLibrary.CancelLoading();
            InputManager.Dispose();
            Program.Exit();

            base.OnClosing(e);
        }

        private void ConfirmExit()
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
           {
               _isClosing = await ContentDialogHelper.CreateExitDialog();

               if (_isClosing)
               {
                   Close();
               }
           });
        }
        
        public async void LoadApplications()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ViewModel.Applications.Clear();

                StatusBarView.LoadProgressBar.IsVisible = true;
                ViewModel.StatusBarProgressMaximum         = 0;
                ViewModel.StatusBarProgressValue           = 0;

                LocaleManager.Instance.UpdateDynamicValue("StatusBarGamesLoaded", 0, 0);
            });

            ReloadGameList();
        }

        private void ReloadGameList()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;

            ApplicationLibrary.LoadApplications(ConfigurationState.Instance.Ui.GameDirs.Value, ConfigurationState.Instance.System.Language);

            _isLoading = false;
        }
        
        public void OpenMiiApplet()
        {
            string contentPath = ContentManager.GetInstalledContentPath(0x0100000000001009, StorageId.BuiltInSystem, NcaContentType.Program);

            if (!string.IsNullOrWhiteSpace(contentPath))
            {
                LoadApplication(contentPath, false, "Mii Applet");
            }
        }
        
        public async void ExitCurrentState()
        {
            if (WindowState == WindowState.FullScreen)
            {
                ToggleFullscreen();
            }
            else if (ViewModel.IsGameRunning)
            {
                await Task.Delay(100);

                ViewModel.AppHost?.ShowExitPrompt();
            }
        }
        
        public async void OpenSettings()
        {
            SettingsWindow = new(VirtualFileSystem, ContentManager);

            await SettingsWindow.ShowDialog(this);

            ViewModel.LoadConfigurableHotKeys();
        }

        public async void ManageProfiles()
        {
            await NavigationDialogHost.Show(AccountManager, ContentManager, VirtualFileSystem, LibHacHorizonManager.RyujinxClient);
        }
        
        public async void OpenAboutWindow()
        {
            await new AboutWindow().ShowDialog(this);
        }
        
        public async void OpenFile()
        {
            var result = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = LocaleManager.Instance["OpenFileDialogTitle"],
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(LocaleManager.Instance["AllSupportedFormats"])
                    {
                        Patterns = new[] { "*.nsp", "*.pfs0", "*.xci", "*.nca", "*.nro", "*.nso"},
                        AppleUniformTypeIdentifiers = new[]
                        {
                            "com.ryujinx.Ryujinx-nsp",
                            "com.ryujinx.Ryujinx-pfs0",
                            "com.ryujinx.Ryujinx-xci",
                            "com.ryujinx.Ryujinx-nca",
                            "com.ryujinx.Ryujinx-nro",
                            "com.ryujinx.Ryujinx-nso"
                        },
                        MimeTypes = new[]
                        {
                            "application/x-nx-nsp",
                            "application/x-nx-pfs0",
                            "application/x-nx-xci",
                            "application/x-nx-nca",
                            "application/x-nx-nro",
                            "application/x-nx-nso"
                        }
                    }
                }
            });

            if (result.Count > 0)
            {
                if (result[0].TryGetUri(out Uri uri))
                {
                    LoadApplication(uri.LocalPath);
                }
            }
        }

        public async void OpenFolder()
        {
            var result = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = LocaleManager.Instance["OpenFolderDialogTitle"],
                AllowMultiple = false
            });

            if (result.Count > 0)
            {
                if (result[0].TryGetUri(out Uri uri))
                {
                    LoadApplication(uri.LocalPath);
                }
            }
        }
        
        public void OpenUserSaveDirectory()
        {
            ApplicationData selection = ViewModel.SelectedApplication;
            if (selection != null)
            {
                Task.Run(() =>
                {
                    if (!ulong.TryParse(selection.TitleId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong titleIdNumber))
                    {
                        Dispatcher.UIThread.Post(async () =>
                        {
                            await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance["DialogRyujinxErrorMessage"], LocaleManager.Instance["DialogInvalidTitleIdErrorMessage"]);
                        });

                        return;
                    }

                    UserId         userId         = new((ulong)AccountManager.LastOpenedUser.UserId.High, (ulong)AccountManager.LastOpenedUser.UserId.Low);
                    SaveDataFilter saveDataFilter = SaveDataFilter.Make(titleIdNumber, saveType: default, userId, saveDataId: default, index: default);
                    ViewModel.OpenSaveDirectory(in saveDataFilter, selection, titleIdNumber);
                });
            }
        }
        
        public async void CheckForUpdates()
        {
            if (Updater.CanUpdate(true, this))
            {
                await Updater.BeginParse(this, true);
            }
        }
        
        public void OpenModsDirectory()
        {
            ApplicationData selection = ViewModel.SelectedApplication;
            if (selection != null)
            {
                string modsBasePath  = VirtualFileSystem.ModLoader.GetModsBasePath();
                string titleModsPath = VirtualFileSystem.ModLoader.GetTitleDir(modsBasePath, selection.TitleId);

                OpenHelper.OpenFolder(titleModsPath);
            }
        }

        public void OpenSdModsDirectory()
        {
            ApplicationData selection = ViewModel.SelectedApplication;

            if (selection != null)
            {
                string sdModsBasePath = VirtualFileSystem.ModLoader.GetSdModsBasePath();
                string titleModsPath  = VirtualFileSystem.ModLoader.GetTitleDir(sdModsBasePath, selection.TitleId);

                OpenHelper.OpenFolder(titleModsPath);
            }
        }

        public async void OpenTitleUpdateManager()
        {
            ApplicationData selection = ViewModel.SelectedApplication;
            if (selection != null)
            {
                await new TitleUpdateWindow(VirtualFileSystem, ulong.Parse(selection.TitleId, NumberStyles.HexNumber), selection.TitleName).ShowDialog(this);
            }
        }

        public async void OpenDownloadableContentManager()
        {
            ApplicationData selection = ViewModel.SelectedApplication;
            if (selection != null)
            {
                await new DownloadableContentManagerWindow(VirtualFileSystem, ulong.Parse(selection.TitleId, NumberStyles.HexNumber), selection.TitleName).ShowDialog(this);
            }
        }

        public async void OpenCheatManager()
        {
            ApplicationData selection = ViewModel.SelectedApplication;
            if (selection != null)
            {
                await new CheatWindow(VirtualFileSystem, selection.TitleId, selection.TitleName).ShowDialog(this);
            }
        }

        public async void OpenCheatManagerForCurrentApp()
        {
            if (!ViewModel.IsGameRunning)
            {
                return;
            }

            ApplicationLoader application = ViewModel.AppHost.Device.Application;
            if (application != null)
            {
                await new CheatWindow(VirtualFileSystem, application.TitleIdText, application.TitleName).ShowDialog(this);

                ViewModel.AppHost.Device.EnableCheats();
            }
        }

        private async Task HandleFirmwareInstallation(string filename)
        {
            try
            {
                SystemVersion firmwareVersion = ContentManager.VerifyFirmwarePackage(filename);

                if (firmwareVersion == null)
                {
                    await ContentDialogHelper.CreateErrorDialog(string.Format(LocaleManager.Instance["DialogFirmwareInstallerFirmwareNotFoundErrorMessage"], filename));

                    return;
                }

                string dialogTitle = string.Format(LocaleManager.Instance["DialogFirmwareInstallerFirmwareInstallTitle"], firmwareVersion.VersionString);

                SystemVersion currentVersion =ContentManager.GetCurrentFirmwareVersion();

                string dialogMessage = string.Format(LocaleManager.Instance["DialogFirmwareInstallerFirmwareInstallMessage"], firmwareVersion.VersionString);

                if (currentVersion != null)
                {
                    dialogMessage += string.Format(LocaleManager.Instance["DialogFirmwareInstallerFirmwareInstallSubMessage"], currentVersion.VersionString);
                }

                dialogMessage += LocaleManager.Instance["DialogFirmwareInstallerFirmwareInstallConfirmMessage"];

                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                    dialogTitle,
                    dialogMessage,
                    LocaleManager.Instance["InputDialogYes"],
                    LocaleManager.Instance["InputDialogNo"],
                    LocaleManager.Instance["RyujinxConfirm"]);

                UpdateWaitWindow waitingDialog = ContentDialogHelper.CreateWaitingDialog(dialogTitle, LocaleManager.Instance["DialogFirmwareInstallerFirmwareInstallWaitMessage"]);

                if (result == UserResult.Yes)
                {
                    Logger.Info?.Print(LogClass.Application, $"Installing firmware {firmwareVersion.VersionString}");

                    Thread thread = new(() =>
                    {
                        Dispatcher.UIThread.InvokeAsync(delegate
                        {
                            waitingDialog.Show();
                        });

                        try
                        {
                            ContentManager.InstallFirmware(filename);

                            Dispatcher.UIThread.InvokeAsync(async delegate
                            {
                                waitingDialog.Close();

                                string message = string.Format(LocaleManager.Instance["DialogFirmwareInstallerFirmwareInstallSuccessMessage"], firmwareVersion.VersionString);

                                await ContentDialogHelper.CreateInfoDialog(dialogTitle, message, LocaleManager.Instance["InputDialogOk"], "", LocaleManager.Instance["RyujinxInfo"]);

                                Logger.Info?.Print(LogClass.Application, message);

                                // Purge Applet Cache.

                                DirectoryInfo miiEditorCacheFolder = new DirectoryInfo(Path.Combine(AppDataManager.GamesDirPath, "0100000000001009", "cache"));

                                if (miiEditorCacheFolder.Exists)
                                {
                                    miiEditorCacheFolder.Delete(true);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                waitingDialog.Close();

                                await ContentDialogHelper.CreateErrorDialog(ex.Message);
                            });
                        }
                        finally
                        {
                            RefreshFirmwareStatus();
                        }
                    });

                    thread.Name = "GUI.FirmwareInstallerThread";
                    thread.Start();
                }
            }
            catch (LibHac.Common.Keys.MissingKeyException ex)
            {
                Logger.Error?.Print(LogClass.Application, ex.ToString());

                Dispatcher.UIThread.Post(async () => await UserErrorDialog.ShowUserErrorDialog(UserError.NoKeys, this));
            }
            catch (Exception ex)
            {
                await ContentDialogHelper.CreateErrorDialog(ex.Message);
            }
        }

        public async void InstallFirmwareFromFile()
        {
            var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(LocaleManager.Instance["FileDialogAllTypes"])
                    {
                        Patterns = new[] { "*.xci", "*.zip" },
                        AppleUniformTypeIdentifiers = new[] { "com.ryujinx.Ryujinx-xci", "public.zip-archive" },
                        MimeTypes = new[] { "application/x-nx-xci", "application/zip" }
                    }
                }
            });

            if (result.Count > 0)
            {
                if (result[0].TryGetUri(out Uri uri))
                {
                    await HandleFirmwareInstallation(uri.LocalPath);
                }
            }
        }

        public async void InstallFirmwareFromFolder()
        {
            var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false
            });

            if (result.Count > 0)
            {
                if (result[0].TryGetUri(out Uri uri))
                {
                    await HandleFirmwareInstallation(uri.LocalPath);
                }
            }
        }
    }
}