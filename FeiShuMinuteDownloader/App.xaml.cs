﻿using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.AppNotifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUICommunity;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FeiShuMinuteDownloader
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            logger = NLog.LogManager.GetCurrentClassLogger();
            logger.Info("日志记录初始化成功");
            DeleteLog();
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            App.Current.UnhandledException += App_UnhandledException;
            this.InitializeComponent();
        }

        public NLog.Logger logger;
        public static IThemeService themeService { get; private set; }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            logger.Info("--------程序启动--------");
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--proxy-server=\"direct://\" --lang=zh-CN");
            Environment.SetEnvironmentVariable("WEBVIEW2_USE_VISUAL_HOSTING_FOR_OWNED_WINDOWS", "1");
            logger.Info("WebView参数初始化成功");

            m_window = new MainWindow();

            themeService = new ThemeService();
            themeService.Initialize(m_window);
            themeService.ConfigBackdrop(BackdropType.AcrylicThin);
            themeService.ConfigElementTheme(ElementTheme.Light);
            themeService.ConfigTitleBar(new TitleBarCustomization
            {
                TitleBarWindowType = TitleBarWindowType.AppWindow,
                LightTitleBarButtons = new TitleBarButtons
                {
                    ButtonBackgroundColor = Colors.Transparent
                },
                DarkTitleBarButtons = new TitleBarButtons
                {
                    ButtonBackgroundColor = Colors.Transparent
                }
            });
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow is not null)
            {
                Microsoft.UI.Windowing.DisplayArea displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                if (displayArea is not null)
                {
                    var CenteredPosition = appWindow.Position;
                    CenteredPosition.X = ((displayArea.WorkArea.Width - appWindow.Size.Width) / 2);
                    CenteredPosition.Y = ((displayArea.WorkArea.Height - appWindow.Size.Height) / 2);
                    appWindow.Move(CenteredPosition);
                }
            }
            m_window.Activate();
        }

        private Window m_window;

        public void DeleteLog()
        {
            try
            {
                string logDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\FeiShuMinuteDownloader\\Log";
                string logFilePrefix = "Log-FeiShuMinuteDownloader-";
                int daysThreshold = 3;
                DateTime deletionDate = DateTime.Now.AddDays(-daysThreshold);
                string[] logFiles = Directory.GetFiles(logDirectory, logFilePrefix + "*.log");

                foreach (string logFile in logFiles)
                {
                    string fileName = System.IO.Path.GetFileName(logFile);
                    string dateString = fileName.Substring(logFilePrefix.Length, 10);
                    DateTime logDate;

                    if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out logDate))
                    {
                        if (logDate <= deletionDate)
                        {
                            File.Delete(logFile);
                            logger.Info("删除过期日志: " + fileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.ToString());
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // 处理未处理的异常
            HandleException(e.Exception);
            // 将事件标记为已处理，以防止应用程序崩溃
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                if (e.Exception is Exception exception)
                {
                    HandleException(exception);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
            finally
            {
                e.SetObserved();
            }
        }

        //非UI线程未捕获异常处理事件(例如自己创建的一个子线程)
        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            try
            {
                if (e.ExceptionObject is Exception exception)
                {
                    HandleException(exception);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        //日志记录
        private void HandleException(Exception ex)
        {
            var builder = new AppNotificationBuilder()
                .AddText(ex.Message + ex.StackTrace);
            var notificationManager = AppNotificationManager.Default;
            notificationManager.Show(builder.BuildNotification());
            //记录日志
            logger.Error(ex.ToString());
        }
    }
}
