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
                        Write.Line("info","File " + file + " classified as Font");
                        break;
                    case "Item":
                        Lists.CustomItemPaths.Add(file);
                        Write.Line("info", "File " + file + " classified as Items");
                        countItems++;
                        break;
                    case "Block":
                        Lists.CustomBlockPaths.Add(file);
                        Write.Line("info", "File " + file + " classified as Blocks");
                        countBlocks++;
                        break;
                    case "Sound":
                        Lists.CustomSoundPaths.Add(file);
                        Write.Line("info", "File " + file + " classified as Sounds");
                        countSounds++;
                        break;
                    case "Furniture":
                        Lists.CustomFurniturePaths.Add(file);
                        Write.Line("info", "File " + file + " classified as Furniture");
                        countFurniture++;
                        break;
                    case "Armor":
                        Lists.CustomArmorPaths.Add(file);
                        Write.Line("info", "File " + file + " classified as Armors");
                        countArmors++;
                        break;
                    case "Unknown":
                        Lists.UnknownPaths.Add(file);
                        Write.Line("info", "File " + file + " classified as Unknown");
                        countUnknown++;
                        break;
                    case "Skip":
                        Write.Line("warning", "File " + file + " was skipped!");
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