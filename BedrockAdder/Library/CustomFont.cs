namespace BedrockAdder.Library
{
    internal class CustomFont
    {
        public string FontImagePath { get; set; } // Path to Font Image
        public string FontID { get; set; } // ItemsAdder ID
        public string FontNamespace { get; set; } // Namespace ("myplugin")
        public int ScaleRatio { get; set; } // ItemsAdder font scale ratio
        public int YPosition { get; set; } // Y Position of the font, ItemsAdder doesn't support X position adjustment
        public string FontSymbol { get; set; } // Symbol to use for the font(Can be found from font_images_unicode_cache)
    }
}
