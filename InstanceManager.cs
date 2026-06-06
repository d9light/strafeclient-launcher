using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
using CmlLib.Core;

namespace StrafeClient
{
    public class InstanceInfo
    {
        public string Name { get; set; }
        public string MinecraftVersion { get; set; }
        public string Modloader { get; set; } // "None", "Fabric"
        public int RamMb { get; set; }
        public string IconPath { get; set; }
        public string CreatedAt { get; set; }
        public bool SyncVanillaWorlds { get; set; }
        public bool EnableOptimization { get; set; } = true;
        public string LastRunVersion { get; set; }
    }

    public static class InstanceManager
    {
        public static string GetInstancesDirectory()
        {
            return Path.Combine(MinecraftPath.GetOSDefaultPath(), "instances");
        }

        // [SECURITY] Canonicalizes an instance-relative path and ensures it stays
        // within the instances root directory. Throws if a path traversal is detected.
        public static string SafeResolvePath(string instanceName)
        {
            string instancesDir = Path.GetFullPath(GetInstancesDirectory());
            // Strip invalid file name chars (same as CreateInstance)
            var invalidChars = Path.GetInvalidFileNameChars();
            string safeName = new string(instanceName.Where(ch => !invalidChars.Contains(ch)).ToArray());
            if (string.IsNullOrWhiteSpace(safeName))
                throw new ArgumentException("Nome de instância inválido.");

            string combined = Path.GetFullPath(Path.Combine(instancesDir, safeName));

            // Ensure the resolved path is still inside the instances directory
            if (!combined.StartsWith(instancesDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !combined.Equals(instancesDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException($"Path traversal detectado: '{instanceName}' resolve fora do diretório de instâncias.");
            }
            return combined;
        }

        public static List<InstanceInfo> GetInstances()
        {
            var instances = new List<InstanceInfo>();
            string instancesDir = GetInstancesDirectory();

            if (!Directory.Exists(instancesDir))
            {
                try { Directory.CreateDirectory(instancesDir); } catch {}
            }

            // Auto-create performance instance if it doesn't exist
            string optInstanceDir = Path.Combine(instancesDir, "Otimizado (Desempenho)");
            if (Directory.Exists(instancesDir) && !Directory.Exists(optInstanceDir))
            {
                try
                {
                    var optInfo = new InstanceInfo
                    {
                        Name = "Otimizado (Desempenho)",
                        MinecraftVersion = "26.1.2",
                        Modloader = "Fabric",
                        SyncVanillaWorlds = true,
                        EnableOptimization = true,
                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                    CreateInstance(optInfo);
                }
                catch { }
            }

            if (!Directory.Exists(instancesDir))
                return instances;

            foreach (var dir in Directory.GetDirectories(instancesDir))
            {
                string jsonPath = Path.Combine(dir, "instance.json");
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        string json = File.ReadAllText(jsonPath);
                        var info = JsonSerializer.Deserialize<InstanceInfo>(json);
                        if (info != null)
                            instances.Add(info);
                    }
                    catch { /* ignorar arquivos corrompidos */ }
                }
            }

            return instances;
        }

        public static void CreateDirectoryJunction(string linkPath, string targetPath)
        {
            if (Directory.Exists(linkPath)) return;
            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);

            // [SECURITY FIX MED-2] Use ArgumentList instead of string interpolation
            // to prevent cmd.exe injection via paths containing quotes or shell operators.
            var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("mklink");
            psi.ArgumentList.Add("/J");
            psi.ArgumentList.Add(linkPath);
            psi.ArgumentList.Add(targetPath);
            System.Diagnostics.Process.Start(psi)?.WaitForExit();
        }

        public static bool CreateInstance(InstanceInfo info)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            string safeName = new string(info.Name.Where(ch => !invalidChars.Contains(ch)).ToArray());
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "Instancia_Padrao";
            info.Name = safeName;

            string instanceDir = Path.Combine(GetInstancesDirectory(), info.Name);
            if (Directory.Exists(instanceDir))
            {
                return false; // Já existe!
            }

            Directory.CreateDirectory(instanceDir);

            string vanillaPath = MinecraftPath.GetOSDefaultPath();
            if (info.SyncVanillaWorlds)
            {
                CreateDirectoryJunction(Path.Combine(instanceDir, "saves"), Path.Combine(vanillaPath, "saves"));
            }

            // Texturas e Shaders são sempre sincronizados com o Vanilla (decisão permanente)
            CreateDirectoryJunction(Path.Combine(instanceDir, "resourcepacks"), Path.Combine(vanillaPath, "resourcepacks"));
            CreateDirectoryJunction(Path.Combine(instanceDir, "shaderpacks"), Path.Combine(vanillaPath, "shaderpacks"));

            info.CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string jsonPath = Path.Combine(instanceDir, "instance.json");
            string json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json);
            return true;
        }

        public static bool UpdateInstance(string oldName, InstanceInfo newInfo)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            string safeName = new string(newInfo.Name.Where(ch => !invalidChars.Contains(ch)).ToArray());
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "Instancia_Padrao";
            newInfo.Name = safeName;

            var dir = GetInstancesDirectory();
            string oldPath = Path.Combine(dir, oldName);
            string newPath = Path.Combine(dir, newInfo.Name);

            if (!Directory.Exists(oldPath))
                return false;

            if (oldName != newInfo.Name)
            {
                if (Directory.Exists(newPath))
                    return false;
                
                Directory.Move(oldPath, newPath);
            }

            string jsonPath = Path.Combine(newPath, "instance.json");
            string json = JsonSerializer.Serialize(newInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json);
            return true;
        }

        public static void DeleteInstance(string name)
        {
            // [SECURITY FIX CRIT-1] Canonicalize and validate before delete
            string path = SafeResolvePath(name);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
