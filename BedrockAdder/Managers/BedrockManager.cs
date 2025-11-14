using BedrockAdder.FileWorker;
using BedrockAdder.Library;
using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace BedrockAdder.Managers
{
    internal static class BedrockManager
    {
        // Creates a new Bedrock resource pack working folder and writes a minimal manifest.
        // Returns a PackSession, or null if we aborted (e.g., no write access or collision).
        public static PackSession? BeginNewPackOrAbort(string geyserPacksFolder, string packName, string packDescription, string packVersion)
        {
            try
            {
                if (!ValidateWriteAccess(geyserPacksFolder))
                {
                    ConsoleWorker.Write.Line("error", "No write access to " + geyserPacksFolder);
                    return null;
                }

                Directory.CreateDirectory(geyserPacksFolder);

                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string folderName = MakeSafeFolderName("bedrock_pack_" + timestamp);
                string packRoot = Path.Combine(geyserPacksFolder, folderName);

                if (PathExistsAndNotEmpty(packRoot))
                {
                    ConsoleWorker.Write.Line("warn", "Target pack root already exists and is not empty: " + packRoot + " (aborting)");
                    return null;
                }

                Directory.CreateDirectory(packRoot);
                ConsoleWorker.Write.Line("info", "Created pack root: " + packRoot);

                // Deterministic UUIDs (stable across runs for the same name+version)
                string manifestUuid = DeterministicGuidFromString("manifest:" + packName + ":" + packVersion).ToString();
                string moduleUuid = DeterministicGuidFromString("module:" + packName + ":" + packVersion).ToString();

                // Minimal structure
                Directory.CreateDirectory(Path.Combine(packRoot, "textures"));
                Directory.CreateDirectory(Path.Combine(packRoot, "sounds"));
                Directory.CreateDirectory(Path.Combine(packRoot, "font"));

                WriteMinimalManifest(packRoot, packName, packDescription, packVersion, manifestUuid, moduleUuid);

                return new PackSession(
                    geyserPacksFolder,
                    packRoot,
                    packName,
                    packDescription,
                    packVersion,
                    manifestUuid,
                    moduleUuid
                );
            }
            catch (Exception ex)
            {
                ConsoleWorker.Write.Line("error", "BeginNewPackOrAbort failed: " + ex.Message);
                return null;
            }
        }

        // Zips the pack folder into a .mcpack placed in OutputRoot.
        // Aborts if target file already exists.
        public static string FinalizeToMcpackOrAbort(PackSession session, string baseFileName)
        {
            try
            {
                Directory.CreateDirectory(session.OutputRoot);

                string safeBase = MakeSafeFileName(baseFileName);
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string mcpackPath = Path.Combine(session.OutputRoot, safeBase + "_" + timestamp + ".mcpack");

                if (File.Exists(mcpackPath))
                {
                    ConsoleWorker.Write.Line("warn", "mcpack already exists: " + mcpackPath + " (aborting)");
                    return string.Empty;
                }

                string tempZip = mcpackPath + ".zip";
                if (File.Exists(tempZip)) File.Delete(tempZip);

                ZipFile.CreateFromDirectory(session.PackRoot, tempZip, CompressionLevel.Optimal, includeBaseDirectory: false);
                File.Move(tempZip, mcpackPath);

                ConsoleWorker.Write.Line("info", "Wrote .mcpack: " + mcpackPath);
                return mcpackPath;
            }
            catch (Exception ex)
            {
                ConsoleWorker.Write.Line("error", "FinalizeToMcpackOrAbort failed: " + ex.Message);
                return string.Empty;
            }
        }

        // ---------- Utilities for builders ----------

        public static void EnsureDir(string absPath)
        {
            Directory.CreateDirectory(absPath);
        }

        public static string MakeBedrockItemId(string ns, string id)
        {
            string safeNs = SanitizeId(ns);
            string safeId = SanitizeId(id);
            return "ia:" + safeNs + "_" + safeId;
        }

        public static string NormalizeAtlasPath(string path)
        {
            return path.Replace("\\", "/").TrimStart('/');
        }

        // ---------- Internals ----------

        private static void WriteMinimalManifest(string rpRoot, string name, string description, string version, string manifestUuid, string moduleUuid)
        {
            string manifestPath = Path.Combine(rpRoot, "manifest.json");

            var manifest = new
            {
                format_version = 2,
                header = new
                {
                    name = name,
                    description = description,
                    uuid = manifestUuid,
                    version = ParseVersion(version),
                    min_engine_version = new[] { 1, 20, 0 }
                },
                modules = new object[]
                {
            new
            {
                type = "resources",
                uuid = moduleUuid,
                version = ParseVersion(version)
            }
                }
            };

            string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            File.WriteAllText(manifestPath, json, Encoding.UTF8);
            ConsoleWorker.Write.Line("info", "Wrote manifest.json (uuid=" + manifestUuid + ", module=" + moduleUuid + ")");
        }

        private static bool ValidateWriteAccess(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string probe = Path.Combine(folder, ".write_probe_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool PathExistsAndNotEmpty(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return false;
                using var e = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
                return e.MoveNext();
            }
            catch
            {
                // If we cannot enumerate, be safe and treat as occupied to avoid overwriting.
                return true;
            }
        }

        private static int[] ParseVersion(string v)
        {
            var parts = (v ?? "1.0.0").Split('.');
            int a = ParseSafe(parts, 0);
            int b = ParseSafe(parts, 1);
            int c = ParseSafe(parts, 2);
            return new[] { a, b, c };
        }

        private static int ParseSafe(string[] arr, int idx)
        {
            if (idx < arr.Length && int.TryParse(arr[idx], out var n)) return n;
            return 0;
        }

        private static string MakeSafeFolderName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                s = s.Replace(c, '_');
            }
            return s;
        }

        private static string MakeSafeFileName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                s = s.Replace(c, '_');
            }
            return s;
        }

        private static string SanitizeId(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "unknown";
            s = s.Trim();
            var sb = new StringBuilder();
            foreach (char ch in s)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_') sb.Append(char.ToLowerInvariant(ch));
                else sb.Append('_');
            }
            return sb.ToString();
        }

        private static Guid DeterministicGuidFromString(string input)
        {
            using var sha1 = SHA1.Create();
            var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));

            var g = new byte[16];
            Array.Copy(bytes, g, 16);

            // RFC 4122 variant + version 5 (SHA-1 derived)
            g[6] = (byte)((g[6] & 0x0F) | (5 << 4));
            g[8] = (byte)((g[8] & 0x3F) | 0x80);

            return new Guid(g);
        }
    }
}