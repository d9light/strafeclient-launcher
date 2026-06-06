using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace StrafeClient
{
    public static class ModloaderInstaller
    {
        public static async Task InstallFabricAsync(string mcVersion, string instanceDir, Action<string> onProgress)
        {
            // URL fixa do instalador universal CLI do Fabric
            string installerUrl = "https://maven.fabricmc.net/net/fabricmc/fabric-installer/1.0.1/fabric-installer-1.0.1.jar";
            string installerPath = Path.Combine(Path.GetTempPath(), "fabric-installer.jar");

            onProgress?.Invoke("Baixando instalador do Fabric...");

            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(installerUrl);
                response.EnsureSuccessStatusCode();
                using (var fs = new FileStream(installerPath, FileMode.Create, FileAccess.Write))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }

            onProgress?.Invoke("Injetando Fabric na instância...");

            var tcs = new TaskCompletionSource<bool>();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{installerPath}\" client -dir \"{instanceDir}\" -mcversion {mcVersion}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            process.Exited += async (s, e) =>
            {
                if (process.ExitCode == 0) tcs.SetResult(true);
                else 
                {
                    string err = await process.StandardError.ReadToEndAsync();
                    string outStr = await process.StandardOutput.ReadToEndAsync();
                    tcs.SetException(new Exception($"Falha (Código {process.ExitCode}): {err}\n{outStr}"));
                }
            };

            process.Start();
            await tcs.Task;

            onProgress?.Invoke("Fabric instalado com sucesso!");
        }
    }
}
