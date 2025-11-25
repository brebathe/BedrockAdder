using BedrockAdder.ConsoleWorker;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace BedrockAdder.FileWorker
{
    internal static class FontYamlParserWorker
    {
        private static readonly object _fontCacheLock = new object();
        private static bool _fontCacheLoaded = false;

        // Key: (namespace, setId, normalizedTextureRel) -> unicode string (e.g. "\uE700")
        private static readonly ConcurrentDictionary<(string ns, string setId, string rel), string> _fontUnicodeCache
            = new ConcurrentDictionary<(string, string, string), string>();

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

        internal static bool TryGetFontImagesRoot(YamlMappingNode root, out YamlMappingNode? fontImages)
        {
            fontImages = null;
            if (root.Children.TryGetValue(new YamlScalarNode("font_images"), out var node) &&
                node is YamlMappingNode map)
            {
                fontImages = map;
                return true;
            }
            return false;
        }

        internal static void EnsureFontUnicodeCacheLoaded(string itemsAdderRoot)
        {
            if (_fontCacheLoaded) return;
            lock (_fontCacheLock)
            {
                if (_fontCacheLoaded) return;

                try
                {
                    string cachePath = Path.Combine(itemsAdderRoot, "storage", "font_images_unicode_cache.yml");
                    if (!File.Exists(cachePath)) { _fontCacheLoaded = true; return; }

                    using var reader = new StreamReader(cachePath);
                    var yaml = new YamlStream();
                    yaml.Load(reader);

                    if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                    { _fontCacheLoaded = true; return; }

                    foreach (var nsEntry in root.Children)
                    {
                        if (nsEntry.Key is not YamlScalarNode nsKey) continue;

                        string fullKey = nsKey.Value ?? "";
                        string nsPart = fullKey;
                        string setPartFromFlat = "";

                        var idx = fullKey.IndexOf(':');
                        if (idx >= 0)
                        {
                            nsPart = fullKey.Substring(0, idx);
                            setPartFromFlat = fullKey.Substring(idx + 1);
                        }

                        // NEW: handle flat "ns:setId: glyph" form
                        if (nsEntry.Value is YamlScalarNode uniScalar)
                        {
                            string glyph = uniScalar.Value ?? "";
                            if (!string.IsNullOrWhiteSpace(nsPart) &&
                                !string.IsNullOrWhiteSpace(setPartFromFlat) &&
                                !string.IsNullOrWhiteSpace(glyph))
                            {
                                // use empty rel as sentinel
                                _fontUnicodeCache.TryAdd((nsPart, setPartFromFlat, string.Empty), glyph);
                            }
                            continue;
                        }

                        // existing logic for mapping/sequence formats
                        if (nsEntry.Value is YamlMappingNode nsValMap)
                        {
                            foreach (var setEntry in nsValMap.Children)
                            {
                                if (setEntry.Key is not YamlScalarNode setKey) continue;
                                string setId = setPartFromFlat.Length > 0 ? setPartFromFlat : (setKey.Value ?? "");

                                if (setEntry.Value is YamlMappingNode map)
                                {
                                    foreach (var img in map.Children)
                                    {
                                        if (img.Key is YamlScalarNode imgKey && img.Value is YamlScalarNode uni)
                                        {
                                            var normalized = NormalizeFontTextureRel(imgKey.Value);
                                            if (!string.IsNullOrWhiteSpace(normalized) && !string.IsNullOrWhiteSpace(uni.Value))
                                                _fontUnicodeCache.TryAdd((nsPart, setId, normalized), uni.Value!);
                                        }
                                    }
                                }
                                else if (setEntry.Value is YamlSequenceNode seq)
                                {
                                    foreach (var node in seq.Children)
                                    {
                                        if (node is not YamlMappingNode rec) continue;
                                        if (!TryGetScalar(rec, "path", out var p) || string.IsNullOrWhiteSpace(p)) continue;
                                        if (!TryGetScalar(rec, "char", out var ch) || string.IsNullOrWhiteSpace(ch)) continue;

                                        var normalized = NormalizeFontTextureRel(p!);
                                        _fontUnicodeCache.TryAdd((nsPart, setId, normalized), ch!);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Write.Line("warn", "Failed to load font_images_unicode_cache.yml: " + ex.Message);
                }
                finally { _fontCacheLoaded = true; }
            }
        }

        internal static string? TryGetUnicodeFromCache(string itemsAdderRoot, string fontNamespace, string setId, string rpRelativeTexture)
        {
            EnsureFontUnicodeCacheLoaded(itemsAdderRoot);

            string normalizedRel = string.Empty;
            if (!string.IsNullOrWhiteSpace(rpRelativeTexture))
                normalizedRel = NormalizeFontTextureRel(rpRelativeTexture);

            // 1) Try exact match (ns + setId + normalized path)
            var keyPath = (fontNamespace, setId, normalizedRel);
            if (_fontUnicodeCache.TryGetValue(keyPath, out var valPath))
                return valPath;

            // 2) Fallback: flat "ns:setId: glyph" entries stored with empty rel
            var keyFlat = (fontNamespace, setId, string.Empty);
            if (_fontUnicodeCache.TryGetValue(keyFlat, out var valFlat))
                return valFlat;

            return null;
        }

        internal static string NormalizeFontTextureRel(string raw)
        {
            string s = (raw ?? string.Empty).Replace("\\", "/").Trim();

            const string AssetsMcTextures = "assets/minecraft/textures/";
            if (s.StartsWith(AssetsMcTextures, StringComparison.OrdinalIgnoreCase))
                s = s.Substring(AssetsMcTextures.Length);

            if (s.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
                s = s.Substring("textures/".Length);

            s = s.TrimStart('/');

            if (!s.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                s += ".png";

            return s;
        }

        internal static string BuildIaContentFontTextureAbs(string itemsAdderRoot, string fontNamespace, string rpRelativeTexture)
        {
            var rel = rpRelativeTexture.Replace("\\", "/").TrimStart('/');
            if (rel.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
                rel = rel.Substring("textures/".Length);
            return Path.Combine(itemsAdderRoot, "contents", fontNamespace, "resourcepack", fontNamespace, "textures", rel);
        }

        internal static string DecodeYamlUnicodeChar(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            string result = Regex.Replace(s, @"\\u([0-9A-Fa-f]{4})", m =>
            {
                var code = Convert.ToInt32(m.Groups[1].Value, 16);
                return char.ConvertFromUtf32(code);
            });

            result = result.Replace("\\\"", "\"").Replace("\\\\", "\\");
            return result;
        }

        internal static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var v in values)
                if (!string.IsNullOrWhiteSpace(v)) return v;
            return null;
        }

        internal static bool DetectIsGuiFile(string filePath)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                string? firstLine = reader.ReadLine();

                if (firstLine == null)
                    return false;

                // Handle possible BOM + whitespace
                firstLine = firstLine.TrimStart('\uFEFF').Trim();

                // User convention: very first line starts with #isGui
                if (firstLine.StartsWith("#isGui", StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            }
            catch
            {
                // On any IO error, just treat it as a normal font file
                return false;
            }
        }

    }
}