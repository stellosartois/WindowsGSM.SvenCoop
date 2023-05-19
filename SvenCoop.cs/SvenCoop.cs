using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class SvenCoop
    {
        // Standard variables
        public Functions.ServerConfig serverData;
        public string Error { get; set; }
        public string Notice { get; set; }

        // - Plugin Details
        public Functions.Plugin Plugin = new Functions.Plugin
        {
            name = "WindowsGSM.SvenCoop", // WindowsGSM.XXXX
            author = "Stellos Artois",
            description = "🧩 WindowsGSM plugin for supporting Sven Co-op Dedicated Server",
            version = "1.0",
            url = "https://github.com/stellosartois", // Github repository link (Best practice)
            color = "#9eff99" // Color Hex
        };

        // - Standard Constructor and properties
        public SvenCoop(Functions.ServerConfig serverData) => this.serverData = serverData;

        // - Settings properties for SteamCMD installer
        public bool loginAnonymous => true;
        public string AppId => "276060"; // Game server appId

        // - Game server Fixed variables
        public string StartPath => "svends.exe"; // Game server start path
        public string FullName = "Sven Co-op Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        // - Game server default values
        public string Port { get { return "27015"; } } // Default port
        public string QueryPort { get { return "27015"; } } // Default query port
        public string Game { get { return "svencoop"; } } // Default game name
        public string Defaultmap { get { return "_server_start"; } } // Default map name
        public string Maxplayers { get { return "24"; } } // Default maxplayers
        public string Additional { get { return "-nocrashdialog +clientport {{clientport}}"; } } // Additional server start parameter

        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            //Download server.cfg
            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, Game, "server.cfg");
            if (await Functions.Github.DownloadGameServerConfig(configPath, serverData.ServerGame))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{hostname}}", serverData.ServerName);
                configText = configText.Replace("{{rcon_password}}", serverData.GetRCONPassword());
                File.WriteAllText(configPath, configText);
            }

            //Create steam_appid.txt
            string txtPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "steam_appid.txt");
            File.WriteAllText(txtPath, AppId);
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string path = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
            if (!File.Exists(path))
            {
                Error = $"{StartPath} not found ({path})";
                return null;
            }

            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, Game, "server.cfg");
            if (!File.Exists(configPath))
            {
                Notice = $"server.cfg not found ({configPath})";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append($"-console -game {Game}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerIP) ? string.Empty : $" -ip {serverData.ServerIP}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerPort) ? string.Empty : $" -port {serverData.ServerPort}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerMaxPlayer) ? string.Empty : $" -maxplayers {serverData.ServerMaxPlayer}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerGSLT) ? string.Empty : $" +sv_setsteamaccount {serverData.ServerGSLT}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerParam) ? string.Empty : $" {serverData.ServerParam}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerMap) ? string.Empty : $" +map {serverData.ServerMap}");
            if (serverData.ServerParam.Contains("-game ")) { sb.Replace($" -game {Game}", ""); }
            string param = sb.ToString();

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = Functions.ServerPath.GetServersServerFiles(serverData.ServerID),
                        FileName = path,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false
                    },
                    EnableRaisingEvents = true
                };
                p.Start();
            }
            else
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = Functions.ServerPath.GetServersServerFiles(serverData.ServerID),
                        FileName = path,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                var serverConsole = new Functions.ServerConsole(serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            return p;
        }

        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "quit");
            });
        }


        public async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();
            Process p = await steamCMD.Install(serverData.ServerID, Game, "276060", true);
            Error = steamCMD.Error;

            return p;
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(serverData.ServerID, "276060", validate, custom: custom, modName: Game);
            Error = error;
            return p;
        }

        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }

        public bool IsInstallValid()
        {
            string installPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
            Error = $"Fail to find {installPath}";
            return File.Exists(installPath);
        }

        public bool IsImportValid(string path)
        {
            string importPath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
        }
    }
}