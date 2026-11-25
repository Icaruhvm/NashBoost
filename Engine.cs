using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Nash
{
    public sealed class JobResult
    {
        public readonly List<string> Lines = new List<string>();
        public bool Reboot;
        public void Add(string line) { Lines.Add(line); Tweaks.Log(line); }
    }

    public sealed class PcStatus
    {
        public string Windows = "";
        public string Cpu = "";
        public string Gpu = "";
        public string Ram = "";
        public bool Admin;
        public bool MouseOk;
        public bool KeyOk;
        public bool UsbOk;
        public bool DvrOk;
        public bool HagsOk;
        public bool TelemetryOk;
        public bool MemoryOk;
        public bool BackgroundOk;
        public bool GameModeOk;
        public string MouseDetail = "";
        public string KeyDetail = "";
        public string LastClean = "";
    }

    public sealed class StoreApp
    {
        public string Id;
        public string Name;
        public string Group;
        public string Desc;
        public bool Installed;
        public bool Provisioned;
    }

    public sealed class StartupItem
    {
        public string Id;
        public string Name;
        public string Command;
        public string Scope;
        public bool Enabled;
        public bool Locked;
    }

    public static class Tweaks
    {
        static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GamerEdition");
        static readonly string LogFile = Path.Combine(DataDir, "log.txt");
        static readonly string StateFile = Path.Combine(DataDir, "lastclean.txt");
        static int denied;

        sealed class AppDef
        {
            public string Id, Name, Group, Desc, Match;
            public AppDef(string id, string name, string group, string desc, string match)
            {
                Id = id; Name = name; Group = group; Desc = desc; Match = match;
            }
        }

        static readonly AppDef[] Catalog = new[]
        {
            new AppDef("candy", "Candy Crush", "Jogos de teste", "Jogo de teste que vem com o Windows.", "*CandyCrush*"),
            new AppDef("bubble", "Bubble Witch", "Jogos de teste", "Jogo de teste que vem com o Windows.", "*BubbleWitch*"),
            new AppDef("march", "March of Empires", "Jogos de teste", "Jogo de teste que vem com o Windows.", "*MarchofEmpires*"),
            new AppDef("solitaire", "Paciência", "Jogos de teste", "Coleção de paciência da Microsoft.", "Microsoft.MicrosoftSolitaireCollection"),
            new AppDef("mcafee", "McAfee de teste", "Testes de fábrica", "Antivírus de teste. O Windows Security continua.", "*McAfee*"),
            new AppDef("widgets", "Widgets", "Interface", "Painel de widgets da barra de tarefas.", "MicrosoftWindows.Client.WebExperience"),
            new AppDef("copilot", "Copilot", "Interface", "Aplicativo do Copilot.", "Microsoft.Copilot"),
            new AppDef("copilot2", "Provedor do Copilot", "Interface", "Componente que mantém o Copilot no sistema.", "Microsoft.Windows.Ai.Copilot.Provider"),
            new AppDef("cortana", "Cortana", "Interface", "Assistente antigo da Microsoft.", "Microsoft.549981C3F5F10"),
            new AppDef("tips", "Dicas", "Interface", "Dicas de uso do Windows.", "Microsoft.Getstarted"),
            new AppDef("teams", "Teams", "Comunicação", "Teams que vem instalado com o Windows.", "MicrosoftTeams"),
            new AppDef("msteams", "Teams novo", "Comunicação", "Versão nova do Teams pré-instalada.", "MSTeams"),
            new AppDef("skype", "Skype", "Comunicação", "Skype da Microsoft Store.", "Microsoft.SkypeApp"),
            new AppDef("phone", "Vincular ao Celular", "Comunicação", "Ligação com o celular.", "Microsoft.YourPhone"),
            new AppDef("cross", "Continuar no PC", "Comunicação", "Continuidade entre aparelhos.", "MicrosoftWindows.CrossDevice"),
            new AppDef("mail", "Email e Calendário antigos", "Comunicação", "Correio e Calendário da Store.", "microsoft.windowscommunicationsapps"),
            new AppDef("people", "Pessoas", "Comunicação", "App de contatos antigo.", "Microsoft.People"),
            new AppDef("clipchamp", "Clipchamp", "Mídia", "Editor de vídeo da Microsoft.", "Clipchamp.Clipchamp"),
            new AppDef("groove", "Groove Música", "Mídia", "Player de música antigo.", "Microsoft.ZuneMusic"),
            new AppDef("zune", "Filmes e TV", "Mídia", "Player de vídeo da Microsoft.", "Microsoft.ZuneVideo"),
            new AppDef("bingnews", "Notícias", "Extras da Microsoft", "App de notícias.", "Microsoft.BingNews"),
            new AppDef("bingweather", "Clima", "Extras da Microsoft", "App de previsão do tempo.", "Microsoft.BingWeather"),
            new AppDef("officehub", "Hub do Microsoft 365", "Extras da Microsoft", "Atalho para o Microsoft 365. Não remove o Office instalado à parte.", "Microsoft.MicrosoftOfficeHub"),
            new AppDef("powerauto", "Power Automate", "Extras da Microsoft", "Automação de desktop.", "Microsoft.PowerAutomateDesktop"),
            new AppDef("todo", "To Do", "Extras da Microsoft", "Lista de tarefas da Microsoft.", "Microsoft.Todos"),
            new AppDef("feedback", "Feedback Hub", "Extras da Microsoft", "Envio de comentários à Microsoft.", "Microsoft.WindowsFeedbackHub"),
            new AppDef("maps", "Mapas", "Extras da Microsoft", "Mapas do Windows.", "Microsoft.WindowsMaps"),
            new AppDef("mr", "Portal de Realidade Mista", "Extras da Microsoft", "Portal de realidade mista.", "Microsoft.MixedReality.Portal"),
            new AppDef("viewer3d", "Visualizador 3D", "Extras da Microsoft", "Visualizador de modelos 3D.", "Microsoft.Microsoft3DViewer"),
            new AppDef("builder3d", "3D Builder", "Extras da Microsoft", "Editor 3D antigo.", "Microsoft.3DBuilder"),
            new AppDef("paint3d", "Paint 3D", "Extras da Microsoft", "Paint 3D. O Paint normal fica.", "Microsoft.Paint3D"),
            new AppDef("devhome", "Dev Home", "Extras da Microsoft", "Painel de desenvolvimento.", "Microsoft.Windows.DevHome"),
            new AppDef("devhome2", "Dev Home (Loja)", "Extras da Microsoft", "Dev Home instalado pela Store.", "Microsoft.DevHome")
        };

        public static void Log(string text)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + text + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        static string cachedWindows;
        static string cachedCpu;
        static string cachedGpu;
        static bool hardwareCached;
        static bool usbCached;
        static bool usbValue;
        static bool? memoryCached;

        public static void DropQuickCache()
        {
            usbCached = false;
            memoryCached = null;
        }

        public static bool IsAdmin()
        {
            var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            var p = new System.Security.Principal.WindowsPrincipal(id);
            return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        public static PcStatus ReadStatus()
        {
            var s = new PcStatus { Admin = IsAdmin() };
            if (!hardwareCached)
            {
                cachedWindows = "Windows";
                try
                {
                    using (var cv = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        var product = Convert.ToString(cv.GetValue("ProductName"));
                        var build = Convert.ToInt32(cv.GetValue("CurrentBuildNumber"));
                        if (build >= 22000) product = (product ?? "").Replace("Windows 10", "Windows 11");
                        if (!string.IsNullOrEmpty(product)) cachedWindows = product;
                    }
                }
                catch { }
                try
                {
                    using (var q = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                    foreach (var o in q.Get()) { cachedCpu = CleanCpu(Convert.ToString(o["Name"])); break; }
                }
                catch { }
                try
                {
                    string best = "";
                    ulong bestRam = 0;
                    using (var q = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                    foreach (ManagementObject o in q.Get())
                    {
                        var name = Convert.ToString(o["Name"]) ?? "";
                        if (Regex.IsMatch(name, "Parsec|Virtual|Basic Display|Remote Display|IddSample|Indirect Display", RegexOptions.IgnoreCase)) continue;
                        ulong ram = 0;
                        try { ram = Convert.ToUInt64(o["AdapterRAM"]); } catch { }
                        if (best.Length == 0 || ram > bestRam) { best = CleanGpu(name); bestRam = ram; }
                    }
                    cachedGpu = best;
                }
                catch { }
                hardwareCached = true;
            }
            s.Windows = cachedWindows ?? "Windows";
            s.Cpu = cachedCpu ?? "";
            s.Gpu = cachedGpu ?? "";
            try
            {
                var mem = new Native.MemStat();
                mem.dwLength = (uint)Marshal.SizeOf(typeof(Native.MemStat));
                if (Native.MemStatus(ref mem) && mem.ullTotalPhys > 0)
                {
                    var total = Math.Round(mem.ullTotalPhys / 1024.0 / 1024.0 / 1024.0, 1);
                    var free = Math.Round(mem.ullAvailPhys / 1024.0 / 1024.0 / 1024.0, 1);
                    s.Ram = free.ToString("0.0") + " GB livres de " + total.ToString("0.0") + " GB";
                }
            }
            catch { }
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse"))
                {
                    var speed = Convert.ToString(k.GetValue("MouseSpeed"));
                    var t1 = Convert.ToString(k.GetValue("MouseThreshold1"));
                    var t2 = Convert.ToString(k.GetValue("MouseThreshold2"));
                    s.MouseOk = speed == "0" && t1 == "0" && t2 == "0";
                    s.MouseDetail = "MouseSpeed " + Blank(speed) + " · limites " + Blank(t1) + " e " + Blank(t2);
                }
            }
            catch { }
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard"))
                {
                    var delay = Convert.ToString(k.GetValue("KeyboardDelay"));
                    var speed = Convert.ToString(k.GetValue("KeyboardSpeed"));
                    s.KeyOk = delay == "0";
                    s.KeyDetail = "Atraso " + Blank(delay) + " · repetição " + Blank(speed);
                }
            }
            catch { }
            if (!usbCached) { usbValue = UsbSuspendOff(); usbCached = true; }
            s.UsbOk = usbValue;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore"))
                    s.DvrOk = Convert.ToInt32(k.GetValue("GameDVR_Enabled", 1)) == 0;
            }
            catch { }
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar"))
                {
                    if (k != null)
                        s.GameModeOk = Convert.ToInt32(k.GetValue("AutoGameModeEnabled", 0)) == 1
                            && Convert.ToInt32(k.GetValue("AllowAutoGameMode", 0)) == 1;
                }
            }
            catch { }
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers"))
                    s.HagsOk = Convert.ToInt32(k.GetValue("HwSchMode", 1)) == 2;
            }
            catch { }
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\DiagTrack"))
                    s.TelemetryOk = string.Equals(Convert.ToString(k.GetValue("Start")), "4");
            }
            catch { }
            s.MemoryOk = MemoryOn();
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"))
                    s.BackgroundOk = k != null && Convert.ToInt32(k.GetValue("GlobalUserDisabled", 0)) == 1;
            }
            catch { }
            try { if (File.Exists(StateFile)) s.LastClean = File.ReadAllText(StateFile).Trim(); } catch { }
            return s;
        }

        public static JobResult Apply(IList<string> ids, bool restoreFirst)
        {
            denied = 0;
            var r = new JobResult();
            var set = new HashSet<string>(ids ?? new string[0]);
            if (restoreFirst) RestorePoint(r);
            if (set.Contains("mouse")) Mouse(r);
            if (set.Contains("keyboard")) Keyboard(r);
            if (set.Contains("gamedvr")) GameDvr(r);
            if (set.Contains("hags")) Hags(r);
            if (set.Contains("games")) Games(r);
            if (set.Contains("power")) Power(r);
            if (set.Contains("network")) Network(r);
            if (set.Contains("telemetry")) Telemetry(r);
            if (set.Contains("ui")) Ui(r);
            if (set.Contains("background")) Background(r);
            if (set.Contains("sysmain")) SysMain(r);
            if (set.Contains("mpo")) Mpo(r);
            if (set.Contains("memon")) Memory(r, true);
            else if (set.Contains("mem")) Memory(r, false);
            if (set.Contains("edge")) Edge(r);
            NoteDenied(r);
            return r;
        }

        public static JobResult OneClick()
        {
            return Apply(new[] { "mouse", "keyboard", "gamedvr", "games", "power", "network", "telemetry", "ui", "background" }, true);
        }

        public static void Reboot()
        {
            Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 15 /c \"Reiniciando para concluir o Nash Boost\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        public static void OpenData()
        {
            Directory.CreateDirectory(DataDir);
            if (!File.Exists(LogFile)) File.WriteAllText(LogFile, "");
            Process.Start("explorer.exe", DataDir);
        }

        static void Mouse(JobResult r)
        {
            Sz(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "0");
            Sz(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "0");
            Sz(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "0");
            Sz(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSensitivity", "10");
            Sz(Registry.CurrentUser, @"Control Panel\Mouse", "MouseTrails", "0");
            Sz(Registry.CurrentUser, @"Control Panel\Mouse", "SnapToDefaultButton", "0");
            var dpi = DisplayDpi();
            var percent = dpi * 100 / 96;
            var curve = CurveX(dpi);
            if (curve != null)
            {
                Binary(Registry.CurrentUser, @"Control Panel\Mouse", "SmoothMouseXCurve", curve);
                Binary(Registry.CurrentUser, @"Control Panel\Mouse", "SmoothMouseYCurve", CurveY());
                r.Add("Aceleração desligada e curva reta aplicada na escala de " + percent + "%. O ponteiro fica na marca do meio. Saia da conta para a curva valer por completo.");
            }
            else r.Add("Aceleração desligada. A escala de " + percent + "% não está na tabela, então a curva do Windows ficou como está. O ponteiro fica na marca do meio.");
            try
            {
                Native.SetMouse(4, 0, new[] { 0, 0, 0 }, 2);
                Native.SetParam(0x71, 10, IntPtr.Zero, 2);
            }
            catch { }
        }

        static int WindowsBuild()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    if (k != null) return Convert.ToInt32(k.GetValue("CurrentBuildNumber"));
            }
            catch { }
            return 0;
        }

        static void SysMain(JobResult r)
        {
            if (WindowsBuild() >= 22000)
            {
                Run("sc.exe", "config SysMain start= auto");
                r.Add("Neste Windows 11 o SysMain continua ligado. Ele ajuda a abrir programas.");
                return;
            }
            Run("sc.exe", "stop SysMain");
            Run("sc.exe", "config SysMain start= disabled");
            r.Add("SysMain desligado neste Windows 10. A próxima abertura de programas pode demorar um pouco mais.");
        }

        static int DisplayDpi()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics"))
                    if (k != null && k.GetValue("AppliedDPI") != null) return Convert.ToInt32(k.GetValue("AppliedDPI"));
            }
            catch { }
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                    if (k != null && k.GetValue("LogPixels") != null) return Convert.ToInt32(k.GetValue("LogPixels"));
            }
            catch { }
            return 96;
        }

        static byte[] CurveY()
        {
            return Hex("0000000000000000000038000000000000007000000000000000A800000000000000E00000000000");
        }

        static byte[] CurveX(int dpi)
        {
            if (dpi == 96) return Hex("0000000000000000C0CC0C0000000000809919000000000040662600000000000033330000000000");
            if (dpi == 120) return Hex("00000000000000000000100000000000000020000000000000003000000000000000400000000000");
            if (dpi == 144) return Hex("0000000000000000303313000000000060662600000000009099390000000000C0CC4C0000000000");
            if (dpi == 168) return Hex("00000000000000006066160000000000C0CC2C000000000020334300000000008099590000000000");
            if (dpi == 192) return Hex("000000000000000090991900000000002033330000000000B0CC4C00000000004066660000000000");
            if (dpi == 216) return Hex("0000000000000000C0CC1C0000000000809939000000000040665600000000000033730000000000");
            if (dpi == 240) return Hex("00000000000000000000200000000000000040000000000000006000000000000000800000000000");
            if (dpi == 288) return Hex("00000000000000006066260000000000C0CC4C000000000020337300000000008099990000000000");
            if (dpi == 336) return Hex("0000000000000000C0CC2C0000000000809959000000000040668600000000000033B30000000000");
            return null;
        }

        static byte[] Hex(string text)
        {
            var n = text.Length / 2;
            var bytes = new byte[n];
            for (var i = 0; i < n; i++) bytes[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
            return bytes;
        }

        static void Keyboard(JobResult r)
        {
            Sz(Registry.CurrentUser, @"Control Panel\Keyboard", "KeyboardDelay", "0");
            Sz(Registry.CurrentUser, @"Control Panel\Keyboard", "KeyboardSpeed", "31");
            Sz(Registry.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", "506");
            Sz(Registry.CurrentUser, @"Control Panel\Accessibility\Keyboard Response", "Flags", "122");
            Sz(Registry.CurrentUser, @"Control Panel\Accessibility\Keyboard Response", "DelayBeforeAcceptance", "0");
            Sz(Registry.CurrentUser, @"Control Panel\Accessibility\Keyboard Response", "AutoRepeatDelay", "0");
            Sz(Registry.CurrentUser, @"Control Panel\Accessibility\Keyboard Response", "AutoRepeatRate", "0");
            Sz(Registry.CurrentUser, @"Control Panel\Accessibility\ToggleKeys", "Flags", "58");
            try
            {
                Native.SetParam(0x17, 0, IntPtr.Zero, 2);
                Native.SetParam(0x0B, 31, IntPtr.Zero, 2);
                var sk = new Native.StickyKeys { cbSize = 8, dwFlags = 506 };
                Native.SetSticky(0x3B, 0, ref sk, 2);
            }
            catch { }
            r.Add("Teclado com atraso zero e sem o aviso de Sticky Keys.");
        }

        static void GameDvr(JobResult r)
        {
            Dword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled", 0);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "ShowStartupPanel", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
            Dword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
            Dword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 10);
            Dword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)4294967295));
            var games = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
            Dword(Registry.LocalMachine, games, "GPU Priority", 8);
            Dword(Registry.LocalMachine, games, "Priority", 6);
            Sz(Registry.LocalMachine, games, "Scheduling Category", "High");
            Sz(Registry.LocalMachine, games, "SFIO Priority", "High");
            Sz(Registry.LocalMachine, games, "Background Only", "False");
            Dword(Registry.LocalMachine, games, "Clock Rate", 10000);
            Dword(Registry.LocalMachine, games, "Affinity", 0);
            r.Add("Gravação do Game Bar desligada e Modo de Jogo ligado.");
            r.Add("Perfil de mídia dos jogos em prioridade alta.");
        }

        static void Hags(JobResult r)
        {
            Dword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences"))
                {
                    if (k == null) denied++;
                    else
                    {
                        var cur = Convert.ToString(k.GetValue("DirectXUserGlobalSettings")) ?? "";
                        if (cur.IndexOf("SwapEffectUpgradeEnable=", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            if (cur.Length > 0 && !cur.EndsWith(";")) cur += ";";
                            cur += "SwapEffectUpgradeEnable=1;";
                            k.SetValue("DirectXUserGlobalSettings", cur, RegistryValueKind.String);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException) { denied++; }
            catch (System.Security.SecurityException) { denied++; }
            r.Add("Programação de GPU acelerada ligada. Reinicie para concluir.");
            r.Add("Jogos em janela usam o modo de apresentação mais rápido.");
            r.Reboot = true;
        }

        static void Games(JobResult r)
        {
            foreach (var exe in new[] { "cs2.exe", "FiveM.exe", "GTA5.exe", "FiveM_GTAProcess.exe" })
                SetPriority(exe);
            var fivem = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"FiveM\FiveM.app");
            if (Directory.Exists(fivem))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(fivem, "FiveM*GTAProcess.exe"))
                    {
                        SetPriority(Path.GetFileName(file));
                        GpuPref(file);
                    }
                    var main = Path.Combine(fivem, "FiveM.exe");
                    if (File.Exists(main)) GpuPref(main);
                }
                catch (UnauthorizedAccessException) { denied++; }
                catch (IOException) { }
            }
            var libs = new List<string>();
            CollectSteam(Registry.CurrentUser, @"Software\Valve\Steam", libs);
            CollectSteam(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", libs);
            CollectSteam(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", libs);
            var roots = libs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var root in roots.ToArray())
            {
                var vdf = Path.Combine(root, @"steamapps\libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                string vdfText;
                try { vdfText = File.ReadAllText(vdf); }
                catch (UnauthorizedAccessException) { denied++; continue; }
                catch (IOException) { continue; }
                foreach (Match m in Regex.Matches(vdfText, "\"path\"\\s+\"([^\"]+)\""))
                    libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
            var foundCs = false;
            foreach (var lib in libs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var cs = Path.Combine(lib, @"steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe");
                var gta = Path.Combine(lib, @"steamapps\common\Grand Theft Auto V\GTA5.exe");
                if (GpuPref(cs)) foundCs = true;
                GpuPref(gta);
            }
            r.Add(foundCs ? "CS2 encontrado nas bibliotecas da Steam." : "CS2 não está instalado. A prioridade fica pronta para quando instalar.");
            var foundVal = false;
            var cands = new List<string>();
            foreach (var root in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                try
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(root))
                    {
                        if (k == null) continue;
                        foreach (var sub in k.GetSubKeyNames())
                        {
                            try
                            {
                                using (var item = k.OpenSubKey(sub))
                                {
                                    if (item == null) continue;
                                    var name = Convert.ToString(item.GetValue("DisplayName"));
                                    var loc = Convert.ToString(item.GetValue("InstallLocation"));
                                    if (!string.IsNullOrEmpty(name) && name.IndexOf("VALORANT", StringComparison.OrdinalIgnoreCase) >= 0 && !string.IsNullOrEmpty(loc))
                                        cands.Add(loc);
                                }
                            }
                            catch (UnauthorizedAccessException) { }
                            catch (System.Security.SecurityException) { }
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (System.Security.SecurityException) { }
            }
            foreach (var d in "CDEFG") cands.Add(d + @":\Riot Games\VALORANT\live");
            foreach (var c in cands)
            {
                var full = Path.Combine(c.TrimEnd('\\'), @"ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe");
                if (GpuPref(full)) { foundVal = true; break; }
            }
            r.Add(foundVal ? "GPU de alto desempenho definida para o Valorant." : "Valorant não encontrado.");
            r.Add("Prioridade acima do normal aplicada aos jogos encontrados.");
        }

        public static JobResult Unpriority()
        {
            var r = new JobResult();
            foreach (var exe in new[] { "cs2.exe", "FiveM.exe", "GTA5.exe", "FiveM_GTAProcess.exe" })
            {
                try
                {
                    using (var parent = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" + exe, true))
                        if (parent != null) parent.DeleteSubKeyTree("PerfOptions", false);
                }
                catch { }
            }
            r.Add("Prioridade do CS2, do FiveM e do GTA removida.");
            return r;
        }

        static void Power(JobResult r)
        {
            Run("powercfg.exe", "/setactive SCHEME_BALANCED");
            var overlay = Run("powercfg.exe", "/overlaysetactive ded574b5-45a0-4f42-8737-46345c09c238");
            if (overlay != 0)
            {
                Run("powercfg.exe", "/setactive SCHEME_MIN");
                r.Add("Este Windows não tem o modo Melhor desempenho. Ficou o Alto desempenho, com mínimo de 5%.");
            }
            else r.Add("Energia no modo Melhor desempenho, sem travar a CPU.");
            r.Add("O programa da frente tem prioridade.");
            Run("powercfg.exe", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 5");
            Run("powercfg.exe", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100");
            Run("powercfg.exe", "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 5");
            Run("powercfg.exe", "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100");
            Run("powercfg.exe", "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
            Run("powercfg.exe", "/setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
            Run("powercfg.exe", "/setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0");
            Run("powercfg.exe", "/setactive SCHEME_CURRENT");
            Dword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38);
            Dword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0);
            var laptop = false;
            try
            {
                using (var q = new ManagementObjectSearcher("SELECT PCSystemType FROM Win32_ComputerSystem"))
                foreach (ManagementObject o in q.Get())
                {
                    var t = Convert.ToInt32(o["PCSystemType"]);
                    laptop = t == 2 || t == 8 || t == 9 || t == 10;
                    break;
                }
            }
            catch { }
            if (!laptop)
            {
                Run("powercfg.exe", "/h off");
                r.Add("Hibernação desligada neste desktop. Isso libera espaço.");
            }
            else r.Add("Hibernação mantida.");
        }

        static void CollectSteam(RegistryKey hive, string path, List<string> libs)
        {
            try
            {
                using (var k = hive.OpenSubKey(path))
                {
                    if (k == null) return;
                    foreach (var n in new[] { "SteamPath", "InstallPath" })
                    {
                        var v = Convert.ToString(k.GetValue(n));
                        if (!string.IsNullOrEmpty(v)) libs.Add(v.Replace('/', '\\'));
                    }
                }
            }
            catch { }
        }

        static void Network(JobResult r)
        {
            RunPowerShell("Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object { $_.MediaType -eq '802.3' } | ForEach-Object { Disable-NetAdapterPowerManagement -Name $_.Name -ErrorAction SilentlyContinue; Set-NetAdapterAdvancedProperty -Name $_.Name -RegistryKeyword '*EEE' -RegistryValue '0' -ErrorAction SilentlyContinue }");
            Run("powercfg.exe", "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
            Run("powercfg.exe", "/setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
            Run("powercfg.exe", "/setactive SCHEME_CURRENT");
            if (!UsbSuspendOff()) r.Add("Não foi possível confirmar a suspensão do USB.");
            r.Add("USB sem suspensão. Na rede com fio, a economia de energia da placa fica desligada. A rede pode piscar uma vez.");
        }

        static void Background(JobResult r)
        {
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0);
            r.Add("Apps da Microsoft Store não ficam mais acordados. Steam, Discord e o que você abre continuam normais.");
            long bytes = 0;
            try
            {
                using (var q = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                foreach (ManagementObject o in q.Get())
                {
                    bytes = Convert.ToInt64(o["TotalPhysicalMemory"]);
                    break;
                }
            }
            catch { }
            var kb = bytes / 1024L;
            if (kb >= 1048576L && kb <= int.MaxValue)
            {
                var before = denied;
                Dword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB", (int)kb);
                if (denied == before)
                {
                    r.Add("Serviços do Windows agrupados em menos processos. Vale depois de reiniciar.");
                    r.Reboot = true;
                }
            }
        }

        static void Mpo(JobResult r)
        {
            Dword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5);
            r.Add("Sobreposição da imagem desligada. Use só se a tela piscar ou engasgar. Reinicie.");
            r.Reboot = true;
        }

        static void Telemetry(JobResult r)
        {
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0);
            Dword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0);
            foreach (var name in new[] {
                "ContentDeliveryAllowed", "OemPreInstalledAppsEnabled", "PreInstalledAppsEnabled", "PreInstalledAppsEverEnabled",
                "SilentInstalledAppsEnabled", "SoftLandingEnabled", "SystemPaneSuggestionsEnabled", "SubscribedContent-338388Enabled",
                "SubscribedContent-338389Enabled", "SubscribedContent-353694Enabled", "SubscribedContent-353696Enabled" })
                Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", name, 0);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
            Dword(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", 0);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0);
            foreach (var svc in new[] { "DiagTrack", "dmwappushservice", "RetailDemo", "Fax", "RemoteRegistry", "wisvc" })
            {
                Run("sc.exe", "stop " + svc);
                Run("sc.exe", "config " + svc + " start= disabled");
            }
            foreach (var svc in new[] { "MapsBroker", "lfsvc", "TrkWks", "WMPNetworkSvc", "diagnosticshub.standardcollector.service", "WerSvc", "DoSvc" })
                Run("sc.exe", "config " + svc + " start= demand");
            foreach (var task in new[] {
                @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                @"\Microsoft\Windows\Feedback\Siuf\DmClient" })
                Run("schtasks.exe", "/Change /TN \"" + task + "\" /Disable");
            r.Add("Telemetria, anúncios, histórico de atividade e localização desligados.");
            r.Add("Serviços de telemetria ajustados. Antivírus e atualização seguem ativos.");
            r.Add("Tarefas de telemetria desativadas.");
            r.Reboot = true;
        }

        static void Ui(JobResult r)
        {
            var build = WindowsBuild();
            if (build >= 22000)
                Sz(Registry.CurrentUser, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "");
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LaunchTo", 1);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 1);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);
            Sz(Registry.CurrentUser, @"Control Panel\Desktop", "DragFullWindows", "0");
            Sz(Registry.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay", "20");
            if (build >= 22000)
                Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarEndTask", 1);
            Sz(Registry.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0");
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0);
            Dword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0);
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                try { p.Kill(); } catch { }
            }
            System.Threading.Thread.Sleep(1500);
            if (Process.GetProcessesByName("explorer").Length == 0)
            {
                Run("schtasks.exe", "/Create /TN NashBoostExplorer /TR explorer.exe /SC ONCE /ST 00:00 /RL LIMITED /F");
                Run("schtasks.exe", "/Run /TN NashBoostExplorer");
                System.Threading.Thread.Sleep(1500);
                Run("schtasks.exe", "/Delete /TN NashBoostExplorer /F");
            }
            r.Add("Interface mais leve aplicada.");
        }

        public static JobResult Memory(bool enabled)
        {
            denied = 0;
            var r = new JobResult();
            Memory(r, enabled);
            NoteDenied(r);
            return r;
        }

        static void Memory(JobResult r, bool enabled)
        {
            Dword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", enabled ? 1 : 0);
            if (enabled)
                Dword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 1);
            r.Add(enabled
                ? "Integridade de memória marcada para ligar. Reinicie. O Valorant precisa disso."
                : "Integridade de memória marcada para desligar. O Valorant pode fechar. Reinicie.");
            r.Reboot = true;
        }

        static void Edge(JobResult r)
        {
            var edge = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe");
            foreach (var name in new[] { "msedge", "MicrosoftEdgeUpdate" })
                foreach (var p in Process.GetProcessesByName(name)) { try { p.Kill(); } catch { } }
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\EdgeUpdate", "Install{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}", 0);
            Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\EdgeUpdate", "Update{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}", 0);
            if (!File.Exists(edge)) { r.Add("Edge removido. O WebView2 foi mantido."); return; }
            string setup = null;
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application");
            if (Directory.Exists(root))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(root, "setup.exe", SearchOption.AllDirectories))
                        if (file.IndexOf(@"\Installer\setup.exe", StringComparison.OrdinalIgnoreCase) >= 0) setup = file;
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            if (setup == null) { r.Add("O Edge não saiu nesta versão do Windows. A pasta do programa não foi apagada."); return; }
            var proc = Process.Start(new ProcessStartInfo(setup, "--uninstall --system-level --force-uninstall") { UseShellExecute = false, CreateNoWindow = true });
            proc.WaitForExit();
            System.Threading.Thread.Sleep(1500);
            if (!File.Exists(edge)) r.Add("Edge removido. O WebView2 foi mantido.");
            else if (proc.ExitCode == 532) { r.Add("Reinicie e rode a remoção do Edge de novo."); r.Reboot = true; }
            else r.Add("O Edge não saiu nesta versão do Windows. A pasta do programa não foi apagada.");
        }

        public static JobResult RestorePoint()
        {
            var r = new JobResult();
            RestorePoint(r);
            return r;
        }

        static void RestorePoint(JobResult r)
        {
            try
            {
                Dword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", "SystemRestorePointCreationFrequency", 0);
                var path = new ManagementPath(@"\\.\root\default:SystemRestore");
                using (var mc = new ManagementClass(path))
                {
                    var inp = mc.GetMethodParameters("CreateRestorePoint");
                    inp["Description"] = "Antes do Nash Boost " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                    inp["RestorePointType"] = 12;
                    inp["EventType"] = 100;
                    var outp = mc.InvokeMethod("CreateRestorePoint", inp, null);
                    if (Convert.ToInt32(outp["ReturnValue"]) == 0)
                    {
                        r.Add("Ponto de restauração criado.");
                        return;
                    }
                }
            }
            catch { }
            r.Add("Não foi possível criar o ponto de restauração. A Proteção do Sistema precisa estar ligada.");
        }

        public static JobResult Repair()
        {
            denied = 0;
            var r = new JobResult();
            Repair(r);
            NoteDenied(r);
            return r;
        }

        static void Repair(JobResult r)
        {
            var qc = Capture("sc.exe", "qc SysMain");
            if (qc.IndexOf("DISABLED", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Run("sc.exe", "config SysMain start= auto");
                Run("sc.exe", "start SysMain");
            }
            else Run("sc.exe", "config SysMain start= auto");
            r.Add("SysMain está ativo.");
            var bcd = Capture("bcdedit.exe", "/enum {current}");
            if (bcd.IndexOf("disabledynamictick", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Run("bcdedit.exe", "/deletevalue disabledynamictick");
                r.Add("Timer dinâmico restaurado.");
                r.Reboot = true;
            }
            Run("bcdedit.exe", "/deletevalue useplatformclock");
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl", true))
                {
                    if (k != null)
                    {
                        var cur = Convert.ToInt32(k.GetValue("Win32PrioritySeparation", 2));
                        if (cur != 2 && cur != 38)
                        {
                            k.SetValue("Win32PrioritySeparation", 2, RegistryValueKind.DWord);
                            r.Add("Prioridade de processador deixada por outro script foi restaurada.");
                        }
                    }
                }
            }
            catch { }
            Dword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 0);
            Dword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 0);
            Dword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_DXGIHonorFSEWindowsCompatible", 0);
            Dword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_EFSEFeatureFlags", 0);
            Run("powercfg.exe", "/delete 99999999-9999-9999-9999-999999999999");
            r.Add("Otimização de tela cheia do Windows ligada.");
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\Dwm", true))
                {
                    if (k != null && k.GetValue("OverlayTestMode") != null)
                    {
                        k.DeleteValue("OverlayTestMode", false);
                        r.Add("Sobreposição da imagem restaurada. Reinicie para concluir.");
                        r.Reboot = true;
                    }
                }
            }
            catch { }
        }

        public static JobResult Clean(IList<string> ids)
        {
            var r = new JobResult();
            var set = new HashSet<string>(ids ?? new string[0]);
            if (set.Contains("temp"))
            {
                Wipe(Path.GetTempPath(), "GamerEdition");
                Wipe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), null);
                r.Add("Temporários limpos.");
            }
            if (set.Contains("recycle"))
            {
                try { RunPowerShell("Clear-RecycleBin -Force -ErrorAction SilentlyContinue"); } catch { }
                r.Add("Lixeira esvaziada.");
            }
            if (set.Contains("wu"))
            {
                Run("net.exe", "stop wuauserv");
                Run("net.exe", "stop bits");
                Wipe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SoftwareDistribution\Download"), null);
                Run("net.exe", "start bits");
                Run("net.exe", "start wuauserv");
                r.Add("Cache do Windows Update limpo.");
            }
            if (set.Contains("thumbs"))
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Explorer");
                if (Directory.Exists(dir))
                    try
                    {
                        foreach (var file in Directory.GetFiles(dir, "thumbcache_*.db"))
                            try { File.Delete(file); } catch { }
                    }
                    catch { }
                r.Add("Miniaturas limpas.");
            }
            if (set.Contains("prefetch"))
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                if (Directory.Exists(dir))
                    try
                    {
                        foreach (var file in Directory.GetFiles(dir, "*.pf"))
                            try { File.Delete(file); } catch { }
                    }
                    catch { }
                r.Add("Prefetch apagado. A próxima abertura pode ser mais lenta.");
            }
            if (set.Contains("valorant"))
            {
                var logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"VALORANT\Saved\Logs");
                var crashes = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"VALORANT\Saved\Crashes");
                Wipe(logs, null);
                try { if (Directory.Exists(crashes)) Directory.Delete(crashes, true); } catch { }
                r.Add("Logs do Valorant limpos.");
            }
            if (set.Contains("fivem"))
            {
                var five = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"FiveM\FiveM.app");
                if (!Directory.Exists(five)) r.Add("FiveM não está instalado.");
                else
                {
                    foreach (var p in Process.GetProcesses())
                    {
                        try
                        {
                            if (p.ProcessName.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase) || p.ProcessName == "GTA5") p.Kill();
                        }
                        catch { }
                    }
                    System.Threading.Thread.Sleep(800);
                    foreach (var dir in new[] { @"data\server-cache", @"data\server-cache-priv", @"data\nui-storage", "crashes" })
                    {
                        var path = Path.Combine(five, dir);
                        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
                    }
                    var cache = Path.Combine(five, @"data\cache");
                    if (Directory.Exists(cache))
                        try
                        {
                            foreach (var entry in Directory.GetFileSystemEntries(cache))
                                if (!string.Equals(Path.GetFileName(entry), "game", StringComparison.OrdinalIgnoreCase))
                                    try { if (Directory.Exists(entry)) Directory.Delete(entry, true); else File.Delete(entry); } catch { }
                        }
                        catch { }
                    Wipe(Path.Combine(five, "logs"), null);
                    r.Add("Cache do FiveM reparado.");
                }
            }
            if (set.Contains("shaders"))
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                Wipe(Path.Combine(local, "D3DSCache"), null);
                Wipe(Path.Combine(local, @"NVIDIA\DXCache"), null);
                Wipe(Path.Combine(local, @"NVIDIA\GLCache"), null);
                Wipe(Path.Combine(local, @"AMD\DxCache"), null);
                Wipe(Path.Combine(local, @"AMD\GLCache"), null);
                r.Add("Cache de shaders apagado. A próxima partida pode engasgar enquanto recria.");
            }
            if (set.Contains("captures"))
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Captures");
                if (!Directory.Exists(dir)) r.Add("Não há vídeos do Game Bar.");
                else
                {
                    Wipe(dir, null);
                    r.Add("Vídeos gravados pelo Game Bar apagados.");
                }
            }
            if (set.Contains("play"))
            {
                CloseHeavy(r);
                if (!set.Contains("ram") && !set.Contains("ramfull"))
                    foreach (var line in CleanRam(false)) r.Add(line);
            }
            if (set.Contains("ram") || set.Contains("ramfull"))
                foreach (var line in CleanRam(set.Contains("ramfull"))) r.Add(line);
            if (set.Contains("ramkey")) r.Add(InstallRamHotkey());
            try { File.WriteAllText(StateFile, DateTime.Now.ToString("dd/MM/yyyy HH:mm")); } catch { }
            return r;
        }

        public static List<string> CleanRam(bool full)
        {
            var lines = new List<string>();
            int beforeAvail, total;
            MemSnap(out beforeAvail, out total);
            var usedBefore = Percent(beforeAvail, total);
            var closed = new List<string>();
            foreach (var name in new[] { "OneDrive", "Teams", "ms-teams", "Widgets", "WidgetService", "PhoneExperienceHost", "YourPhone", "GameBar", "GameBarFTServer", "GameBarPresenceWriter", "SkypeApp", "SkypeBackgroundHost", "Cortana" })
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try { p.Kill(); if (!closed.Contains(p.ProcessName)) closed.Add(p.ProcessName); }
                    catch { }
                }
            }
            var edge = Process.GetProcessesByName("msedge");
            var edgeWindow = false;
            foreach (var p in edge)
                if (p.MainWindowHandle != IntPtr.Zero) edgeWindow = true;
            if (!edgeWindow)
            {
                foreach (var p in edge)
                {
                    try { p.Kill(); if (!closed.Contains("msedge")) closed.Add("msedge"); }
                    catch { }
                }
            }
            if (closed.Count > 0) lines.Add("Fechados: " + string.Join(", ", closed.ToArray()) + ".");
            else lines.Add("Nenhum processo inútil aberto.");
            EnablePriv("SeProfileSingleProcessPrivilege");
            EnablePriv("SeIncreaseQuotaPrivilege");
            int fg;
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out fg);
            var self = Process.GetCurrentProcess().Id;
            var trimmed = 0;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == fg || p.Id == self) continue;
                    if (RamKeepName(p.ProcessName)) continue;
                    if (p.WorkingSet64 < 30L * 1024L * 1024L) continue;
                    var handle = Native.OpenProcess(0x0500, false, p.Id);
                    if (handle == IntPtr.Zero) continue;
                    try { if (Native.EmptyWorkingSet(handle)) trimmed++; }
                    finally { Native.CloseHandle(handle); }
                }
                catch { }
            }
            lines.Add("Memória reduzida em " + trimmed + " processos grandes.");
            MemCmd(3);
            if (full)
            {
                MemCmd(4);
                lines.Add("Cache completo liberado.");
            }
            else
            {
                MemCmd(5);
                lines.Add("Cache de baixa prioridade liberado, modo suave.");
            }
            System.Threading.Thread.Sleep(2000);
            int afterAvail;
            MemSnap(out afterAvail, out total);
            var usedAfter = Percent(afterAvail, total);
            var gain = afterAvail - beforeAvail;
            var sign = gain >= 0 ? "+" : "";
            lines.Add("RAM livre: " + beforeAvail + " para " + afterAvail + " MB (" + sign + gain + " MB). Em uso: " + usedBefore + "% para " + usedAfter + "%.");
            return lines;
        }

        static int Percent(int avail, int total)
        {
            if (total < 1) return 0;
            return (int)Math.Round(100.0 * (total - avail) / total);
        }

        static void MemSnap(out int availMb, out int totalMb)
        {
            var mem = new Native.MemStat();
            mem.dwLength = (uint)Marshal.SizeOf(typeof(Native.MemStat));
            availMb = 0;
            totalMb = 1;
            if (!Native.MemStatus(ref mem)) return;
            availMb = (int)(mem.ullAvailPhys / (1024UL * 1024UL));
            totalMb = (int)(mem.ullTotalPhys / (1024UL * 1024UL));
            if (totalMb < 1) totalMb = 1;
        }

        static void MemCmd(int command)
        {
            Native.NtSetSystemInformation(80, ref command, 4);
        }

        static void EnablePriv(string name)
        {
            IntPtr token;
            if (!Native.OpenProcessToken(Native.CurrentProcess(), 0x28, out token)) return;
            try
            {
                var tp = new Native.TokenPriv();
                tp.Count = 1;
                tp.Attr = 2;
                if (!Native.LookupPrivilegeValue(null, name, out tp.Luid)) return;
                Native.AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally { Native.CloseHandle(token); }
        }

        static bool RamKeepName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            var n = name.ToLowerInvariant();
            if (n.StartsWith("valorant") || n.StartsWith("riotclient") || n.StartsWith("gta5") || n.StartsWith("fivem") || n.StartsWith("nashboost")) return true;
            switch (n)
            {
                case "system":
                case "idle":
                case "registry":
                case "memory compression":
                case "secure system":
                case "smss":
                case "csrss":
                case "wininit":
                case "winlogon":
                case "services":
                case "lsass":
                case "dwm":
                case "audiodg":
                case "fontdrvhost":
                case "sihost":
                case "taskhostw":
                case "ctfmon":
                case "explorer":
                case "shellexperiencehost":
                case "startmenuexperiencehost":
                case "searchhost":
                case "textinputhost":
                case "vgc":
                case "vgtray":
                case "cs2":
                case "steam":
                case "steamwebhelper":
                case "discord":
                case "obs64":
                case "opera":
                case "ipvanish":
                case "parsec":
                case "parsecd":
                case "msmpeng":
                case "nissrv":
                case "securityhealthservice":
                    return true;
            }
            return false;
        }

        static string InstallRamHotkey()
        {
            string exe;
            try { exe = Process.GetCurrentProcess().MainModule.FileName; }
            catch { exe = System.Reflection.Assembly.GetExecutingAssembly().Location; }
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return "Não achei o programa para criar o atalho.";
            var code = Run("schtasks.exe", "/Create /TN LimparRAM /TR \"\\\"" + exe + "\\\" /ram\" /SC ONCE /ST 00:00 /RL HIGHEST /F");
            if (code != 0) return "Não consegui criar a tarefa do atalho. Feche e abra de novo aceitando o pedido de administrador.";
            RunPowerShell("Set-ScheduledTask -TaskName 'LimparRAM' -Settings (New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable) | Out-Null");
            try
            {
                var desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var lnk = Path.Combine(desk, "Limpar RAM.lnk");
                var sch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                var shell = Activator.CreateInstance(shellType);
                var link = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                var linkType = link.GetType();
                var set = System.Reflection.BindingFlags.SetProperty;
                linkType.InvokeMember("TargetPath", set, null, link, new object[] { sch });
                linkType.InvokeMember("Arguments", set, null, link, new object[] { "/Run /TN LimparRAM" });
                linkType.InvokeMember("WorkingDirectory", set, null, link, new object[] { Environment.GetFolderPath(Environment.SpecialFolder.System) });
                linkType.InvokeMember("WindowStyle", set, null, link, new object[] { 7 });
                linkType.InvokeMember("Description", set, null, link, new object[] { "Limpa a RAM sem fechar jogos" });
                linkType.InvokeMember("Hotkey", set, null, link, new object[] { "CTRL+ALT+R" });
                linkType.InvokeMember("IconLocation", set, null, link, new object[] { exe + ",0" });
                linkType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, link, null);
            }
            catch { return "A tarefa foi criada, mas o atalho da área de trabalho não."; }
            return "Atalho Limpar RAM na área de trabalho. CTRL+ALT+R limpa no modo suave. Para tirar, apague o atalho e a tarefa LimparRAM.";
        }

        public static void Notify(string text)
        {
            var icon = new System.Windows.Forms.NotifyIcon();
            icon.Icon = System.Drawing.SystemIcons.Information;
            icon.Visible = true;
            icon.ShowBalloonTip(4000, "Limpar RAM", text ?? "", System.Windows.Forms.ToolTipIcon.Info);
            System.Threading.Thread.Sleep(5000);
            icon.Visible = false;
            icon.Dispose();
        }

        public static List<StoreApp> ScanApps()
        {
            var script = new StringBuilder();
            script.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
            script.AppendLine("$pkgs = @(Get-AppxPackage)");
            script.AppendLine("$prov = @(Get-AppxProvisionedPackage -Online)");
            script.AppendLine("$rows = @(");
            foreach (var app in Catalog)
                script.AppendLine("  @{ id = '" + app.Id + "'; match = '" + app.Match.Replace("'", "''") + "' },");
            script.AppendLine(")");
            script.AppendLine("foreach ($w in $rows) {");
            script.AppendLine("  $i = @($pkgs | Where-Object { $_.Name -like $w.match }).Count -gt 0");
            script.AppendLine("  $p = @($prov | Where-Object { $_.DisplayName -like $w.match }).Count -gt 0");
            script.AppendLine("  Write-Output ($w.id + '|' + [int]$i + '|' + [int]$p)");
            script.AppendLine("}");
            var output = RunPowerShell(script.ToString());
            var map = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Trim().Split('|');
                if (parts.Length != 3) continue;
                map[parts[0]] = new[] { parts[1] == "1", parts[2] == "1" };
            }
            var list = new List<StoreApp>();
            foreach (var app in Catalog)
            {
                bool[] flags;
                map.TryGetValue(app.Id, out flags);
                list.Add(new StoreApp
                {
                    Id = app.Id,
                    Name = app.Name,
                    Group = app.Group,
                    Desc = app.Desc,
                    Installed = flags != null && flags[0],
                    Provisioned = flags != null && flags[1]
                });
            }
            return list;
        }

        public static JobResult RemoveApps(IList<string> ids, bool onedrive)
        {
            denied = 0;
            var r = new JobResult();
            var chosen = Catalog.Where(a => ids != null && ids.Contains(a.Id)).ToList();
            if (chosen.Count > 0)
            {
                var script = new StringBuilder();
                script.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
                script.AppendLine("$pkgs = @(Get-AppxPackage -AllUsers)");
                script.AppendLine("$prov = @(Get-AppxProvisionedPackage -Online)");
                script.AppendLine("$matches = @(");
                foreach (var app in chosen) script.AppendLine("  '" + app.Match.Replace("'", "''") + "',");
                script.AppendLine(")");
                script.AppendLine("foreach ($m in $matches) {");
                script.AppendLine("  foreach ($p in @($prov | Where-Object { $_.DisplayName -like $m })) { Remove-AppxProvisionedPackage -Online -PackageName $p.PackageName | Out-Null }");
                script.AppendLine("  foreach ($p in @($pkgs | Where-Object { $_.Name -like $m })) { Remove-AppxPackage -Package $p.PackageFullName -AllUsers }");
                script.AppendLine("}");
                RunPowerShell(script.ToString());
                r.Add("Remoção de apps concluída.");
            }
            if (onedrive)
            {
                foreach (var p in Process.GetProcessesByName("OneDrive")) { try { p.Kill(); } catch { } }
                var setup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SysWOW64\OneDriveSetup.exe");
                if (!File.Exists(setup)) setup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\OneDriveSetup.exe");
                if (File.Exists(setup))
                {
                    var proc = Process.Start(new ProcessStartInfo(setup, "/uninstall") { UseShellExecute = false, CreateNoWindow = true });
                    proc.WaitForExit();
                }
                Dword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 1);
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                        if (k != null) k.DeleteValue("OneDrive", false);
                }
                catch { }
                r.Add("OneDrive removido. Arquivos que já estavam na pasta foram mantidos.");
            }
            NoteDenied(r);
            if (r.Lines.Count == 0) r.Add("Nada foi marcado.");
            return r;
        }

        public static List<StartupItem> ReadStartup()
        {
            var list = new List<StartupItem>();
            ReadRun("hkcu-run", "Usuário", Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", list);
            ReadRun("hklm-run", "Sistema", Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", list);
            ReadRun("wow-run", "Sistema", Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32", list);
            ReadFolder("hkcu-folder", "Usuário", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Startup"), Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", list);
            ReadFolder("hklm-folder", "Sistema", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs\Startup"), Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", list);
            return list.OrderByDescending(i => i.Enabled).ThenBy(i => i.Name).ToList();
        }

        public static string SetStartup(string id, bool enabled)
        {
            var cut = (id ?? "").IndexOf(':');
            if (cut < 1) return "Não foi possível alterar este item.";
            var hive = id.Substring(0, cut);
            string name;
            try { name = Encoding.UTF8.GetString(Convert.FromBase64String(id.Substring(cut + 1))); }
            catch { return "Não foi possível alterar este item."; }
            RegistryKey runHive, approvedHive;
            string runPath, approvedPath, folder = null;
            if (!MapHive(hive, out runHive, out runPath, out approvedHive, out approvedPath, out folder))
                return "Não foi possível alterar este item.";
            if (folder == null)
            {
                using (var k = runHive.OpenSubKey(runPath))
                {
                    if (k == null || k.GetValue(name) == null) return "Não foi possível alterar este item.";
                    var command = Convert.ToString(k.GetValue(name));
                    if (Locked(name, command)) return "Item protegido.";
                }
            }
            else
            {
                var file = Path.Combine(folder, name);
                if (!File.Exists(file)) return "Não foi possível alterar este item.";
                if (Locked(name, file)) return "Item protegido.";
            }
            var bytes = new byte[12];
            bytes[0] = (byte)(enabled ? 2 : 3);
            try
            {
                using (var k = approvedHive.CreateSubKey(approvedPath))
                {
                    if (k == null) return "O Windows recusou gravar este item.";
                    k.SetValue(name, bytes, RegistryValueKind.Binary);
                }
            }
            catch (UnauthorizedAccessException)
            {
                return "O Windows recusou gravar este item. Feche o Nash Boost e abra de novo aceitando o pedido de administrador.";
            }
            catch (System.Security.SecurityException)
            {
                return "O Windows recusou gravar este item. Feche o Nash Boost e abra de novo aceitando o pedido de administrador.";
            }
            return null;
        }

        static void ReadRun(string hive, string scope, RegistryKey runHive, string runPath, RegistryKey approvedHive, string approvedPath, List<StartupItem> list)
        {
            using (var run = runHive.OpenSubKey(runPath))
            {
                if (run == null) return;
                using (var approved = approvedHive.OpenSubKey(approvedPath))
                foreach (var name in run.GetValueNames())
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    var command = Convert.ToString(run.GetValue(name));
                    if (string.IsNullOrEmpty(command)) continue;
                    var on = true;
                    if (approved != null)
                    {
                        var raw = approved.GetValue(name) as byte[];
                        if (raw != null && raw.Length > 0 && raw[0] == 3) on = false;
                    }
                    var shortCmd = command.Length > 110 ? command.Substring(0, 107) + "..." : command;
                    list.Add(new StartupItem
                    {
                        Id = hive + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(name)),
                        Name = name,
                        Command = shortCmd,
                        Scope = scope,
                        Enabled = on,
                        Locked = Locked(name, command)
                    });
                }
            }
        }

        static void ReadFolder(string hive, string scope, string folder, RegistryKey approvedHive, string approvedPath, List<StartupItem> list)
        {
            if (!Directory.Exists(folder)) return;
            using (var approved = approvedHive.OpenSubKey(approvedPath))
            foreach (var file in Directory.GetFiles(folder))
            {
                var valueName = Path.GetFileName(file);
                if (string.Equals(valueName, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                var on = true;
                if (approved != null)
                {
                    var raw = approved.GetValue(valueName) as byte[];
                    if (raw != null && raw.Length > 0 && raw[0] == 3) on = false;
                }
                var display = valueName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? valueName.Substring(0, valueName.Length - 4) : valueName;
                list.Add(new StartupItem
                {
                    Id = hive + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(valueName)),
                    Name = display,
                    Command = valueName,
                    Scope = scope,
                    Enabled = on,
                    Locked = Locked(display, file)
                });
            }
        }

        static bool MapHive(string hive, out RegistryKey runHive, out string runPath, out RegistryKey approvedHive, out string approvedPath, out string folder)
        {
            runHive = null; runPath = null; approvedHive = null; approvedPath = null; folder = null;
            if (hive == "hkcu-run")
            {
                runHive = Registry.CurrentUser; runPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
                approvedHive = Registry.CurrentUser; approvedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
                return true;
            }
            if (hive == "hklm-run")
            {
                runHive = Registry.LocalMachine; runPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                approvedHive = Registry.LocalMachine; approvedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
                return true;
            }
            if (hive == "wow-run")
            {
                runHive = Registry.LocalMachine; runPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
                approvedHive = Registry.LocalMachine; approvedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
                return true;
            }
            if (hive == "hkcu-folder")
            {
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Startup");
                approvedHive = Registry.CurrentUser; approvedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
                return true;
            }
            if (hive == "hklm-folder")
            {
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs\Startup");
                approvedHive = Registry.LocalMachine; approvedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
                return true;
            }
            return false;
        }

        static bool Locked(string name, string command)
        {
            var blob = ((name ?? "") + " " + (command ?? "")).ToLowerInvariant();
            foreach (var word in new[] { "securityhealth", "windowsdefender", "msmpeng", "smartscreen", "ctfmon", "nashboost", "gameredition" })
                if (blob.Contains(word)) return true;
            return false;
        }

        static bool MemoryOn()
        {
            if (memoryCached.HasValue) return memoryCached.Value;
            var on = false;
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"))
                {
                    if (k != null && k.GetValue("Enabled") != null)
                    {
                        on = Convert.ToInt32(k.GetValue("Enabled")) == 1;
                        memoryCached = on;
                        return on;
                    }
                }
            }
            catch { }
            try
            {
                using (var q = new ManagementObjectSearcher(@"root\Microsoft\Windows\DeviceGuard", "SELECT SecurityServicesConfigured, SecurityServicesRunning FROM Win32_DeviceGuard"))
                foreach (ManagementObject o in q.Get())
                {
                    on = HasTwo(o["SecurityServicesConfigured"]) || HasTwo(o["SecurityServicesRunning"]);
                    break;
                }
            }
            catch { }
            memoryCached = on;
            return on;
        }

        static bool HasTwo(object value)
        {
            var items = value as System.Collections.IEnumerable;
            if (items == null || value is string) return false;
            foreach (var item in items)
                if (Convert.ToInt32(item) == 2) return true;
            return false;
        }

        static bool UsbSuspendOff()
        {
            var text = Capture("powercfg.exe", "/q SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226");
            var m = Regex.Match(text ?? "", "0x([0-9a-fA-F]+)");
            if (!m.Success) return false;
            return Convert.ToInt32(m.Groups[1].Value, 16) == 0;
        }

        static void SetPriority(string exe)
        {
            var path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" + exe + @"\PerfOptions";
            Dword(Registry.LocalMachine, path, "CpuPriorityClass", 6);
            Dword(Registry.LocalMachine, path, "IoPriority", 2);
        }

        static bool GpuPref(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                if (!File.Exists(path)) return false;
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences"))
                {
                    if (k == null) { denied++; return false; }
                    k.SetValue(path, "GpuPreference=2;", RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException) { denied++; return false; }
            catch (System.Security.SecurityException) { denied++; return false; }
            catch (IOException) { return false; }
            catch (ArgumentException) { return false; }
            return true;
        }

        static void Dword(RegistryKey hive, string sub, string name, int value)
        {
            try
            {
                using (var k = hive.CreateSubKey(sub))
                    if (k != null) k.SetValue(name, value, RegistryValueKind.DWord);
            }
            catch (UnauthorizedAccessException) { denied++; }
            catch (System.Security.SecurityException) { denied++; }
        }

        static void Sz(RegistryKey hive, string sub, string name, string value)
        {
            try
            {
                using (var k = hive.CreateSubKey(sub))
                    if (k != null) k.SetValue(name, value ?? "", RegistryValueKind.String);
            }
            catch (UnauthorizedAccessException) { denied++; }
            catch (System.Security.SecurityException) { denied++; }
        }

        static void Binary(RegistryKey hive, string sub, string name, byte[] value)
        {
            try
            {
                using (var k = hive.CreateSubKey(sub))
                    if (k != null) k.SetValue(name, value, RegistryValueKind.Binary);
            }
            catch (UnauthorizedAccessException) { denied++; }
            catch (System.Security.SecurityException) { denied++; }
        }

        static void NoteDenied(JobResult r)
        {
            if (denied <= 0) return;
            if (!IsAdmin())
                r.Add("O Windows recusou parte das gravações. Feche o Nash Boost e abra de novo aceitando o pedido de administrador.");
            else
                r.Add("Uma parte não foi gravada porque o Windows recusou a chave. O restante entrou.");
        }

        static void Wipe(string dir, string skip)
        {
            if (!Directory.Exists(dir)) return;
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(dir); }
            catch { return; }
            foreach (var entry in entries)
            {
                if (skip != null && string.Equals(Path.GetFileName(entry), skip, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    if (Directory.Exists(entry)) Directory.Delete(entry, true);
                    else File.Delete(entry);
                }
                catch { }
            }
        }

        public static JobResult HeavyBoot()
        {
            var r = new JobResult();
            var off = new List<string>();
            var skipped = 0;
            foreach (var item in ReadStartup())
            {
                if (!item.Enabled || item.Locked) continue;
                if (!HeavyStartup(item.Name, item.Command)) continue;
                var err = SetStartup(item.Id, false);
                if (err == null) off.Add(item.Name);
                else skipped++;
            }
            if (off.Count == 0)
                r.Add(skipped > 0 ? "Não deu para tirar o papel de parede da inicialização." : "Nenhum papel de parede animado abria com o Windows.");
            else r.Add("Deixa de abrir com o Windows: " + string.Join(", ", off.ToArray()) + ".");
            r.Add("O resto da inicialização continua. O que sobrar, desligue na lista.");
            return r;
        }

        static void CloseHeavy(JobResult r)
        {
            var closed = new List<string>();
            foreach (var name in new[] { "wallpaper64", "wallpaper32", "Lively", "livelywpf", "RainWallpaper" })
            {
                Process[] found;
                try { found = Process.GetProcessesByName(name); }
                catch { continue; }
                foreach (var p in found)
                {
                    try
                    {
                        p.Kill();
                        if (!closed.Contains(name)) closed.Add(name);
                    }
                    catch { }
                }
            }
            if (closed.Count == 0) r.Add("Nenhum papel de parede animado estava aberto.");
            else r.Add("Papel de parede fechado: " + string.Join(", ", closed.ToArray()) + ".");
        }

        static bool HeavyStartup(string name, string command)
        {
            var blob = ((name ?? "") + " " + (command ?? "")).ToLowerInvariant();
            foreach (var word in new[] { "wallpaper32", "wallpaper64", "wallpaper engine", "lively", "rainwallpaper" })
                if (blob.Contains(word)) return true;
            return false;
        }

        static int Run(string file, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit();
                return p.ExitCode;
            }
            catch { return -1; }
        }

        static string Capture(string file, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                var text = p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit();
                return text;
            }
            catch { return ""; }
        }

        static string RunPowerShell(string script)
        {
            var path = Path.Combine(Path.GetTempPath(), "nashboost-" + Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(path, script, new UTF8Encoding(true));
            try { return Capture("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + path + "\""); }
            finally { try { File.Delete(path); } catch { } }
        }

        static string Blank(string value) { return string.IsNullOrEmpty(value) ? "—" : value; }

        static string CleanCpu(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            value = value.Replace("(R)", " ").Replace("(TM)", " ").Replace(" CPU", "");
            value = Regex.Replace(value, @"\s+\d+-Core.*$", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\s+Processor.*$", "", RegexOptions.IgnoreCase);
            return Regex.Replace(value, @"\s+", " ").Trim();
        }

        static string CleanGpu(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            value = value.Replace("NVIDIA ", "").Replace("Intel(R) ", "Intel ").Replace("AMD ", "");
            return Regex.Replace(value, @"\s+", " ").Trim();
        }

        static class Native
        {
            [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
            public static extern bool SetMouse(uint action, uint param, int[] values, uint flags);
            [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
            public static extern bool SetParam(uint action, uint param, IntPtr values, uint flags);
            [StructLayout(LayoutKind.Sequential)]
            public struct StickyKeys { public uint cbSize; public uint dwFlags; }
            [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
            public static extern bool SetSticky(uint action, uint param, ref StickyKeys value, uint flags);
            [DllImport("kernel32.dll", EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
            public static extern bool MemStatus(ref MemStat status);
            [DllImport("kernel32.dll", EntryPoint = "GetCurrentProcess")]
            public static extern IntPtr CurrentProcess();
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess(int access, bool inherit, int pid);
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr handle);
            [DllImport("psapi.dll")]
            public static extern bool EmptyWorkingSet(IntPtr handle);
            [DllImport("ntdll.dll")]
            public static extern int NtSetSystemInformation(int cls, ref int info, int len);
            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool OpenProcessToken(IntPtr process, int access, out IntPtr token);
            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool LookupPrivilegeValue(string system, string name, out long luid);
            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool AdjustTokenPrivileges(IntPtr token, bool disable, ref TokenPriv next, int len, IntPtr prev, IntPtr ret);
            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();
            [DllImport("user32.dll")]
            public static extern int GetWindowThreadProcessId(IntPtr window, out int pid);
            [StructLayout(LayoutKind.Sequential)]
            public struct MemStat
            {
                public uint dwLength;
                public uint dwMemoryLoad;
                public ulong ullTotalPhys;
                public ulong ullAvailPhys;
                public ulong ullTotalPageFile;
                public ulong ullAvailPageFile;
                public ulong ullTotalVirtual;
                public ulong ullAvailVirtual;
                public ulong ullAvailExtendedVirtual;
            }
            [StructLayout(LayoutKind.Sequential, Pack = 4)]
            public struct TokenPriv
            {
                public int Count;
                public long Luid;
                public int Attr;
            }
        }
    }
}
