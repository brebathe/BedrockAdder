using BedrockAdder.ConsoleWorker;
using BedrockAdder.Library;
using System.IO;
using System.Threading.Tasks;

namespace BedrockAdder.FileWorker
{
    internal static class LoadFiles
    {
        internal static void ScanYamlFiles(string iaPluginFolder)
        {
            if (!Directory.Exists(iaPluginFolder))
            {
                Task.Run(() => Write.Line("error", $"ItemsAdder plugin folder not found: {iaPluginFolder}"));
                return;
            }

            string[] yamlFiles = Directory.GetFiles(iaPluginFolder, "*.yml", SearchOption.AllDirectories);

            int countFonts = 0;
            int countItems = 0;
            int countBlocks = 0;
            int countSounds = 0;
            int countFurniture = 0;
            int countArmors = 0;
            int countUnknown = 0;

            foreach (var file in yamlFiles)
            {
                Write.Line("info", $"Checking file: {file}");

                string type = MainYamlParserWorker.ClassifyYaml(file);
                switch (type)
                {
                    case "Font":
                        countFonts++;
                        Lists.CustomFontPaths.Add(file);
                        break;
                    case "Item":
                        Lists.CustomItemPaths.Add(file);
                        countItems++;
                        break;
                    case "Block":
                        Lists.CustomBlockPaths.Add(file);
                        countBlocks++;
                        break;
                    case "Sound":
                        Lists.CustomSoundPaths.Add(file);
                        countSounds++;
                        break;
                    case "Furniture":
                        Lists.CustomFurniturePaths.Add(file);
                        countFurniture++;
                        break;
                    case "Armor":
                        Lists.CustomArmorPaths.Add(file);
                        countArmors++;
                        break;
                    case "Unknown":
                        Lists.UnknownPaths.Add(file);
                        countUnknown++;
                        break;
                    case "Skip":
                        Lists.SkippedFilePaths.Add(file);
                        break;
                }
            }

            int total = countFonts + countItems + countBlocks + countSounds + countFurniture + countArmors;

            Write.Line("info", $"Scan complete ✅ Found: {total} files to parse");
            Write.Line("info", $"Fonts: {countFonts}, Items: {countItems}, Blocks: {countBlocks}, Sounds: {countSounds}, Furniture: {countFurniture}, Armors: {countArmors}, Unknown: {countUnknown}");
        }
    }
}