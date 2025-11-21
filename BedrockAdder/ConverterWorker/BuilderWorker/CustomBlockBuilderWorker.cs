using BedrockAdder.ConverterWorker.ObjectWorker;   // ModelBuilderWorker, IModelIconRenderer
using BedrockAdder.Library;
using BedrockAdder.Managers;
using BedrockAdder.Renderer;                       // CefOffscreenIconRenderer
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

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
                    ConsoleWorker.Write.Line("error", "BuildOneBlock (block) failed: " + ex.Message);
                }
            }
            try
            {
                BuildTerrainAtlas(session);
            }
            catch (Exception ex)
            {
                ConsoleWorker.Write.Line("warn", "BuildTerrainAtlas failed: " + ex.Message);
            }
        }

        public static void BuildOneBlock(PackSession session, CustomBlock block, IModelIconRenderer renderer)
        {
            BedrockManager.EnsureDir(Path.Combine(session.PackRoot, "textures", "blocks"));
            if (block == null)
            {
                ConsoleWorker.Write.Line("warn", "BuildOneBlock: block is null");
                return;
            }

            string ns = string.IsNullOrWhiteSpace(block.BlockNamespace) ? "unknown" : block.BlockNamespace;
            string id = string.IsNullOrWhiteSpace(block.BlockItemID) ? "unknown" : block.BlockItemID;

            // Bedrock identifier + atlas key + icon path
            string bedrockId = BedrockManager.MakeBedrockItemId(ns, id);                 // ia:ns_id
            string atlasKey = "ia_block_" + Sanitize(ns) + "_" + Sanitize(id);           // key inside item_texture.json
            string iconAtlasRel = "textures/items/" + Sanitize(ns) + "/" + Sanitize(id) + ".png";
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
                        ConsoleWorker.Write.Line("info", ns + ":" + id + " wrote geometry " + built.GeometryOutRel);
                    }
                    catch (Exception ex)
                    {
                        ConsoleWorker.Write.Line("warn", ns + ":" + id + " failed writing geometry: " + geoAbs + " ex=" + ex.Message);
                    }
                }

                // 2) Write attachable (.json) if present – same idea as for items
                if (!string.IsNullOrWhiteSpace(built.AttachableJson))
                {
                    string attAbs = Path.Combine(session.PackRoot, built.AttachableOutRel.Replace('/', Path.DirectorySeparatorChar));
                    string? attDir = Path.GetDirectoryName(attAbs);
                    if (!string.IsNullOrWhiteSpace(attDir))
                        Directory.CreateDirectory(attDir);

                    try
                    {
                        File.WriteAllText(attAbs, built.AttachableJson, System.Text.Encoding.UTF8);
                        ConsoleWorker.Write.Line("info", ns + ":" + id + " wrote attachable " + built.AttachableOutRel);
                    }
                    catch (Exception ex)
                    {
                        ConsoleWorker.Write.Line("warn", ns + ":" + id + " failed writing attachable: " + attAbs + " ex=" + ex.Message);
                    }
                }
                else
                {
                    ConsoleWorker.Write.Line("warn", ns + ":" + id + " has no AttachableJson produced by ModelBuilderWorker.");
                }

                // 3) Copy textures used by the 3D model
                if (built.TexturesToCopy != null)
                {
                    foreach (var (srcAbs, dstRel) in built.TexturesToCopy)
                    {
                        if (string.IsNullOrWhiteSpace(srcAbs) || string.IsNullOrWhiteSpace(dstRel))
                            continue;

                        string toAbs = Path.Combine(session.PackRoot, dstRel.Replace('/', Path.DirectorySeparatorChar));
                        string? toDir = Path.GetDirectoryName(toAbs);
                        if (!string.IsNullOrWhiteSpace(toDir))
                            Directory.CreateDirectory(toDir);

                        try
                        {
                            File.Copy(srcAbs, toAbs, true);
                            ConsoleWorker.Write.Line("info", ns + ":" + id + " copied texture " + srcAbs + " → " + dstRel);
                        }
                        catch (Exception ex)
                        {
                            ConsoleWorker.Write.Line("warn", ns + ":" + id + " failed copying texture " + srcAbs + " → " + dstRel + " ex=" + ex.Message);
                        }
                    }
                }

                // ---------- 3D ICON RENDERING (for 3D blocks) ----------

                bool hasModel = !string.IsNullOrWhiteSpace(block.ModelPath) && File.Exists(block.ModelPath);
                if (hasModel)
                {
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
                                iconSourceAbs = iconAbs;
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

            // ---------- BASE TEXTURES FOR CUBOID BLOCKS (non-3D) ----------
            if (!block.Is3D)
            {
                CopyCuboidBlockTextures(session, block, ns, id);
            }

            // ---------- CUBE ICON RENDER (for non-3D cuboid blocks) ----------
            if (iconSourceAbs == null && !block.Is3D)
            {
                string cubeModelPath = Path.Combine(AppContext.BaseDirectory, "Library", "example_cuboid.json");
                if (File.Exists(cubeModelPath))
                {
                    var texMap = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    if (block.PerFaceTexture && block.FaceTexturePaths != null && block.FaceTexturePaths.Count > 0)
                    {
                        foreach (var kv in block.FaceTexturePaths)
                        {
                            if (!string.IsNullOrWhiteSpace(kv.Value) && File.Exists(kv.Value))
                            {
                                texMap[kv.Key] = kv.Value;
                            }
                        }
                    }

                    if (texMap.Count == 0 &&
                        !string.IsNullOrWhiteSpace(block.TexturePath) &&
                        File.Exists(block.TexturePath))
                    {
                        texMap["default"] = block.TexturePath;
                    }

                    if (texMap.Count > 0)
                    {
                        try
                        {
                            bool rendered = renderer.TryRenderIcon(cubeModelPath, texMap, iconAbs);
                            if (rendered && File.Exists(iconAbs))
                            {
                                iconSourceAbs = iconAbs;
                                ConsoleWorker.Write.Line(
                                    "info",
                                    ns + ":" + id + " cube block icon rendered → " + iconAtlasRel);
                            }
                            else
                            {
                                ConsoleWorker.Write.Line(
                                    "warn",
                                    ns + ":" + id + " cube block icon render failed, will fall back to flat texture.");
                            }
                        }
                        catch (Exception ex)
                        {
                            ConsoleWorker.Write.Line(
                                "warn",
                                ns + ":" + id + " exception while rendering cube block icon: " + ex.Message);
                        }
                    }
                }
                else
                {
                    ConsoleWorker.Write.Line(
                        "warn",
                        "Cube model not found at " + cubeModelPath + " – skipping cube icon render for " + ns + ":" + id);
                }
            }

            // ---------- FLAT ICON FALLBACKS (for non-3D or failed 3D / cube render) ----------

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

            if (!string.IsNullOrWhiteSpace(iconSourceAbs) && File.Exists(iconSourceAbs))
            {
                string? iconDir = Path.GetDirectoryName(iconAbs);
                if (!string.IsNullOrWhiteSpace(iconDir))
                    Directory.CreateDirectory(iconDir);

                // If the renderer already wrote directly to iconAbs, don't copy again.
                if (string.Equals(iconSourceAbs, iconAbs, StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleWorker.Write.Line(
                        "info",
                        ns + ":" + id + " icon already at target path " + iconAbs + ", skipping copy.");
                }
                else
                {
                    try
                    {
                        File.Copy(iconSourceAbs, iconAbs, true);
                    }
                    catch (Exception ex)
                    {
                        ConsoleWorker.Write.Line(
                            "warn",
                            ns + ":" + id + " failed copying block icon " + iconSourceAbs + " → " + iconAbs +
                            " ex=" + ex.Message);
                    }
                }
            }

            // ---------- UPDATE item_texture.json ----------

            try
            {
                string atlasPath = Path.Combine(session.PackRoot, "textures", "item_texture.json");
                JObject atlasRoot;
                if (File.Exists(atlasPath))
                {
                    atlasRoot = JObject.Parse(File.ReadAllText(atlasPath));
                }
                else
                {
                    atlasRoot = new JObject
                    {
                        ["resource_pack_name"] = session.PackName ?? "BedrockAdder",
                        ["texture_name"] = "atlas.items",
                        ["texture_data"] = new JObject()
                    };
                }

                if (atlasRoot["texture_data"] is not JObject texData)
                {
                    texData = new JObject();
                    atlasRoot["texture_data"] = texData;
                }

                // Bedrock happily accepts a string here; stay consistent with item builder.
                texData[atlasKey] = new JObject
                {
                    ["textures"] = BedrockManager.NormalizeAtlasPath(iconAtlasRel)
                };

                File.WriteAllText(atlasPath, atlasRoot.ToString(Newtonsoft.Json.Formatting.Indented));
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

            // ---------- MINIMAL ITEM DEFINITION (so the block exists as a Bedrock item) ----------

            WriteItemDefinition(session, bedrockId, atlasKey);
        }

        // ---------- helpers ----------

        private static void WriteItemDefinition(PackSession session, string bedrockId, string atlasKey)
        {
            // Same format as CustomItemBuilderWorker, but re-usable for blocks
            string safeName = bedrockId.Replace(':', '_'); // ia_ns_id
            string itemJsonRel = Path.Combine("items", safeName + ".json");
            string itemJsonAbs = Path.Combine(session.PackRoot, itemJsonRel);

            Directory.CreateDirectory(Path.GetDirectoryName(itemJsonAbs)!);

            var item = new JObject
            {
                ["format_version"] = "1.20.0",
                ["minecraft:item"] = new JObject
                {
                    ["description"] = new JObject
                    {
                        ["identifier"] = bedrockId
                    },
                    ["components"] = new JObject
                    {
                        ["minecraft:icon"] = new JObject { ["texture"] = atlasKey }
                    }
                }
            };

            File.WriteAllText(itemJsonAbs, item.ToString(Newtonsoft.Json.Formatting.Indented));
            ConsoleWorker.Write.Line("info", "Wrote block item json → " + itemJsonRel + " (id=" + bedrockId + ")");
        }

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
        /// <summary>
        /// For non-3D cuboid blocks, copy their base textures into the Bedrock pack
        /// so future block models / terrain_texture.json can reference them.
        /// </summary>
        private static void CopyCuboidBlockTextures(PackSession session, CustomBlock block, string ns, string id)
        {
            // Only makes sense for non-3D blocks that actually have textures
            if (block == null || block.Is3D)
                return;

            string nsSafe = Sanitize(ns);
            string idSafe = Sanitize(id);

            // textures/blocks/{ns}/...
            string blocksRoot = Path.Combine(session.PackRoot, "textures", "blocks", nsSafe);
            try
            {
                Directory.CreateDirectory(blocksRoot);
            }
            catch (Exception ex)
            {
                ConsoleWorker.Write.Line("warn", nsSafe + ":" + idSafe + " failed creating blocks texture dir: " + blocksRoot + " ex=" + ex.Message);
                return;
            }

            // ---- Per-face textures (graphics.textures: up/down/north/south/east/west) ----
            if (block.PerFaceTexture &&
                block.FaceTexturePaths != null &&
                block.FaceTexturePaths.Count > 0)
            {
                foreach (var kv in block.FaceTexturePaths)
                {
                    string faceKey = kv.Key;
                    string srcAbs = kv.Value;

                    if (string.IsNullOrWhiteSpace(faceKey))
                        continue;
                    if (string.IsNullOrWhiteSpace(srcAbs) || !File.Exists(srcAbs))
                        continue;

                    // Normalize face: "Up" / "UP" → "up"
                    faceKey = faceKey.Trim().ToLowerInvariant(); // up, down, north, south, east, west

                    string fileName = idSafe + "_" + faceKey + ".png";  // e.g. my_block_up.png
                    string dstAbs = Path.Combine(blocksRoot, fileName);

                    try
                    {
                        File.Copy(srcAbs, dstAbs, true);
                        ConsoleWorker.Write.Line(
                            "info",
                            nsSafe + ":" + idSafe + " copied face texture " + faceKey + " " + srcAbs + " → textures/blocks/" + nsSafe + "/" + fileName);
                    }
                    catch (Exception ex)
                    {
                        ConsoleWorker.Write.Line(
                            "warn",
                            nsSafe + ":" + idSafe + " failed copying face texture " + faceKey + " " + srcAbs +
                            " → " + dstAbs + " ex=" + ex.Message);
                    }
                }

                return; // per-face variant handled; no need for single-texture fallback
            }

            // ---- Single shared texture (graphics.texture or inferred from model) ----
            if (!string.IsNullOrWhiteSpace(block.TexturePath) &&
                File.Exists(block.TexturePath))
            {
                string fileName = idSafe + ".png"; // e.g. my_block.png
                string dstAbs = Path.Combine(blocksRoot, fileName);

                try
                {
                    File.Copy(block.TexturePath, dstAbs, true);
                    ConsoleWorker.Write.Line(
                        "info",
                        nsSafe + ":" + idSafe + " copied block texture " + block.TexturePath +
                        " → textures/blocks/" + nsSafe + "/" + fileName);
                }
                catch (Exception ex)
                {
                    ConsoleWorker.Write.Line(
                        "warn",
                        nsSafe + ":" + idSafe + " failed copying block texture " + block.TexturePath +
                        " → " + dstAbs + " ex=" + ex.Message);
                }
            }
            else
            {
                ConsoleWorker.Write.Line(
                    "warn",
                    nsSafe + ":" + idSafe + " has no TexturePath to copy for cuboid block.");
            }
        }

        /// <summary>
        /// Build or update textures/terrain_texture.json for all custom blocks.
        /// We only register entries for cuboid (non-3D) blocks here, since 3D blocks
        /// use models/entity geometries with their own textures.
        /// </summary>
        private static void BuildTerrainAtlas(PackSession session)
        {
            string atlasPath = Path.Combine(session.PackRoot, "textures", "terrain_texture.json");
            JObject root;

            if (File.Exists(atlasPath))
            {
                try
                {
                    root = JObject.Parse(File.ReadAllText(atlasPath));
                }
                catch
                {
                    root = new JObject();
                }
            }
            else
            {
                root = new JObject
                {
                    ["resource_pack_name"] = session.PackName ?? "BedrockAdder",
                    ["texture_name"] = "atlas.terrain",
                    ["padding"] = 8,
                    ["num_mip_levels"] = 4,
                    ["texture_data"] = new JObject()
                };
            }

            if (root["texture_data"] is not JObject data)
            {
                data = new JObject();
                root["texture_data"] = data;
            }

            // For each custom block, register its block textures into the terrain atlas.
            foreach (CustomBlock block in Lists.CustomBlocks)
            {
                if (block == null) continue;

                string ns = string.IsNullOrWhiteSpace(block.BlockNamespace) ? "unknown" : block.BlockNamespace;
                string id = string.IsNullOrWhiteSpace(block.BlockItemID) ? "unknown" : block.BlockItemID;
                string nsSafe = Sanitize(ns);
                string idSafe = Sanitize(id);

                // Skip 3D blocks for now; their visuals are handled via entity geometry.
                if (block.Is3D)
                    continue;

                // Per-face textures: up/down/north/south/east/west
                if (block.PerFaceTexture && block.FaceTexturePaths != null && block.FaceTexturePaths.Count > 0)
                {
                    foreach (var kv in block.FaceTexturePaths)
                    {
                        if (string.IsNullOrWhiteSpace(kv.Key))
                            continue;
                        string faceKey = kv.Key.Trim().ToLowerInvariant();
                        if (string.IsNullOrWhiteSpace(faceKey))
                            continue;

                        // textures/blocks/{ns}/{id}_{face}.png
                        string atlasKey = "ia_block_" + nsSafe + "_" + idSafe + "_" + faceKey;
                        string relPath = "textures/blocks/" + nsSafe + "/" + idSafe + "_" + faceKey + ".png";
                        string texPath = BedrockManager.NormalizeAtlasPath(relPath);

                        data[atlasKey] = new JObject
                        {
                            ["textures"] = texPath
                        };
                    }
                }
                else
                {
                    // Single-texture cuboid block: textures/blocks/{ns}/{id}.png
                    string atlasKey = "ia_block_" + nsSafe + "_" + idSafe;
                    string relPath = "textures/blocks/" + nsSafe + "/" + idSafe + ".png";
                    string texPath = BedrockManager.NormalizeAtlasPath(relPath);

                    data[atlasKey] = new JObject
                    {
                        ["textures"] = texPath
                    };
                }
            }
            File.WriteAllText(atlasPath, root.ToString(Formatting.Indented), System.Text.Encoding.UTF8);
            ConsoleWorker.Write.Line("info", "Updated terrain_texture.json for custom blocks.");
        }
    }
}