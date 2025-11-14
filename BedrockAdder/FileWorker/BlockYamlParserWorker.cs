using System;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace BedrockAdder.FileWorker
{
    internal static class BlockYamlParserWorker
    {
        internal static string? TryGetScalar(YamlMappingNode node, string key)
        {
            return node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar
                ? scalar.Value
                : null;
        }

        internal static bool TryGetScalar(YamlMappingNode node, string key, out string? value)
        {
            value = null;
            if (node.Children.TryGetValue(new YamlScalarNode(key), out var val) && val is YamlScalarNode scalar)
            {
                value = scalar.Value;
                return true;
            }
            return false;
        }

        internal static bool TryGetMapping(YamlMappingNode node, string key, out YamlMappingNode? map)
        {
            map = null;
            if (node.Children.TryGetValue(new YamlScalarNode(key), out var val) && val is YamlMappingNode m)
            {
                map = m;
                return true;
            }
            return false;
        }

        internal static int? AsInt(string? s)
        {
            return int.TryParse(s, out var result) ? result : (int?)null;
        }

        internal static string? ExtractNamespaceFromPath(string filePath)
        {
            var segments = filePath.Split(Path.DirectorySeparatorChar);
            int index = Array.IndexOf(segments, "contents");
            return (index != -1 && index + 1 < segments.Length) ? segments[index + 1] : null;
        }

        internal static int? GetCustomModelData(string itemsAdderFolder, string? itemNamespace, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemNamespace))
                return null;

            string path = Path.Combine(itemsAdderFolder, "storage", "items_ids_cache.yml");
            if (!File.Exists(path))
                return null;

            using var reader = new StreamReader(path);
            var yaml = new YamlStream();
            yaml.Load(reader);

            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return null;

            string key = itemNamespace + ":" + itemId;
            if (root.Children.TryGetValue(new YamlScalarNode(key), out var valueNode) && valueNode is YamlScalarNode scalar)
            {
                return AsInt(scalar.Value);
            }

            return null;
        }

        // --- Block-specific helpers ---

        internal static bool TryGetBlockSpecificProps(YamlMappingNode itemProps, out YamlMappingNode? blockMap)
        {
            blockMap = null;
            if (itemProps == null) return false;

            if (!TryGetMapping(itemProps, "specific_properties", out var spec) || spec == null)
                return false;

            if (!TryGetMapping(spec, "block", out var blk) || blk == null)
                return false;

            blockMap = blk;
            return true;
        }

        internal static bool TryGetPlacedModelType(YamlMappingNode itemProps, out string? placedType)
        {
            placedType = null;
            if (!TryGetBlockSpecificProps(itemProps, out var blockMap) || blockMap == null)
                return false;

            if (TryGetMapping(blockMap, "placed_model", out var pm) && pm != null &&
                TryGetScalar(pm, "type", out var t) && !string.IsNullOrWhiteSpace(t))
            {
                placedType = t.Trim().ToUpperInvariant();
                return true;
            }

            return false;
        }

        internal static bool TryGetPlacedModelName(YamlMappingNode itemProps, out string? placedModel)
        {
            placedModel = null;
            if (!TryGetBlockSpecificProps(itemProps, out var blockMap) || blockMap == null)
                return false;

            if (TryGetScalar(blockMap, "placed_model", out var scalarVal) && !string.IsNullOrWhiteSpace(scalarVal))
            {
                placedModel = scalarVal.Trim();
                return true;
            }

            if (TryGetMapping(blockMap, "placed_model", out var pm) && pm != null)
            {
                if (TryGetScalar(pm, "name", out var name) && !string.IsNullOrWhiteSpace(name))
                {
                    placedModel = name.Trim();
                    return true;
                }
                if (TryGetScalar(pm, "id", out var id) && !string.IsNullOrWhiteSpace(id))
                {
                    placedModel = id.Trim();
                    return true;
                }
            }

            return false;
        }

        internal static bool TryGet2DTexturePathNormalized(YamlMappingNode itemProps, out string normalizedPath)
        {
            normalizedPath = string.Empty;

            if (TryGetMapping(itemProps, "graphics", out var graphics) &&
                TryGetScalar(graphics!, "texture", out var gfxTex) &&
                !string.IsNullOrWhiteSpace(gfxTex))
            {
                normalizedPath = JsonParserWorker.NormalizeTexturePathFromModelValue(gfxTex!);
                return !string.IsNullOrWhiteSpace(normalizedPath);
            }

            if (TryGetMapping(itemProps, "resource", out var resource) &&
                TryGetScalar(resource!, "texture_path", out var resTex) &&
                !string.IsNullOrWhiteSpace(resTex))
            {
                normalizedPath = JsonParserWorker.NormalizeTexturePathFromModelValue(resTex!);
                return !string.IsNullOrWhiteSpace(normalizedPath);
            }

            return false;
        }
    }
}