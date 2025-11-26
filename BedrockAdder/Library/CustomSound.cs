namespace BedrockAdder.Library
{
    internal class CustomSound
    {
        public string SoundNamespace { get; set; }
        public string SoundID { get; set; } // ItemsAdder ID
        public string SoundPath { get; set; } // Path to the sound file
        public float Volume { get; set; } = 1.0f;
        public float Pitch { get; set; } = 1.0f;
        public bool Stream { get; set; } = false; // For large audio files (music/discs)
        public int? AtteniuationDistance { get; set; } = 0; // Whether the sound attenuates with distance
        public int? Weight { get; set; } = 1; // Weight for random selection
    }
}