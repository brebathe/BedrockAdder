using System;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace BedrockAdder.FileWorker
{
    internal static class FurnitureYamlParserWorker
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

        internal static bool TryIsFurnitureItem(YamlMappingNode itemProps)
        {
            if (TryGetMapping(itemProps, "behaviours", out var behaviours) && behaviours != null)
            {
                if (TryGetMapping(behaviours, "furniture", out var furnMap) && furnMap != null)
                    return true;

                if (TryGetScalar(behaviours, "furniture", out var furnScalar) && !string.IsNullOrWhiteSpace(furnScalar))
                {
                    if (furnScalar.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (TryGetMapping(itemProps, "specific_properties", out var spec) && spec != null)
            {
                if (TryGetMapping(spec, "furniture", out var _) || TryGetScalar(spec, "furniture", out var _))
                    return true;
            }

            return false;
        }

        internal static string? TryGetFurnitureModelPathFromItem(YamlMappingNode itemProps)
        {
            if (TryGetMapping(itemProps, "resource", out var resource) && resource != null)
            {
                if (TryGetScalar(resource, "model_path", out var modelPath) && !string.IsNullOrWhiteSpace(modelPath))
                    return modelPath;
            }
            if (TryGetMapping(itemProps, "graphics", out var graphics) && graphics != null)
            {
                if (TryGetScalar(graphics, "model", out var model2) && !string.IsNullOrWhiteSpace(model2))
                    return model2;
            }
            return null;
        }

        internal static string NormalizeModelNameForZipLookup(string raw)
        {
            string s = (raw ?? string.Empty).Replace("\\", "/").TrimStart('/');
            if (s.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 5);
            return s;
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

        internal static string TryGetArmorMaterial(YamlMappingNode itemProps, string defaultMaterial)
        {
            if (itemProps.Children.TryGetValue("resource", out var resourceNode) &&
                resourceNode is YamlMappingNode resourceMap)
            {
                if (resourceMap.Children.TryGetValue("material", out var materialNode) &&
                    materialNode is YamlScalarNode materialScalar &&
                    !string.IsNullOrWhiteSpace(materialScalar.Value))
                    return materialScalar.Value!;
            }
            if (itemProps.Children.TryGetValue("material", out var materialNode2) &&
                materialNode2 is YamlScalarNode materialScalar2 &&
                !string.IsNullOrWhiteSpace(materialScalar2.Value))
                return materialScalar2.Value!;
            return defaultMaterial;
        }

        internal static int? TryGetCustomModelDataFromCache(string itemsAdderRootPath, string ns, string id)
        {
            try
            {
                string cachePath = Path.Combine(itemsAdderRootPath, "storage", "items_ids_cache.yml");
                if (!File.Exists(cachePath))
                    return null;

                using var reader = new StreamReader(cachePath);
                var yaml = new YamlStream();
                yaml.Load(reader);

                if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                    return null;

                if (root.Children.TryGetValue(ns, out var namespaceNode) &&
                    namespaceNode is YamlMappingNode namespaceMap)
                {
                    if (namespaceMap.Children.TryGetValue(id, out var idNode) &&
                        idNode is YamlScalarNode idScalar &&
                        int.TryParse(idScalar.Value, out int value))
                    {
                        return value;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}