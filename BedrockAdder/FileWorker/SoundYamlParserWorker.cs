using System;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace BedrockAdder.FileWorker
{
    internal static class SoundYamlParserWorker
    {
        internal static string GetFileNamespaceOrDefault(YamlMappingNode root, string defaultNamespace)
        {
            if (root.Children.TryGetValue("info", out var infoNode) && infoNode is YamlMappingNode infoMap)
            {
                if (infoMap.Children.TryGetValue("namespace", out var namespaceNode) &&
                    namespaceNode is YamlScalarNode namespaceScalar &&
                    !string.IsNullOrWhiteSpace(namespaceScalar.Value))
                {
                    return namespaceScalar.Value!;
                }
            }
            return defaultNamespace;
        }

        internal static string NormalizeSoundPathRel(string raw)
        {
            string s = (raw ?? string.Empty).Replace("\\", "/").Trim();

            if (s.StartsWith("sounds/", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring("sounds/".Length);
            }

            s = s.TrimStart('/');

            if (!s.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                && !s.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                && !s.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                s += ".ogg";
            }

            return s;
        }

        internal static string BuildIaContentSoundAbs(string itemsAdderRoot, string soundNamespace, string rel)
        {
            string r = (rel ?? string.Empty).Replace("\\", "/").TrimStart('/');
            if (r.StartsWith("sounds/", StringComparison.OrdinalIgnoreCase))
            {
                r = r.Substring("sounds/".Length);
            }

            return Path.Combine(itemsAdderRoot, "contents", soundNamespace, "sounds", r);
        }
    }
}