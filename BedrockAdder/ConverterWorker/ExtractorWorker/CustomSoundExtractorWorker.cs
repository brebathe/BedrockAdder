using System;
using System.Collections.Generic;
using System.IO;
using BedrockAdder.FileWorker;
using BedrockAdder.Library;
using YamlDotNet.RepresentationModel;

namespace BedrockAdder.ExtractorWorker.ConverterWorker
{
    internal static class CustomSoundExtractorWorker
    {
        // Parse all provided sounds.yml files and append to Lists (or return the list).
        internal static void ExtractCustomSoundsFromPaths(string itemsAdderRoot)
        {
            int filesProcessed = 0;

            foreach (var filePath in Lists.CustomSoundPaths)
            {
                filesProcessed++;

                try
                {
                    if (!File.Exists(filePath))
                    {
                        ConsoleWorker.Write.Line("warn", "Sounds file missing: " + filePath);
                        continue;
                    }

                    using var reader = new StreamReader(filePath);
                    var yaml = new YamlStream();
                    yaml.Load(reader);

                    if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                        continue;

                    string ns = SoundYamlParserWorker.GetFileNamespaceOrDefault(root, "unknown");

                    if (!TryGetSoundsRoot(root, out var soundsMap) || soundsMap is null)
                        continue;

                    foreach (var kv in soundsMap.Children)
                    {
                        if (kv.Key is not YamlScalarNode sKey || kv.Value is not YamlMappingNode sMap)
                            continue;

                        string localId = sKey.Value ?? "unnamed";
                        string soundId = ns + ":" + localId;

                        // Base path + settings
                        string basePathRel = TryGetScalar(sMap, "path", out var p0)
                            ? SoundYamlParserWorker.NormalizeSoundPathRel(p0)
                            : string.Empty;

                        if (string.IsNullOrWhiteSpace(basePathRel))
                        {
                            ConsoleWorker.Write.Line("warn", soundId + " has no 'path' entry.");
                            continue;
                        }

                        string absPath = SoundYamlParserWorker.BuildIaContentSoundAbs(itemsAdderRoot, ns, basePathRel);

                        var (vol, pitch, stream) = ReadSettings(sMap);

                        Lists.CustomSounds.Add(new CustomSound
                        {
                            SoundNamespace = ns,
                            SoundID = soundId,
                            SoundPath = absPath,
                            Volume = vol,
                            Pitch = pitch,
                            Stream = stream
                        });

                        ConsoleWorker.Write.Line("info", "Processed: " + ns + ":" + soundId + " at " + absPath + " with properties:[Volume:" + vol + ",Pitch:" + pitch + ",Stream:" + stream + "]");

                        // Variants: keys starting with "variant"
                        foreach (var vpair in sMap.Children)
                        {
                            if (vpair.Key is not YamlScalarNode vKey || vpair.Value is not YamlMappingNode vMap)
                                continue;

                            string k = vKey.Value ?? string.Empty;
                            if (!k.StartsWith("variant", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string vRel = TryGetScalar(vMap, "path", out var vp)
                                ? SoundYamlParserWorker.NormalizeSoundPathRel(vp)
                                : basePathRel; // fallback to base

                            string vAbs = SoundYamlParserWorker.BuildIaContentSoundAbs(itemsAdderRoot, ns, vRel);

                            // Inherit base settings but allow overrides (pitch/volume/stream)
                            var (vVol, vPitch, vStream) = ReadSettingsOverride(vMap, vol, pitch, stream);

                            Lists.CustomSounds.Add(new CustomSound
                            {
                                SoundNamespace = ns,
                                SoundID = soundId,          // same ID; Bedrock builder will group these
                                SoundPath = vAbs,
                                Volume = vVol,
                                Pitch = vPitch,
                                Stream = vStream
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    ConsoleWorker.Write.Line("error", "Sounds parse failed: " + filePath + " ex=" + ex.Message);
                }
            }

            ConsoleWorker.Write.Line("info", "Sounds: processed files=" + filesProcessed + " entries=" + Lists.CustomSounds.Count);
        }

        // --- local YAML helpers (mirror style of your existing parser) ---

        private static bool TryGetSoundsRoot(YamlMappingNode root, out YamlMappingNode? sounds)
        {
            sounds = null;
            if (root.Children.TryGetValue(new YamlScalarNode("sounds"), out var node)
                && node is YamlMappingNode map)
            {
                sounds = map;
                return true;
            }
            return false;
        }

        private static bool TryGetScalar(YamlMappingNode map, string key, out string value)
        {
            value = string.Empty;
            if (map.Children.TryGetValue(new YamlScalarNode(key), out var n)
                && n is YamlScalarNode s && !string.IsNullOrWhiteSpace(s.Value))
            {
                value = s.Value!;
                return true;
            }
            return false;
        }

        private static (float vol, float pitch, bool stream) ReadSettings(YamlMappingNode sMap)
        {
            float vol = 1.0f;
            float pitch = 1.0f;
            bool stream = false;

            if (sMap.Children.TryGetValue(new YamlScalarNode("settings"), out var node)
                && node is YamlMappingNode set)
            {
                if (TryGetScalar(set, "volume", out var v) && float.TryParse(v, out var vf)) vol = vf;
                if (TryGetScalar(set, "pitch", out var p) && float.TryParse(p, out var pf)) pitch = pf;
                if (TryGetScalar(set, "stream", out var st) && bool.TryParse(st, out var sb)) stream = sb;
            }
            return (vol, pitch, stream);
        }

        private static (float vol, float pitch, bool stream) ReadSettingsOverride(YamlMappingNode vMap, float baseVol, float basePitch, bool baseStream)
        {
            float vol = baseVol;
            float pitch = basePitch;
            bool stream = baseStream;

            if (TryGetScalar(vMap, "volume", out var v) && float.TryParse(v, out var vf)) vol = vf;
            if (TryGetScalar(vMap, "pitch", out var p) && float.TryParse(p, out var pf)) pitch = pf;
            if (TryGetScalar(vMap, "stream", out var st) && bool.TryParse(st, out var sb)) stream = sb;

            return (vol, pitch, stream);
        }
    }
}