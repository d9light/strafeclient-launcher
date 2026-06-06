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
    /// </summary>
    public static class UpdateManager
    {
        // ============================================================
        // VERSÃO ATUAL — incremente isso a cada release publicado
        // ============================================================
        public const string VERSAO_ATUAL = "1.0.0";

        private const string API_VERSAO_URL = "https://brlaucher-api.vercel.app/api/versao";
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // ============================================================
        // Resultado da verificação de atualização
        // ============================================================
        public record UpdateInfo(string Versao, string Url, string Notas);

        // ============================================================
        // 1. VERIFICAR SE HÁ ATUALIZAÇÃO
        // ============================================================
        public static async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("User-Agent", $"StrafeClient/{VERSAO_ATUAL}");

                string json = await _http.GetStringAsync(API_VERSAO_URL);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string versaoRemota = root.GetProperty("version").GetString() ?? "0.0.0";
                string url = root.GetProperty("url").GetString() ?? "";
                string notas = root.TryGetProperty("notes", out var notesProp)
                    ? notesProp.GetString() ?? ""
                    : "";

                if (IsNewerVersion(versaoRemota, VERSAO_ATUAL))
                    return new UpdateInfo(versaoRemota, url, notas);
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
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string destPath = Path.Combine(exeDir, "StrafeClient_new.exe");

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
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string currentExe = Path.Combine(exeDir, "StrafeClient.exe");
            string updaterPath = Path.Combine(exeDir, "updater.bat");

            // Script que aguarda o processo fechar, substitui o exe e reinicia
            string script = $@"@echo off
title Strafe Client — Atualizando...
echo Aguardando o Strafe Client fechar...
timeout /t 2 /nobreak >nul

:wait_loop
tasklist /FI ""IMAGENAME eq StrafeClient.exe"" 2>nul | find /I ""StrafeClient.exe"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait_loop
)

echo Aplicando atualizacao...
move /Y ""{newExePath}"" ""{currentExe}"" >nul

echo Reiniciando o Strafe Client...
start """" ""{currentExe}""

del ""%~f0""
";

            File.WriteAllText(updaterPath, script, System.Text.Encoding.ASCII);

            // Lança o updater.bat de forma minimizada e fecha o app
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{updaterPath}\"",
                WindowStyle = ProcessWindowStyle.Minimized,
                CreateNoWindow = false
            });
        }

        // ============================================================
        // Helper — compara versões SemVer simples (Major.Minor.Patch)
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
