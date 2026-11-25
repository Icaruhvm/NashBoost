using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NashSetup
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            ServicePointManager.Expect100Continue = false;
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(new SetupWindow());
        }
    }

    sealed class SetupWindow : Window
    {
        const string Api = "https://api.github.com/repos/Icaruhvm/NashBoost/releases/latest";
        readonly string installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NashBoost");
        readonly string desktopLink;
        readonly string startLink;
        TextBlock status;
        ProgressBar bar;
        Button installButton;
        Button openButton;
        Button removeButton;
        WebClient client;

        public SetupWindow()
        {
            desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Nash Boost.lnk");
            startLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Nash Boost.lnk");
            Title = "Instalar Nash Boost";
            Width = 480;
            Height = 360;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            Background = BrushOf(0x1e, 0x1f, 0x22);
            FontFamily = new FontFamily("Segoe UI");
            Foreground = BrushOf(0xdc, 0xde, 0xe2);
            try
            {
                var ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nash.ico");
                if (File.Exists(ico)) Icon = BitmapFrame.Create(new Uri(ico));
            }
            catch { }

            var root = new Grid { Margin = new Thickness(28, 22, 28, 22) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(Logo());
            var titles = new StackPanel { Margin = new Thickness(12, 4, 0, 0) };
            titles.Children.Add(Line("Nash Boost", 18, BrushOf(0xdc, 0xde, 0xe2), FontWeights.SemiBold, new Thickness(0)));
            titles.Children.Add(Line("Instalador", 12, BrushOf(0x8e, 0x93, 0x9a), FontWeights.Normal, new Thickness(0, 2, 0, 0)));
            head.Children.Add(titles);
            var close = MakeButton("Fechar", false);
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.VerticalAlignment = VerticalAlignment.Top;
            close.Click += delegate { Close(); };
            var headGrid = new Grid();
            headGrid.ColumnDefinitions.Add(new ColumnDefinition());
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(close, 1);
            headGrid.Children.Add(head);
            headGrid.Children.Add(close);
            root.Children.Add(headGrid);

            var intro = Line("Baixa só o programa, direto do GitHub. Não precisa do código.\nVai para " + installDir + ".", 13, BrushOf(0x8e, 0x93, 0x9a), FontWeights.Normal, new Thickness(0, 18, 0, 0));
            Grid.SetRow(intro, 1);
            root.Children.Add(intro);

            bar = new ProgressBar { Height = 4, Minimum = 0, Maximum = 100, Foreground = BrushOf(0x3f, 0x64, 0x86), Background = BrushOf(0x2e, 0x30, 0x34), BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 10), VerticalAlignment = VerticalAlignment.Bottom };
            Grid.SetRow(bar, 3);
            root.Children.Add(bar);

            status = Line(Installed() ? "O Nash Boost já está neste PC. Atualizar baixa a versão mais recente." : "Pronto para instalar.", 13, BrushOf(0x8e, 0x93, 0x9a), FontWeights.Normal, new Thickness(0, 0, 0, 16));
            status.TextWrapping = TextWrapping.Wrap;
            Grid.SetRow(status, 4);
            root.Children.Add(status);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            removeButton = MakeButton("Remover", false);
            removeButton.Margin = new Thickness(0, 0, 8, 0);
            removeButton.Visibility = Installed() ? Visibility.Visible : Visibility.Collapsed;
            removeButton.Click += delegate { Remove(); };
            openButton = MakeButton("Abrir", false);
            openButton.Margin = new Thickness(0, 0, 8, 0);
            openButton.Visibility = Installed() ? Visibility.Visible : Visibility.Collapsed;
            openButton.Click += delegate { OpenApp(); };
            installButton = MakeButton(Installed() ? "Atualizar" : "Instalar", true);
            installButton.Click += delegate { BeginInstall(); };
            buttons.Children.Add(removeButton);
            buttons.Children.Add(openButton);
            buttons.Children.Add(installButton);
            Grid.SetRow(buttons, 5);
            root.Children.Add(buttons);

            var frame = new Border { Background = BrushOf(0x1e, 0x1f, 0x22), BorderBrush = BrushOf(0x3c, 0x3e, 0x44), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = root };
            frame.SizeChanged += delegate
            {
                if (frame.ActualWidth <= 0 || frame.ActualHeight <= 0) return;
                frame.Clip = new RectangleGeometry(new Rect(0, 0, frame.ActualWidth, frame.ActualHeight), 8, 8);
            };
            Content = frame;
            MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
        }

        bool Installed()
        {
            return File.Exists(Path.Combine(installDir, "NashBoost.exe"));
        }

        void BeginInstall()
        {
            installButton.IsEnabled = false;
            removeButton.IsEnabled = false;
            openButton.IsEnabled = false;
            bar.Value = 0;
            status.Text = "Buscando a versão mais recente…";
            if (client != null) client.Dispose();
            client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "NashBoost-Setup";
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            client.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            client.Encoding = System.Text.Encoding.UTF8;
            client.DownloadStringCompleted += OnApi;
            client.DownloadStringAsync(new Uri(Api));
        }

        void OnApi(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Fail("Não consegui falar com o GitHub. " + Clean(e.Error));
                return;
            }
            var urlMatch = Regex.Match(e.Result ?? "", "\"browser_download_url\"\\s*:\\s*\"(https://[^\"]+/NashBoost\\.exe)\"");
            if (!urlMatch.Success)
            {
                Fail("A versão publicada ainda não tem o NashBoost.exe.");
                return;
            }
            var tag = Regex.Match(e.Result, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            var label = tag.Success ? tag.Groups[1].Value : "a versão atual";
            var url = urlMatch.Groups[1].Value.Replace("\\u0026", "&");
            var temp = Path.Combine(Path.GetTempPath(), "NashBoost-setup-payload.exe");
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            status.Text = "Baixando " + label + "…";
            var download = new WebClient();
            download.Headers[HttpRequestHeader.UserAgent] = "NashBoost-Setup";
            download.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            if (client != null) client.Dispose();
            client = download;
            download.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs ev) { bar.Value = ev.ProgressPercentage; };
            download.DownloadFileCompleted += delegate(object s, AsyncCompletedEventArgs ev) { OnFile(ev, temp, label); };
            download.DownloadFileAsync(new Uri(url), temp);
        }

        void OnFile(AsyncCompletedEventArgs e, string temp, string label)
        {
            if (e.Error != null)
            {
                Fail("O download falhou. " + Clean(e.Error));
                return;
            }
            try
            {
                var info = new FileInfo(temp);
                if (!info.Exists || info.Length < 200000 || !IsExe(temp))
                    throw new InvalidOperationException("O arquivo baixado não é o Nash Boost.");
                foreach (var proc in Process.GetProcessesByName("NashBoost"))
                {
                    try { proc.CloseMainWindow(); } catch { }
                }
                System.Threading.Thread.Sleep(400);
                Directory.CreateDirectory(installDir);
                var dest = Path.Combine(installDir, "NashBoost.exe");
                try { File.Copy(temp, dest, true); }
                catch (IOException)
                {
                    throw new InvalidOperationException("Feche o Nash Boost e clique em Atualizar de novo.");
                }
                try { File.Delete(temp); } catch { }
                Shortcut(startLink, dest, installDir);
                Shortcut(desktopLink, dest, installDir);
                status.Text = label + " instalado. Atalhos no Menu Iniciar e na área de trabalho.";
                installButton.Content = "Atualizar";
                installButton.IsEnabled = true;
                removeButton.Visibility = Visibility.Visible;
                removeButton.IsEnabled = true;
                openButton.Visibility = Visibility.Visible;
                openButton.IsEnabled = true;
                bar.Value = 100;
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
            }
        }

        void Remove()
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName("NashBoost"))
                {
                    try { proc.CloseMainWindow(); } catch { }
                }
                System.Threading.Thread.Sleep(400);
                if (Directory.Exists(installDir)) Directory.Delete(installDir, true);
                if (File.Exists(desktopLink)) File.Delete(desktopLink);
                if (File.Exists(startLink)) File.Delete(startLink);
                status.Text = "O Nash Boost foi removido. A conta neste PC foi mantida.";
                installButton.Content = "Instalar";
                openButton.Visibility = Visibility.Collapsed;
                removeButton.Visibility = Visibility.Collapsed;
                bar.Value = 0;
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
            }
        }

        void OpenApp()
        {
            var dest = Path.Combine(installDir, "NashBoost.exe");
            if (!File.Exists(dest))
            {
                status.Text = "O programa não está instalado.";
                return;
            }
            Process.Start(new ProcessStartInfo(dest) { UseShellExecute = true });
            Close();
        }

        void Fail(string message)
        {
            status.Text = message;
            installButton.IsEnabled = true;
            removeButton.IsEnabled = true;
            openButton.IsEnabled = true;
        }

        static bool IsExe(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length < 2) return false;
                return stream.ReadByte() == 0x4D && stream.ReadByte() == 0x5A;
            }
        }

        static string Clean(Exception ex)
        {
            var inner = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            if (string.IsNullOrEmpty(inner)) return "Tente de novo.";
            return inner;
        }

        static void Shortcut(string linkPath, string target, string workDir)
        {
            var dir = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            var shell = Activator.CreateInstance(shellType);
            var link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
            var linkType = link.GetType();
            linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
            linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { workDir });
            linkType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { target + ",0" });
            linkType.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "Nash Boost" });
            linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }

        Image Logo()
        {
            var image = new Image { Width = 48, Height = 48, Stretch = Stretch.Uniform };
            try
            {
                var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Nash.logo.png");
                if (stream != null)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = stream;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    image.Source = bmp;
                }
            }
            catch { }
            return image;
        }

        static TextBlock Line(string text, double size, Brush color, FontWeight weight, Thickness margin)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = color, FontWeight = weight, Margin = margin, TextWrapping = TextWrapping.Wrap };
        }

        static Button MakeButton(string text, bool primary)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 108,
                Height = 38,
                Padding = new Thickness(14, 0, 14, 0),
                Background = primary ? BrushOf(0x3f, 0x64, 0x86) : BrushOf(0x26, 0x27, 0x2b),
                Foreground = primary ? BrushOf(0xf3, 0xf5, 0xf7) : BrushOf(0xdc, 0xde, 0xe2),
                BorderBrush = primary ? BrushOf(0x3f, 0x64, 0x86) : BrushOf(0x3c, 0x3e, 0x44),
                BorderThickness = new Thickness(1),
                FontSize = 13,
                FontWeight = FontWeights.Normal,
                Cursor = Cursors.Hand,
                Template = Flat()
            };
            return button;
        }

        static ControlTemplate Flat()
        {
            var border = new System.Windows.FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new System.Windows.TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new System.Windows.TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new System.Windows.TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            var content = new System.Windows.FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            var template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            return template;
        }

        static SolidColorBrush BrushOf(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
