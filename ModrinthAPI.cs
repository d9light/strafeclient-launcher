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

        public static async Task<string> SearchModpacksAsync(string query)
        {
            try
            {
                string facets = $"[[\"project_type:modpack\"]]";
                string encodedFacets = Uri.EscapeDataString(facets);
                string encodedQuery = Uri.EscapeDataString(query);
                
                string url = $"https://api.modrinth.com/v2/search?query={encodedQuery}&facets={encodedFacets}&limit=20";
                
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
                // Format facets for version and modloader
                string facets = $"[[\"versions:{version}\"],[\"categories:{modloader.ToLower()}\"]]";
                string encodedFacets = Uri.EscapeDataString(facets);
                string encodedQuery = Uri.EscapeDataString(query);
                
                string url = $"https://api.modrinth.com/v2/search?query={encodedQuery}&facets={encodedFacets}&limit=20";
                
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
                        
                        // Download file
                        byte[] fileBytes = await client.GetByteArrayAsync(downloadUrl);
                        await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);
                        
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
    }
}
