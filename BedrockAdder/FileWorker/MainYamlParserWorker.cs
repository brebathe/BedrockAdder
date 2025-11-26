using BedrockAdder.ConsoleWorker;
using System;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace BedrockAdder.FileWorker
{
    internal static class MainYamlParserWorker
    {
        //DO NOT TOUCH THIS METHOD
        internal static string ClassifyYaml(string filePath)
        {
            if (!File.Exists(filePath))
                return "Invalid";

            using var reader = new StreamReader(filePath);
            var yaml = new YamlStream();
            yaml.Load(reader);

            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                Write.Line("warning", "File at " + filePath + " is unknown!");
                return "Unknown";
            }

            // trivial skips
            if (root.Children.ContainsKey("categories"))
                return "Skip";

            // top-level types
            if (root.Children.ContainsKey("font_images"))
                return "Font";

            // treat pure sounds config as Sound
            if (root.Children.ContainsKey("sounds"))
                return "Sound";

            // files that only define armor sets but no items
            if (root.Children.ContainsKey("equipments") && !root.Children.ContainsKey("items"))
                return "Armor";

            // item-bearing files → inspect entries precisely
            if (root.Children.TryGetValue(new YamlScalarNode("items"), out var itemsNode) && itemsNode is YamlMappingNode itemsMap)
            {
                int total = 0, blocks = 0, furn = 0, armors = 0;

                foreach (var kv in itemsMap.Children)
                {
                    if (kv.Value is not YamlMappingNode props)
                        continue;

                    total++;

                    if (TryIsBlockEntry(props, out _))
                    {
                        blocks++;
                        continue;
                    }

                    if (TryIsFurnitureItem(props))
                    {
                        furn++;
                        continue;
                    }

                    if (TryIsArmorItem(props))
                    {
                        armors++;
                        continue;
                    }
                }

                if (total == 0)
                    return "Unknown";

                if (blocks == total) return "Block";
                if (furn == total) return "Furniture";
                if (armors == total) return "Armor";

                // mixed or plain items → treat as Item; per-entry workers decide what to extract
                return "Item";
            }

            Write.Line("warning", "File at " + filePath + " is unknown!");
            return "Unknown";
        }

        #region Minimal YAML helpers for classification

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

        #endregion

        #region Classification helpers (per-entry)


        /// <summary>
        /// Quick per-entry check: is this items[*] entry a block?
        /// Looks for a mapping at specific_properties.block.
        /// Returns false with a short reason if not.
        /// </summary>
        internal static bool TryIsBlockEntry(YamlMappingNode itemProps, out string reason)
        {
            reason = "";
            if (itemProps == null) { reason = "null props"; return false; }

            if (!TryGetMapping(itemProps, "specific_properties", out var spec) || spec == null)
            {
                reason = "missing specific_properties";
                return false;
            }
            if (!TryGetMapping(spec, "block", out var blockMap) || blockMap == null)
            {
                reason = "missing specific_properties.block";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Detect if an items[*] node represents furniture.
        /// We accept either behaviours.furniture present or specific_properties.furniture present.
        /// </summary>
        internal static bool TryIsFurnitureItem(YamlMappingNode itemProps)
        {
            // A. behaviours.furniture present (mapping) → treat as furniture regardless of "enabled" flag
            if (TryGetMapping(itemProps, "behaviours", out var behaviours) && behaviours != null)
            {
                if (TryGetMapping(behaviours, "furniture", out var furnMap) && furnMap != null)
                    return true;

                // (legacy scalar styles also allowed)
                if (TryGetScalar(behaviours, "furniture", out var furnScalar) && !string.IsNullOrWhiteSpace(furnScalar))
                {
                    if (furnScalar.Trim().Equals("true", System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // B. specific_properties.furniture present (some packs put it here)
            if (TryGetMapping(itemProps, "specific_properties", out var spec) && spec != null)
            {
                if (TryGetMapping(spec, "furniture", out var _) || TryGetScalar(spec, "furniture", out var _))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Detect if an items[*] node represents armor.
        /// </summary>
        internal static bool TryIsArmorItem(YamlMappingNode itemProps)
        {
            // Armor if 'equipment' is a mapping (ItemsAdder style with id/slot)
            if (itemProps.Children.TryGetValue("equipment", out var equipmentNode) && equipmentNode is YamlMappingNode)
                return true;

            // Older/other styles
            if (itemProps.Children.TryGetValue("equipments", out var equipmentsNode) && equipmentsNode is YamlMappingNode)
                return true;

            if (itemProps.Children.TryGetValue("equipment", out var equipmentScalarNode) &&
                equipmentScalarNode is YamlScalarNode equipmentScalar &&
                (equipmentScalar.Value?.Equals("true", System.StringComparison.OrdinalIgnoreCase) ?? false))
                return true;

            return false;
        }

        #endregion
    }
}