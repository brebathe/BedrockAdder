using BedrockAdder.ConsoleWorker;
using System;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace BedrockAdder.FileWorker
{
    internal static class ArmorYamlParserWorker
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

        internal static int? TryGetCustomModelDataFromCache(string itemsAdderRootPath, string armorNamespace, string armorId)
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

                if (root.Children.TryGetValue(armorNamespace, out var namespaceNode) &&
                    namespaceNode is YamlMappingNode namespaceMap)
                {
                    if (namespaceMap.Children.TryGetValue(armorId, out var idNode) &&
                        idNode is YamlScalarNode idScalar &&
                        int.TryParse(idScalar.Value, out int value))
                    {
                        return value;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Write.Line("warning", "Failed to read items_ids_cache.yml: " + ex.Message);
                return null;
            }
        }

        internal static bool TryIsArmorItem(YamlMappingNode itemProps)
        {
            if (itemProps.Children.TryGetValue("equipment", out var equipmentNode) && equipmentNode is YamlMappingNode)
                return true;

            if (itemProps.Children.TryGetValue("equipments", out var equipmentsNode) && equipmentsNode is YamlMappingNode)
                return true;

            if (itemProps.Children.TryGetValue("equipment", out var equipmentScalarNode) &&
                equipmentScalarNode is YamlScalarNode equipmentScalar &&
                (equipmentScalar.Value?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false))
                return true;

            return false;
        }

        internal static string? TryGetHelmetModelPath(YamlMappingNode itemProps)
        {
            if (itemProps.Children.TryGetValue("resource", out var resourceNode) &&
                resourceNode is YamlMappingNode resourceMap)
            {
                if (resourceMap.Children.TryGetValue("model_path", out var modelNode) &&
                    modelNode is YamlScalarNode modelScalar &&
                    !string.IsNullOrWhiteSpace(modelScalar.Value))
                    return modelScalar.Value!;
            }

            if (itemProps.Children.TryGetValue("graphics", out var graphicsNode) &&
                graphicsNode is YamlMappingNode graphicsMap)
            {
                if (graphicsMap.Children.TryGetValue("model", out var modelNode) &&
                    modelNode is YamlScalarNode modelScalar &&
                    !string.IsNullOrWhiteSpace(modelScalar.Value))
                    return modelScalar.Value!;
            }
            return null;
        }

        internal static (string? chest, string? legs) GuessArmorLayersFromId(string armorId)
        {
            string basePath = "textures/models/armor/" + armorId.ToLowerInvariant();
            return (basePath + "_layer_1.png", basePath + "_layer_2.png");
        }

        internal static string GetArmorSlotFromItem(YamlMappingNode itemProps)
        {
            if (itemProps.Children.TryGetValue("equipment", out var equipmentNode) &&
                equipmentNode is YamlMappingNode equipmentMap)
            {
                if (equipmentMap.Children.TryGetValue("slot", out var slotNode) &&
                    slotNode is YamlScalarNode slotScalar &&
                    !string.IsNullOrWhiteSpace(slotScalar.Value))
                {
                    string slotValue = slotScalar.Value!.Trim().ToUpperInvariant();
                    return slotValue switch
                    {
                        "HEAD" => "helmet",
                        "CHEST" => "chestplate",
                        "LEGS" => "leggings",
                        "FEET" => "boots",
                        _ => "helmet"
                    };
                }
            }
            return "helmet";
        }

        internal static string? GetEquipmentId(YamlMappingNode itemProps)
        {
            if (itemProps.Children.TryGetValue("equipment", out var equipmentNode) &&
                equipmentNode is YamlMappingNode equipmentMap)
            {
                if (equipmentMap.Children.TryGetValue("id", out var idNode) &&
                    idNode is YamlScalarNode idScalar &&
                    !string.IsNullOrWhiteSpace(idScalar.Value))
                    return idScalar.Value!;
            }
            return null;
        }

        internal static (string? chest, string? legs) GetLayersFromEquipments(YamlMappingNode root, string equipmentId)
        {
            if (root.Children.TryGetValue("equipments", out var equipmentsNode) &&
                equipmentsNode is YamlMappingNode equipmentsMap)
            {
                if (equipmentsMap.Children.TryGetValue(equipmentId, out var setNode) &&
                    setNode is YamlMappingNode setMap)
                {
                    string? layer1 = setMap.Children.TryGetValue("layer_1", out var n1) && n1 is YamlScalarNode s1 ? s1.Value : null;
                    string? layer2 = setMap.Children.TryGetValue("layer_2", out var n2) && n2 is YamlScalarNode s2 ? s2.Value : null;
                    return (NormalizeArmorLayerRelativePath(layer1), NormalizeArmorLayerRelativePath(layer2));
                }
            }
            return (null, null);
        }

        internal static string? NormalizeArmorLayerRelativePath(string? layerPath)
        {
            if (string.IsNullOrWhiteSpace(layerPath))
                return null;

            string normalized = layerPath.Replace("\\", "/").TrimStart('/');
            if (!normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                normalized += ".png";
            return normalized;
        }

        internal static string BuildItemsAdderContentTexturePath(string itemsAdderRootPath, string armorNamespace, string relativeTexturePath)
        {
            string rel = relativeTexturePath.Replace("\\", "/").TrimStart('/');
            return Path.Combine(itemsAdderRootPath, "contents", armorNamespace, "resourcepack", armorNamespace, "textures", rel);
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

        internal static string? TryGet2DIcon(YamlMappingNode itemProps)
        {
            if (TryGetMapping(itemProps, "graphics", out var graphicsNode) && graphicsNode is not null)
            {
                if (TryGetScalar(graphicsNode, "texture", out var tex) && !string.IsNullOrWhiteSpace(tex))
                {
                    string norm = JsonParserWorker.NormalizeTexturePathFromModelValue(tex!);
                    return string.IsNullOrWhiteSpace(norm) ? null : norm;
                }
            }

            if (TryGetMapping(itemProps, "resource", out var resourceNode) && resourceNode is not null)
            {
                if (TryGetScalar(resourceNode, "texture", out var tex2) && !string.IsNullOrWhiteSpace(tex2))
                {
                    string norm = JsonParserWorker.NormalizeTexturePathFromModelValue(tex2!);
                    return string.IsNullOrWhiteSpace(norm) ? null : norm;
                }
                if (TryGetScalar(resourceNode, "texture_path", out var texPath) && !string.IsNullOrWhiteSpace(texPath))
                {
                    string norm = JsonParserWorker.NormalizeTexturePathFromModelValue(texPath!);
                    return string.IsNullOrWhiteSpace(norm) ? null : norm;
                }
            }

            return null;
        }
        internal static bool TryGetVanillaRecolorInfo(YamlMappingNode itemProps, out string? vanillaTextureId, out string? tintHex)
        {
            vanillaTextureId = null;
            tintHex = null;

            if (ArmorYamlParserWorker.TryGetMapping(itemProps, "graphics", out var graphicsMap) && graphicsMap is not null)
            {
                ArmorYamlParserWorker.TryGetScalar(graphicsMap, "texture", out var texRaw);
                ArmorYamlParserWorker.TryGetScalar(graphicsMap, "color", out var colorRaw);

                if (!string.IsNullOrWhiteSpace(texRaw) && !string.IsNullOrWhiteSpace(colorRaw))
                {
                    vanillaTextureId = texRaw.Trim();   // e.g. "minecraft:item/iron_helmet.png"
                    tintHex = colorRaw.Trim();          // e.g. "FFCC66"
                    return true;
                }
            }

            return false;
        }
    }
}