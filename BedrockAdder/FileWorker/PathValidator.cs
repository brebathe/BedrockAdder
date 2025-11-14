using BedrockAdder.Library;
using System.Diagnostics;
using System.IO;

namespace BedrockAdder.FileWorker
{
    internal class PathValidator
    {
        internal static void CheckPaths(string pathToCheck, string pathType)
        {
            Debug.WriteLine("Selected path: " + pathToCheck);
            if (pathToCheck.EndsWith("\\") == false)
            {
                pathToCheck = pathToCheck + "\\";
            }
            if (Directory.Exists(pathToCheck))
            {
                if (pathType == "IAFolder")
                {
                    Debug.WriteLine("Checking ItemsAdder folder...");
                    if (File.Exists(pathToCheck + "storage\\font_images_unicode_cache.yml") && File.Exists(pathToCheck + "storage\\items_ids_cache.yml") && File.Exists(pathToCheck + "storage\\real_blocks_ids_cache.yml") && File.Exists(pathToCheck + "storage\\real_blocks_note_ids_cache.yml") && File.Exists(pathToCheck + "storage\\real_transparent_blocks_ids_cache.yml") && File.Exists(pathToCheck + "storage\\real_wire_ids_cache.yml"))
                    {
                        Bools.ValidIAFolder = true;
                        Debug.WriteLine("Found required ItemsAdder files.");
                    }
                }
                if (pathType == "GeyserPackFolder")
                {
                    Bools.ValidGeyserPackFolder = true;
                    Debug.WriteLine("Found Geyser Pack folder.");
                }
                if (pathType == "GeyserMappingsFolder")
                {
                    Bools.ValidGeyserMappingsFolder = true;
                    Debug.WriteLine("Found Geyser Mappings folder.");
                }
            }
        }
    }
}
