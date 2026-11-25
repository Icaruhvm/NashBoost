using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace Nash
{
    public static class AppEntry
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && string.Equals(args[0], "/ram", StringComparison.OrdinalIgnoreCase))
            {
                var full = false;
                for (var i = 1; i < args.Length; i++)
                    if (string.Equals(args[i], "/full", StringComparison.OrdinalIgnoreCase)) full = true;
                try { System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal; } catch { }
                var lines = Tweaks.CleanRam(full);
                var msg = lines.Count == 0 ? "Limpeza de RAM concluída." : lines[lines.Count - 1];
                try { Tweaks.Notify(msg); } catch { }
                return;
            }
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(new Shell());
        }
    }

    static class Palette
    {
        public static SolidColorBrush Make(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
        public static readonly SolidColorBrush Bg = Make(0x18, 0x1a, 0x1e);
        public static readonly SolidColorBrush Side = Make(0x12, 0x13, 0x16);
        public static readonly SolidColorBrush Card = Make(0x22, 0x24, 0x29);
        public static readonly SolidColorBrush Card2 = Make(0x2a, 0x2d, 0x33);
        public static readonly SolidColorBrush Line = Make(0x3a, 0x3e, 0x46);
        public static readonly SolidColorBrush Text = Make(0xe4, 0xe6, 0xea);
        public static readonly SolidColorBrush Muted = Make(0x8b, 0x91, 0x9a);
        public static readonly SolidColorBrush Accent = Make(0x4c, 0x7e, 0xa8);
        public static readonly SolidColorBrush AccentHot = Make(0x5d, 0x92, 0xbe);
        public static readonly SolidColorBrush Ink = Make(0xf3, 0xf5, 0xf7);
        public static readonly SolidColorBrush WarnBg = Make(0x32, 0x2c, 0x1e);
        public static readonly SolidColorBrush Warn = Make(0xc4, 0xa3, 0x5a);
        public static readonly SolidColorBrush Danger = Make(0xd0, 0x76, 0x76);
        public static readonly SolidColorBrush Field = Make(0x14, 0x15, 0x18);
        public static readonly SolidColorBrush Thumb = Make(0x5a, 0x5e, 0x66);
    }

    sealed class SwitchBox : Border
    {
        readonly Border knob = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        public bool IsOn { get; private set; }
        public SwitchBox(bool on)
        {
            Width = 36;
            Height = 18;
            CornerRadius = new CornerRadius(9);
            BorderThickness = new Thickness(1);
            BorderBrush = Palette.Line;
            Cursor = Cursors.Hand;
            Child = knob;
            SetOn(on);
            MouseLeftButtonUp += delegate
            {
                if (!IsEnabled) return;
                SetOn(!IsOn);
            };
        }
        public void SetOn(bool on)
        {
            IsOn = on;
            Background = on ? Palette.Accent : Palette.Field;
            knob.Background = on ? Palette.Ink : Palette.Muted;
            knob.Margin = new Thickness(on ? 20 : 2, 0, 0, 0);
            Opacity = IsEnabled ? 1 : 0.45;
        }
    }

    sealed class PageScroll : Grid
    {
        readonly ScrollViewer viewer;
        readonly Grid rail;
        readonly Border thumb;
        bool drag;
        bool over;
        double dragY;
        double dragOffset;
        int layoutTries;

        public PageScroll(Thickness padding, Brush background)
        {
            Background = background;
            viewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = padding,
                Background = background,
                CanContentScroll = false,
                PanningMode = PanningMode.VerticalOnly
            };
            Children.Add(viewer);
            rail = new Grid
            {
                Width = 12,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 16, 8, 16),
                Background = Brushes.Transparent,
                Visibility = Visibility.Collapsed
            };
            thumb = new Border
            {
                Width = 5,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Background = Palette.Thumb,
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand
            };
            rail.Children.Add(thumb);
            Children.Add(rail);
            viewer.ScrollChanged += delegate { LayoutThumb(); };
            SizeChanged += delegate { layoutTries = 0; LayoutThumb(); };
            thumb.MouseEnter += delegate { over = true; thumb.Background = Palette.Accent; };
            thumb.MouseLeave += delegate { over = false; if (!drag) thumb.Background = Palette.Thumb; };
            thumb.MouseLeftButtonDown += OnThumbDown;
            thumb.MouseMove += OnThumbMove;
            thumb.MouseLeftButtonUp += OnThumbUp;
            thumb.LostMouseCapture += delegate { drag = false; if (!over) thumb.Background = Palette.Thumb; };
            rail.MouseLeftButtonDown += OnRailDown;
            rail.PreviewMouseWheel += OnWheel;
        }

        public object Content
        {
            get { return viewer.Content; }
            set { viewer.Content = value; viewer.ScrollToVerticalOffset(0); }
        }

        public void Cap(double height)
        {
            MaxHeight = height;
            viewer.MaxHeight = height;
        }

        void OnWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) viewer.LineUp();
            else viewer.LineDown();
            e.Handled = true;
        }

        void OnThumbDown(object sender, MouseButtonEventArgs e)
        {
            drag = true;
            dragY = e.GetPosition(rail).Y;
            dragOffset = viewer.VerticalOffset;
            thumb.CaptureMouse();
            thumb.Background = Palette.Accent;
            e.Handled = true;
        }

        void OnThumbMove(object sender, MouseEventArgs e)
        {
            if (!drag) return;
            var travel = rail.ActualHeight - thumb.Height;
            if (travel < 1) return;
            var dy = e.GetPosition(rail).Y - dragY;
            viewer.ScrollToVerticalOffset(dragOffset + (dy / travel) * viewer.ScrollableHeight);
        }

        void OnThumbUp(object sender, MouseButtonEventArgs e)
        {
            if (!drag) return;
            drag = false;
            thumb.ReleaseMouseCapture();
            if (!over) thumb.Background = Palette.Thumb;
        }

        void OnRailDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == thumb) return;
            var y = e.GetPosition(rail).Y;
            if (y < thumb.Margin.Top) viewer.PageUp();
            else if (y > thumb.Margin.Top + thumb.Height) viewer.PageDown();
            e.Handled = true;
        }

        void LayoutThumb()
        {
            var extent = viewer.ExtentHeight;
            var viewport = viewer.ViewportHeight;
            var scrollable = viewer.ScrollableHeight;
            if (scrollable <= 1 || viewport < 1 || extent < 1)
            {
                rail.Visibility = Visibility.Collapsed;
                layoutTries = 0;
                return;
            }
            rail.Visibility = Visibility.Visible;
            var trackH = rail.ActualHeight;
            if (trackH < 8)
            {
                if (layoutTries >= 6) return;
                layoutTries++;
                Dispatcher.BeginInvoke(new Action(LayoutThumb), System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }
            layoutTries = 0;
            var thumbH = trackH * (viewport / extent);
            if (thumbH < 42) thumbH = 42;
            if (thumbH > trackH - 4) thumbH = trackH - 4;
            var travel = trackH - thumbH;
            var y = scrollable <= 0 ? 0 : travel * (viewer.VerticalOffset / scrollable);
            if (y < 0) y = 0;
            if (y > travel) y = travel;
            if (Math.Abs(thumb.Height - thumbH) > 0.5) thumb.Height = thumbH;
            if (Math.Abs(thumb.Margin.Top - y) > 0.5) thumb.Margin = new Thickness(0, y, 0, 0);
        }
    }

    sealed class Shell : Window
    {
        readonly Dictionary<string, Button> nav = new Dictionary<string, Button>();
        readonly Dictionary<string, UIElement> pages = new Dictionary<string, UIElement>();
        readonly Dictionary<string, SwitchBox> game = new Dictionary<string, SwitchBox>();
        readonly Dictionary<string, SwitchBox> system = new Dictionary<string, SwitchBox>();
        readonly Dictionary<string, SwitchBox> clean = new Dictionary<string, SwitchBox>();
        readonly List<AppPick> picks = new List<AppPick>();
        Grid login;
        Grid app;
        Grid busy;
        Grid modal;
        TextBlock busyTitle;
        TextBlock busyText;
        TextBlock modalTitle;
        TextBlock modalText;
        StackPanel modalList;
        Button modalOk;
        Button modalCancel;
        TaskCompletionSource<bool> modalWait;
        StackPanel toasts;
        TextBlock who;
        string user = "";
        bool busyOn;
        TextBlock winText;
        TextBlock ramText;
        TextBlock gpuText;
        TextBlock cpuText;
        TextBlock hello;
        TextBlock lastClean;
        StackPanel flags;
        TextBlock jogoMouse;
        TextBlock jogoKey;
        TextBlock jogoUsb;
        TextBlock jogoMem;
        StackPanel installedHost;
        StackPanel startupHost;
        StackPanel catalogHost;
        TextBlock appSummary;
        SwitchBox onedrive;
        bool appsReady;
        ColumnDefinition captionCol;
        Border captionSide;
        TextBlock maxGlyph;
        Border frame;
        TextBlock captionTitle;
        const int SideW = 232;

        public Shell()
        {
            Title = "Nash Boost";
            Width = 1180;
            Height = 760;
            MinWidth = 1040;
            MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = Palette.Bg;
            FontFamily = new FontFamily("Segoe UI");
            Foreground = Palette.Text;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            var chrome = new WindowChrome
            {
                CaptionHeight = 40,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                UseAeroCaptionButtons = false
            };
            WindowChrome.SetWindowChrome(this, chrome);
            try
            {
                var ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nash.ico");
                if (File.Exists(ico)) Icon = BitmapFrame.Create(new Uri(ico));
            }
            catch { }
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(41) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var caption = BuildCaption();
            Grid.SetRow(caption, 0);
            var body = new Grid();
            Grid.SetRow(body, 1);
            login = new Grid { Visibility = Visibility.Collapsed };
            app = BuildApp();
            busy = BuildBusy();
            modal = BuildModal();
            toasts = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(18) };
            body.Children.Add(login);
            body.Children.Add(app);
            root.Children.Add(caption);
            root.Children.Add(body);
            root.Children.Add(busy);
            root.Children.Add(modal);
            root.Children.Add(toasts);
            Grid.SetRowSpan(busy, 2);
            Grid.SetRowSpan(modal, 2);
            Grid.SetRowSpan(toasts, 2);
            frame = new Border { Background = Palette.Bg, BorderBrush = Palette.Line, BorderThickness = new Thickness(1), Child = root };
            frame.SizeChanged += delegate { ApplyClip(frame); };
            Content = frame;
            KeyDown += OnKey;
            StateChanged += delegate { UpdateChrome(); };
            SourceInitialized += OnSourceInitialized;
            UpdateChrome();
            OpenApp();
        }

        static BitmapImage logoBmp;

        Image Logo(double size)
        {
            var image = new Image { Width = size, Height = size, Stretch = Stretch.Uniform };
            if (logoBmp != null) { image.Source = logoBmp; return image; }
            try
            {
                var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Nash.logo.png");
                if (stream == null)
                {
                    var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
                    if (File.Exists(path)) stream = File.OpenRead(path);
                }
                if (stream != null)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = stream;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 160;
                    bmp.EndInit();
                    bmp.Freeze();
                    if (logoBmp == null) logoBmp = bmp;
                    stream.Dispose();
                    image.Source = logoBmp;
                }
            }
            catch { }
            return image;
        }

        Grid BuildLogin()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star) });
            var hero = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 18) };
            brand.Children.Add(Logo(40));
            var brandText = new StackPanel { Margin = new Thickness(12, 4, 0, 0) };
            brandText.Children.Add(Text("Nash Boost", 18, Palette.Text, FontWeights.SemiBold, new Thickness(0)));
            brandText.Children.Add(Text("Windows 10 e 11", 12, Palette.Muted, FontWeights.Normal, new Thickness(0, 1, 0, 0)));
            brand.Children.Add(brandText);
            hero.Children.Add(brand);
            hero.Children.Add(Text("Ajustes de Windows para quem joga.", 22, Palette.Text, FontWeights.SemiBold, new Thickness(0, 0, 0, 10)));
            hero.Children.Add(Text("O pacote recomendado fica no Início. O restante é opcional e começa desmarcado. A conta fica só neste computador.", 14, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 0, 14)));
            foreach (var line in new[] { "Mouse, teclado e USB.", "Limpeza separada da otimização.", "Antivírus, Windows Update e a busca continuam ligados." })
                hero.Children.Add(Text(line, 13, Palette.Text, FontWeights.Normal, new Thickness(0, 3, 0, 0)));
            hero.Children.Add(Text("Versão de testes. O programa ainda não está pronto.", 12, Palette.Warn, FontWeights.Normal, new Thickness(0, 18, 0, 0)));
            var left = new Border { Background = Palette.Side, BorderBrush = Palette.Line, BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(40, 36, 32, 36), Child = hero };
            var card = new Border { Background = Palette.Card, BorderBrush = Palette.Line, BorderThickness = new Thickness(1), Margin = new Thickness(36, 48, 40, 48), Padding = new Thickness(24), VerticalAlignment = VerticalAlignment.Center };
            Round(card, 8);
            var form = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var seg = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            seg.ColumnDefinitions.Add(new ColumnDefinition());
            seg.ColumnDefinitions.Add(new ColumnDefinition());
            var tabLogin = Button("Entrar", false);
            var tabRegister = Button("Criar conta", false);
            tabRegister.Margin = new Thickness(8, 0, 0, 0);
            var modeRegister = false;
            var title = Text("Entrar neste PC", 20, Palette.Text, FontWeights.SemiBold, new Thickness(0, 0, 0, 6));
            var hint = Text("A conta fica só neste computador.", 13, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 0, 14));
            var name = Box();
            var pass = new PasswordBox { Height = 34, Background = Palette.Field, Foreground = Palette.Text, BorderBrush = Palette.Line, BorderThickness = new Thickness(1), Padding = new Thickness(8, 6, 70, 6), FontSize = 13, CaretBrush = Palette.Text, Template = InputTemplate(typeof(PasswordBox)) };
            var passShown = Box();
            passShown.Visibility = Visibility.Collapsed;
            var passHost = new Grid();
            passHost.Children.Add(pass);
            passHost.Children.Add(passShown);
            var show = Button("Mostrar", false);
            show.Height = 28;
            show.Padding = new Thickness(8, 0, 8, 0);
            show.HorizontalAlignment = HorizontalAlignment.Right;
            show.VerticalAlignment = VerticalAlignment.Center;
            show.Margin = new Thickness(0, 0, 6, 0);
            show.Click += delegate
            {
                if (pass.Visibility == Visibility.Visible)
                {
                    passShown.Text = pass.Password;
                    pass.Visibility = Visibility.Collapsed;
                    passShown.Visibility = Visibility.Visible;
                    show.Content = "Ocultar";
                }
                else
                {
                    pass.Password = passShown.Text;
                    passShown.Visibility = Visibility.Collapsed;
                    pass.Visibility = Visibility.Visible;
                    show.Content = "Mostrar";
                }
            };
            passHost.Children.Add(show);
            var remember = Button("Lembrar usuário e senha", false);
            remember.HorizontalAlignment = HorizontalAlignment.Left;
            remember.Margin = new Thickness(0, 8, 0, 8);
            var remembered = false;
            Action paintRemember = delegate
            {
                remember.Background = Brushes.Transparent;
                remember.Foreground = remembered ? Palette.Text : Palette.Muted;
                remember.BorderBrush = remembered ? Palette.Accent : Palette.Line;
                remember.BorderThickness = new Thickness(1);
            };
            paintRemember();
            remember.Click += delegate { remembered = !remembered; paintRemember(); };
            var error = Text("", 13, Palette.Danger, FontWeights.Normal, new Thickness(0, 4, 0, 8));
            var submit = Button("Entrar", true);
            submit.Height = 34;
            Action<bool> setMode = delegate(bool register)
            {
                modeRegister = register;
                tabLogin.Background = register ? Brushes.Transparent : Palette.Card2;
                tabLogin.Foreground = register ? Palette.Muted : Palette.Text;
                tabLogin.BorderBrush = register ? Palette.Line : Palette.Accent;
                tabRegister.Background = register ? Palette.Card2 : Brushes.Transparent;
                tabRegister.Foreground = register ? Palette.Text : Palette.Muted;
                tabRegister.BorderBrush = register ? Palette.Accent : Palette.Line;
                title.Text = register ? "Criar conta neste PC" : "Entrar neste PC";
                hint.Text = register ? "Use de 3 a 20 letras ou números e uma senha com pelo menos 6 caracteres." : "A conta fica só neste computador.";
                submit.Content = register ? "Criar conta" : "Entrar";
                error.Text = "";
            };
            tabLogin.Click += delegate { setMode(false); };
            tabRegister.Click += delegate { setMode(true); };
            Func<string> currentPass = delegate { return pass.Visibility == Visibility.Visible ? pass.Password : passShown.Text; };
            Action doSubmit = async delegate
            {
                error.Text = "";
                submit.IsEnabled = false;
                var typedName = name.Text;
                var typedPass = currentPass();
                string fail = null;
                try
                {
                    await Task.Run(delegate { fail = modeRegister ? Accounts.Register(typedName, typedPass) : Accounts.Login(typedName, typedPass); });
                }
                catch (Exception ex)
                {
                    fail = ex.Message;
                }
                submit.IsEnabled = true;
                if (fail != null) { error.Text = fail; return; }
                var saved = typedName.Trim().ToLowerInvariant();
                if (remembered) Accounts.Remember(saved, typedPass);
                else Accounts.Forget();
                Enter(saved);
            };
            submit.Click += delegate { doSubmit(); };
            name.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) doSubmit(); };
            pass.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) doSubmit(); };
            passShown.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) doSubmit(); };
            string recalledUser;
            string recalledPass;
            if (Accounts.TryRecall(out recalledUser, out recalledPass))
            {
                name.Text = recalledUser;
                pass.Password = recalledPass;
                remembered = true;
                paintRemember();
            }
            form.Children.Add(seg);
            Grid.SetColumn(tabLogin, 0);
            Grid.SetColumn(tabRegister, 1);
            seg.Children.Add(tabLogin);
            seg.Children.Add(tabRegister);
            form.Children.Add(title);
            form.Children.Add(hint);
            form.Children.Add(Label("Nome"));
            form.Children.Add(name);
            form.Children.Add(Label("Senha"));
            form.Children.Add(passHost);
            form.Children.Add(remember);
            form.Children.Add(error);
            form.Children.Add(submit);
            card.Child = form;
            Grid.SetColumn(card, 1);
            grid.Children.Add(left);
            grid.Children.Add(card);
            setMode(false);
            return grid;
        }

        Grid BuildApp()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SideW) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var side = new DockPanel { Background = Palette.Side };
            var brand = new StackPanel { Margin = new Thickness(18, 16, 16, 12) };
            brand.Children.Add(Text("Windows 10 e 11", 11, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 0, 8)));
            var chip = new Border { BorderBrush = Palette.Warn, BorderThickness = new Thickness(1), Padding = new Thickness(6, 2, 6, 2), HorizontalAlignment = HorizontalAlignment.Left };
            chip.Child = Text("VERSÃO DE TESTES", 10, Palette.Warn, FontWeights.SemiBold, new Thickness(0));
            brand.Children.Add(chip);
            DockPanel.SetDock(brand, Dock.Top);
            var foot = new StackPanel { Margin = new Thickness(18, 8, 16, 16) };
            who = Text("", 13, Palette.Muted, FontWeights.Normal, new Thickness(0));
            foot.Children.Add(new Border { Height = 1, Background = Palette.Line, Margin = new Thickness(0, 0, 0, 12) });
            foot.Children.Add(Text("Versão", 11, Palette.Muted, FontWeights.Normal, new Thickness(0)));
            foot.Children.Add(Text("2.0.4", 14, Palette.Text, FontWeights.SemiBold, new Thickness(0, 2, 0, 0)));
            DockPanel.SetDock(foot, Dock.Bottom);
            var menu = new StackPanel { Margin = new Thickness(10, 2, 10, 0) };
            foreach (var item in new[] {
                new[] { "home", "Início", "\uE80F" },
                new[] { "jogo", "Jogo", "\uE7FC" },
                new[] { "sistema", "Sistema", "\uE713" },
                new[] { "limpeza", "Limpeza", "\uE74D" },
                new[] { "apps", "Aplicativos", "\uE71D" },
                new[] { "reparar", "Reparar", "\uE90F" } })
            {
                var page = item[0];
                var b = NavButton(item[1], item[2]);
                b.Click += delegate { Navigate(page); };
                nav[page] = b;
                menu.Children.Add(b);
            }
            side.Children.Add(brand);
            side.Children.Add(foot);
            side.Children.Add(menu);
            var sideHost = new Border { Background = Palette.Side, BorderBrush = Palette.Line, BorderThickness = new Thickness(0, 0, 1, 0), Child = side };
            var host = new PageScroll(new Thickness(28, 22, 24, 28), Palette.Bg);
            host.Content = new Grid();
            Grid.SetColumn(host, 1);
            grid.Children.Add(sideHost);
            grid.Children.Add(host);
            grid.Tag = host;
            return grid;
        }

        Button NavButton(string label, string glyph)
        {
            var icon = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                Width = 22,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var caption = new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(icon);
            row.Children.Add(caption);
            var b = new Button
            {
                Content = row,
                Height = 36,
                Margin = new Thickness(8, 2, 8, 2),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                Foreground = Palette.Muted,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 0, 10, 0),
                FontWeight = FontWeights.Normal,
                Cursor = Cursors.Hand,
                Focusable = false,
                Tag = "off",
                Template = Flat()
            };
            b.MouseEnter += delegate { if ((string)b.Tag != "on") b.Background = Palette.Card; };
            b.MouseLeave += delegate { if ((string)b.Tag != "on") b.Background = Brushes.Transparent; };
            return b;
        }

        void Enter(string name)
        {
            user = name;
            if (who != null) who.Text = name;
            OpenApp();
        }

        void OpenApp()
        {
            login.Visibility = Visibility.Collapsed;
            app.Visibility = Visibility.Visible;
            SetChrome(true);
            Navigate("home");
        }

        static string PageTitle(string page)
        {
            if (page == "home") return "Início";
            if (page == "jogo") return "Jogo";
            if (page == "sistema") return "Sistema";
            if (page == "limpeza") return "Limpeza";
            if (page == "apps") return "Aplicativos";
            if (page == "reparar") return "Reparar";
            return "Nash Boost";
        }

        void Navigate(string page)
        {
            foreach (var pair in nav)
            {
                var on = pair.Key == page;
                pair.Value.Tag = on ? "on" : "off";
                pair.Value.Background = on ? Palette.Card2 : Brushes.Transparent;
                pair.Value.Foreground = on ? Palette.Ink : Palette.Muted;
                pair.Value.BorderBrush = on ? Palette.Accent : Brushes.Transparent;
                pair.Value.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
            }
            if (captionTitle != null) captionTitle.Text = PageTitle(page);
            UIElement view;
            if (!pages.TryGetValue(page, out view))
            {
                if (page == "home") view = PageHome();
                else if (page == "jogo") view = PageJogo();
                else if (page == "sistema") view = PageSistema();
                else if (page == "limpeza") view = PageLimpeza();
                else if (page == "apps") view = PageApps();
                else if (page == "reparar") view = PageReparar();
                else view = PageConta();
                pages[page] = view;
            }
            var host = (PageScroll)app.Tag;
            host.Content = view;
            if (page == "home" || page == "jogo") RefreshStatus();
            if (page == "apps" && !appsReady) LoadApps();
        }

        UIElement PageHome()
        {
            var root = new StackPanel();
            var head = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            head.Children.Add(Text("Início", 22, Palette.Ink, FontWeights.SemiBold, new Thickness(0, 0, 0, 4)));
            head.Children.Add(Text("O pacote recomendado fica aqui. Nas outras abas, nada começa marcado.", 13, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 0, 4)));
            hello = Text("Lendo este PC.", 13, Palette.Text, FontWeights.Normal, new Thickness(0));
            head.Children.Add(hello);
            head.Children.Add(new Border { Height = 1, Background = Palette.Line, Margin = new Thickness(0, 14, 0, 0) });
            root.Children.Add(head);
            var notice = new Border { Background = Palette.WarnBg, BorderBrush = Palette.Warn, BorderThickness = new Thickness(1), Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 0, 0, 16) };
            Round(notice, 6);
            notice.Child = Text("Versão de testes. O programa ainda não está pronto. Crie um ponto de restauração antes de aplicar.", 12, Palette.Warn, FontWeights.Normal, new Thickness(0));
            root.Children.Add(notice);
            TextBlock[] stats;
            root.Children.Add(MetricStrip(new[] { "Windows", "Memória", "Placa de vídeo", "Processador" }, out stats));
            winText = stats[0];
            ramText = stats[1];
            gpuText = stats[2];
            cpuText = stats[3];
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
            var hero = Card();
            hero.BorderBrush = Palette.Accent;
            hero.BorderThickness = new Thickness(1);
            hero.Padding = new Thickness(18, 16, 18, 16);
            var heroStack = new StackPanel();
            heroStack.Children.Add(Text("PACOTE RECOMENDADO", 11, Palette.Accent, FontWeights.SemiBold, new Thickness(0, 0, 0, 6)));
            heroStack.Children.Add(Text("Otimizar em 1 clique", 20, Palette.Ink, FontWeights.SemiBold, new Thickness(0, 0, 0, 8)));
            heroStack.Children.Add(Text("Mouse, teclado, USB, Game Bar, energia, rede, telemetria, interface, apps em segundo plano e prioridade do CS2 e do FiveM. Cria um ponto de restauração antes. A GPU acelerada fica de fora.", 13, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 0, 12)));
            foreach (var line in new[] { "Não remove seus programas, o Edge nem o OneDrive.", "Não mexe na integridade de memória. O Valorant precisa dela ligada.", "Não esvazia a RAM." })
                heroStack.Children.Add(Bullet(line));
            var go = Button("Otimizar agora", true);
            go.HorizontalAlignment = HorizontalAlignment.Left;
            go.MinWidth = 160;
            go.Margin = new Thickness(0, 16, 0, 0);
            go.Click += async delegate
            {
                if (!await Ask("Otimizar em 1 clique?", "Será criado um ponto de restauração e aplicado só o pacote recomendado.", new[] { "Mouse, teclado e USB", "Game Bar e prioridade de CS2 e FiveM", "Energia, rede, telemetria, interface e apps em segundo plano", "GPU acelerada, Edge, OneDrive e integridade de memória ficam de fora" }, "Confirmar", false)) return;
                await Run("Pacote aplicado", "Criando o ponto de restauração e gravando o pacote.", () => Tweaks.OneClick());
            };
            heroStack.Children.Add(go);
            hero.Child = heroStack;
            var state = Card();
            state.Padding = new Thickness(16, 16, 16, 14);
            var stateStack = new StackPanel();
            stateStack.Children.Add(Text("NESTE PC", 11, Palette.Muted, FontWeights.SemiBold, new Thickness(0, 0, 0, 4)));
            stateStack.Children.Add(Text("Estado atual", 16, Palette.Text, FontWeights.SemiBold, new Thickness(0, 0, 0, 12)));
            flags = new StackPanel();
            stateStack.Children.Add(flags);
            state.Child = stateStack;
            Grid.SetColumn(state, 2);
            row.Children.Add(hero);
            row.Children.Add(state);
            root.Children.Add(row);
            return root;
        }

        UIElement PageJogo()
        {
            var root = new StackPanel();
            root.Children.Add(PageHead("Jogo", "O que muda na mira, no teclado, na placa e no Valorant. Nada começa marcado. Os números acima são lidos do Windows."));
            TextBlock[] meters;
            root.Children.Add(MetricStrip(new[] { "Mouse", "Teclado", "USB", "Integridade" }, out meters));
            jogoMouse = meters[0];
            jogoKey = meters[1];
            jogoUsb = meters[2];
            jogoMem = meters[3];
            var card = Card();
            var list = new StackPanel();
            list.Children.Add(Row("Aceleração do mouse", "Desliga a aceleração e deixa a curva reta na escala desta tela. O ponteiro vai para a marca do meio.", game, "mouse", false, null));
            list.Children.Add(Row("Teclado imediato", "Atraso inicial em zero e sem o aviso de Sticky Keys.", game, "keyboard", false, null));
            list.Children.Add(Row("USB acordado na tomada", "Mouse e teclado não dormem. Na rede com fio, a placa deixa de economizar energia.", game, "network", false, null));
            list.Children.Add(Row("Gravação do Game Bar", "Para a captura em segundo plano e deixa o Modo de Jogo ligado.", game, "gamedvr", false, null));
            list.Children.Add(Row("GPU acelerada", "Em umas placas a latência cai, em outras a imagem engasga. Por isso não entra no 1 clique. Vale depois de reiniciar.", game, "hags", false, "Teste"));
            list.Children.Add(Row("Prioridade do CS2 e do FiveM", "Um nível acima do normal, e a GPU dedicada quando o notebook tem duas.", game, "games", false, null));
            list.Children.Add(Row("Ligar integridade de memória", "O Valorant fecha quando isso está desligado. Vale depois de reiniciar.", game, "memon", false, "Valorant"));
            card.Child = list;
            root.Children.Add(card);
            var bar = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
            var note = Text("O mouse muda na hora. Mexa o ponteiro depois de aplicar.", 13, Palette.Muted, FontWeights.Normal, new Thickness(0, 8, 12, 0));
            var apply = Button("Aplicar ajustes de jogo", true);
            apply.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(apply, Dock.Right);
            apply.Click += async delegate
            {
                var ids = On(game);
                if (ids.Count == 0) { Toast("Marque pelo menos um ajuste."); return; }
                if (!await Ask("Aplicar os ajustes de jogo?", ids.Contains("memon") ? "A integridade de memória será ligada. Reinicie depois." : "Só o que está marcado nesta página.", Names(ids), "Aplicar", false)) return;
                await Run("Ajustes de jogo", "Gravando mouse, teclado, placa e jogos.", () => Tweaks.Apply(ids, false));
            };
            bar.Children.Add(apply);
            bar.Children.Add(note);
            root.Children.Add(bar);
            return root;
        }

        UIElement PageSistema()
        {
            var root = new StackPanel();
            root.Children.Add(PageHead("Sistema", "Energia, o que o Windows coleta e o peso da interface. Nada começa marcado. Antivírus e Windows Update continuam ligados."));
            var card = Card();
            var list = new StackPanel();
            list.Children.Add(Row("Energia para jogos", "Plano equilibrado no modo Melhor desempenho. A CPU não fica presa em 100%, e o programa da frente tem prioridade.", system, "power", false, null));
            list.Children.Add(Row("Apps em segundo plano", "Apps da Store não ficam acordados, e os serviços do Windows se agrupam em menos processos. Steam, Discord e o que você abre continuam normais.", system, "background", false, null));
            list.Children.Add(Row("Telemetria e anúncios", "Desliga coleta de diagnóstico, sugestões, histórico de atividade, localização, o Copilot e o download de atualizações de outros PCs. A atualização continua.", system, "telemetry", false, null));
            list.Children.Add(Row("Interface mais leve", "Menos animação e transparência. No Windows 11, o menu do botão direito volta ao formato clássico e a barra ganha Encerrar tarefa.", system, "ui", false, null));
            list.Children.Add(Row("SysMain no Windows 10", "No Windows 10, para o serviço que fica lendo o disco. No Windows 11 ele continua ligado, porque nessa versão ajuda a abrir programas.", system, "sysmain", false, "Só Windows 10"));
            card.Child = list;
            root.Children.Add(card);
            var apply = Button("Aplicar ajustes de sistema", true);
            apply.HorizontalAlignment = HorizontalAlignment.Right;
            apply.Margin = new Thickness(0, 14, 0, 0);
            apply.Click += async delegate
            {
                var ids = On(system);
                if (ids.Count == 0) { Toast("Marque pelo menos um ajuste."); return; }
                if (!await Ask("Aplicar os ajustes de sistema?", "Só o que está marcado nesta página.", Names(ids), "Aplicar", false)) return;
                await Run("Ajustes de sistema", "Gravando energia, segundo plano, telemetria e interface.", () => Tweaks.Apply(ids, false));
            };
            root.Children.Add(apply);
            return root;
        }

        UIElement PageLimpeza()
        {
            var root = new StackPanel();
            root.Children.Add(PageHead("Limpeza", "Nada começa marcado. Isso libera espaço ou RAM. Não sobe FPS sozinho."));
            lastClean = Text("Última limpeza: ainda não foi feita por aqui.", 13, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 0, 12));
            root.Children.Add(lastClean);
            var card = Card();
            var list = new StackPanel();
            list.Children.Add(GroupLabel("Espaço em disco"));
            list.Children.Add(Row("Arquivos temporários", "Remove temporários do Windows e do usuário.", clean, "temp", false, null));
            list.Children.Add(Row("Lixeira", "Apaga o que está na lixeira de todas as unidades.", clean, "recycle", false, null));
            list.Children.Add(Row("Cache do Windows Update", "Remove instaladores já baixados. A atualização continua.", clean, "wu", false, null));
            list.Children.Add(Row("Miniaturas", "As miniaturas do Explorador são recriadas depois.", clean, "thumbs", false, null));
            list.Children.Add(Row("Logs do Valorant", "Apaga logs e crashes. Configuração e sensibilidade ficam.", clean, "valorant", false, null));
            list.Children.Add(Row("Cache do FiveM", "Use se o FiveM estiver travando. A próxima entrada baixa os arquivos de novo.", clean, "fivem", false, "Redownload"));
            list.Children.Add(Row("Vídeos do Game Bar", "Apaga os clipes gravados na pasta Vídeos. Não mexe em outras pastas.", clean, "captures", false, null));
            list.Children.Add(GroupLabel("Memória"));
            list.Children.Add(Row("Preparar para jogar", "Fecha papel de parede animado, se estiver aberto, e libera memória no modo suave. Não fecha launcher, Spotify, navegador, Discord, Steam nem o jogo.", clean, "play", false, null));
            list.Children.Add(Row("Limpar memória, modo suave", "Igual ao limpador antigo: fecha só programa inútil e solta a memória do resto. O que está na frente fica, junto com Valorant, CS2, FiveM, GTA, anti-cheat, Discord, Steam e OBS.", clean, "ram", false, null));
            list.Children.Add(Row("Limpar memória, modo completo", "Libera o cache todo. Pode engasgar um instante.", clean, "ramfull", false, "Pode engasgar"));
            list.Children.Add(Row("Atalho CTRL+ALT+R", "Cria um atalho na área de trabalho. O atalho de teclado limpa no modo suave, sem abrir a janela, inclusive dentro do jogo.", clean, "ramkey", false, null));
            card.Child = list;
            root.Children.Add(card);
            var apply = Button("Limpar selecionados", true);
            apply.HorizontalAlignment = HorizontalAlignment.Right;
            apply.Margin = new Thickness(0, 14, 0, 0);
            apply.Click += async delegate
            {
                var ids = On(clean);
                if (ids.Count == 0) { Toast("Marque pelo menos um item."); return; }
                var hasRam = ids.Contains("ram") || ids.Contains("ramfull");
                var onlyRam = true;
                foreach (var id in ids)
                    if (id != "ram" && id != "ramfull" && id != "ramkey") onlyRam = false;
                var hint = hasRam && onlyRam ? "Libera RAM na hora. Não aumenta FPS e não apaga arquivos." : hasRam ? "Os arquivos saem de vez. A RAM liberada é na hora e não aumenta FPS." : "A limpeza libera espaço. Ela não aumenta FPS sozinha.";
                if (!await Ask("Limpar os itens marcados?", hint, Names(ids), "Limpar", false)) return;
                await Run("Limpeza concluída", hasRam ? "Liberando memória e o que mais estiver marcado." : "Apagando os arquivos marcados.", () => Tweaks.Clean(ids));
            };
            root.Children.Add(apply);
            return root;
        }

        UIElement PageApps()
        {
            var root = new StackPanel();
            root.Children.Add(PageHead("Aplicativos", "O que o Windows colocou sozinho, e os programas que abrem junto com o PC. Nada começa marcado. Spotify, Discord, Steam, a Loja, o Paint e o Game Bar não entram na remoção."));
            appSummary = Text("Lendo os aplicativos deste PC…", 13, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 0, 14));
            root.Children.Add(appSummary);
            root.Children.Add(Section("Instalados agora"));
            var installed = Card();
            installedHost = new StackPanel();
            installedHost.Children.Add(Text("Lendo…", 14, Palette.Muted, FontWeights.Normal, new Thickness(0)));
            installed.Child = installedHost;
            root.Children.Add(installed);
            var oneRow = new StackPanel { Orientation = Orientation.Horizontal };
            onedrive = new SwitchBox(false);
            oneRow.Children.Add(onedrive);
            oneRow.Children.Add(Text("Remover o OneDrive também. Não apaga arquivos que já estão na pasta.", 13, Palette.Text, FontWeights.Normal, new Thickness(12, 1, 0, 0)));
            var oneHost = new Border { Background = Palette.Card, BorderBrush = Palette.Line, BorderThickness = new Thickness(1), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 10, 0, 18), Child = oneRow };
            Round(oneHost, 8);
            root.Children.Add(oneHost);
            root.Children.Add(Section("Abrem com o Windows"));
            root.Children.Add(Text("Desligar tira o programa da inicialização. Ele continua instalado. Antivírus e o teclado do Windows ficam protegidos.", 13, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 0, 8)));
            var startup = Card();
            startupHost = new StackPanel();
            startupHost.Children.Add(Text("Lendo…", 14, Palette.Muted, FontWeights.Normal, new Thickness(0)));
            startup.Child = startupHost;
            root.Children.Add(startup);
            var catalog = Card();
            catalog.Margin = new Thickness(0, 8, 0, 0);
            catalog.Visibility = Visibility.Collapsed;
            catalogHost = new StackPanel();
            catalog.Child = catalogHost;
            var fold = Button("Mostrar lista completa", false);
            fold.HorizontalAlignment = HorizontalAlignment.Left;
            fold.Margin = new Thickness(0, 14, 0, 0);
            fold.Click += delegate
            {
                var open = catalog.Visibility != Visibility.Visible;
                catalog.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                fold.Content = open ? "Ocultar lista completa" : "Mostrar lista completa";
            };
            root.Children.Add(fold);
            root.Children.Add(catalog);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var reload = Button("Atualizar", false);
            reload.Margin = new Thickness(0, 0, 8, 0);
            reload.Click += delegate { appsReady = false; LoadApps(); };
            var mark = Button("Marcar instalados", false);
            mark.Margin = new Thickness(0, 0, 8, 0);
            mark.Click += delegate { foreach (var pick in picks) pick.Box.SetOn(pick.App.Installed || pick.App.Provisioned); };
            var heavy = Button("Tirar papel de parede", false);
            heavy.Margin = new Thickness(0, 0, 8, 0);
            heavy.Click += async delegate
            {
                if (!await Ask("Tirar o papel de parede da inicialização?", "Se houver Wallpaper Engine, Lively ou parecido, ele deixa de abrir com o Windows. Continua instalado. O resto você desliga na lista, um por um.", new[] { "Só papel de parede animado", "Launcher, Spotify, Discord e Steam ficam" }, "Tirar", false)) return;
                await Run("Inicialização", "Tirando o papel de parede da abertura.", () => Tweaks.HeavyBoot());
                appsReady = false;
                LoadApps();
            };
            var remove = Button("Remover selecionados", true);
            remove.Click += async delegate
            {
                var ids = picks.Where(p => p.Box.IsOn).Select(p => p.App.Id).ToList();
                if (ids.Count == 0 && !onedrive.IsOn) { Toast("Marque pelo menos um app."); return; }
                var names = picks.Where(p => ids.Contains(p.App.Id)).Select(p => p.App.Name).ToList();
                if (onedrive.IsOn) names.Add("OneDrive");
                if (!await Ask("Remover estes apps?", "Dá para instalar de novo pela Microsoft Store.", names, "Remover", false)) return;
                await Run("Aplicativos", "Removendo os apps marcados.", () => Tweaks.RemoveApps(ids, onedrive.IsOn));
                appsReady = false;
                LoadApps();
            };
            actions.Children.Add(reload);
            actions.Children.Add(mark);
            actions.Children.Add(heavy);
            actions.Children.Add(remove);
            root.Children.Add(actions);
            return root;
        }

        UIElement PageReparar()
        {
            var root = new StackPanel();
            root.Children.Add(PageHead("Reparar", "Desfaz o que a versão antiga piorava e guarda um ponto antes de mudanças grandes."));
            root.Children.Add(ActionCard("Ponto de restauração", "Crie antes de qualquer mudança grande. O 1 clique também tenta criar um.", "Criar agora", async delegate
            {
                await Run("Restauração", "Criando o ponto de restauração.", () => Tweaks.RestorePoint());
            }));
            root.Children.Add(ActionCard("Corrigir ajustes ruins", "Religa o SysMain, a otimização de tela cheia, o timer dinâmico e a sobreposição da imagem. Tira o plano que deixava a CPU em 100%.", "Corrigir", async delegate
            {
                await Run("Correção", "Restaurando os ajustes que atrapalhavam.", () => Tweaks.Repair());
            }));
            root.Children.Add(ActionCard("Prioridade dos jogos", "Remove a prioridade do CS2, do FiveM e do GTA.", "Remover", async delegate
            {
                await Run("Prioridades", "Removendo a prioridade dos jogos.", () => Tweaks.Unpriority());
            }));
            root.Children.Add(ActionCard("Ligar integridade de memória", "O Valorant fecha quando isso está desligado. Liga a proteção e pede reinício.", "Ligar agora", async delegate
            {
                if (!await Ask("Ligar a integridade de memória?", "O Valorant precisa dela. O PC deve ser reiniciado depois.", new string[0], "Ligar", false)) return;
                await Run("Integridade de memória", "Marcando a proteção para ligar.", () => Tweaks.Memory(true));
            }, true));
            root.Children.Add(Text("AVANÇADO", 11, Palette.Warn, FontWeights.SemiBold, new Thickness(0, 8, 0, 8)));
            var advanced = new Dictionary<string, SwitchBox>();
            var card = Card();
            var list = new StackPanel();
            list.Children.Add(Row("Desligar integridade de memória", "Pode subir um pouco o FPS em CPU antiga, mas o Valorant fecha. Não mexe em TPM nem Secure Boot.", advanced, "mem", false, "Cuidado"));
            list.Children.Add(Row("Remover o navegador Edge", "Não aumenta FPS. O componente que outros programas usam fica.", advanced, "edge", false, "Cuidado"));
            list.Children.Add(Row("Corrigir engasgo da imagem", "Desliga uma sobreposição que pisca ou trava em algumas placas. Não aumenta FPS. Vale depois de reiniciar.", advanced, "mpo", false, "Só se engasgar"));
            card.Child = list;
            root.Children.Add(card);
            var apply = Button("Aplicar avançado", false);
            apply.HorizontalAlignment = HorizontalAlignment.Right;
            apply.Margin = new Thickness(0, 14, 0, 0);
            apply.Click += async delegate
            {
                var ids = On(advanced);
                if (ids.Count == 0) { Toast("Marque pelo menos uma opção avançada."); return; }
                var hint = ids.Contains("mem") ? "Desligar a integridade de memória faz o Valorant fechar." : ids.Contains("edge") ? "O Edge será removido. O componente que outros programas usam fica." : "A sobreposição da imagem será desligada. Reinicie depois.";
                if (!await Ask("Aplicar opções avançadas?", hint, Names(ids), "Aplicar", false)) return;
                await Run("Avançado", "Gravando a opção avançada.", () => Tweaks.Apply(ids, false));
            };
            root.Children.Add(apply);
            return root;
        }

        UIElement PageConta()
        {
            var root = new StackPanel();
            root.Children.Add(Text("Conta", 20, Palette.Text, FontWeights.SemiBold, new Thickness(0)));
            root.Children.Add(Text("A conta fica só neste PC. Não há servidor na internet.", 14, Palette.Muted, FontWeights.Normal, new Thickness(0, 4, 0, 14)));
            var card = Card();
            card.Padding = new Thickness(16, 14, 16, 8);
            var stack = new StackPanel();
            stack.Children.Add(Text("Trocar senha", 16, Palette.Text, FontWeights.SemiBold, new Thickness(0, 0, 0, 10)));
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var oldPass = new PasswordBox { Height = 34, Background = Palette.Field, Foreground = Palette.Text, BorderBrush = Palette.Line, BorderThickness = new Thickness(1), Padding = new Thickness(8, 6, 8, 6), CaretBrush = Palette.Text, Template = InputTemplate(typeof(PasswordBox)) };
            var newPass = new PasswordBox { Height = 34, Background = Palette.Field, Foreground = Palette.Text, BorderBrush = Palette.Line, BorderThickness = new Thickness(1), Padding = new Thickness(8, 6, 8, 6), CaretBrush = Palette.Text, Template = InputTemplate(typeof(PasswordBox)) };
            var change = Button("Trocar senha", false);
            change.Click += delegate
            {
                var fail = Accounts.ChangePassword(user, oldPass.Password, newPass.Password);
                if (fail != null) Toast(fail);
                else
                {
                    oldPass.Password = "";
                    newPass.Password = "";
                    Toast("Senha alterada.");
                }
            };
            var left = new StackPanel();
            left.Children.Add(Label("Senha atual"));
            left.Children.Add(oldPass);
            var mid = new StackPanel();
            mid.Children.Add(Label("Nova senha"));
            mid.Children.Add(newPass);
            Grid.SetColumn(mid, 2);
            Grid.SetColumn(change, 4);
            change.VerticalAlignment = VerticalAlignment.Bottom;
            row.Children.Add(left);
            row.Children.Add(mid);
            row.Children.Add(change);
            stack.Children.Add(row);
            card.Child = stack;
            root.Children.Add(card);
            var notes = Card();
            notes.Padding = new Thickness(16, 14, 16, 14);
            var noteStack = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
            notes.Margin = new Thickness(0, 14, 0, 0);
            noteStack.Children.Add(Text("O que este programa não faz", 16, Palette.Text, FontWeights.SemiBold, new Thickness(0, 0, 0, 8)));
            foreach (var line in new[] {
                "Não desliga antivírus, Windows Update, busca ou SysMain.",
                "A limpeza de RAM fica na aba Limpeza. Ela não aumenta FPS.",
                "Não trava o processador em 100% nem desliga a otimização de tela cheia.",
                "Não baixa programa da NVIDIA. Use o Reflex dentro do jogo." })
                noteStack.Children.Add(Text("–  " + line, 14, Palette.Text, FontWeights.Normal, new Thickness(0, 3, 0, 0)));
            var open = Button("Abrir pasta do registro", false);
            open.HorizontalAlignment = HorizontalAlignment.Left;
            open.Margin = new Thickness(0, 14, 0, 0);
            open.Click += delegate { Tweaks.OpenData(); Toast("Pasta aberta."); };
            noteStack.Children.Add(open);
            notes.Child = noteStack;
            root.Children.Add(notes);
            return root;
        }

        async void RefreshStatus()
        {
            PcStatus status = null;
            await Task.Run(() => { try { status = Tweaks.ReadStatus(); } catch { } });
            if (status == null) return;
            var captured = status;
#pragma warning disable 4014
            Dispatcher.BeginInvoke(new Action(delegate
            {
                try { PaintStatus(captured); }
                catch { }
            }));
#pragma warning restore 4014
        }

        void PaintStatus(PcStatus status)
        {
            if (winText != null) { winText.Text = status.Windows; winText.ToolTip = status.Windows; }
            if (ramText != null) { ramText.Text = status.Ram; ramText.ToolTip = status.Ram; }
            if (gpuText != null) { gpuText.Text = string.IsNullOrEmpty(status.Gpu) ? "—" : status.Gpu; gpuText.ToolTip = gpuText.Text; }
            if (cpuText != null) { cpuText.Text = string.IsNullOrEmpty(status.Cpu) ? "—" : status.Cpu; cpuText.ToolTip = cpuText.Text; }
            if (hello != null)
            {
                var ready = status.Admin ? "Pronto para aplicar." : "Abra de novo e aceite o pedido de administrador.";
                hello.Text = string.IsNullOrEmpty(user) ? ready : "Olá, " + user + ". " + ready;
            }
            if (lastClean != null) lastClean.Text = string.IsNullOrEmpty(status.LastClean) ? "Última limpeza: ainda não foi feita por aqui." : "Última limpeza: " + status.LastClean;
            if (flags != null)
            {
                flags.Children.Clear();
                Flag(flags, "Mouse sem aceleração", status.MouseOk, "Aplicado", "Pendente");
                Flag(flags, "Teclado sem atraso", status.KeyOk, "Aplicado", "Pendente");
                Flag(flags, "USB sem suspensão", status.UsbOk, "Aplicado", "Pendente");
                Flag(flags, "Game Bar sem gravação", status.DvrOk, "Aplicado", "Pendente");
                Flag(flags, "Modo de jogo", status.GameModeOk, "Ligado", "Desligado");
                Flag(flags, "GPU acelerada", status.HagsOk, "Aplicado", "Pendente");
                Flag(flags, "Integridade de memória", status.MemoryOk, "Ligada", "Desligada");
                Flag(flags, "Apps em segundo plano", status.BackgroundOk, "Contidos", "Livres");
                Flag(flags, "Telemetria reduzida", status.TelemetryOk, "Aplicado", "Pendente");
            }
            if (jogoMouse != null) jogoMouse.Text = status.MouseOk ? "1:1 ativo" : "Aceleração ligada";
            if (jogoKey != null) jogoKey.Text = status.KeyOk ? "Atraso em 0" : "Com espera";
            if (jogoUsb != null) jogoUsb.Text = status.UsbOk ? "Acordado" : "Pode dormir";
            if (jogoMem != null) jogoMem.Text = status.MemoryOk ? "Ligada" : "Desligada";
        }

        async void LoadApps()
        {
            if (installedHost == null) return;
            installedHost.Children.Clear();
            installedHost.Children.Add(Text("Lendo os aplicativos e a inicialização…", 14, Palette.Muted, FontWeights.Normal, new Thickness(0)));
            startupHost.Children.Clear();
            List<StoreApp> apps = null;
            List<StartupItem> startup = null;
            string error = null;
            await Task.Run(() =>
            {
                try
                {
                    apps = Tweaks.ScanApps();
                    startup = Tweaks.ReadStartup();
                }
                catch (Exception ex) { error = ex.Message; }
            });
            if (error != null)
            {
                installedHost.Children.Clear();
                installedHost.Children.Add(Text(error, 14, Palette.Danger, FontWeights.Normal, new Thickness(0)));
                return;
            }
            picks.Clear();
            var installed = apps.Where(a => a.Installed || a.Provisioned).ToList();
            var starting = startup.Count(s => s.Enabled && !s.Locked);
            appSummary.Text = installed.Count + " neste PC  ·  " + apps.Count(a => !a.Installed && !a.Provisioned) + " já fora  ·  " + starting + " abrem com o Windows";
            installedHost.Children.Clear();
            if (installed.Count == 0)
                installedHost.Children.Add(Text("Nenhum app de fábrica está instalado. Os seus programas não aparecem aqui.", 14, Palette.Muted, FontWeights.Normal, new Thickness(0)));
            else
            {
                foreach (var group in installed.Select(a => a.Group).Distinct())
                {
                    installedHost.Children.Add(Text(group, 13, Palette.Text, FontWeights.SemiBold, new Thickness(0, 10, 0, 4)));
                    foreach (var appItem in installed.Where(a => a.Group == group))
                    {
                        var sw = new SwitchBox(false);
                        picks.Add(new AppPick { App = appItem, Box = sw });
                        installedHost.Children.Add(InfoRow(appItem.Name, appItem.Desc, appItem.Installed ? "Instalado" : "Provisionado", sw));
                    }
                }
            }
            startupHost.Children.Clear();
            if (startup.Count == 0)
                startupHost.Children.Add(Text("Nenhum programa extra abre com o Windows.", 14, Palette.Muted, FontWeights.Normal, new Thickness(0)));
            else
            {
                foreach (var item in startup)
                {
                    var sw = new SwitchBox(item.Enabled);
                    sw.IsEnabled = !item.Locked;
                    sw.SetOn(item.Enabled && !item.Locked ? item.Enabled : item.Enabled);
                    if (item.Locked) sw.IsEnabled = false;
                    var captured = item;
                    sw.MouseLeftButtonUp += async delegate
                    {
                        var turnOn = sw.IsOn;
                        if (captured.Locked) return;
                        if (busyOn)
                        {
                            sw.SetOn(!turnOn);
                            return;
                        }
                        var ok = await RunQuiet("Inicialização", turnOn ? "Ativando " + captured.Name + "." : "Tirando " + captured.Name + " da inicialização.", delegate
                        {
                            var fail = Tweaks.SetStartup(captured.Id, turnOn);
                            if (fail != null) throw new InvalidOperationException(fail);
                            return new JobResult();
                        });
                        if (!ok)
                        {
                            sw.SetOn(!turnOn);
                            return;
                        }
                        Toast(turnOn ? captured.Name + " vai abrir com o Windows." : captured.Name + " não abre mais com o Windows. Vale na próxima inicialização.");
                    };
                    startupHost.Children.Add(InfoRow(item.Name + "  ·  " + item.Scope + (item.Locked ? "  ·  Protegido" : ""), item.Command, null, sw, true));
                }
            }
            catalogHost.Children.Clear();
            foreach (var group in apps.Select(a => a.Group).Distinct())
            {
                catalogHost.Children.Add(Text(group, 13, Palette.Text, FontWeights.SemiBold, new Thickness(0, 10, 0, 4)));
                foreach (var appItem in apps.Where(a => a.Group == group))
                {
                    var state = appItem.Installed ? "Instalado" : appItem.Provisioned ? "Provisionado" : "Fora";
                    catalogHost.Children.Add(InfoRow(appItem.Name, appItem.Desc, state, null));
                }
            }
            appsReady = true;
        }

        async Task Run(string title, string busyLine, Func<JobResult> work)
        {
            if (busyOn) return;
            if (!Tweaks.IsAdmin())
            {
                Toast("O Nash Boost não está como administrador. Feche e abra de novo, e aceite o aviso do Windows.");
                return;
            }
            busyOn = true;
            ShowBusy(title, busyLine);
            JobResult result = null;
            Exception error = null;
            await Task.Run(() => { try { result = work(); } catch (Exception ex) { error = ex; } });
            HideBusy();
            busyOn = false;
            if (error != null) { Toast(Friendly(error)); return; }
            await Ask(title, result.Reboot ? "Parte disso só vale depois de reiniciar." : "Concluído.", result.Lines, "Fechar", true);
            if (result.Reboot && await Ask("Reiniciar agora?", "O PC reinicia em 15 segundos. Salve o que estiver aberto.", new string[0], "Reiniciar", false))
                Tweaks.Reboot();
            Tweaks.DropQuickCache();
            RefreshStatus();
        }

        async Task<bool> RunQuiet(string title, string busyLine, Func<JobResult> work)
        {
            if (busyOn) return false;
            if (!Tweaks.IsAdmin())
            {
                Toast("O Nash Boost não está como administrador. Feche e abra de novo, e aceite o aviso do Windows.");
                return false;
            }
            busyOn = true;
            ShowBusy(title, busyLine);
            Exception error = null;
            await Task.Run(delegate { try { work(); } catch (Exception ex) { error = ex; } });
            HideBusy();
            busyOn = false;
            if (error != null)
            {
                Toast(Friendly(error));
                return false;
            }
            return true;
        }

        static string Friendly(Exception error)
        {
            var msg = error == null || error.Message == null ? "" : error.Message;
            if (error is UnauthorizedAccessException || error is System.Security.SecurityException
                || msg.IndexOf("não autorizada", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0)
                return "O Windows recusou a operação. Feche o Nash Boost e abra de novo aceitando o pedido de administrador.";
            return msg;
        }

        void ShowBusy(string title, string text)
        {
            busyTitle.Text = title;
            busyText.Text = text;
            busy.Visibility = Visibility.Visible;
        }

        void HideBusy() { busy.Visibility = Visibility.Collapsed; }

        Task<bool> Ask(string title, string text, IList<string> lines, string ok, bool hideCancel)
        {
            modalWait = new TaskCompletionSource<bool>();
            modalTitle.Text = title;
            modalText.Text = text ?? "";
            modalList.Children.Clear();
            if (lines != null)
                foreach (var line in lines)
                    modalList.Children.Add(Text("–  " + line, 14, Palette.Text, FontWeights.Normal, new Thickness(0, 3, 0, 0)));
            modalOk.Content = ok;
            modalOk.IsEnabled = true;
            modalCancel.IsEnabled = true;
            modalCancel.Visibility = hideCancel ? Visibility.Collapsed : Visibility.Visible;
            modal.Visibility = Visibility.Visible;
            modalOk.Focus();
            return modalWait.Task;
        }

        void CloseModal(bool ok)
        {
            modal.Visibility = Visibility.Collapsed;
            if (modalWait != null) modalWait.TrySetResult(ok);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && modal.Visibility == Visibility.Visible)
            {
                if (modalCancel.Visibility == Visibility.Visible) CloseModal(false);
                else CloseModal(true);
            }
        }

        Grid BuildBusy()
        {
            var grid = new Grid { Background = new SolidColorBrush(Color.FromArgb(160, 10, 10, 12)), Visibility = Visibility.Collapsed };
            var card = new Border { Background = Palette.Card, BorderBrush = Palette.Accent, BorderThickness = new Thickness(1), Padding = new Thickness(22, 18, 22, 20), Width = 440, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Round(card, 8);
            var stack = new StackPanel();
            stack.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 3, Margin = new Thickness(0, 0, 0, 14), Foreground = Palette.Accent, Background = Palette.Card2, BorderThickness = new Thickness(0) });
            busyTitle = Text("Aplicando", 18, Palette.Text, FontWeights.SemiBold, new Thickness(0, 0, 0, 6));
            busyText = Text("", 14, Palette.Muted, FontWeights.Normal, new Thickness(0));
            stack.Children.Add(busyTitle);
            stack.Children.Add(busyText);
            card.Child = stack;
            grid.Children.Add(card);
            return grid;
        }

        Grid BuildModal()
        {
            var grid = new Grid { Background = new SolidColorBrush(Color.FromArgb(160, 10, 10, 12)), Visibility = Visibility.Collapsed };
            var card = new Border { Background = Palette.Card, BorderBrush = Palette.Line, BorderThickness = new Thickness(1), Padding = new Thickness(22, 18, 22, 18), Width = 520, MaxHeight = 560, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Round(card, 8);
            var stack = new DockPanel();
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            modalCancel = Button("Cancelar", false);
            modalCancel.Margin = new Thickness(0, 0, 8, 0);
            modalCancel.MinWidth = 120;
            modalOk = Button("Confirmar", true);
            modalOk.MinWidth = 120;
            modalCancel.Click += delegate { CloseModal(false); };
            modalOk.Click += delegate { CloseModal(true); };
            buttons.Children.Add(modalCancel);
            buttons.Children.Add(modalOk);
            DockPanel.SetDock(buttons, Dock.Bottom);
            modalTitle = Text("", 18, Palette.Text, FontWeights.SemiBold, new Thickness(0, 0, 0, 8));
            modalText = Text("", 14, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 0, 8));
            modalList = new StackPanel();
            var scroll = new PageScroll(new Thickness(0, 0, 16, 0), Brushes.Transparent);
            scroll.Cap(280);
            scroll.Content = modalList;
            var body = new StackPanel();
            body.Children.Add(modalTitle);
            body.Children.Add(modalText);
            body.Children.Add(scroll);
            stack.Children.Add(buttons);
            stack.Children.Add(body);
            card.Child = stack;
            grid.Children.Add(card);
            return grid;
        }

        void Toast(string text)
        {
            var item = new Border { Background = Palette.Card2, BorderBrush = Palette.Accent, BorderThickness = new Thickness(1), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 6, 0, 0), MaxWidth = 380, Child = Text(text, 13, Palette.Text, FontWeights.Normal, new Thickness(0)) };
            Round(item, 6);
            toasts.Children.Add(item);
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += delegate { timer.Stop(); toasts.Children.Remove(item); };
            timer.Start();
        }

        static List<string> On(Dictionary<string, SwitchBox> map)
        {
            return map.Where(p => p.Value.IsOn).Select(p => p.Key).ToList();
        }

        static List<string> Names(IList<string> ids)
        {
            var map = new Dictionary<string, string> {
                { "mouse", "Aceleração do mouse" }, { "keyboard", "Teclado imediato" }, { "network", "USB acordado" },
                { "gamedvr", "Game Bar sem gravação" }, { "hags", "GPU acelerada" }, { "games", "Prioridade dos jogos" },
                { "memon", "Ligar integridade de memória" }, { "power", "Energia para jogos" }, { "telemetry", "Telemetria e anúncios" },
                { "ui", "Interface mais leve" }, { "background", "Apps em segundo plano" }, { "sysmain", "SysMain no Windows 10" }, { "mpo", "Corrigir engasgo da imagem" },
                { "mem", "Desligar integridade de memória" }, { "edge", "Remover o Edge" },
                { "temp", "Arquivos temporários" }, { "recycle", "Lixeira" }, { "wu", "Cache do Windows Update" },
                { "thumbs", "Miniaturas" }, { "valorant", "Logs do Valorant" }, { "fivem", "Cache do FiveM" },
                { "captures", "Vídeos do Game Bar" },
                { "ram", "Limpar memória, modo suave" }, { "ramfull", "Limpar memória, modo completo" }, { "ramkey", "Atalho CTRL+ALT+R" },
                { "play", "Preparar para jogar" }
            };
            return ids.Select(id => map.ContainsKey(id) ? map[id] : id).ToList();
        }

        static StackPanel PageHead(string title, string sub)
        {
            var head = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            head.Children.Add(Text(title, 22, Palette.Ink, FontWeights.SemiBold, new Thickness(0, 0, 0, 4)));
            if (!string.IsNullOrEmpty(sub))
                head.Children.Add(Text(sub, 13, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 0, 12)));
            head.Children.Add(new Border { Height = 1, Background = Palette.Line });
            return head;
        }

        static TextBlock Section(string title)
        {
            return Text(title.ToUpperInvariant(), 11, Palette.Muted, FontWeights.SemiBold, new Thickness(0, 2, 0, 8));
        }

        static StackPanel Bullet(string line)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 0) };
            item.Children.Add(new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = Palette.Accent, Margin = new Thickness(0, 5, 10, 0), VerticalAlignment = VerticalAlignment.Top });
            item.Children.Add(Text(line, 13, Palette.Text, FontWeights.Normal, new Thickness(0)));
            return item;
        }

        static Border GroupLabel(string title)
        {
            var row = new DockPanel { Margin = new Thickness(14, 10, 14, 8) };
            var mark = new Border { Width = 3, Height = 12, CornerRadius = new CornerRadius(1.5), Background = Palette.Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(mark, Dock.Left);
            row.Children.Add(mark);
            row.Children.Add(Text(title.ToUpperInvariant(), 11, Palette.Muted, FontWeights.SemiBold, new Thickness(0, 1, 0, 0)));
            return new Border { Background = Palette.Side, BorderBrush = Palette.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = row };
        }

        static Border MetricStrip(string[] captions, out TextBlock[] values)
        {
            var grid = new UniformGrid { Columns = captions.Length };
            values = new TextBlock[captions.Length];
            for (var i = 0; i < captions.Length; i++)
            {
                var cell = new Border
                {
                    Padding = new Thickness(14, 12, 12, 12),
                    BorderBrush = Palette.Line,
                    BorderThickness = i == captions.Length - 1 ? new Thickness(0) : new Thickness(0, 0, 1, 0)
                };
                var stack = new StackPanel();
                stack.Children.Add(Text(captions[i].ToUpperInvariant(), 11, Palette.Muted, FontWeights.SemiBold, new Thickness(0, 0, 0, 6)));
                var value = Text("…", 14, Palette.Ink, FontWeights.SemiBold, new Thickness(0));
                value.TextWrapping = TextWrapping.NoWrap;
                value.TextTrimming = TextTrimming.CharacterEllipsis;
                stack.Children.Add(value);
                cell.Child = stack;
                grid.Children.Add(cell);
                values[i] = value;
            }
            var strip = new Border { Background = Palette.Card, BorderBrush = Palette.Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 16), Child = grid };
            Round(strip, 8);
            return strip;
        }

        static void Flag(Panel parent, string name, bool ok, string on, string off)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var mark = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = ok ? Palette.Accent : Palette.Line, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            var label = Text(name, 13, Palette.Text, FontWeights.Normal, new Thickness(0));
            var state = Text(ok ? on : off, 12, ok ? Palette.Ink : Palette.Muted, FontWeights.SemiBold, new Thickness(8, 0, 0, 0));
            Grid.SetColumn(label, 1);
            Grid.SetColumn(state, 2);
            row.Children.Add(mark);
            row.Children.Add(label);
            row.Children.Add(state);
            parent.Children.Add(row);
        }

        static Border Card()
        {
            var card = new Border { Background = Palette.Card, BorderBrush = Palette.Line, BorderThickness = new Thickness(1), Padding = new Thickness(0) };
            Round(card, 8);
            return card;
        }

        static void Round(Border border, double radius)
        {
            border.CornerRadius = new CornerRadius(radius);
            border.SizeChanged += delegate { ApplyClip(border); };
        }

        static void ApplyClip(Border border)
        {
            if (border == null || border.ActualWidth <= 0 || border.ActualHeight <= 0) return;
            var radius = border.CornerRadius.TopLeft;
            border.Clip = radius <= 0 ? null : new RectangleGeometry(new Rect(0, 0, border.ActualWidth, border.ActualHeight), radius, radius);
        }

        static ControlTemplate InputTemplate(Type type)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var host = new FrameworkElementFactory(typeof(ScrollViewer));
            host.Name = "PART_ContentHost";
            border.AppendChild(host);
            var template = new ControlTemplate(type);
            template.VisualTree = border;
            return template;
        }

        static Border Row(string title, string desc, Dictionary<string, SwitchBox> map, string id, bool on, string badge)
        {
            var sw = new SwitchBox(on);
            map[id] = sw;
            return InfoRow(title, desc, badge, sw);
        }

        static Border InfoRow(string title, string desc, string badge, FrameworkElement right, bool singleLine = false)
        {
            var grid = new Grid { Margin = new Thickness(14, 11, 14, 11) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(Text(title, 14, Palette.Text, FontWeights.SemiBold, new Thickness(0, 0, 8, 0)));
            if (!string.IsNullOrEmpty(badge))
            {
                var caution = badge == "Cuidado" || badge == "Não recomendado" || badge == "Redownload" || badge == "Recria" || badge == "Só se engasgar" || badge == "Pode engasgar" || badge == "Teste";
                var pill = new Border { Background = caution ? Palette.WarnBg : Brushes.Transparent, BorderBrush = caution ? Palette.Warn : Palette.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 1, 8, 1), VerticalAlignment = VerticalAlignment.Center };
                pill.Child = Text(badge, 11, caution ? Palette.Warn : Palette.Muted, FontWeights.Normal, new Thickness(0));
                head.Children.Add(pill);
            }
            text.Children.Add(head);
            if (!string.IsNullOrEmpty(desc))
            {
                var line = Text(desc, 12, Palette.Muted, FontWeights.Normal, new Thickness(0, 2, 12, 0));
                if (singleLine)
                {
                    line.TextWrapping = TextWrapping.NoWrap;
                    line.TextTrimming = TextTrimming.CharacterEllipsis;
                    line.ToolTip = desc;
                }
                text.Children.Add(line);
            }
            if (right != null)
            {
                right.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(right, 1);
                grid.Children.Add(right);
            }
            grid.Children.Add(text);
            var shell = new Border { BorderBrush = Palette.Line, BorderThickness = new Thickness(0, 0, 0, 1), Background = Brushes.Transparent, Child = grid };
            shell.MouseEnter += delegate { shell.Background = Palette.Card2; };
            shell.MouseLeave += delegate { shell.Background = Brushes.Transparent; };
            return shell;
        }

        UIElement ActionCard(string title, string desc, string button, System.Action click, bool primary = false)
        {
            var card = Card();
            card.Margin = new Thickness(0, 0, 0, 10);
            card.Padding = new Thickness(16, 14, 16, 14);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel();
            text.Children.Add(Text(title, 16, Palette.Text, FontWeights.SemiBold, new Thickness(0, 0, 0, 4)));
            text.Children.Add(Text(desc, 13, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 16, 0)));
            var b = Button(button, primary);
            b.Click += delegate { click(); };
            Grid.SetColumn(b, 1);
            b.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(text);
            grid.Children.Add(b);
            card.Child = grid;
            return card;
        }

        static TextBlock Label(string text)
        {
            return Text(text, 12, Palette.Muted, FontWeights.Normal, new Thickness(0, 0, 0, 4));
        }

        static TextBox Box()
        {
            return new TextBox { Height = 34, Background = Palette.Field, Foreground = Palette.Text, BorderBrush = Palette.Line, BorderThickness = new Thickness(1), Padding = new Thickness(8, 6, 8, 6), FontSize = 13, CaretBrush = Palette.Text, Margin = new Thickness(0, 0, 0, 10), Template = InputTemplate(typeof(TextBox)) };
        }

        static TextBlock Text(string value, double size, Brush color, FontWeight weight, Thickness margin)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = color, FontWeight = weight, Margin = margin, TextWrapping = TextWrapping.Wrap };
        }

        static Button Button(string text, bool primary)
        {
            var button = new Button
            {
                Content = text,
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0),
                Background = primary ? Palette.Accent : Brushes.Transparent,
                Foreground = primary ? Palette.Ink : Palette.Text,
                BorderBrush = primary ? Palette.Accent : Palette.Line,
                BorderThickness = new Thickness(1),
                FontSize = 13,
                FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
                Cursor = Cursors.Hand,
                Template = Flat()
            };
            button.MouseEnter += delegate
            {
                button.Background = primary ? Palette.AccentHot : Palette.Card2;
            };
            button.MouseLeave += delegate
            {
                button.Background = primary ? Palette.Accent : Brushes.Transparent;
                button.BorderBrush = primary ? Palette.Accent : Palette.Line;
            };
            return button;
        }

        static ControlTemplate Flat()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            var template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            return template;
        }

        sealed class AppPick { public StoreApp App; public SwitchBox Box; }

        Grid BuildCaption()
        {
            var bar = new Grid { Height = 41 };
            bar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            bar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            captionCol = new ColumnDefinition { Width = new GridLength(0) };
            bar.ColumnDefinitions.Add(captionCol);
            bar.ColumnDefinitions.Add(new ColumnDefinition());
            captionSide = new Border { Background = Palette.Side, BorderBrush = Palette.Line };
            var word = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0), IsHitTestVisible = false };
            word.Children.Add(Logo(18));
            word.Children.Add(Text("Nash Boost", 13, Palette.Ink, FontWeights.SemiBold, new Thickness(8, 0, 0, 0)));
            captionSide.Child = word;
            var main = new Border { Background = Palette.Bg };
            Grid.SetColumn(main, 1);
            var mainGrid = new Grid();
            captionTitle = new TextBlock { Text = "Nash Boost", FontFamily = new FontFamily("Segoe UI"), FontSize = 12, Foreground = Palette.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0), IsHitTestVisible = false };
            mainGrid.Children.Add(captionTitle);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            var min = ChromeButton("\uE921", false);
            System.Windows.Automation.AutomationProperties.SetName(min, "Minimizar");
            min.Click += delegate { WindowState = WindowState.Minimized; };
            var max = ChromeButton("\uE922", false);
            maxGlyph = (TextBlock)max.Content;
            System.Windows.Automation.AutomationProperties.SetName(max, "Maximizar");
            max.Click += delegate { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; };
            var close = ChromeButton("\uE8BB", true);
            System.Windows.Automation.AutomationProperties.SetName(close, "Fechar");
            close.Click += delegate { Close(); };
            buttons.Children.Add(min);
            buttons.Children.Add(max);
            buttons.Children.Add(close);
            mainGrid.Children.Add(buttons);
            main.Child = mainGrid;
            bar.Children.Add(captionSide);
            bar.Children.Add(main);
            var rule = new Border { Background = Palette.Line };
            Grid.SetRow(rule, 1);
            Grid.SetColumnSpan(rule, 2);
            bar.Children.Add(rule);
            return bar;
        }

        void SetChrome(bool loggedIn)
        {
            captionCol.Width = loggedIn ? new GridLength(SideW) : new GridLength(0);
            if (captionSide != null) captionSide.BorderThickness = loggedIn ? new Thickness(0, 0, 1, 0) : new Thickness(0);
        }

        void UpdateChrome()
        {
            var maxed = WindowState == WindowState.Maximized;
            if (maxGlyph != null) maxGlyph.Text = maxed ? "\uE923" : "\uE922";
            var chrome = WindowChrome.GetWindowChrome(this);
            if (chrome != null) chrome.CornerRadius = new CornerRadius(maxed ? 0 : 8);
            if (frame == null) return;
            frame.BorderThickness = new Thickness(maxed ? 0 : 1);
            frame.CornerRadius = new CornerRadius(maxed ? 0 : 8);
            ApplyClip(frame);
        }

        Button ChromeButton(string glyph, bool close)
        {
            var icon = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Foreground = Palette.Text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var button = new Button
            {
                Content = icon,
                Width = 46,
                Height = 32,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Template = ChromeTemplate(),
                Focusable = false
            };
            WindowChrome.SetIsHitTestVisibleInChrome(button, true);
            var hover = close ? new SolidColorBrush(Color.FromRgb(0xC4, 0x42, 0x3A)) : Palette.Card2;
            button.MouseEnter += delegate { button.Background = hover; icon.Foreground = Brushes.White; };
            button.MouseLeave += delegate { button.Background = Brushes.Transparent; icon.Foreground = Palette.Text; };
            return button;
        }

        static ControlTemplate ChromeTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            var template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            return template;
        }

        void OnSourceInitialized(object sender, EventArgs e)
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(WndProc);
            var hwnd = new WindowInteropHelper(this).Handle;
            var corner = 2;
            DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
            var color = 0x001E1A18;
            DwmSetWindowAttribute(hwnd, 34, ref color, 4);
            DwmSetWindowAttribute(hwnd, 35, ref color, 4);
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024)
            {
                var mmi = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));
                var monitor = MonitorFromWindow(hwnd, 2);
                var info = new MonitorInfo();
                info.cbSize = Marshal.SizeOf(typeof(MonitorInfo));
                if (GetMonitorInfo(monitor, ref info))
                {
                    mmi.ptMaxPosition.X = info.rcWork.Left - info.rcMonitor.Left;
                    mmi.ptMaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top;
                    mmi.ptMaxSize.X = info.rcWork.Right - info.rcWork.Left;
                    mmi.ptMaxSize.Y = info.rcWork.Bottom - info.rcWork.Top;
                    Marshal.StructureToPtr(mmi, lParam, true);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [StructLayout(LayoutKind.Sequential)]
        struct WinPoint { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)]
        struct MinMaxInfo
        {
            public WinPoint ptReserved;
            public WinPoint ptMaxSize;
            public WinPoint ptMaxPosition;
            public WinPoint ptMinTrackSize;
            public WinPoint ptMaxTrackSize;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct WinRect { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct MonitorInfo
        {
            public int cbSize;
            public WinRect rcMonitor;
            public WinRect rcWork;
            public int dwFlags;
        }
    }
}
