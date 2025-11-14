using System.Collections.Generic;

namespace BedrockAdder.Library
{
    internal class CustomFurniture
    {
        //No need for is 3d because all furniture are custom 3d models
        public string ModelPath { get; set; } // Path to Java model file

        public Dictionary<string, string> TexturePaths = new Dictionary<string, string>(); // List of texture paths used by the model
        public string FurnitureItemID { get; set; } // ItemsAdder ID
        public string FurnitureNamespace { get; set; } // Namespace ("myplugin")
        public string? IconPath { get; set; } // 2D icon path
        public string Material { get; set; } // Java material like "minecraft:stick"
        public int? CustomModelData { get; set; } // Optional
    }
}