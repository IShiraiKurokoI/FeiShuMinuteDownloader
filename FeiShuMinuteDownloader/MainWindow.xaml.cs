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
            this.Title = "�������������";
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
            // ��ʾ�ļ���ѡ��Ի���
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
                    .AddText($"����ȡ����");
                var notificationManager1 = AppNotificationManager.Default;
                notificationManager1.Show(builder1.BuildNotification());
                return; // �û�ȡ�����ļ���ѡ��
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
                    // ��һ���֣����ض�ý���ļ�
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
                        logger.Debug($"\n��Ƶ/��Ƶ��ַ: {mediaUrl}");
                    }

                    // ���ض�ý���ļ���ѡ���ļ���
                    if (!string.IsNullOrEmpty(mediaUrl))
                    {
                        using (var handler = new HttpClientHandler { UseCookies = false })
                        using (var httpClient = new HttpClient(handler) { BaseAddress = baseAddress })
                        {
                            httpClient.DefaultRequestHeaders.Add("Cookie", cookie);
                            httpClient.DefaultRequestHeaders.Add("Referer", $"https://{personalHost}");

                            // ����GET�����Ի�ȡý������
                            using (var response = await httpClient.GetAsync(mediaUrl, HttpCompletionOption.ResponseHeadersRead))
                            {
                                response.EnsureSuccessStatusCode();

                                // ���� Content-Disposition ͷ
                                var contentDisposition = response.Content.Headers.ContentDisposition;
                                if (contentDisposition != null)
                                {
                                    // ���Դ� filename* �����л�ȡ UTF-8 ������ļ���
                                    fileName = contentDisposition.FileNameStar;
                                    if (string.IsNullOrEmpty(fileName))
                                    {
                                        // ��� filename* ����Ϊ�գ�����˵� filename ����
                                        fileName = DecodeFileName(contentDisposition.FileName);
                                    }

                                    // ���� UTF-8 ������ļ���
                                    if (!string.IsNullOrEmpty(fileName))
                                    {
                                        fileName = DecodeFileNameFromContentDisposition(fileName);
                                    }
                                }
                                else
                                {
                                    // ��� Content-Disposition ͷ�����ڣ�ʹ�� URL �е��ļ�����Ϊ��ѡ����
                                    fileName = Path.GetFileName(new Uri(mediaUrl).AbsolutePath);
                                }
                                fileName = $"({recordObject.topic})" + fileName;
                                // ȷ���ļ����������ļ�������Ψһ��
                                string filePath = Path.Combine(downloadFolder,fileName);
                                logger.Debug($"��ý���ļ�����λ�� {filePath}");
                                filePath = EnsureUniqueFileName(filePath);

                                // ���ļ����������ص�����
                                using (var fileStream = new FileStream(filePath, FileMode.Create))
                                {
                                    await response.Content.CopyToAsync(fileStream);
                                }
                            }
                        }
                    }

                    // �ڶ����֣������ı�����
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
                            // ���Դ� filename* �����л�ȡ UTF-8 ������ļ���
                            fileName = contentDisposition.FileNameStar;
                            if (string.IsNullOrEmpty(fileName))
                            {
                                // ��� filename* ����Ϊ�գ�����˵� filename ����
                                fileName = DecodeFileName(contentDisposition.FileName);
                            }

                            // ���� UTF-8 ������ļ���
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                fileName = DecodeFileNameFromContentDisposition(fileName);
                            }
                        }
                        else
                        {
                            // ��� Content-Disposition ͷ�����ڣ�ʹ�� URL �е��ļ�����Ϊ��ѡ����
                            fileName = Path.GetFileName(new Uri(mediaUrl).AbsolutePath);
                        }

                        fileName = $"({recordObject.topic})" + fileName;

                        // ȷ���ļ����������ļ�������Ψһ��
                        string filePath = Path.Combine(downloadFolder, fileName);
                        logger.Debug($"�ı��ļ�����λ�� {filePath}");
                        filePath = EnsureUniqueFileName(filePath);

                        // д���ı����ݵ��ļ�
                        File.WriteAllText(filePath, content);
                    }

                    completedFiles++;

                    var builder1 = new AppNotificationBuilder()
                        .AddText($"��¼ {recordObject.topic} ������ɡ�");
                    var notificationManager1 = AppNotificationManager.Default;
                    notificationManager1.Show(builder1.BuildNotification());
                }
                catch (HttpRequestException ex)
                {
                    var errorText = $"�޷����ؼ�¼�ļ�: {recordObject.topic}\n����: {ex.Message}";
                    logger.Error(errorText);

                    var builder1 = new AppNotificationBuilder()
                        .AddText($"�ļ� {recordObject.topic} ����ʧ�ܡ�");
                    var notificationManager1 = AppNotificationManager.Default;
                    notificationManager1.Show(builder1.BuildNotification());
                }
                catch (Exception ex)
                {
                    var errorText = $"��������: {ex.Message}";
                    logger.Error(errorText);

                    var builder1 = new AppNotificationBuilder()
                        .AddText($"�ļ� {fileName} ����ʧ�ܡ�");
                    var notificationManager1 = AppNotificationManager.Default;
                    notificationManager1.Show(builder1.BuildNotification());
                }
            }

            var builder = new AppNotificationBuilder()
                .AddText("������ɣ�");
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

        // ���� UTF-8 ������ļ���
        private static string DecodeFileNameFromContentDisposition(string fileName)
        {
            // �� filename* �����н��� UTF-8 ������ļ���
            const string utf8EncodingPrefix = "UTF-8''";
            if (fileName.StartsWith(utf8EncodingPrefix))
            {
                fileName = fileName.Substring(utf8EncodingPrefix.Length);

                try
                {
                    // ���� UTF-8 ������ļ���
                    fileName = Uri.UnescapeDataString(fileName);
                    fileName = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(fileName));
                }
                catch (Exception ex)
                {
                    // ��������쳣
                    Console.WriteLine($"�޷������ļ���: {ex.Message}");
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
                .AddText("�˳���¼��ɣ������µ�½��");
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

