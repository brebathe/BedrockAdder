namespace BedrockAdder.Library
{
    internal class CustomBlock
    {
        public bool Is3D { get; set; } = false; // Is the custom block a 3D model?
        public string BlockNamespace { get; set; }
        public string BlockItemID { get; set; } // The held item ID of the block
        public string ModelPath { get; set; } // Path of the Java model
        public string TexturePath { get; set; } // Shared block/item texture
        public string? IconPath { get; set; } // Optional icon override for inventory
        public string Material { get; set; } // Material for the held item
        public string PlacedBlockType { get; set; } // The block type when placed in the world such as mushroom block, noteblock, chorus fruit or string
        public int? CustomModelData { get; set; } // Optional CMD
    }
}