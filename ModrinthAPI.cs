using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace StrafeClient
{
    public class ModrinthAPI
    {
        // [SECURITY FIX LOW-3] Set timeout to prevent infinite hangs on slow/unresponsive servers
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        static ModrinthAPI()
        {
            client.DefaultRequestHeaders.Add("User-Agent", "StrafeClient/1.0 (contact@brlauncher.com)");
        }

        private static async Task DownloadFileAsync(string url, string destinationPath)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new System.IO.FileStream(destinationPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, 8192, true);
            await contentStream.CopyToAsync(fileStream);
        }

        public static async Task<string> SearchModpacksAsync(string query)
        {
            try
            {
                string facets = $"[[\"project_type:modpack\"]]";
                string encodedFacets = Uri.EscapeDataString(facets);
                string encodedQuery = Uri.EscapeDataString(query);
                string sortIndex = string.IsNullOrWhiteSpace(query) ? "&index=downloads" : "";
                string url = $"https://api.modrinth.com/v2/search?query={encodedQuery}&facets={encodedFacets}&limit=20{sortIndex}";
                
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar Modpacks no Modrinth: {ex.Message}");
                return "{\"hits\": []}";
            }
        }

        public static async Task<string> SearchModsAsync(string query, string version, string modloader)
        {
            try
            {
                string facets = $"[[\"versions:{version}\"],[\"categories:{modloader.ToLower()}\"]]";
                string encodedFacets = Uri.EscapeDataString(facets);
                string encodedQuery = Uri.EscapeDataString(query);
                string sortIndex = string.IsNullOrWhiteSpace(query) ? "&index=downloads" : "";
                string url = $"https://api.modrinth.com/v2/search?query={encodedQuery}&facets={encodedFacets}&limit=20{sortIndex}";
                
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar no Modrinth: {ex.Message}");
                return "{\"hits\": []}";
            }
        }

        public static async Task<string> InstallModAsync(string projectId, string version, string modloader, string instancePath)
        {
            try
            {
                string loader = modloader.ToLower();
                string url = $"https://api.modrinth.com/v2/project/{projectId}/version?loaders=[\"{loader}\"]&game_versions=[\"{version}\"]";
                
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.GetArrayLength() > 0)
                {
                    var latestVersion = doc.RootElement[0];
                    var files = latestVersion.GetProperty("files");
                    if (files.GetArrayLength() > 0)
                    {
                        var primaryFile = files[0]; // TODO: Check if primary
                        string downloadUrl = primaryFile.GetProperty("url").GetString();
                        string filename = primaryFile.GetProperty("filename").GetString();
                        
                        string modsDir = System.IO.Path.Combine(instancePath, "mods");
                        System.IO.Directory.CreateDirectory(modsDir);
                        
                        string filePath = System.IO.Path.Combine(modsDir, filename);
                        
                        // Download file using stream to prevent OOM
                        await DownloadFileAsync(downloadUrl, filePath);
                        
                        return "sucesso";
                    }
                }
                return "Nenhuma versão compatível encontrada para download.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao instalar mod: {ex.Message}");
                return ex.Message;
            }
        }
        public static async Task<string> InstallModByVersionIdAsync(string versionId, string instancePath)
        {
            try
            {
                string url = $"https://api.modrinth.com/v2/version/{versionId}";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("files", out var files) && files.GetArrayLength() > 0)
                {
                    // Prefer the primary file; fall back to [0]
                    JsonElement chosen = files[0];
                    foreach (var f in files.EnumerateArray())
                    {
                        if (f.TryGetProperty("primary", out var pProp) && pProp.GetBoolean())
                        {
                            chosen = f;
                            break;
                        }
                    }

                    string downloadUrl = chosen.GetProperty("url").GetString();
                    string filename    = chosen.GetProperty("filename").GetString();

                    string modsDir  = System.IO.Path.Combine(instancePath, "mods");
                    System.IO.Directory.CreateDirectory(modsDir);

                    string filePath = System.IO.Path.Combine(modsDir, filename);
                    
                    // Download file using stream to prevent OOM
                    await DownloadFileAsync(downloadUrl, filePath);

                    return "sucesso";
                }

                return "Nenhum arquivo encontrado para a versão selecionada.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao instalar mod por versão: {ex.Message}");
                return ex.Message;
            }
        }
        public static async Task<string> SearchResourcePacksAsync(string query)
        {
            try
            {
                string facets = $"[[\"project_type:resourcepack\"]]";
                string encodedFacets = Uri.EscapeDataString(facets);
                string encodedQuery = Uri.EscapeDataString(query);
                string sortIndex = string.IsNullOrWhiteSpace(query) ? "&index=downloads" : "";
                string url = $"https://api.modrinth.com/v2/search?query={encodedQuery}&facets={encodedFacets}&limit=20{sortIndex}";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar Resource Packs: {ex.Message}");
                return "{\"hits\": []}";
            }
        }

        public static async Task<string> SearchShadersAsync(string query)
        {
            try
            {
                string facets = $"[[\"project_type:shader\"]]";
                string encodedFacets = Uri.EscapeDataString(facets);
                string encodedQuery = Uri.EscapeDataString(query);
                string sortIndex = string.IsNullOrWhiteSpace(query) ? "&index=downloads" : "";
                string url = $"https://api.modrinth.com/v2/search?query={encodedQuery}&facets={encodedFacets}&limit=20{sortIndex}";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar Shaders: {ex.Message}");
                return "{\"hits\": []}";
            }
        }

        public static async Task<string> InstallResourcePackAsync(string versionId, string instancePath)
        {
            try
            {
                string url = $"https://api.modrinth.com/v2/version/{versionId}";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("files", out var files) && files.GetArrayLength() > 0)
                {
                    JsonElement chosen = files[0];
                    foreach (var f in files.EnumerateArray())
                    {
                        if (f.TryGetProperty("primary", out var pProp) && pProp.GetBoolean())
                        { chosen = f; break; }
                    }
                    string downloadUrl = chosen.GetProperty("url").GetString();
                    string filename = chosen.GetProperty("filename").GetString();

                    string resourcepacksDir = System.IO.Path.Combine(instancePath, "resourcepacks");
                    System.IO.Directory.CreateDirectory(resourcepacksDir);
                    string filePath = System.IO.Path.Combine(resourcepacksDir, filename);
                    await DownloadFileAsync(downloadUrl, filePath);
                    return "sucesso";
                }
                return "Nenhum arquivo encontrado.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao instalar Resource Pack: {ex.Message}");
                return ex.Message;
            }
        }

        public static async Task<string> InstallShaderAsync(string versionId, string instancePath)
        {
            try
            {
                string url = $"https://api.modrinth.com/v2/version/{versionId}";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("files", out var files) && files.GetArrayLength() > 0)
                {
                    JsonElement chosen = files[0];
                    foreach (var f in files.EnumerateArray())
                    {
                        if (f.TryGetProperty("primary", out var pProp) && pProp.GetBoolean())
                        { chosen = f; break; }
                    }
                    string downloadUrl = chosen.GetProperty("url").GetString();
                    string filename = chosen.GetProperty("filename").GetString();

                    string shaderpacksDir = System.IO.Path.Combine(instancePath, "shaderpacks");
                    System.IO.Directory.CreateDirectory(shaderpacksDir);
                    string filePath = System.IO.Path.Combine(shaderpacksDir, filename);
                    await DownloadFileAsync(downloadUrl, filePath);
                    return "sucesso";
                }
                return "Nenhum arquivo encontrado.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao instalar Shader: {ex.Message}");
                return ex.Message;
            }
        }
    }
}
