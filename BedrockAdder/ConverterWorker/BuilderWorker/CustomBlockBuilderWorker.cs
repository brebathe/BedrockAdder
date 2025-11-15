using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BedrockAdder.Library;
using BedrockAdder.Managers;
using BedrockAdder.ConverterWorker.ObjectWorker;   // ModelBuilderWorker, IModelIconRenderer
using BedrockAdder.Renderer;                       // CefOffscreenIconRenderer

namespace BedrockAdder.ConverterWorker.BuilderWorker
{
    internal static class CustomBlockBuilderWorker
    {
        // Convenience: build ALL blocks with a renderer created from render.html
        public static void BuildCustomBlocks(PackSession session, int iconSize, string itemsAdderFolder)
        {
            string renderHtmlAbs = Path.Combine(AppContext.BaseDirectory, "Renderer", "cef", "render.html");

            if (session == null)
            {
                ConsoleWorker.Write.Line("error", "CustomBlockBuilderWorker: session is null");
                return;
            }

            if (Lists.CustomBlocks == null || Lists.CustomBlocks.Count == 0)
            {
                ConsoleWorker.Write.Line("info", "CustomBlockBuilderWorker: no custom blocks to build.");
                return;
            }

            IModelIconRenderer renderer = new CefOffscreenIconRenderer(iconSize, true, renderHtmlAbs);

            foreach (CustomBlock block in Lists.CustomBlocks)
            {
                try
                {
                    BuildOneBlock(session, block, renderer);
                }
                catch (Exception ex)
                {
                    ConsoleWorker.Write.Line(
                        "error",
                        "Failed building block " + (block?.BlockNamespace ?? "unknown") + ":" +
                        (block?.BlockItemID ?? "unknown") + " ex=" + ex.Message);
                }
            }
        }

        // ---------- core for a single block ----------

        public static void BuildOneBlock(PackSession session, CustomBlock block, IModelIconRenderer renderer)
        {
            if (block == null)
            {
                ConsoleWorker.Write.Line("warn", "BuildOneBlock: block is null");
                return;
            }

            string ns = block.BlockNamespace ?? "unknown";
            string id = block.BlockItemID ?? "unknown";

            // For now we treat the held-block item id just like items do
            string bedrockId = BedrockManager.MakeBedrockItemId(ns, id);         // ia:ns_id
            string atlasKey = "ia_block_" + Sanitize(ns) + "_" + Sanitize(id);   // key inside item_texture.json
            string iconAtlasRel = Built3DObjectNaming.MakeIconRel(ns, id);       // textures/items/ns/id.png

            string iconAbs = Path.Combine(session.PackRoot, iconAtlasRel.Replace('/', Path.DirectorySeparatorChar));

            // Ensure base dirs
            BedrockManager.EnsureDir(Path.Combine(session.PackRoot, "items"));
            BedrockManager.EnsureDir(Path.Combine(session.PackRoot, "attachables"));
            BedrockManager.EnsureDir(Path.Combine(session.PackRoot, "models", "entity"));
            BedrockManager.EnsureDir(Path.Combine(session.PackRoot, "textures", "items"));
            BedrockManager.EnsureDir(Path.Combine(session.PackRoot, "textures", "models", ns));

            string? iconSourceAbs = null;

            // ---------- 3D GEOMETRY / MODEL TEXTURES (for 3D blocks) ----------

            if (block.Is3D && !string.IsNullOrWhiteSpace(block.ModelPath) && File.Exists(block.ModelPath))
            {
                var built = ModelBuilderWorker.Build(block, iconRenderer: null);

                // 1) Write geometry (.geo.json) if present
                if (!string.IsNullOrWhiteSpace(built.GeometryJson))
                {
                    string geoAbs = Path.Combine(session.PackRoot, built.GeometryOutRel.Replace('/', Path.DirectorySeparatorChar));
                    string? geoDir = Path.GetDirectoryName(geoAbs);
                    if (!string.IsNullOrWhiteSpace(geoDir))
                        Directory.CreateDirectory(geoDir);

                    try
                    {
                        File.WriteAllText(geoAbs, built.GeometryJson, System.Text.Encoding.UTF8);
                        ConsoleWorker.Write.Line("info", ns + ":" + id + " geometry → " + built.GeometryOutRel);
                    }
                    catch (Exception ex)
                    {
                        ConsoleWorker.Write.Line("warn",
                            ns + ":" + id + " failed writing geometry: " + geoAbs + " ex=" + ex.Message);
                    }
                }
                else if (built.Notes != null && built.Notes.Count > 0)
                {
                    foreach (string note in built.Notes)
                        ConsoleWorker.Write.Line("info", ns + ":" + id + " " + note);
                }

                // 2) Copy model textures (if any)
                if (built.TexturesToCopy != null)
                {
                    foreach (var (src, dstRel) in built.TexturesToCopy)
                    {
                        if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dstRel))
                            continue;

                        string dstAbs = Path.Combine(session.PackRoot, dstRel.Replace('/', Path.DirectorySeparatorChar));
                        string? dstDir = Path.GetDirectoryName(dstAbs);
                        if (!string.IsNullOrWhiteSpace(dstDir))
                            Directory.CreateDirectory(dstDir);

                        try
                        {
                            File.Copy(src, dstAbs, true);
                        }
                        catch (Exception ex)
                        {
                            ConsoleWorker.Write.Line(
                                "warn",
                                "Failed copying block model texture " + src + " -> " + dstAbs + " ex=" + ex.Message);
                        }
                    }
                }

                // ---------- 3D ICON RENDERING (for 3D blocks) ----------

                bool hasModel = !string.IsNullOrWhiteSpace(block.ModelPath) && File.Exists(block.ModelPath);
                if (hasModel)
                {
                    // Build texture map for the renderer
                    var texMap = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in block.ModelTexturePaths)
                    {
                        if (!string.IsNullOrWhiteSpace(kv.Value))
                            texMap[kv.Key] = kv.Value;
                    }

                    if (texMap.Count == 0 && !string.IsNullOrWhiteSpace(block.TexturePath))
                    {
                        texMap["default"] = block.TexturePath;
                    }

                    if (texMap.Count > 0)
                    {
                        try
                        {
                            bool rendered = renderer.TryRenderIcon(block.ModelPath, texMap, iconAbs);
                            if (rendered && File.Exists(iconAbs))
                            {
                                iconSourceAbs = iconAbs; // snapshot already written in-place
                                ConsoleWorker.Write.Line(
                                    "info",
                                    ns + ":" + id + " 3D block icon rendered → " + iconAtlasRel);
                            }
                            else
                            {
                                ConsoleWorker.Write.Line(
                                    "warn",
                                    ns + ":" + id + " 3D block icon render failed, will fall back to flat texture.");
                            }
                        }
                        catch (Exception ex)
                        {
                            ConsoleWorker.Write.Line(
                                "warn",
                                ns + ":" + id + " exception while rendering block icon: " + ex.Message);
                        }
                    }
                }
            }

            // ---------- FLAT ICON FALLBACKS (for non-3D or failed 3D render) ----------

            if (iconSourceAbs == null)
            {
                // User-provided icon override (inventory-only)
                if (!string.IsNullOrWhiteSpace(block.IconPath) && File.Exists(block.IconPath))
                {
                    iconSourceAbs = block.IconPath;
                    ConsoleWorker.Write.Line(
                        "info",
                        ns + ":" + id + " icon override → " + block.IconPath);
                }
                else if (!string.IsNullOrWhiteSpace(block.TexturePath) && File.Exists(block.TexturePath))
                {
                    // Use the main block texture as the inventory icon
                    iconSourceAbs = block.TexturePath;
                    ConsoleWorker.Write.Line(
                        "info",
                        ns + ":" + id + " icon from TexturePath → " + block.TexturePath);
                }
                else
                {
                    ConsoleWorker.Write.Line(
                        "error",
                        ns + ":" + id + " has no usable icon or texture; skipping icon creation.");
                }
            }

            // ---------- COPY ICON INTO PACK ----------

            if (iconSourceAbs != null)
            {
                try
                {
                    string? iconDir = Path.GetDirectoryName(iconAbs);
                    if (!string.IsNullOrWhiteSpace(iconDir))
                        Directory.CreateDirectory(iconDir);

                    if (!string.Equals(iconSourceAbs, iconAbs, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(iconSourceAbs, iconAbs, true);
                    }
                }
                catch (Exception ex)
                {
                    ConsoleWorker.Write.Line(
                        "warn",
                        ns + ":" + id + " failed copying block icon " + iconSourceAbs + " -> " + iconAbs + " ex=" + ex.Message);
                }
            }

            // ---------- UPDATE item_texture.json (same atlas as items for now) ----------

            try
            {
                string itemTexJsonAbs = Path.Combine(session.PackRoot, "textures", "item_texture.json");
                JObject root;

                if (File.Exists(itemTexJsonAbs))
                {
                    root = JObject.Parse(File.ReadAllText(itemTexJsonAbs));
                }
                else
                {
                    root = new JObject
                    {
                        ["resource_pack_name"] = "BedrockAdder",
                        ["texture_name"] = "atlas.items",
                        ["texture_data"] = new JObject()
                    };
                }

                if (root["texture_data"] == null || root["texture_data"]!.Type != JTokenType.Object)
                {
                    root["texture_data"] = new JObject();
                }

                var texData = (JObject)root["texture_data"]!;
                texData[atlasKey] = new JObject
                {
                    ["textures"] = new JArray(iconAtlasRel.Replace('\\', '/'))
                };

                File.WriteAllText(itemTexJsonAbs, root.ToString(Formatting.Indented));
                ConsoleWorker.Write.Line(
                    "info",
                    ns + ":" + id + " item_texture.json → " + atlasKey + " = " + iconAtlasRel);
            }
            catch (Exception ex)
            {
                ConsoleWorker.Write.Line(
                    "warn",
                    ns + ":" + id + " failed updating item_texture.json: " + ex.Message);
            }

            // NOTE: We are not yet creating a dedicated minecraft:block definition here.
            // That will be wired up together with Geyser custom_block_mappings in a later step.
        }

        // ---------- helpers ----------

        private static string Sanitize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "unknown";
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s.Trim())
            {
                if (char.IsLetterOrDigit(ch) || ch == '_')
                    sb.Append(char.ToLowerInvariant(ch));
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }
    }
}