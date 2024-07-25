using CommunityToolkit.WinUI;
using FeiShuMinuteDownloader.models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.WebUI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using System.Globalization;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.AppNotifications;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FeiShuMinuteDownloader
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {

        private string personalHost;

        private string cookie;

        public NLog.Logger logger;

        private AppWindow m_AppWindow;

        [DllImport("user32.dll")]
        static extern int GetDpiForWindow(IntPtr hwnd);

        public MainWindow()
        {
            this.InitializeComponent();
            int dpi = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
            m_AppWindow = this.AppWindow;
            m_AppWindow.Resize(new SizeInt32((int)(760 * (double)((double)dpi / (double)120)), (int)(720 * (double)((double)dpi / (double)120))));
            m_AppWindow.SetIcon("ms-appx:///Assets/logo.ico");
            this.Title = "飞书妙记下载器";
            OverlappedPresenter overlappedPresenter = AppWindow.Presenter as OverlappedPresenter ?? Microsoft.UI.Windowing.OverlappedPresenter.Create();
            overlappedPresenter.IsResizable = false;
            logger = NLog.LogManager.GetCurrentClassLogger();
        }
        public ObservableCollection<Record> Records { get; set; }

        private async Task LoadFeishuData()
        {
            try
            {
                var baseAddress = new Uri($"https://{personalHost}");
                using (var handler = new HttpClientHandler { UseCookies = false })
                using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
                {
                    var message = new HttpRequestMessage(HttpMethod.Get, "/minutes/api/space/list?size=1000&space_name=2&rank=1&asc=false&language=zh_cn");
                    message.Headers.Add("Cookie", cookie);
                    message.Headers.Add("Referer", $"https://{personalHost}");
                    var result = await client.SendAsync(message);
                    result.EnsureSuccessStatusCode();
                    var content = await result.Content.ReadAsStringAsync();

                    MinuteListApiResponse response = JsonConvert.DeserializeObject<MinuteListApiResponse>(content);
                    Records = new ObservableCollection<Record>(response.data.list);
                    RecordsList.ItemsSource = Records;
                }
            }
            catch (Exception ex)
            {
                Content = new TextBlock { Text = $"Error: {ex.Message} \n {ex.StackTrace}" };
            }
        }

        private async void DownloadAll_Click(object sender, RoutedEventArgs e)
        {
            foreach(Record recordObject in Records)
            {
                string mediaUrl;
                var baseAddress = new Uri($"https://{personalHost}");
                using (var handler = new HttpClientHandler { UseCookies = false })
                using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
                {
                    var message = new HttpRequestMessage(HttpMethod.Get, $"/minutes/api/status?object_token={recordObject.object_token}&language=zh_cn");
                    message.Headers.Add("Cookie", cookie);
                    message.Headers.Add("Referer", $"https://{personalHost}");
                    var result = await client.SendAsync(message);
                    result.EnsureSuccessStatusCode();
                    var content = await result.Content.ReadAsStringAsync();
                    RecordDetail response = JsonConvert.DeserializeObject<RecordDetail>(content);
                    mediaUrl = response.data.video_info.video_download_url;
                    logger.Debug($"\n视频/音频地址: {response.data.video_info.video_download_url}");
                }

                using (var handler = new HttpClientHandler { UseCookies = false })
                using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
                {
                    var message = new HttpRequestMessage(HttpMethod.Post, $"/minutes/api/export");
                    message.Headers.Add("Cookie", cookie);
                    message.Headers.Add("Referer", $"https://{personalHost}/minutes/{recordObject.object_token}");
                    message.Headers.Add("bv-csrf-token", cookie.Split("bv_csrf_token=")[1].Split(";")[0]);

                    message.Content = new StringContent($"add_speaker=true&add_timestamp=true&format=2&is_fluent=false&language=zh_cn&object_token={recordObject.object_token}&translate_lang=default", Encoding.UTF8, new MediaTypeHeaderValue("application/x-www-form-urlencoded"));
                    var result = await client.SendAsync(message);
                    result.EnsureSuccessStatusCode();
                    var content = await result.Content.ReadAsStringAsync();
                    logger.Debug($"\n记录内容: {content}");
                }
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            LoginWebview.CoreWebView2.CookieManager.DeleteAllCookies();
            cookie = null;
            personalHost = null;
            LoginWebview.Visibility = Visibility.Visible;
            RecordsList.Visibility = Visibility.Collapsed;
            RecordsListHeader.Visibility = Visibility.Collapsed;
            DownloadAll.Visibility = Visibility.Collapsed;
            Logout.Visibility = Visibility.Collapsed;
            LoginWebview.Source = new Uri("https://bytedance.feishu.cn/minutes/me");
            var builder = new AppNotificationBuilder()
                .AddText("退出登录完成，请重新登陆！");
            var notificationManager = AppNotificationManager.Default;
            notificationManager.Show(builder.BuildNotification());
        }

        private void LoginWebview_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            LoginWebview.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
            LoginWebview.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
            LoginWebview.CoreWebView2.Settings.IsZoomControlEnabled = true;
            LoginWebview.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;
            LoginWebview.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
            LoginWebview.CoreWebView2.NewWindowRequested += (sender, args) =>
            {
                LoginWebview.CoreWebView2.ExecuteScriptAsync("window.location.href='" + args.Uri.ToString() + "'");
                args.Handled = true;
            };
        }

        private async void LoginWebview_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (sender.Source.AbsoluteUri.Contains(".feishu.cn") && !sender.Source.AbsoluteUri.Contains("account.feishu.cn") && !sender.Source.AbsoluteUri.Contains("bytedance.feishu.cn"))
            {
                string cookiesString = await GetCookiesStringAsync(LoginWebview.CoreWebView2.CookieManager);
                personalHost = LoginWebview.Source.Host;
                cookie = cookiesString;
                LoginWebview.Visibility = Visibility.Collapsed;
                RecordsList.Visibility = Visibility.Visible;
                RecordsListHeader.Visibility = Visibility.Visible;
                DownloadAll.Visibility = Visibility.Visible;
                Logout.Visibility = Visibility.Visible;
                await LoadFeishuData();
            }
        }

        private async Task<string> GetCookiesStringAsync(CoreWebView2CookieManager cookieManager)
        {
            var uri = LoginWebview.Source.ToString();
            var cookies = await cookieManager.GetCookiesAsync(uri);

            var cookieStrings = cookies.Select(cookie => $"{cookie.Name}={cookie.Value}");
            return string.Join("; ", cookieStrings);
        }
    }
}

