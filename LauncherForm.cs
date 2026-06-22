using System;
using System.Runtime.InteropServices;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using CmlLib.Core;
using CmlLib.Core.Auth;
using System.Diagnostics;
using CmlLib.Core.ProcessBuilder;

namespace StrafeClient
{
    public class LauncherForm : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        private WebView2 webView;
        private MinecraftLauncher launcher;
        
        // [SECURITY FIX HIGH-3] Regex for offline username validation
        private static readonly System.Text.RegularExpressions.Regex _usernameRegex =
            new System.Text.RegularExpressions.Regex(@"^[a-zA-Z0-9_]{3,16}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        public LauncherForm()
        {
            // [SECURITY FIX HIGH-4] REMOVIDO: delete de cache do WebView2 em produção
            // era código de desenvolvimento e cria race condition + symlink attack vector.
            this.Text = "Strafe Client";
            this.Width = 1100;
            this.Height = 700;
            this.MinimumSize = new System.Drawing.Size(900, 600);
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            
            // Fundo escuro enquanto carrega o WebView
            this.BackColor = System.Drawing.Color.FromArgb(13, 13, 18);

            // Ícone do app (taskbar, alt-tab, janela)
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "strafe.ico");
            if (File.Exists(iconPath))
                this.Icon = new System.Drawing.Icon(iconPath);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            this.Controls.Add(webView);

            // Inicializa o ambiente do WebView2
            await webView.EnsureCoreWebView2Async(null);

            // [SECURITY FIX LOW-1 + LOW-2] Disable DevTools and context menus in production
            // Prevents users from injecting arbitrary IPC messages via browser devtools.
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            // Configura o caminho do HTML local
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "index.html");
            webView.CoreWebView2.Navigate(htmlPath);

            // Evento para receber mensagens do JavaScript
            webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

            // Verificação automática de atualização após a página carregar
            webView.CoreWebView2.NavigationCompleted += async (s, args) =>
            {
                if (!args.IsSuccess) return;
                var update = await UpdateManager.CheckAsync();
                if (update != null)
                {
                    this.Invoke(new Action(() =>
                    {
                        var msg = new
                        {
                            type = "updateAvailable",
                            version = update.Versao,
                            url = update.Url,
                            notes = update.Notas,
                            currentVersion = UpdateManager.VERSAO_ATUAL
                        };
                        webView.CoreWebView2.PostWebMessageAsString(System.Text.Json.JsonSerializer.Serialize(msg));
                    }));
                }
            };
        }

        private async void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message = e.TryGetWebMessageAsString();
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                if (root.TryGetProperty("action", out var actionProp))
                {
                    string action = actionProp.GetString();
                    if (action == "play")
                    {
                        string version = root.GetProperty("version").GetString();
                        int ramMb = root.TryGetProperty("ramMb", out var ramProp) ? ramProp.GetInt32() : 2048;
                        string instanceName = root.TryGetProperty("instanceName", out var instProp) ? instProp.GetString() : "padrao";
                        
                        var activeAcc = AccountManager.GetActiveAccount();
                        if (activeAcc == null) {
                            var eMsg = new { type = "error", text = "Nenhuma conta selecionada! Vá em Contas e adicione uma." };
                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                            return;
                        }
                        await LaunchGame(activeAcc.Username, version, ramMb, instanceName);
                    }
                    else if (action == "getAccounts")
                    {
                        var accs = AccountManager.GetAccounts();
                        var active = AccountManager.GetActiveAccount();
                        var msg = new { type = "accounts", list = accs, activeId = active?.Id };
                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                    }
                    else if (action == "getLocalMods")
                    {
                        var mods = new System.Collections.Generic.List<object>();
                        string modsPath = System.IO.Path.Combine(CmlLib.Core.MinecraftPath.GetOSDefaultPath(), "mods");
                        if (System.IO.Directory.Exists(modsPath))
                        {
                            foreach (var file in System.IO.Directory.GetFiles(modsPath, "*.jar"))
                            {
                                mods.Add(new {
                                    filename = System.IO.Path.GetFileName(file),
                                    path = file
                                });
                            }
                        }
                        var msg = new { type = "localMods", list = mods };
                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                    }
                    else if (action == "addOfflineAccount")
                    {
                        string username = root.GetProperty("username").GetString() ?? "";
                        // [SECURITY FIX HIGH-3] Validate username: 3-16 alphanumeric + underscore
                        if (!_usernameRegex.IsMatch(username))
                        {
                            var eMsg = new { type = "error", text = "Username inválido. Use 3-16 caracteres: letras, números ou underscore." };
                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                            return;
                        }
                        AccountManager.AddOfflineAccount(username);
                        var msg = new { type = "accounts", list = AccountManager.GetAccounts(), activeId = AccountManager.GetActiveAccount()?.Id };
                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                    }
                    else if (action == "deleteAccount")
                    {
                        string id = root.GetProperty("id").GetString();
                        AccountManager.DeleteAccount(id);
                        var msg = new { type = "accounts", list = AccountManager.GetAccounts(), activeId = AccountManager.GetActiveAccount()?.Id };
                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                    }
                    else if (action == "setActiveAccount")
                    {
                        string id = root.GetProperty("id").GetString();
                        AccountManager.SetActiveAccount(id);
                        var msg = new { type = "accounts", list = AccountManager.GetAccounts(), activeId = AccountManager.GetActiveAccount()?.Id };
                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                    }
                    else if (action == "logout")
                    {
                        AccountManager.Logout();
                        var msg = new { type = "accounts", list = AccountManager.GetAccounts(), activeId = AccountManager.GetActiveAccount()?.Id };
                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                    }
                    else if (action == "REGISTRO_SUCESSO" || action == "LOGIN_SUCESSO")
                    {
                        string nick = root.GetProperty("nick").GetString();
                        string token = root.TryGetProperty("token", out var tokenProp) ? tokenProp.GetString() : "";
                        
                        // Salva a conta como StrafeAPI
                        AccountManager.AddStrafeAccount(nick, token);
                        
                        // Atualiza as contas no front
                        var msg = new { type = "accounts", list = AccountManager.GetAccounts(), activeId = AccountManager.GetActiveAccount()?.Id };
                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                    }
                    else if (action == "loginMicrosoft")
                    {
                        this.Invoke((MethodInvoker)delegate {
                            // Intercept the navigation on the MAIN webview
                            EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs> navHandler = null;
                            
                            navHandler = async (sender, args) =>
                            {
                                if (args.Uri.StartsWith("https://login.live.com/oauth20_desktop.srf"))
                                {
                                    args.Cancel = true;
                                    
                                    // Remove the handler so it doesn't fire again
                                    webView.CoreWebView2.NavigationStarting -= navHandler;
                                    
                                    var uri = new Uri(args.Uri);
                                    string authCode = "";
                                    if (uri.Query.Contains("code=")) {
                                        authCode = uri.Query.Split("code=")[1].Split('&')[0];
                                        authCode = Uri.UnescapeDataString(authCode); // FIX: URL decode the code!
                                    }

                                    bool loginSuccess = false;
                                    string loginMsg = "";
                                    string nick = "";
                                    
                                    if (!string.IsNullOrEmpty(authCode))
                                    {
                                        try
                                        {
                                            var authResult = await AccountManager.LoginMicrosoftAsync(authCode);
                                            
                                            if (authResult != null && authResult.Session != null && !string.IsNullOrEmpty(authResult.Session.Username))
                                            {
                                                string newId = Guid.NewGuid().ToString();
                                                var accounts = AccountManager.GetAccounts();
                                                accounts.RemoveAll(a => a.Username == authResult.Session.Username && a.IsMicrosoft);
                                                accounts.Add(new AccountInfo
                                                {
                                                    Id = newId,
                                                    Username = authResult.Session.Username,
                                                    Type = "Microsoft",
                                                    Token = authResult.Session.AccessToken,
                                                    RefreshToken = authResult.RefreshToken,
                                                    IsMicrosoft = true,
                                                    UUID = authResult.Session.UUID
                                                });
                                                AccountManager.SetActiveAccount(newId);
                                                
                                                loginSuccess = true;
                                                nick = authResult.Session.Username;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            loginMsg = ex.Message;
                                            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ms_login_error.txt"), ex.ToString());
                                        }
                                    }
                                    else if (uri.Query.Contains("error="))
                                    {
                                        loginMsg = "O login foi cancelado ou ocorreu um erro (error=" + uri.Query.Split("error=")[1].Split('&')[0] + ")";
                                    }
                                    
                                    // Navigate back to the launcher UI
                                    string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "index.html");
                                    
                                    // Inject success/error message after page reloads
                                    EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs> compHandler = null;
                                    compHandler = (s, eArgs) => {
                                        webView.CoreWebView2.NavigationCompleted -= compHandler;
                                        if (loginSuccess) {
                                            var successObj = new { type = "microsoftLoginSuccess", nick = nick };
                                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(successObj));
                                        } else if (!string.IsNullOrEmpty(loginMsg)) {
                                            var errObj = new { type = "microsoftLoginError", message = loginMsg };
                                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(errObj));
                                        }
                                    };
                                    webView.CoreWebView2.NavigationCompleted += compHandler;
                                    
                                    webView.CoreWebView2.Navigate(htmlPath);
                                }
                            };
                            
                            webView.CoreWebView2.NavigationStarting += navHandler;
                            webView.CoreWebView2.Navigate(MicrosoftAuthHelper.GetLoginUrl());
                        });
                    }
                    else if (action == "openUrl")
                    {
                        string url = root.GetProperty("url").GetString() ?? "";
                        // [SECURITY FIX CRIT-2] Allowlist: only open http/https URLs via shell.
                        // Prevents RCE via Process.Start with arbitrary shell protocols, executables, etc.
                        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                        {
                            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                        }
                        else
                        {
                            var eMsg = new { type = "error", text = "URL bloqueada por segurança: apenas http/https são permitidos." };
                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                        }
                    }
                    else if (action == "getVersions")
                    {
                        await SendVersionsToWeb();
                    }
                    else if (action == "getSystemInfo")
                    {
                        SendSystemInfoToWeb();
                    }
                    else if (action == "windowControl")
                    {
                        string command = root.GetProperty("command").GetString();
                        if (command == "minimize") this.WindowState = FormWindowState.Minimized;
                        else if (command == "maximize")
                            this.WindowState = this.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                        else if (command == "close") Application.Exit();
                        else if (command == "drag")
                        {
                            ReleaseCapture();
                            SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                        }
                    }
                    else if (action == "getInstances")
                    {
                        var instances = InstanceManager.GetInstances();
                        var msg = new { type = "instances", list = instances };
                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                    }
                    else if (action == "createInstance")
                    {
                        var info = JsonSerializer.Deserialize<InstanceInfo>(root.GetProperty("info").GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        var localMods = new System.Collections.Generic.List<string>();
                        if (root.TryGetProperty("localMods", out var lMods)) {
                            foreach (var lm in lMods.EnumerateArray()) {
                                localMods.Add(lm.GetString());
                            }
                        }
                        
                        Task.Run(async () => {
                            try
                            {
                                if (!InstanceManager.CreateInstance(info))
                                {
                                    this.Invoke(new Action(() => SendErrorToWeb("Já existe uma instância com esse nome!")));
                                    return;
                                }

                                string instancePath = System.IO.Path.Combine(InstanceManager.GetInstancesDirectory(), info.Name);
                                string targetModsDir = System.IO.Path.Combine(instancePath, "mods");
                                if (localMods.Count > 0 && !System.IO.Directory.Exists(targetModsDir)) System.IO.Directory.CreateDirectory(targetModsDir);

                                foreach (var localMod in localMods)
                                {
                                    if (System.IO.File.Exists(localMod))
                                    {
                                        System.IO.File.Copy(localMod, System.IO.Path.Combine(targetModsDir, System.IO.Path.GetFileName(localMod)), true);
                                    }
                                }

                                if (info.Modloader == "Fabric" && !info.MinecraftVersion.Contains("fabric-loader"))
                                {
                                    string defaultPath = CmlLib.Core.MinecraftPath.GetOSDefaultPath();
                                    await ModloaderInstaller.InstallFabricAsync(info.MinecraftVersion, defaultPath, text => {
                                        this.Invoke(new Action(() => SendStatusToWeb(text)));
                                    });
                                }
                                
                                this.Invoke(new Action(() => {
                                    SendStatusToWeb("Instância pronta!");
                                    var instances = InstanceManager.GetInstances();
                                    var msg = new { type = "instances", list = instances };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                                }));
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(new Action(() => SendErrorToWeb("Erro ao criar instância: " + ex.Message, ex)));
                            }
                        });
                    }
                    else if (action == "editInstance")
                    {
                        string oldName = root.GetProperty("oldName").GetString();
                        var info = JsonSerializer.Deserialize<InstanceInfo>(root.GetProperty("info").GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        try
                        {
                            if (!InstanceManager.UpdateInstance(oldName, info))
                            {
                                SendErrorToWeb("Já existe uma instância com esse nome (ou erro ao renomear)!", null);
                                return;
                            }
                            
                            // Reinstala o fabric na global se mudou para Fabric, ou só garante que tá lá
                            if (info.Modloader == "Fabric" && !info.MinecraftVersion.Contains("fabric-loader"))
                            {
                                Task.Run(async () => {
                                    string defaultPath = CmlLib.Core.MinecraftPath.GetOSDefaultPath();
                                    await ModloaderInstaller.InstallFabricAsync(info.MinecraftVersion, defaultPath, text => {});
                                });
                            }

                            var instances = InstanceManager.GetInstances();
                            var msg = new { type = "instances", list = instances };
                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                        }
                        catch (Exception ex)
                        {
                            SendErrorToWeb("Erro ao editar: " + ex.Message, ex);
                        }
                    }
                    else if (action == "deleteInstance")
                    {
                        string name = root.GetProperty("name").GetString();
                        try
                        {
                            InstanceManager.DeleteInstance(name);
                            var instances = InstanceManager.GetInstances();
                            var msg = new { type = "instances", list = instances };
                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                        }
                        catch (Exception ex)
                        {
                            SendErrorToWeb("Erro ao deletar: " + ex.Message, ex);
                        }
                    }
                    else if (action == "searchModpacks")
                    {
                        string query = root.GetProperty("query").GetString();
                        
                        Task.Run(async () => {
                            try
                            {
                                string resultsJson = await ModrinthAPI.SearchModpacksAsync(query);
                                using JsonDocument doc = JsonDocument.Parse(resultsJson);
                                var hits = doc.RootElement.GetProperty("hits");
                                
                                var msg = new { type = "modpackResults", results = hits };
                                this.Invoke(new Action(() => {
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                                }));
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(new Action(() => SendErrorToWeb("Erro na busca: " + ex.Message, ex)));
                            }
                        });
                    }
                    else if (action == "installModpack")
                    {
                        string slug = root.GetProperty("slug").GetString();
                        string projectId = root.GetProperty("projectId").GetString();

                        Task.Run(async () => {
                            try
                            {
                                string result = await ModpackInstaller.InstallModpackAsync(projectId, slug, (percent, msg) => {
                                    this.Invoke(new Action(() => {
                                        var pMsg = new { type = "progress", taskId = slug, percent = percent, detail = msg };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(pMsg));
                                    }));
                                });

                                this.Invoke(new Action(() => {
                                    if (result == "sucesso") {
                                        var sMsg = new { type = "downloadSuccess", taskId = slug, text = $"Modpack {slug} instalado!" };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(sMsg));
                                        
                                        // Force UI reload instances
                                        var instances = InstanceManager.GetInstances();
                                        var msgInstance = new { type = "instances", list = instances };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msgInstance));
                                    } else {
                                        var eMsg = new { type = "downloadError", taskId = slug, text = result };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                                    }
                                }));
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(new Action(() => {
                                    var eMsg = new { type = "downloadError", taskId = slug, text = "Erro ao instalar: " + ex.Message };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                                }));
                            }
                        });
                    }
                    else if (action == "searchMods" || action == "searchBuilderMods")
                    {
                        string query = root.GetProperty("query").GetString();
                        string ver = GetBaseMinecraftVersion(root.GetProperty("version").GetString());
                        string modloader = root.GetProperty("modloader").GetString();
                        if (modloader == "None") modloader = "fabric"; 
                        
                        Task.Run(async () => {
                            try
                            {
                                string resultsJson = await ModrinthAPI.SearchModsAsync(query, ver, modloader);
                                using JsonDocument doc = JsonDocument.Parse(resultsJson);
                                var hits = doc.RootElement.GetProperty("hits");
                                
                                string typeMsg = action == "searchMods" ? "modResults" : "builderModResults";
                                var msg = new { type = typeMsg, results = hits };
                                this.Invoke(new Action(() => {
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                                }));
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(new Action(() => SendErrorToWeb("Erro na busca: " + ex.Message, ex)));
                            }
                        });
                    }
                    else if (action == "getModVersions")
                    {
                        string projectId = root.GetProperty("projectId").GetString() ?? "";
                        string ver       = GetBaseMinecraftVersion(root.GetProperty("version").GetString() ?? "");
                        string modloader = root.GetProperty("modloader").GetString() ?? "fabric";
                        string slug      = root.GetProperty("slug").GetString() ?? "";
                        string title     = root.TryGetProperty("title", out var tp) ? tp.GetString() ?? slug : slug;
                        if (modloader == "None") modloader = "fabric";

                        Task.Run(async () =>
                        {
                            try
                            {
                                string loader = modloader.ToLower();
                                string url = $"https://api.modrinth.com/v2/project/{projectId}/version?loaders=[\"{loader}\"]&game_versions=[\"{ver}\"]";

                                using var http = new System.Net.Http.HttpClient();
                                http.DefaultRequestHeaders.Add("User-Agent", "StrafeClient/1.0 (contact@brlauncher.com)");
                                http.Timeout = TimeSpan.FromSeconds(15);

                                var resp = await http.GetAsync(url);
                                resp.EnsureSuccessStatusCode();
                                string json = await resp.Content.ReadAsStringAsync();

                                using var doc = JsonDocument.Parse(json);
                                var versions = new System.Collections.Generic.List<object>();
                                foreach (var v in doc.RootElement.EnumerateArray())
                                {
                                    string versionId     = v.TryGetProperty("id",             out var idP)     ? idP.GetString()     ?? "" : "";
                                    string versionName   = v.TryGetProperty("name",           out var nameP)   ? nameP.GetString()   ?? "" : "";
                                    string versionNumber = v.TryGetProperty("version_number", out var numP)    ? numP.GetString()    ?? "" : "";
                                    string datePublished = v.TryGetProperty("date_published",  out var dateP)   ? dateP.GetString()   ?? "" : "";
                                    string changelog     = v.TryGetProperty("changelog",       out var changeP) ? changeP.GetString() ?? "" : "";
                                    string vType         = v.TryGetProperty("version_type",   out var vtP)     ? vtP.GetString()     ?? "release" : "release";

                                    // Primary file info
                                    string filename = "";
                                    if (v.TryGetProperty("files", out var files) && files.GetArrayLength() > 0)
                                        filename = files[0].TryGetProperty("filename", out var fnP) ? fnP.GetString() ?? "" : "";

                                    versions.Add(new {
                                        id            = versionId,
                                        name          = versionName,
                                        version_number = versionNumber,
                                        date_published = datePublished,
                                        changelog     = changelog,
                                        version_type  = vType,
                                        filename
                                    });
                                }

                                this.Invoke(new Action(() =>
                                {
                                    var msg = new { type = "modVersions", slug, title, versions, projectId, modloader = modloader };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                                }));
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(new Action(() => SendErrorToWeb("Erro ao buscar versões: " + ex.Message, ex)));
                            }
                        });
                    }
                    else if (action == "installMod")
                    {
                        string slug        = root.GetProperty("slug").GetString() ?? "";
                        string projectId   = root.GetProperty("projectId").GetString() ?? "";
                        string ver         = GetBaseMinecraftVersion(root.GetProperty("version").GetString() ?? "");
                        string modloader   = root.GetProperty("modloader").GetString() ?? "fabric";
                        string instanceName = root.GetProperty("instanceName").GetString() ?? "";
                        // Optional: specific version id chosen by the user
                        string versionId   = root.TryGetProperty("versionId", out var vidP) ? vidP.GetString() ?? "" : "";
                        if (modloader == "None") modloader = "fabric";

                        // [SECURITY FIX CRIT-1] Canonicalize instance path to prevent traversal
                        string instancePath = InstanceManager.SafeResolvePath(instanceName);
 
                        Task.Run(async () => {
                            try
                            {
                                this.Invoke(new Action(() => {
                                    var pMsg = new { type = "progress", taskId = slug, percent = 50, detail = "Baixando .jar..." };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(pMsg));
                                }));

                                string result;
                                if (!string.IsNullOrEmpty(versionId))
                                    // User picked a specific version — install by exact version id
                                    result = await ModrinthAPI.InstallModByVersionIdAsync(versionId, instancePath);
                                else
                                    // Default: install latest compatible
                                    result = await ModrinthAPI.InstallModAsync(projectId, ver, modloader, instancePath);
                                
                                this.Invoke(new Action(() => {
                                    if (result == "sucesso") {
                                        var sMsg = new { type = "downloadSuccess", taskId = slug, text = $"Mod {slug} baixado!" };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(sMsg));
                                        // Refresh installed mods list
                                        string modsDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(instancePath, "mods"));
                                        var mods = new System.Collections.Generic.List<object>();
                                        if (System.IO.Directory.Exists(modsDir)) {
                                            foreach (var f in System.IO.Directory.GetFiles(modsDir, "*.jar"))
                                                mods.Add(new { filename = System.IO.Path.GetFileName(f), path = f, enabled = true });
                                            foreach (var f in System.IO.Directory.GetFiles(modsDir, "*.jar.disabled"))
                                                mods.Add(new { filename = System.IO.Path.GetFileNameWithoutExtension(f), path = f, enabled = false });
                                        }
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(new { type = "instanceMods", list = mods }));
                                    } else {
                                        var eMsg = new { type = "downloadError", taskId = slug, text = result };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                                    }
                                }));
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(new Action(() => {
                                    var eMsg = new { type = "downloadError", taskId = slug, text = "Erro ao baixar mod: " + ex.Message };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                                }));
                            }
                        });
                    }

                    else if (action == "searchResourcePacks")
                    {
                        string query = root.GetProperty("query").GetString();
                        Task.Run(async () => {
                            try
                            {
                                string resultsJson = await ModrinthAPI.SearchResourcePacksAsync(query);
                                using JsonDocument doc = JsonDocument.Parse(resultsJson);
                                var hits = doc.RootElement.GetProperty("hits");
                                var msg = new { type = "resourcePackResults", results = hits };
                                this.Invoke(new Action(() => {
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                                }));
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(new Action(() => SendErrorToWeb("Erro na busca de Resource Packs: " + ex.Message, ex)));
                            }
                        });
                    }
                    else if (action == "searchShaders")
                    {
                        string query = root.GetProperty("query").GetString();
                        Task.Run(async () => {
                            try
                            {
                                string resultsJson = await ModrinthAPI.SearchShadersAsync(query);
                                using JsonDocument doc = JsonDocument.Parse(resultsJson);
                                var hits = doc.RootElement.GetProperty("hits");
                                var msg = new { type = "shaderResults", results = hits };
                                this.Invoke(new Action(() => {
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                                }));
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(new Action(() => SendErrorToWeb("Erro na busca de Shaders: " + ex.Message, ex)));
                            }
                        });
                    }
                    else if (action == "installResourcePack")
                    {
                        string slug = root.GetProperty("slug").GetString() ?? "";
                        string versionId = root.GetProperty("versionId").GetString() ?? "";
                        string instanceName = root.GetProperty("instanceName").GetString() ?? "";
                        string instancePath = InstanceManager.SafeResolvePath(instanceName);

                        Task.Run(async () => {
                            try
                            {
                                this.Invoke(new Action(() => {
                                    var pMsg = new { type = "progress", taskId = slug, percent = 50, detail = "Baixando resource pack..." };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(pMsg));
                                }));
                                string result = await ModrinthAPI.InstallResourcePackAsync(versionId, instancePath);
                                this.Invoke(new Action(() => {
                                    if (result == "sucesso") {
                                        var sMsg = new { type = "downloadSuccess", taskId = slug, text = $"Resource Pack '{slug}' instalado!" };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(sMsg));
                                    } else {
                                        var eMsg = new { type = "downloadError", taskId = slug, text = result };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                                    }
                                }));
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(new Action(() => {
                                    var eMsg = new { type = "downloadError", taskId = slug, text = "Erro: " + ex.Message };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                                }));
                            }
                        });
                    }
                    else if (action == "installShader")
                    {
                        string slug = root.GetProperty("slug").GetString() ?? "";
                        string versionId = root.GetProperty("versionId").GetString() ?? "";
                        string instanceName = root.GetProperty("instanceName").GetString() ?? "";
                        string instancePath = InstanceManager.SafeResolvePath(instanceName);

                        // Verificar se a instância tem Iris ou OptiFine
                        bool hasIris = false;
                        bool hasOptifine = false;
                        string modsDir = System.IO.Path.Combine(instancePath, "mods");
                        if (System.IO.Directory.Exists(modsDir))
                        {
                            foreach (var f in System.IO.Directory.GetFiles(modsDir, "*.jar"))
                            {
                                string fn = System.IO.Path.GetFileName(f).ToLower();
                                if (fn.Contains("iris")) hasIris = true;
                                if (fn.Contains("optifine") || fn.Contains("optifabric")) hasOptifine = true;
                            }
                        }
                        bool hasShaderMod = hasIris || hasOptifine;

                        if (!hasShaderMod)
                        {
                            // Avisa o frontend mas ainda permite instalar
                            this.Invoke(new Action(() => {
                                var warnMsg = new { type = "shaderWarning", instanceName, slug };
                                webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(warnMsg));
                            }));
                        }

                        Task.Run(async () => {
                            try
                            {
                                this.Invoke(new Action(() => {
                                    var pMsg = new { type = "progress", taskId = slug, percent = 50, detail = "Baixando shader..." };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(pMsg));
                                }));
                                string result = await ModrinthAPI.InstallShaderAsync(versionId, instancePath);
                                this.Invoke(new Action(() => {
                                    if (result == "sucesso") {
                                        var sMsg = new { type = "downloadSuccess", taskId = slug, text = $"Shader '{slug}' instalado!" };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(sMsg));
                                    } else {
                                        var eMsg = new { type = "downloadError", taskId = slug, text = result };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                                    }
                                }));
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(new Action(() => {
                                    var eMsg = new { type = "downloadError", taskId = slug, text = "Erro: " + ex.Message };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                                }));
                            }
                        });
                    }
                    else if (action == "buildModpack")
                    {
                        string name = root.GetProperty("name").GetString();
                        string ver = root.GetProperty("version").GetString();
                        string modloader = root.GetProperty("modloader").GetString();
                        bool syncWorlds = root.TryGetProperty("syncWorlds", out var sw) && sw.GetBoolean();
                        var modsArray = root.GetProperty("mods").EnumerateArray();
                        
                        var modIds = new System.Collections.Generic.List<string>();
                        foreach (var mod in modsArray) {
                            modIds.Add(mod.GetString());
                        }

                        var localMods = new System.Collections.Generic.List<string>();
                        if (root.TryGetProperty("localMods", out var lMods)) {
                            foreach (var lm in lMods.EnumerateArray()) {
                                localMods.Add(lm.GetString());
                            }
                        }
                        
                        // Cria Instância
                        InstanceManager.CreateInstance(new InstanceInfo {
                            Name = name,
                            MinecraftVersion = ver,
                            Modloader = modloader,
                            SyncVanillaWorlds = syncWorlds
                        });
                        
                        string instancePath = System.IO.Path.Combine(InstanceManager.GetInstancesDirectory(), name);
                        
                        // Envia atualização de instâncias
                        var instancesMsg = new { type = "instances", list = InstanceManager.GetInstances() };
                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(instancesMsg));

                        Task.Run(async () => {
                            try
                            {
                                int total = modIds.Count + localMods.Count;
                                int done = 0;
                                
                                // Copia Local Mods
                                string targetModsDir = System.IO.Path.Combine(instancePath, "mods");
                                if (localMods.Count > 0 && !System.IO.Directory.Exists(targetModsDir)) System.IO.Directory.CreateDirectory(targetModsDir);

                                foreach (var localMod in localMods)
                                {
                                    if (System.IO.File.Exists(localMod))
                                    {
                                        System.IO.File.Copy(localMod, System.IO.Path.Combine(targetModsDir, System.IO.Path.GetFileName(localMod)), true);
                                    }
                                    done++;
                                    this.Invoke(new Action(() => {
                                        int pct = total == 0 ? 100 : (int)(((float)done / total) * 100);
                                        var pMsg = new { type = "progress", taskId = "builder", percent = pct, detail = $"Importando {System.IO.Path.GetFileName(localMod)}..." };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(pMsg));
                                    }));
                                }
                                
                                foreach (var projectId in modIds)
                                {
                                    await ModrinthAPI.InstallModAsync(projectId, GetBaseMinecraftVersion(ver), modloader, instancePath);
                                    done++;
                                    
                                    this.Invoke(new Action(() => {
                                        int pct = (int)(((float)done / total) * 100);
                                        var pMsg = new { type = "progress", taskId = "builder", percent = pct, detail = $"Baixando mod {done}/{total}..." };
                                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(pMsg));
                                    }));
                                }
                                
                                this.Invoke(new Action(() => {
                                    var sMsg = new { type = "downloadSuccess", taskId = "builder", text = $"Modpack {name} criado!" };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(sMsg));
                                }));
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(new Action(() => SendErrorToWeb("Erro ao gerar Modpack: " + ex.Message, ex)));
                                this.Invoke(new Action(() => {
                                    var eMsg = new { type = "downloadError", taskId = "builder", text = "Falha na montagem" };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(eMsg));
                                }));
                            }
                        });
                    }
                    else if (action == "checkForUpdates")
                    {
                        // Verificação manual de atualização (botão no UI)
                        Task.Run(async () =>
                        {
                            var update = await UpdateManager.CheckAsync();
                            this.Invoke(new Action(() =>
                            {
                                object msg;
                                if (update != null)
                                {
                                    msg = new
                                    {
                                        type = "updateAvailable",
                                        version = update.Versao,
                                        url = update.Url,
                                        notes = update.Notas,
                                        currentVersion = UpdateManager.VERSAO_ATUAL
                                    };
                                }
                                else
                                {
                                    msg = new { type = "updateStatus", hasUpdate = false, currentVersion = UpdateManager.VERSAO_ATUAL };
                                }
                                webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                            }));
                        });
                    }
                    else if (action == "downloadUpdate")
                    {
                        string updateUrl = root.GetProperty("url").GetString() ?? "";
                        if (string.IsNullOrEmpty(updateUrl)) return;

                        Task.Run(async () =>
                        {
                            // Envia progresso para o WebView
                            string? newExePath = await UpdateManager.DownloadAsync(updateUrl, percent =>
                            {
                                this.Invoke(new Action(() =>
                                {
                                    var p = new { type = "downloadProgress", percent };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(p));
                                }));
                            });

                            this.Invoke(new Action(() =>
                            {
                                if (newExePath != null)
                                {
                                    // Download OK — aplica update e fecha o app
                                    var doneMsg = new { type = "downloadComplete" };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(doneMsg));

                                    // Pequeno delay para o JS mostrar "Reiniciando..."
                                    System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ =>
                                    {
                                        this.Invoke(new Action(() =>
                                        {
                                            UpdateManager.ApplyUpdate(newExePath);
                                            Application.Exit();
                                        }));
                                    });
                                }
                                else
                                {
                                    var errMsg = new { type = "error", text = "Falha ao baixar atualização. Tente novamente." };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(errMsg));
                                }
                            }));
                        });
                    }
                    else if (action == "openInstanceFolder")
                    {
                        try
                        {
                            string name = root.GetProperty("name").GetString() ?? "";
                            // [SECURITY FIX CRIT-1] Canonicalize path to prevent traversal
                            string instanceBase = InstanceManager.SafeResolvePath(name);
                            string instancePath = System.IO.Path.Combine(instanceBase, "mods");
                            if (!System.IO.Directory.Exists(instancePath))
                            {
                                System.IO.Directory.CreateDirectory(instancePath);
                            }
                            // Only open directory paths (not arbitrary files)
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(instancePath) { UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            SendErrorToWeb("Erro ao abrir pasta: " + ex.Message, ex);
                        }
                    }
                    else if (action == "getInstanceMods")
                    {
                        try
                        {
                            string name = root.GetProperty("instanceName").GetString() ?? "";
                            string instanceBase = InstanceManager.SafeResolvePath(name);
                            string modsDir = System.IO.Path.Combine(instanceBase, "mods");
                            var mods = new System.Collections.Generic.List<object>();
                            if (System.IO.Directory.Exists(modsDir))
                            {
                                // Active mods (.jar)
                                foreach (var file in System.IO.Directory.GetFiles(modsDir, "*.jar"))
                                {
                                    mods.Add(new {
                                        filename = System.IO.Path.GetFileName(file),
                                        path = file,
                                        enabled = true
                                    });
                                }
                                // Disabled mods (.jar.disabled)
                                foreach (var file in System.IO.Directory.GetFiles(modsDir, "*.jar.disabled"))
                                {
                                    mods.Add(new {
                                        filename = System.IO.Path.GetFileNameWithoutExtension(file), // removes .disabled
                                        path = file,
                                        enabled = false
                                    });
                                }
                            }
                            var msg = new { type = "instanceMods", list = mods };
                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                        }
                        catch (Exception ex)
                        {
                            SendErrorToWeb("Erro ao listar mods: " + ex.Message, ex);
                        }
                    }
                    else if (action == "toggleMod")
                    {
                        try
                        {
                            string instanceName = root.GetProperty("instanceName").GetString() ?? "";
                            string filePath = root.GetProperty("filePath").GetString() ?? "";
                            bool currentlyEnabled = root.GetProperty("enabled").GetBoolean();

                            // Validate: file must be inside the instance mods directory
                            string instanceBase = InstanceManager.SafeResolvePath(instanceName);
                            string modsDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(instanceBase, "mods"));
                            string fullFilePath = System.IO.Path.GetFullPath(filePath);

                            if (!fullFilePath.StartsWith(modsDir + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                throw new System.Security.SecurityException("Path traversal detectado no toggleMod.");

                            if (!System.IO.File.Exists(fullFilePath))
                                throw new Exception("Arquivo não encontrado.");

                            string newPath;
                            if (currentlyEnabled)
                            {
                                // Disable: rename .jar -> .jar.disabled
                                newPath = fullFilePath + ".disabled";
                            }
                            else
                            {
                                // Enable: rename .jar.disabled -> .jar (strip .disabled)
                                newPath = fullFilePath.Substring(0, fullFilePath.Length - ".disabled".Length);
                            }

                            System.IO.File.Move(fullFilePath, newPath, overwrite: false);

                            // Re-send updated list
                            var mods = new System.Collections.Generic.List<object>();
                            foreach (var file in System.IO.Directory.GetFiles(modsDir, "*.jar"))
                                mods.Add(new { filename = System.IO.Path.GetFileName(file), path = file, enabled = true });
                            foreach (var file in System.IO.Directory.GetFiles(modsDir, "*.jar.disabled"))
                                mods.Add(new { filename = System.IO.Path.GetFileNameWithoutExtension(file), path = file, enabled = false });

                            var msg = new { type = "instanceMods", list = mods };
                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                        }
                        catch (Exception ex)
                        {
                            SendErrorToWeb("Erro ao alternar mod: " + ex.Message, ex);
                        }
                    }
                    else if (action == "removeMod")
                    {
                        try
                        {
                            string instanceName = root.GetProperty("instanceName").GetString() ?? "";
                            string filePath = root.GetProperty("filePath").GetString() ?? "";

                            // Validate: file must be inside the instance mods directory
                            string instanceBase = InstanceManager.SafeResolvePath(instanceName);
                            string modsDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(instanceBase, "mods"));
                            string fullFilePath = System.IO.Path.GetFullPath(filePath);

                            if (!fullFilePath.StartsWith(modsDir + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                throw new System.Security.SecurityException("Path traversal detectado no removeMod.");

                            // Only allow .jar and .jar.disabled extensions
                            string ext = System.IO.Path.GetExtension(fullFilePath).ToLowerInvariant();
                            string extWithoutDisabled = System.IO.Path.GetExtension(
                                System.IO.Path.GetFileNameWithoutExtension(fullFilePath)).ToLowerInvariant();
                            bool isValidExt = ext == ".jar" || (ext == ".disabled" && extWithoutDisabled == ".jar");
                            if (!isValidExt)
                                throw new Exception("Tipo de arquivo não permitido.");

                            if (System.IO.File.Exists(fullFilePath))
                                System.IO.File.Delete(fullFilePath);

                            // Re-send updated list
                            var mods = new System.Collections.Generic.List<object>();
                            if (System.IO.Directory.Exists(modsDir))
                            {
                                foreach (var file in System.IO.Directory.GetFiles(modsDir, "*.jar"))
                                    mods.Add(new { filename = System.IO.Path.GetFileName(file), path = file, enabled = true });
                                foreach (var file in System.IO.Directory.GetFiles(modsDir, "*.jar.disabled"))
                                    mods.Add(new { filename = System.IO.Path.GetFileNameWithoutExtension(file), path = file, enabled = false });
                            }

                            var msg = new { type = "instanceMods", list = mods };
                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
                        }
                        catch (Exception ex)
                        {
                            SendErrorToWeb("Erro ao remover mod: " + ex.Message, ex);
                        }
                    }
                    else if (action == "exportInstance")
                    {
                        try
                        {
                            string instanceName = root.GetProperty("instanceName").GetString() ?? "";
                            string instanceBase = InstanceManager.SafeResolvePath(instanceName);
                            string modsDir = System.IO.Path.Combine(instanceBase, "mods");

                            // Sanitize name for filename
                            string safeFileName = string.Join("_", instanceName.Split(System.IO.Path.GetInvalidFileNameChars()));
                            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                            string zipPath = System.IO.Path.Combine(desktopPath, $"{safeFileName}.zip");

                            // Remove previous export if exists
                            if (System.IO.File.Exists(zipPath))
                                System.IO.File.Delete(zipPath);

                            using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
                            {
                                // Add instance.json
                                string jsonPath = System.IO.Path.Combine(instanceBase, "instance.json");
                                if (System.IO.File.Exists(jsonPath))
                                    archive.CreateEntryFromFile(jsonPath, "instance.json", System.IO.Compression.CompressionLevel.Optimal);

                                // Add all .jar mods (active + disabled)
                                if (System.IO.Directory.Exists(modsDir))
                                {
                                    foreach (var jar in System.IO.Directory.GetFiles(modsDir, "*.jar"))
                                        archive.CreateEntryFromFile(jar, "mods/" + System.IO.Path.GetFileName(jar), System.IO.Compression.CompressionLevel.Optimal);
                                    foreach (var jar in System.IO.Directory.GetFiles(modsDir, "*.jar.disabled"))
                                        archive.CreateEntryFromFile(jar, "mods/" + System.IO.Path.GetFileName(jar), System.IO.Compression.CompressionLevel.Optimal);
                                }
                            }

                            int modCount = System.IO.Directory.Exists(modsDir)
                                ? System.IO.Directory.GetFiles(modsDir, "*.jar").Length + System.IO.Directory.GetFiles(modsDir, "*.jar.disabled").Length
                                : 0;

                            var successMsg = new { type = "exportInstanceResult", success = true, path = zipPath, modCount };
                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(successMsg));
                        }
                        catch (Exception ex)
                        {
                            var failMsg = new { type = "exportInstanceResult", success = false, error = ex.Message };
                            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(failMsg));
                        }
                    }
                    else if (action == "importInstance")
                    {
                        // Must run on UI thread (OpenFileDialog)
                        this.Invoke((Action)(() =>
                        {
                            try
                            {
                                string targetInstance = root.GetProperty("instanceName").GetString() ?? "";
                                string instanceBase = InstanceManager.SafeResolvePath(targetInstance);
                                string modsDir = System.IO.Path.Combine(instanceBase, "mods");

                                using var dlg = new OpenFileDialog
                                {
                                    Title = "Selecionar pacote de mods (.zip)",
                                    Filter = "Pacote de Instância (*.zip)|*.zip",
                                    Multiselect = false
                                };

                                if (dlg.ShowDialog(this) != DialogResult.OK)
                                {
                                    var cancelMsg = new { type = "importInstanceResult", success = false, cancelled = true };
                                    webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(cancelMsg));
                                    return;
                                }

                                string zipPath = dlg.FileName;
                                Directory.CreateDirectory(modsDir);

                                int imported = 0;
                                bool instanceJsonExtracted = false;
                                using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
                                {
                                    foreach (var entry in archive.Entries)
                                    {
                                        if (entry.FullName.Equals("instance.json", StringComparison.OrdinalIgnoreCase))
                                        {
                                            string destPathJson = System.IO.Path.Combine(instanceBase, "instance.json");
                                            entry.ExtractToFile(destPathJson, overwrite: true);
                                            instanceJsonExtracted = true;
                                            continue;
                                        }

                                        // Only extract files inside the mods/ folder
                                        if (!entry.FullName.StartsWith("mods/", StringComparison.OrdinalIgnoreCase))
                                            continue;

                                        string fileName = System.IO.Path.GetFileName(entry.FullName);
                                        if (string.IsNullOrEmpty(fileName)) continue;

                                        // Validate extension
                                        string entryExt = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
                                        string innerExt = System.IO.Path.GetExtension(System.IO.Path.GetFileNameWithoutExtension(fileName)).ToLowerInvariant();
                                        bool validExt = entryExt == ".jar" || (entryExt == ".disabled" && innerExt == ".jar");
                                        if (!validExt) continue;

                                        string destPath = System.IO.Path.Combine(modsDir, fileName);
                                        // Safety: ensure dest is still within modsDir
                                        string fullDest = System.IO.Path.GetFullPath(destPath);
                                        if (!fullDest.StartsWith(System.IO.Path.GetFullPath(modsDir) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                            continue;

                                        entry.ExtractToFile(destPath, overwrite: true);
                                        imported++;
                                    }
                                }

                                if (instanceJsonExtracted)
                                {
                                    string destPathJson = System.IO.Path.Combine(instanceBase, "instance.json");
                                    string json = System.IO.File.ReadAllText(destPathJson);
                                    var info = JsonSerializer.Deserialize<InstanceInfo>(json);
                                    if (info != null)
                                    {
                                        info.Name = targetInstance;
                                        System.IO.File.WriteAllText(destPathJson, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
                                    }
                                }
                                else
                                {
                                    var info = new InstanceInfo
                                    {
                                        Name = targetInstance,
                                        MinecraftVersion = "1.20.1",
                                        Modloader = "Fabric",
                                        SyncVanillaWorlds = true,
                                        EnableOptimization = true,
                                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                    };
                                    string destPathJson = System.IO.Path.Combine(instanceBase, "instance.json");
                                    System.IO.File.WriteAllText(destPathJson, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
                                }

                                var resultMsg = new { type = "importInstanceResult", success = true, instanceName = targetInstance, modCount = imported };
                                webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(resultMsg));
                            }
                            catch (Exception ex)
                            {
                                var failMsg = new { type = "importInstanceResult", success = false, error = ex.Message };
                                webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(failMsg));
                            }
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                SendErrorToWeb("Erro ao ler dados: " + ex.Message, ex);
            }
        }

        private async Task SendVersionsToWeb()
        {
            try
            {
                var path = new MinecraftPath();
                if (launcher == null) launcher = new MinecraftLauncher(path);
                
                CmlLib.Core.VersionMetadata.VersionMetadataCollection versions = null;
                int retries = 3;
                while (retries > 0)
                {
                    try
                    {
                        versions = await launcher.GetAllVersionsAsync();
                        break;
                    }
                    catch (IOException)
                    {
                        retries--;
                        if (retries == 0) throw;
                        await Task.Delay(1000);
                    }
                }
                
                var versionList = versions.Select(v => new 
                {
                    Name = v.Name,
                    Type = v.Type,
                    IsLocal = v.GetType().Name == "LocalVersionMetadata"
                }).ToList();

                var msg = new { type = "versions", list = versionList };
                webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
            }
            catch (Exception ex)
            {
                SendErrorToWeb("Erro ao iniciar jogo: " + ex.Message, ex);
            }
        }

        private async Task SetupCustomSkinLoader(string instanceRoot, string mcVersion, string loader)
        {
            try
            {
                SendStatusToWeb($"Configurando CustomSkinLoader para {mcVersion} ({loader})...");
                
                string modsPath = Path.Combine(instanceRoot, "mods");
                Directory.CreateDirectory(modsPath);
                
                // Limpa arquivos antigos do CSL para evitar duplicatas e problemas de versao
                foreach (var oldFile in Directory.GetFiles(modsPath, "CustomSkinLoader*.jar"))
                {
                    File.Delete(oldFile);
                }

                string cslPath = Path.Combine(modsPath, $"CustomSkinLoader_{loader}_{mcVersion}.jar");
                
                if (!File.Exists(cslPath))
                {
                    SendStatusToWeb("Buscando versão compatível do CustomSkinLoader na Modrinth...");
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "StrafeClient/1.0");
                        string apiUrl = $"https://api.modrinth.com/v2/project/customskinloader/version?game_versions=[\"{mcVersion}\"]&loaders=[\"{loader}\"]";
                        var responseStr = await client.GetStringAsync(apiUrl);
                        
                        using var doc = JsonDocument.Parse(responseStr);
                        var root = doc.RootElement;
                        if (root.GetArrayLength() > 0)
                        {
                            var latestVersion = root[0];
                            var files = latestVersion.GetProperty("files");
                            string fileUrl = null;
                            
                            // Encontra o arquivo primário ou usa o primeiro
                            foreach (var file in files.EnumerateArray())
                            {
                                if (file.TryGetProperty("primary", out var prim) && prim.GetBoolean())
                                {
                                    fileUrl = file.GetProperty("url").GetString();
                                    break;
                                }
                            }
                            if (fileUrl == null) fileUrl = files[0].GetProperty("url").GetString();
                            
                            SendStatusToWeb("Baixando CustomSkinLoader...");
                            var bytes = await client.GetByteArrayAsync(fileUrl);
                            File.WriteAllBytes(cslPath, bytes);
                        }
                        else
                        {
                            SendStatusToWeb("Nenhuma versão do CustomSkinLoader encontrada para este Minecraft.");
                            return; // Ignora se não houver versão
                        }
                    }
                }

                // Configura o CSL para apontar primariamente para nossa API Yggdrasil
                string cslConfigFolder = Path.Combine(instanceRoot, "CustomSkinLoader");
                if (!Directory.Exists(cslConfigFolder)) {
                    Directory.CreateDirectory(cslConfigFolder);
                }
                
                string cslConfigFile = Path.Combine(cslConfigFolder, "CustomSkinLoader.json");
                string configJson = @"{
  ""version"": ""14.19"",
  ""loadlist"": [
    {
      ""name"": ""StrafeAPI"",
      ""type"": ""Yggdrasil"",
      ""apiRoot"": ""https://brlaucher-api.vercel.app/api/yggdrasil/""
    },
    {
      ""name"": ""Mojang"",
      ""type"": ""MojangAPI""
    }
  ]
}";
                File.WriteAllText(cslConfigFile, configJson);

            }
            catch (Exception ex)
            {
                SendErrorToWeb("Aviso: Falha ao configurar Skins Customizadas. " + ex.Message, ex);
            }
        }

        private void SendSystemInfoToWeb()
        {
            try
            {
                long totalRamBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                int totalRamMb = (int)(totalRamBytes / (1024 * 1024));
                var msg = new { type = "systemInfo", totalRamMb = totalRamMb };
                webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
            }
            catch (Exception ex)
            {
                SendErrorToWeb("Erro ao ler info do sistema: " + ex.Message, ex);
            }
        }

        private static string GetBaseMinecraftVersion(string fullVersion)
        {
            if (string.IsNullOrWhiteSpace(fullVersion)) return "";
            if (fullVersion.Contains("fabric-loader")) return fullVersion.Split('-').Last();
            if (fullVersion.Contains("forge") || fullVersion.Contains("Forge")) return fullVersion.Split('-').First();
            if (fullVersion.Contains("OptiFine")) return fullVersion.Split('-').First();
            return fullVersion;
        }

        private static async Task EnsureFabricInstalled(string mcVersion, Action<string> statusCallback)
        {
            string defaultPath = MinecraftPath.GetOSDefaultPath();
            string versionsDir = Path.Combine(defaultPath, "versions");
            bool exists = false;
            if (Directory.Exists(versionsDir))
            {
                foreach (var dir in Directory.GetDirectories(versionsDir))
                {
                    string name = Path.GetFileName(dir);
                    if (name.Contains("fabric-loader") && name.Contains(mcVersion))
                    {
                        exists = true;
                        break;
                    }
                }
            }
            if (!exists)
            {
                statusCallback($"Instalando Fabric Loader para a versão {mcVersion}...");
                await ModloaderInstaller.InstallFabricAsync(mcVersion, defaultPath, text => statusCallback(text));
            }
        }

        private async Task SetupPerformanceMods(string instancePath, string mcVersion)
        {
            var performanceMods = new Dictionary<string, string>
            {
                { "sodium", "sodium" },
                { "lithium", "lithium" },
                { "ferritecore", "ferritecore" },
                { "entityculling", "entityculling" },
                { "immediatelyfast", "immediatelyfast" },
                { "modernfix", "modernfix" },
                { "krypton", "krypton" }
            };

            string modsDir = Path.Combine(instancePath, "mods");
            if (!Directory.Exists(modsDir))
                Directory.CreateDirectory(modsDir);

            var existingFiles = Directory.GetFiles(modsDir, "*.jar")
                                         .Select(Path.GetFileName)
                                         .ToList();

            foreach (var mod in performanceMods)
            {
                string modSlug = mod.Key;
                bool alreadyExists = existingFiles.Any(f => f.StartsWith(modSlug, StringComparison.OrdinalIgnoreCase));
                if (alreadyExists)
                    continue;

                this.Invoke(new Action(() => SendStatusToWeb($"Instalando mod de performance: {modSlug}...")));
                
                try
                {
                    string result = await ModrinthAPI.InstallModAsync(modSlug, mcVersion, "fabric", instancePath);
                    if (result == "sucesso")
                    {
                        Console.WriteLine($"Instalado {modSlug} com sucesso.");
                    }
                    else
                    {
                        Console.WriteLine($"Aviso ao instalar {modSlug}: {result}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao instalar {modSlug}: {ex.Message}");
                }
            }
        }

        private void SetupPerformanceOptions(string instancePath)
        {
            try
            {
                string optionsPath = Path.Combine(instancePath, "options.txt");
                if (!File.Exists(optionsPath))
                {
                    string content = 
                        "maxFps:260\r\n" +
                        "useVsync:false\r\n" +
                        "renderClouds:false\r\n" +
                        "ao:true\r\n" +
                        "graphicsMode:1\r\n" +
                        "renderDistance:8\r\n" +
                        "simulationDistance:8\r\n";
                    File.WriteAllText(optionsPath, content);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao criar options.txt de desempenho: " + ex.Message);
            }
        }

        private async Task LaunchGame(string username, string versaoAlvo, int ramMb, string instanceName)
        {
            try
            {
                SendStatusToWeb("Inicializando motor do launcher...");

                var instanceInfo = string.IsNullOrWhiteSpace(instanceName) || instanceName.ToLower() == "padrao"
                    ? null
                    : InstanceManager.GetInstances().FirstOrDefault(i => i.Name == instanceName);

                bool enableOptimization = instanceInfo == null || instanceInfo.EnableOptimization;

                MinecraftPath path;
                if (instanceInfo == null)
                {
                    path = new MinecraftPath();
                }
                else
                {
                    string defaultPath = MinecraftPath.GetOSDefaultPath();
                    string instancePath = Path.Combine(defaultPath, "instances", instanceInfo.Name);
                    path = new MinecraftPath(instancePath);
                    
                    // Centralizar arquivos pesados (economia de espaço brutal e resolve compatibilidade)
                    path.Versions = Path.Combine(defaultPath, "versions");
                    path.Assets = Path.Combine(defaultPath, "assets");
                    path.Library = Path.Combine(defaultPath, "libraries");
                    path.Runtime = Path.Combine(defaultPath, "runtime");

                    string targetMcVer = versaoAlvo;
                    if (targetMcVer.Contains("fabric-loader"))
                    {
                        targetMcVer = targetMcVer.Split('-').Last();
                    }

                    // Se mudou a versão jogada, limpa os mods de performance antigos
                    bool versionChanged = instanceInfo.LastRunVersion != targetMcVer;
                    if (versionChanged)
                    {
                        string modsDir = Path.Combine(path.BasePath, "mods");
                        if (Directory.Exists(modsDir))
                        {
                            var performanceModsSlugs = new[] { "sodium", "lithium", "ferritecore", "entityculling", "immediatelyfast", "modernfix", "krypton" };
                            foreach (var file in Directory.GetFiles(modsDir, "*.jar"))
                            {
                                string fileName = Path.GetFileName(file);
                                if (performanceModsSlugs.Any(slug => fileName.StartsWith(slug + "-", StringComparison.OrdinalIgnoreCase) || 
                                                                    fileName.StartsWith(slug + "_", StringComparison.OrdinalIgnoreCase) || 
                                                                    fileName.Equals(slug + ".jar", StringComparison.OrdinalIgnoreCase)))
                                {
                                    try { File.Delete(file); } catch {}
                                }
                            }
                        }

                        // Atualiza no JSON a versão e o LastRunVersion
                        instanceInfo.MinecraftVersion = targetMcVer;
                        instanceInfo.LastRunVersion = targetMcVer;
                        InstanceManager.UpdateInstance(instanceInfo.Name, instanceInfo);
                    }

                    bool isFabric = instanceInfo.Modloader == "Fabric";

                    // Se for Fabric, garante que o Fabric Loader está instalado para a versão selecionada
                    if (isFabric)
                    {
                        string mcVer = versaoAlvo;
                        if (mcVer.Contains("fabric-loader"))
                        {
                            mcVer = mcVer.Split('-').Last();
                        }
                        await EnsureFabricInstalled(mcVer, text => this.Invoke(new Action(() => SendStatusToWeb(text))));
                    }

                    // Auto-resolve Fabric profile if it exists globally AND instance asks for it
                    string versionsDir = path.Versions;
                    if (isFabric && Directory.Exists(versionsDir))
                    {
                        foreach (var dir in Directory.GetDirectories(versionsDir))
                        {
                            string dName = Path.GetFileName(dir);
                            if (dName.Contains("fabric-loader") && dName.Contains(versaoAlvo) && !versaoAlvo.Contains("fabric-loader"))
                            {
                                versaoAlvo = dName;
                                break;
                            }
                        }
                    }
                }

                launcher = new MinecraftLauncher(path);

                launcher.FileProgressChanged += (sender, args) =>
                {
                    int percent = args.TotalTasks > 0 ? (args.ProgressedTasks * 100) / args.TotalTasks : 0;
                    SendProgressToWeb(percent, args.Name);
                };

                SendStatusToWeb($"Verificando arquivos da versão {versaoAlvo}...");

                var activeAcc = AccountManager.GetActiveAccount();
                MSession session;

                if (activeAcc != null && activeAcc.IsMicrosoft)
                {
                    if (!string.IsNullOrEmpty(activeAcc.RefreshToken))
                    {
                        try
                        {
                            SendStatusToWeb("Renovando sessão da Microsoft...");
                            var newAuth = await MicrosoftAuthHelper.RefreshMicrosoftTokenAsync(activeAcc.RefreshToken);
                            activeAcc.Token = newAuth.Session.AccessToken;
                            activeAcc.RefreshToken = newAuth.RefreshToken;
                            AccountManager.SaveAccounts();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Erro ao renovar token (tentando prosseguir com token atual): " + ex.Message);
                            SendErrorToWeb("Sua sessão da Microsoft expirou ou é inválida. Por favor, remova sua conta e adicione-a novamente para poder jogar Multiplayer e abrir em LAN.");
                            return;
                        }
                    }

                    session = new MSession { 
                        Username = activeAcc.Username, 
                        AccessToken = activeAcc.Token, 
                        UUID = activeAcc.UUID,
                        ClientToken = Guid.NewGuid().ToString("N"),
                        UserType = "msa"
                    };
                }
                else
                {
                    // Gera o UUID offline determinístico igual ao usado na API Yggdrasil
                    // Padrão: MD5("OfflinePlayer:<nick>") com versão 3 (type 3) e variante 2
                    byte[] md5Bytes = System.Security.Cryptography.MD5.Create()
                        .ComputeHash(System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
                    md5Bytes[6] = (byte)((md5Bytes[6] & 0x0f) | 0x30);
                    md5Bytes[8] = (byte)((md5Bytes[8] & 0x3f) | 0x80);
                    string offlineUuid = BitConverter.ToString(md5Bytes).Replace("-", "").ToLower();
                    string offlineUuidFormatted = $"{offlineUuid.Substring(0,8)}-{offlineUuid.Substring(8,4)}-{offlineUuid.Substring(12,4)}-{offlineUuid.Substring(16,4)}-{offlineUuid.Substring(20)}";

                    string fakeToken = (activeAcc != null && !string.IsNullOrEmpty(activeAcc.Token)) ? activeAcc.Token : Guid.NewGuid().ToString("N");
                    session = new MSession(username, fakeToken, offlineUuidFormatted);
                    session.UserType = "mojang";
                }

                // Baixa explicitamente
                await launcher.InstallAsync(versaoAlvo);

                // Configurações e downloads de performance se estiver ativado
                if (enableOptimization && instanceInfo != null)
                {
                    string mcVer = versaoAlvo;
                    if (mcVer.Contains("fabric-loader"))
                    {
                        mcVer = mcVer.Split('-').Last();
                    }

                    if (instanceInfo.Modloader == "Fabric")
                    {
                        await SetupPerformanceMods(path.BasePath, mcVer);
                    }

                    SetupPerformanceOptions(path.BasePath);
                }

                // (O mod CustomSkinLoader foi removido em favor do Authlib-Injector)
                string authlibPath = System.IO.Path.Combine(MinecraftPath.GetOSDefaultPath(), "authlib-injector-1.2.7.jar");
                if (activeAcc == null || !activeAcc.IsMicrosoft)
                {
                    if (!System.IO.File.Exists(authlibPath))
                    {
                        SendStatusToWeb("Baixando sistema nativo de skins...");
                        using (var client = new System.Net.WebClient())
                        {
                            await client.DownloadFileTaskAsync("https://github.com/yushijinhun/authlib-injector/releases/download/v1.2.7/authlib-injector-1.2.7.jar", authlibPath);
                        }

                        // [SECURITY FIX HIGH-5] Verify SHA-256 of downloaded authlib-injector
                        // to prevent MITM attacks from injecting a malicious javaagent.
                        const string expectedSha256 = "eaf14bc5acffc7d885bd5bd5942b99f36d6299302beae356b2fc5807fe42652b"; // SHA-256 de authlib-injector-1.2.7.jar
                        if (!string.IsNullOrEmpty(expectedSha256))
                        {
                            using var sha = System.Security.Cryptography.SHA256.Create();
                            using var fs = System.IO.File.OpenRead(authlibPath);
                            string actualHash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLower();
                            if (!actualHash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                            {
                                System.IO.File.Delete(authlibPath); // Delete tampered file
                                throw new Exception($"[SEGURANÇA] Hash do authlib-injector não confere! Arquivo deletado. Esperado: {expectedSha256}, Recebido: {actualHash}");
                            }
                        }
                    }
                }


                // O CustomSkinLoader conflitava com o authlib-injector (que já intercepta as requisições de skin nativamente).
                // Removemos o CSL da pasta mods para garantir que o authlib-injector funcione perfeitamente.
                if (versaoAlvo.Contains("fabric") || versaoAlvo.Contains("forge"))
                {
                    string modsPath = Path.Combine(launcher.MinecraftPath.BasePath, "mods");
                    if (Directory.Exists(modsPath))
                    {
                        foreach (var cslJar in Directory.GetFiles(modsPath, "CustomSkinLoader*.jar"))
                        {
                            try { File.Delete(cslJar); } catch {}
                        }
                        
                        string cslFolder = Path.Combine(launcher.MinecraftPath.BasePath, "CustomSkinLoader");
                        if (Directory.Exists(cslFolder))
                        {
                            try { Directory.Delete(cslFolder, true); } catch {}
                        }
                    }
                }

                SendStatusToWeb("Pronto! Abrindo o Minecraft...");

                // Constrói o processo SEM ExtraJvmArguments (o CmlLib não os coloca na posição correta)
                var process = await launcher.BuildProcessAsync(versaoAlvo, new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = ramMb
                });

                // =========================================================
                // OBTÉM A STRING DE ARGUMENTOS DO PROCESSO
                // =========================================================
                string argsStr;
                if (process.StartInfo.ArgumentList != null && process.StartInfo.ArgumentList.Count > 0)
                {
                    argsStr = string.Join(" ", process.StartInfo.ArgumentList);
                    process.StartInfo.ArgumentList.Clear();
                }
                else
                {
                    argsStr = process.StartInfo.Arguments ?? "";
                }

                // PASSO 1: Corrigir fabric.gameJarPath se for versao Fabric
                if (versaoAlvo.Contains("fabric-loader"))
                {
                    string baseVer = versaoAlvo.Split('-').Last();
                    string gameJarPath = System.IO.Path.Combine(MinecraftPath.GetOSDefaultPath(), "versions", baseVer, baseVer + ".jar");
                    argsStr = System.Text.RegularExpressions.Regex.Replace(argsStr, @"-Dfabric\.gameJarPath=""[^""]*""", "");
                    argsStr = System.Text.RegularExpressions.Regex.Replace(argsStr, @"-Dfabric\.gameJarPath=\S+", "");
                    argsStr = $"-Dfabric.gameJarPath=\"{gameJarPath}\" " + argsStr.Trim();
                }

                // PASSO 2: Injetar authlib-injector para contas não-Microsoft
                if (activeAcc == null || !activeAcc.IsMicrosoft)
                {
                    argsStr = $"-javaagent:\"{authlibPath}\"=https://brlaucher-api.vercel.app/api/yggdrasil " + argsStr.Trim();
                }

                // PASSO 3: Remover arg incompativel e forçar IPv4 para LAN
                argsStr = argsStr.Replace("--sun-misc-unsafe-memory-access=allow", "");
                argsStr = "-Djava.net.preferIPv4Stack=true " + argsStr;

                // =========================================================
                // PASSO 4: AIKAR'S FLAGS — INJEÇÃO DIRETA ANTES DA MAIN CLASS
                // =========================================================
                string foundMainClass = null;
                if (enableOptimization)
                {
                    string aikarsFlags =
                        "-XX:+UseG1GC " +
                        "-XX:+ParallelRefProcEnabled " +
                        "-XX:MaxGCPauseMillis=200 " +
                        "-XX:+UnlockExperimentalVMOptions " +
                        "-XX:+DisableExplicitGC " +
                        "-XX:+AlwaysPreTouch " +
                        "-XX:G1NewSizePercent=30 " +
                        "-XX:G1MaxNewSizePercent=40 " +
                        "-XX:G1HeapRegionSize=8M " +
                        "-XX:G1ReservePercent=20 " +
                        "-XX:G1HeapWastePercent=5 " +
                        "-XX:G1MixedGCCountTarget=4 " +
                        "-XX:InitiatingHeapOccupancyPercent=15 " +
                        "-XX:G1MixedGCLiveThresholdPercent=90 " +
                        "-XX:G1RSetUpdatingPauseTimePercent=5 " +
                        "-XX:SurvivorRatio=32 " +
                        "-XX:+PerfDisableSharedMem " +
                        "-XX:MaxTenuringThreshold=1 " +
                        "-Daikars.new.flags=true ";

                    string[] mainClasses = {
                        "net.fabricmc.loader.impl.launch.knot.KnotClient",
                        "net.fabricmc.loader.impl.launch.knot.KnotServer",
                        "net.minecraft.client.main.Main",
                        "net.minecraft.server.Main",
                        "cpw.mods.bootstraplauncher.BootstrapLauncher"
                    };

                    foreach (var mc in mainClasses)
                    {
                        if (argsStr.Contains(mc))
                        {
                            foundMainClass = mc;
                            break;
                        }
                    }

                    if (foundMainClass != null)
                    {
                        argsStr = argsStr.Replace(foundMainClass, aikarsFlags + foundMainClass);
                    }
                    else
                    {
                        argsStr = aikarsFlags + argsStr;
                    }
                }

                // Aplica de volta no processo
                process.StartInfo.Arguments = argsStr;

                // LOG DE DIAGNÓSTICO
                // [SECURITY FIX HIGH-2] Redact access tokens from log before writing
                string logPath = System.IO.Path.Combine(MinecraftPath.GetOSDefaultPath(), "brlauncher-launch.log");
                string safeArgsStr = System.Text.RegularExpressions.Regex.Replace(
                    argsStr,
                    @"(--accessToken|accessToken[:=])\s*\S+",
                    "$1 [REDACTED]",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                // Also redact javaagent token (brlaucher-api URL contains no secret, but redact arg value after =)
                safeArgsStr = System.Text.RegularExpressions.Regex.Replace(
                    safeArgsStr,
                    @"(-javaagent:[^\s]+)=""[^""]+""",
                    "$1=[REDACTED]");
                System.IO.File.WriteAllText(logPath,
                    $"=== STRAFE CLIENT LAUNCH LOG ===\n" +
                    $"Data: {DateTime.Now}\n" +
                    $"Nick: {username}\n" +
                    $"Versao: {versaoAlvo}\n" +
                    $"RAM: {ramMb}MB\n" +
                    $"Otimizacao ativada: {enableOptimization}\n" +
                    $"MainClass detectada: {foundMainClass ?? "NÃO ENCONTRADA"}\n\n" +
                    $"=== ARGUMENTOS (TOKENS REDACTADOS) ===\n{safeArgsStr}");

                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    this.Invoke(new Action(() =>
                    {
                        try { webView.CoreWebView2.Resume(); } catch {}
                        this.Show();
                        this.WindowState = FormWindowState.Maximized;
                        SendStatusToWeb("Minecraft fechado. Bem-vindo de volta!");
                        var resetMsg = new { type = "status", text = "Pronto", resetUI = true };
                        webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(resetMsg));
                    }));
                };

                process.Start();

                // Eleva prioridade do processo para HIGH em background se as otimizações estiverem ativas
                if (enableOptimization)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        try { process.PriorityClass = ProcessPriorityClass.High; } catch {}
                    });
                }

                // Esconde o launcher e suspende WebView2 — libera RAM e CPU para o jogo
                this.Hide();
                try { await webView.CoreWebView2.TrySuspendAsync(); } catch {}
            }
            catch (Exception ex)
            {
                // Gravar erro completo em arquivo para diagnóstico
                string errLog = System.IO.Path.Combine(MinecraftPath.GetOSDefaultPath(), "brlauncher-error.log");
                System.IO.File.WriteAllText(errLog,
                    $"=== ERRO NO LAUNCHGAME ===\n" +
                    $"Data: {DateTime.Now}\n" +
                    $"Username: {username}\n" +
                    $"Versao: {versaoAlvo}\n" +
                    $"Mensagem: {ex.Message}\n\n" +
                    $"=== STACK TRACE ===\n{ex.StackTrace}\n\n" +
                    $"=== INNER EXCEPTION ===\n{ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}");
                SendErrorToWeb($"Erro no LaunchGame: {ex.Message}\nLog salvo em: {errLog}", ex);
            }
        }

        private void SendStatusToWeb(string text, bool resetUI = false)
        {
            var msg = new { type = "status", text = text, resetUI = resetUI };
            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
        }

        private void SendProgressToWeb(int percent, string detail)
        {
            var msg = new { type = "progress", percent = percent, detail = detail };
            webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
        }

        private void SendErrorToWeb(string message, Exception ex = null)
        {
            if (webView != null && webView.CoreWebView2 != null)
            {
                var msg = new { type = "error", text = message };
                webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(msg));
            }
            try 
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BrLauncher_error.log");
                string logContent = $"[{DateTime.Now}] {message}\n{(ex != null ? ex.StackTrace : "")}\n\n";
                File.AppendAllText(logPath, logContent);
            } catch {}
        }
    }
}
