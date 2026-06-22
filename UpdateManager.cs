#nullable enable
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace StrafeClient
{
    /// <summary>
    /// Gerencia verificação e aplicação de atualizações automáticas do Strafe Client.
    /// Fluxo: CheckAsync → DownloadAsync → ApplyUpdate → updater.bat substitui o .exe
    ///
    /// Como funciona:
    ///   1. Consulta a API do GitHub Releases para o repositório configurado.
    ///   2. Compara a tag da última release com VERSAO_ATUAL.
    ///   3. Se houver versão mais nova, procura um asset .exe na release e retorna a URL.
    ///   4. O launcher baixa o novo .exe e o updater.bat substitui o atual em disco.
    ///
    /// O que você precisa fazer para lançar uma atualização:
    ///   1. Compile o projeto (dotnet publish ou via Visual Studio).
    ///   2. Vá em github.com/d9light/strafeclient-launcher → Releases → "Draft a new release".
    ///   3. Crie uma tag no formato vX.Y.Z (ex: v1.0.1).
    ///   4. Faça upload do StrafeClient.exe (ou do .zip com tudo) como asset da release.
    ///   5. Publique a release. O launcher dos usuários vai detectar e baixar automaticamente!
    /// </summary>
    public static class UpdateManager
    {
        // ============================================================
        // VERSÃO ATUAL — deve bater com a tag da release mais recente
        // Incremente isso ANTES de compilar e criar a release no GitHub.
        // Ex: se criar a release com tag "v1.0.1", coloque "1.0.1" aqui.
        // ============================================================
        public const string VERSAO_ATUAL = "1.0.2";

        // ============================================================
        // Repositório do GitHub — altere se mudar o repo
        // ============================================================
        private const string GITHUB_OWNER = "d9light";
        private const string GITHUB_REPO  = "strafeclient-launcher";

        private const string GITHUB_API_LATEST =
            "https://api.github.com/repos/" + GITHUB_OWNER + "/" + GITHUB_REPO + "/releases/latest";

        // Nome do arquivo .exe que você faz upload na Release.
        // Pode mudar se quiser, mas tem que bater com o nome do asset lá no GitHub.
        private const string EXE_ASSET_NAME = "StrafeClient.exe";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // ============================================================
        // Resultado da verificação de atualização
        // ============================================================
        public record UpdateInfo(string Versao, string Url, string Notas);

        // ============================================================
        // 1. VERIFICAR SE HÁ ATUALIZAÇÃO (consulta GitHub Releases)
        // ============================================================
        public static async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                // GitHub exige User-Agent
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("User-Agent", $"StrafeClient/{VERSAO_ATUAL}");
                _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

                string json = await _http.GetStringAsync(GITHUB_API_LATEST);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // A tag da release (ex: "v1.0.1")
                string tagName = root.GetProperty("tag_name").GetString() ?? "0.0.0";

                // Notas da release (corpo do markdown)
                string notas = root.TryGetProperty("body", out var bodyProp)
                    ? bodyProp.GetString() ?? ""
                    : "";

                if (!IsNewerVersion(tagName, VERSAO_ATUAL))
                    return null;

                // Procura o asset .exe nos arquivos da release
                string downloadUrl = "";
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string assetName = asset.TryGetProperty("name", out var nameProp)
                            ? nameProp.GetString() ?? ""
                            : "";

                        // Aceita o asset cujo nome termina em .exe (ou bate com EXE_ASSET_NAME)
                        if (assetName.Equals(EXE_ASSET_NAME, StringComparison.OrdinalIgnoreCase)
                            || assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.TryGetProperty("browser_download_url", out var urlProp)
                                ? urlProp.GetString() ?? ""
                                : "";
                            break;
                        }
                    }
                }

                // Se não há asset .exe, não tem como atualizar automaticamente
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    System.Diagnostics.Debug.WriteLine("[Update] Release encontrada mas sem asset .exe para download.");
                    return null;
                }

                return new UpdateInfo(tagName, downloadUrl, notas);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                // Nenhuma release publicada ainda — normal no início do projeto
                System.Diagnostics.Debug.WriteLine("[Update] Nenhuma release encontrada no GitHub (404).");
            }
            catch (Exception ex)
            {
                // Falha silenciosa — não bloqueia o lançamento do app
                System.Diagnostics.Debug.WriteLine($"[Update] Falha na verificação: {ex.Message}");
            }

            return null;
        }

        // ============================================================
        // 2. BAIXAR O NOVO EXECUTÁVEL (com callback de progresso 0–100)
        // ============================================================
        public static async Task<string?> DownloadAsync(string url, Action<int> onProgress)
        {
            try
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StrafeClient.exe");
                string exeDir = Path.GetDirectoryName(currentExe) ?? AppDomain.CurrentDomain.BaseDirectory;
                string destPath = Path.Combine(exeDir, "StrafeClient_new.exe");

                // GitHub Releases redireciona para o CDN — precisa seguir redirects
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("User-Agent", $"StrafeClient/{VERSAO_ATUAL}");

                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                byte[] buffer = new byte[8192];
                long downloadedBytes = 0;
                int bytesRead;
                int lastPercent = -1;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        int percent = (int)(downloadedBytes * 100L / totalBytes);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            onProgress(percent);
                        }
                    }
                }

                return destPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Update] Falha no download: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // 3. APLICAR A ATUALIZAÇÃO — grava updater.bat e fecha o app
        // ============================================================
        public static void ApplyUpdate(string newExePath)
        {
            // [SECURITY FIX HIGH-1] Validar se o arquivo baixado é realmente um executável PE (MZ header)
            // Impede execução de HTML (páginas de erro de proxy/CDN) ou arquivos corrompidos.
            // Para maior segurança, implemente checagem Authenticode com: X509Certificate.CreateFromSignedFile
            try
            {
                using var fs = new FileStream(newExePath, FileMode.Open, FileAccess.Read);
                var header = new byte[2];
                fs.Read(header, 0, 2);
                if (header[0] != 0x4D || header[1] != 0x5A) // 'M' 'Z'
                {
                    System.Diagnostics.Debug.WriteLine("O arquivo baixado não é um executável válido.");
                    return;
                }
            }
            catch { return; }

            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StrafeClient.exe");
            string exeDir = Path.GetDirectoryName(currentExe) ?? AppDomain.CurrentDomain.BaseDirectory;
            string updaterPath = Path.Combine(exeDir, "updater.ps1");
            string exeName = Path.GetFileName(currentExe);
            string processName = Path.GetFileNameWithoutExtension(exeName);

            // Script PowerShell oculto que aguarda o processo fechar, substitui o exe e reinicia
            string script = $@"
$ErrorActionPreference = 'SilentlyContinue'
$processName = '{processName}'
$currentExe = '{currentExe}'
$newExePath = '{newExePath}'

$timeout = 15
while ((Get-Process -Name $processName -ErrorAction SilentlyContinue) -and $timeout -gt 0) {{
    Start-Sleep -Seconds 1
    $timeout--
}}

Move-Item -Path $newExePath -Destination $currentExe -Force

if (Test-Path $currentExe) {{
    Start-Process -FilePath $currentExe
}}

Remove-Item -Path $PSCommandPath -Force
";
            File.WriteAllText(updaterPath, script, System.Text.Encoding.UTF8);

            // Inicia o powershell oculto
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{updaterPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        // ============================================================
        // Helper — compara versões SemVer simples (Major.Minor.Patch)
        // Aceita tags com ou sem "v" na frente (ex: "v1.0.1" ou "1.0.1")
        // ============================================================
        private static bool IsNewerVersion(string remote, string current)
        {
            try
            {
                var r = ParseVersion(remote);
                var c = ParseVersion(current);
                return r.Item1 > c.Item1
                    || (r.Item1 == c.Item1 && r.Item2 > c.Item2)
                    || (r.Item1 == c.Item1 && r.Item2 == c.Item2 && r.Item3 > c.Item3);
            }
            catch { return false; }
        }

        private static (int, int, int) ParseVersion(string v)
        {
            var parts = v.TrimStart('v').Split('.');
            return (
                parts.Length > 0 ? int.Parse(parts[0]) : 0,
                parts.Length > 1 ? int.Parse(parts[1]) : 0,
                parts.Length > 2 ? int.Parse(parts[2]) : 0
            );
        }
    }
}
