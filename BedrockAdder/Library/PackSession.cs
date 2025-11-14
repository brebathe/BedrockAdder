using System.IO;

namespace BedrockAdder.Library
{
    public sealed class PackSession
    {
        public string OutputRoot { get; }
        public string PackRoot { get; }
        public string PackName { get; }
        public string PackDescription { get; }
        public string PackVersion { get; }
        public string ManifestUuid { get; }
        public string ModuleUuid { get; }

        public PackSession(string outputRoot, string packRoot, string packName, string packDescription, string packVersion, string manifestUuid, string moduleUuid)
        {
            OutputRoot = outputRoot;
            PackRoot = packRoot;
            PackName = packName;
            PackDescription = packDescription;
            PackVersion = packVersion;
            ManifestUuid = manifestUuid;
            ModuleUuid = moduleUuid;
        }

        public string RpPath(string relative)
        {
            return Path.Combine(PackRoot, relative);
        }
    }
}