using System.Collections.Generic;

namespace BedrockAdder.Library
{
    internal static class Lists
    {
        // Parsed content (populated after parsing file contents)
        public static List<CustomItem> CustomItems = new List<CustomItem>();
        public static List<CustomBlock> CustomBlocks = new List<CustomBlock>();
        public static List<CustomFurniture> CustomFurniture = new List<CustomFurniture>();
        public static List<CustomFont> CustomFonts = new List<CustomFont>();
        public static List<CustomArmor> CustomArmors = new List<CustomArmor>();
        public static List<CustomSound> CustomSounds = new List<CustomSound>();
        // YAML paths (populated during initial scan)
        public static List<string> CustomItemPaths = new List<string>();
        public static List<string> CustomBlockPaths = new List<string>();
        public static List<string> CustomFurniturePaths = new List<string>();
        public static List<string> CustomFontPaths = new List<string>();
        public static List<string> CustomArmorPaths = new List<string>();
        public static List<string> CustomSoundPaths = new List<string>();
        // Miscellaneous
        public static List<string> UnknownPaths = new List<string>();
        public static List<string> SkippedFilePaths = new List<string>();
    }
}