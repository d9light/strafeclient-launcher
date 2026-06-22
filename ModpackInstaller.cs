using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace StrafeClient
{
    public class ModpackInstaller
    {
        private static readonly HttpClient client = new HttpClient();

        static ModpackInstaller()
        {
            client.DefaultRequestHeaders.Add("User-Agent", "StrafeClient/1.0 (contact@brlauncher.com)");
        }

        private static async Task DownloadFileWithProgressAsync(string url, string destinationPath, Action<int> progressCallback)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            long? totalBytes = response.Content.Headers.ContentLength;
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            
            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            int lastPercent = -1;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;
                if (totalBytes.HasValue)
                {
                    int percent = (int)(totalRead * 100 / totalBytes.Value);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progressCallback?.Invoke(percent);
                    }
                }
            }
        }

        public static async Task<string> InstallModpackAsync(string projectId, string slug, Action<int, string> progressCallback)
        {
            string tempZipPath = null;
            string extractPath = null;
            try
            {
                progressCallback(5, "Buscando versão do modpack...");
                
                string versionsUrl = $"https://api.modrinth.com/v2/project/{projectId}/version";
                HttpResponseMessage response = await client.GetAsync(versionsUrl);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.GetArrayLength() == 0)
                    return "Nenhuma versão encontrada para este Modpack.";

                var latestVersion = doc.RootElement[0];
                var files = latestVersion.GetProperty("files");
                
                string downloadUrl = null;
                foreach (var file in files.EnumerateArray())
                {
                    string filename = file.GetProperty("filename").GetString();
                    if (filename != null && filename.EndsWith(".mrpack"))
                    {
                        downloadUrl = file.GetProperty("url").GetString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                    return "Arquivo .mrpack não encontrado na versão mais recente.";

                progressCallback(10, "Baixando arquivo .mrpack...");
                tempZipPath = Path.Combine(Path.GetTempPath(), $"{projectId}.mrpack");
                
                await DownloadFileWithProgressAsync(downloadUrl, tempZipPath, p => {
                    progressCallback(10 + (p * 5 / 100), $"Baixando arquivo .mrpack ({p}%)...");
                });

                progressCallback(15, "Extraindo pacote...");
                extractPath = Path.Combine(Path.GetTempPath(), $"{projectId}_extracted");
                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);
                
                // [SECURITY FIX HIGH-2] Extract zip safely to prevent ZipSlip traversal
                Directory.CreateDirectory(extractPath);
                using (var archive = ZipFile.OpenRead(tempZipPath))
                {
                    string fullExtractPath = Path.GetFullPath(extractPath);
                    foreach (var entry in archive.Entries)
                    {
                        string destinationPath = Path.GetFullPath(Path.Combine(fullExtractPath, entry.FullName));
                        if (!destinationPath.StartsWith(fullExtractPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[SECURITY] ZipSlip bloqueado: {entry.FullName}");
                            continue;
                        }
                            
                        if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                            entry.ExtractToFile(destinationPath, true);
                        }
                    }
                }

                string indexJsonPath = Path.Combine(extractPath, "modrinth.index.json");
                if (!File.Exists(indexJsonPath))
                    return "modrinth.index.json não encontrado dentro do pacote.";

                string indexJson = await File.ReadAllTextAsync(indexJsonPath);
                using JsonDocument indexDoc = JsonDocument.Parse(indexJson);
                var root = indexDoc.RootElement;
                
                var deps = root.GetProperty("dependencies");
                string mcVersion = deps.TryGetProperty("minecraft", out var mcElement) ? mcElement.GetString() : "1.20.1";
                string modloader = "None";
                
                if (deps.TryGetProperty("fabric-loader", out _)) modloader = "Fabric";
                else if (deps.TryGetProperty("forge", out _)) modloader = "Forge"; // Exemplo caso o BrLauncher vá suportar
                
                progressCallback(20, $"Criando instância {slug} ({mcVersion} {modloader})...");

                // Criar instância
                var instanceInfo = new InstanceInfo
                {
                    Name = slug,
                    MinecraftVersion = mcVersion,
                    Modloader = modloader,
                    RamMb = 2048,
                    IconPath = ""
                };
                InstanceManager.CreateInstance(instanceInfo);
                
                string instanceDir = Path.Combine(InstanceManager.GetInstancesDirectory(), slug);
                
                // Instalar o Modloader caso seja Fabric
                if (modloader == "Fabric")
                {
                    progressCallback(25, "Garantindo que o Fabric Loader está instalado...");
                    string defaultPath = CmlLib.Core.MinecraftPath.GetOSDefaultPath();
                    await ModloaderInstaller.InstallFabricAsync(mcVersion, defaultPath, text => {
                        Console.WriteLine(text);
                    });
                }

                var downloadFiles = root.GetProperty("files");
                int totalFiles = downloadFiles.GetArrayLength();
                int currentFile = 0;

                progressCallback(30, $"Baixando mods (0/{totalFiles})...");

                foreach (var file in downloadFiles.EnumerateArray())
                {
                    string relPath = file.GetProperty("path").GetString() ?? "";
                    var downloads = file.GetProperty("downloads");
                    if (downloads.GetArrayLength() > 0)
                    {
                        string dlUrl = downloads[0].GetString();

                        // [SECURITY FIX CRIT-3] ZipSlip / Path Traversal protection:
                        // Canonicalize the destination and ensure it stays inside instanceDir.
                        string destPath = Path.GetFullPath(Path.Combine(instanceDir, relPath));
                        string canonicalBase = Path.GetFullPath(instanceDir);
                        if (!destPath.StartsWith(canonicalBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[SECURITY] Blocked path traversal attempt in modpack: '{relPath}'");
                            currentFile++;
                            continue; // Skip this malicious entry silently
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                        
                        try 
                        {
                            await DownloadFileWithProgressAsync(dlUrl, destPath, null);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao baixar {dlUrl}: {ex.Message}");
                        }
                    }
                    currentFile++;
                    
                    int p = 30 + (int)((currentFile / (float)totalFiles) * 60); // 30% a 90%
                    progressCallback(p, $"Baixando mods ({currentFile}/{totalFiles})...");
                }

                progressCallback(90, "Copiando configurações personalizadas (overrides)...");
                string overridesDir = Path.Combine(extractPath, "overrides");
                if (Directory.Exists(overridesDir))
                {
                    CopyDirectory(overridesDir, instanceDir);
                }

                progressCallback(100, "Modpack instalado com sucesso!");
                return "sucesso";
            }
            catch (Exception ex)
            {
                return $"Erro na instalação do modpack: {ex.Message}";
            }
            finally
            {
                // Limpeza
                if (tempZipPath != null && File.Exists(tempZipPath))
                    File.Delete(tempZipPath);
                
                if (extractPath != null && Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}
