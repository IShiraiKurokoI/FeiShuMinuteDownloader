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
using Windows.Storage.Pickers;
using Windows.Storage.AccessCache;
using WinUICommunity;
using System.Text.RegularExpressions;

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
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            int dpi = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
            m_AppWindow = this.AppWindow;
            m_AppWindow.Resize(new SizeInt32((int)(900 * (double)((double)dpi / (double)120)), (int)(800 * (double)((double)dpi / (double)120))));
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
                    RecordsDataGrid.ItemsSource = Records;
                }
            }
            catch (Exception ex)
            {
                Content = new TextBlock { Text = $"Error: {ex.Message} \n {ex.StackTrace}" };
            }
        }

        private async void DownloadAll_Click(object sender, RoutedEventArgs e)
        {
            // 显示文件夹选择对话框
            FolderPicker openPicker = new Windows.Storage.Pickers.FolderPicker();
            var window = this;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add("*");
            StorageFolder selectedFolder = await openPicker.PickSingleFolderAsync();

            if (selectedFolder == null)
            {
                var builder1 = new AppNotificationBuilder()
                    .AddText($"下载取消。");
                var notificationManager1 = AppNotificationManager.Default;
                notificationManager1.Show(builder1.BuildNotification());
                return; // 用户取消了文件夹选择
            }

            string downloadFolder = selectedFolder.Path;

            int totalFiles = Records.Count;
            int completedFiles = 0;

            foreach (Record recordObject in Records)
            {
                string mediaUrl = "";
                string fileName = "";

                try
                {
                    // 第一部分：下载多媒体文件
                    var baseAddress = new Uri($"https://{personalHost}");
                    using (var handler = new HttpClientHandler { UseCookies = false })
                    using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
                    {
                        var message = new HttpRequestMessage(HttpMethod.Get, $"/minutes/api/status?object_token={recordObject.object_token}&language=zh_cn");
                        message.Headers.Add("Cookie", cookie);
                        message.Headers.Add("Referer", $"https://{personalHost}");
                        var resultMessage = await client.SendAsync(message);
                        resultMessage.EnsureSuccessStatusCode();
                        var content = await resultMessage.Content.ReadAsStringAsync();
                        RecordDetail response = JsonConvert.DeserializeObject<RecordDetail>(content);
                        mediaUrl = response.data.video_info.video_download_url;
                        logger.Debug($"\n视频/音频地址: {mediaUrl}");
                    }

                    // 下载多媒体文件到选定文件夹
                    if (!string.IsNullOrEmpty(mediaUrl))
                    {
                        using (var handler = new HttpClientHandler { UseCookies = false })
                        using (var httpClient = new HttpClient(handler) { BaseAddress = baseAddress })
                        {
                            httpClient.DefaultRequestHeaders.Add("Cookie", cookie);
                            httpClient.DefaultRequestHeaders.Add("Referer", $"https://{personalHost}");

                            // 发送GET请求以获取媒体内容
                            using (var response = await httpClient.GetAsync(mediaUrl, HttpCompletionOption.ResponseHeadersRead))
                            {
                                response.EnsureSuccessStatusCode();

                                // 解析 Content-Disposition 头
                                var contentDisposition = response.Content.Headers.ContentDisposition;
                                if (contentDisposition != null)
                                {
                                    // 尝试从 filename* 参数中获取 UTF-8 编码的文件名
                                    fileName = contentDisposition.FileNameStar;
                                    if (string.IsNullOrEmpty(fileName))
                                    {
                                        // 如果 filename* 参数为空，则回退到 filename 参数
                                        fileName = DecodeFileName(contentDisposition.FileName);
                                    }

                                    // 解码 UTF-8 编码的文件名
                                    if (!string.IsNullOrEmpty(fileName))
                                    {
                                        fileName = DecodeFileNameFromContentDisposition(fileName);
                                    }
                                }
                                else
                                {
                                    // 如果 Content-Disposition 头不存在，使用 URL 中的文件名作为备选方案
                                    fileName = Path.GetFileName(new Uri(mediaUrl).AbsolutePath);
                                }
                                fileName = $"({recordObject.topic})" + fileName;
                                // 确保文件名在下载文件夹中是唯一的
                                string filePath = Path.Combine(downloadFolder,fileName);
                                logger.Debug($"多媒体文件下载位置 {filePath}");
                                filePath = EnsureUniqueFileName(filePath);

                                // 将文件内容流下载到磁盘
                                using (var fileStream = new FileStream(filePath, FileMode.Create))
                                {
                                    await response.Content.CopyToAsync(fileStream);
                                }
                            }
                        }
                    }

                    // 第二部分：下载文本内容
                    using (var handler = new HttpClientHandler { UseCookies = false })
                    using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
                    {
                        var message = new HttpRequestMessage(HttpMethod.Post, $"/minutes/api/export");
                        message.Headers.Add("Cookie", cookie);
                        message.Headers.Add("Referer", $"https://{personalHost}/minutes/{recordObject.object_token}");
                        message.Headers.Add("bv-csrf-token", cookie.Split("bv_csrf_token=")[1].Split(";")[0]);
                        message.Headers.Add("Accept", "text/plain;charset=utf-8");
                        message.Content = new StringContent($"add_speaker=true&add_timestamp=true&format=2&is_fluent=false&language=zh_cn&object_token={recordObject.object_token}&translate_lang=default", Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded"));

                        var resultMessage = await client.SendAsync(message);
                        resultMessage.EnsureSuccessStatusCode();
                        var content = await resultMessage.Content.ReadAsStringAsync();

                        var contentDisposition = resultMessage.Content.Headers.ContentDisposition;
                        if (contentDisposition != null)
                        {
                            // 尝试从 filename* 参数中获取 UTF-8 编码的文件名
                            fileName = contentDisposition.FileNameStar;
                            if (string.IsNullOrEmpty(fileName))
                            {
                                // 如果 filename* 参数为空，则回退到 filename 参数
                                fileName = DecodeFileName(contentDisposition.FileName);
                            }

                            // 解码 UTF-8 编码的文件名
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                fileName = DecodeFileNameFromContentDisposition(fileName);
                            }
                        }
                        else
                        {
                            // 如果 Content-Disposition 头不存在，使用 URL 中的文件名作为备选方案
                            fileName = Path.GetFileName(new Uri(mediaUrl).AbsolutePath);
                        }

                        fileName = $"({recordObject.topic})" + fileName;

                        // 确保文件名在下载文件夹中是唯一的
                        string filePath = Path.Combine(downloadFolder, fileName);
                        logger.Debug($"文本文件下载位置 {filePath}");
                        filePath = EnsureUniqueFileName(filePath);

                        // 写入文本内容到文件
                        File.WriteAllText(filePath, content);
                    }

                    completedFiles++;

                    var builder1 = new AppNotificationBuilder()
                        .AddText($"记录 {recordObject.topic} 下载完成。");
                    var notificationManager1 = AppNotificationManager.Default;
                    notificationManager1.Show(builder1.BuildNotification());
                }
                catch (HttpRequestException ex)
                {
                    var errorText = $"无法下载记录文件: {recordObject.topic}\n错误: {ex.Message}";
                    logger.Error(errorText);

                    var builder1 = new AppNotificationBuilder()
                        .AddText($"文件 {recordObject.topic} 下载失败。");
                    var notificationManager1 = AppNotificationManager.Default;
                    notificationManager1.Show(builder1.BuildNotification());
                }
                catch (Exception ex)
                {
                    var errorText = $"发生错误: {ex.Message}";
                    logger.Error(errorText);

                    var builder1 = new AppNotificationBuilder()
                        .AddText($"文件 {fileName} 下载失败。");
                    var notificationManager1 = AppNotificationManager.Default;
                    notificationManager1.Show(builder1.BuildNotification());
                }
            }

            var builder = new AppNotificationBuilder()
                .AddText("下载完成！");
            var notificationManager = AppNotificationManager.Default;
            notificationManager.Show(builder.BuildNotification());
        }

        static string DecodeFileName(string filename)
        {
            filename = filename.Trim('\"');
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(filename);
            byte[] windows1252Bytes = Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding("ISO-8859-1"), utf8Bytes);
            string output = Encoding.UTF8.GetString(windows1252Bytes);
            return output;
        }

        // 解码 UTF-8 编码的文件名
        private static string DecodeFileNameFromContentDisposition(string fileName)
        {
            // 从 filename* 参数中解析 UTF-8 编码的文件名
            const string utf8EncodingPrefix = "UTF-8''";
            if (fileName.StartsWith(utf8EncodingPrefix))
            {
                fileName = fileName.Substring(utf8EncodingPrefix.Length);

                try
                {
                    // 解码 UTF-8 编码的文件名
                    fileName = Uri.UnescapeDataString(fileName);
                    fileName = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(fileName));
                }
                catch (Exception ex)
                {
                    // 处理解码异常
                    Console.WriteLine($"无法解码文件名: {ex.Message}");
                }
            }

            return fileName;
        }

        private string EnsureUniqueFileName(string filePath)
        {
            if (File.Exists(filePath))
            {
                string directory = Path.GetDirectoryName(filePath);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                string fileExtension = Path.GetExtension(filePath);

                int count = 1;
                string newFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}_{count}{fileExtension}");

                while (File.Exists(newFilePath))
                {
                    count++;
                    newFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}_{count}{fileExtension}");
                }

                return newFilePath;
            }

            return filePath;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            LoginWebview.CoreWebView2.CookieManager.DeleteAllCookies();
            cookie = null;
            personalHost = null;
            Records = null;
            RecordsDataGrid.ItemsSource = Records;
            LoginWebview.Visibility = Visibility.Visible;
            RecordsDataGrid.Visibility = Visibility.Collapsed;
            BottomBar.Visibility = Visibility.Collapsed;
            DownloadAll.Visibility = Visibility.Collapsed;
            DownloadProgress.Visibility = Visibility.Collapsed;
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
                RecordsDataGrid.Visibility = Visibility.Visible;
                BottomBar.Visibility = Visibility.Visible;
                DownloadAll.Visibility = Visibility.Visible;
                DownloadProgress.Visibility = Visibility.Visible;
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

