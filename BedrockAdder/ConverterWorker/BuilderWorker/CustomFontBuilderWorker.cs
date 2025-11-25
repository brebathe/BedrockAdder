using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using BedrockAdder.Library;      // Lists, CustomFont, PackSession
using BedrockAdder.Managers;    // BedrockManager
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BedrockAdder.ConverterWorker.BuilderWorker
{
    internal static class CustomFontBuilderWorker
    {
        /// <summary>
        /// Entry point: build all font-related assets (GUI backgrounds + HUD/emote glyph pages).
        /// - GUI fonts (IsGui=true) → textures/ui/chestgui/*.png + ui/chest_screen.json
        /// - Non-GUI fonts          → font/glyph_XX.png bitmap pages
        /// - Also ensures ui/ui_common.json exists (for close button layer).
        /// </summary>
        public static void BuildCustomFonts(PackSession session, string itemsAdderRoot)
        {
            if (session == null)
            {
                ConsoleWorker.Write.Line("error", "CustomFontBuilderWorker: session is null");
                return;
            }

            if (Lists.CustomFonts == null || Lists.CustomFonts.Count == 0)
            {
                ConsoleWorker.Write.Line("info", "CustomFontBuilderWorker: no custom fonts to build.");
                return;
            }

            ConsoleWorker.Write.Line("info", "CustomFontBuilderWorker: build started. Fonts=" + Lists.CustomFonts.Count);

            try
            {
                EnsureUiCommon(session);
            }
            catch (Exception ex)
            {
                ConsoleWorker.Write.Line("warn", "CustomFontBuilderWorker: EnsureUiCommon failed: " + ex.Message);
            }

            try
            {
                BuildGuiFonts(session, itemsAdderRoot);
            }
            catch (Exception ex)
            {
                ConsoleWorker.Write.Line("warn", "CustomFontBuilderWorker: BuildGuiFonts failed: " + ex.Message);
            }

            try
            {
                BuildBitmapFontPages(session, itemsAdderRoot);
            }
            catch (Exception ex)
            {
                ConsoleWorker.Write.Line("warn", "CustomFontBuilderWorker: BuildBitmapFontPages failed: " + ex.Message);
            }

            ConsoleWorker.Write.Line("info", "CustomFontBuilderWorker: build finished.");
        }

        // --------------------------------------------------------------------
        // 1) GUI FONTS → chest_screen.json + textures/ui/chestgui
        // --------------------------------------------------------------------

        private static void BuildGuiFonts(PackSession session, string itemsAdderRoot)
        {
            var guiFonts = Lists.CustomFonts
                .Where(f => f != null && f.IsGui && !string.IsNullOrWhiteSpace(f.FontSymbol))
                .ToList();

            if (guiFonts.Count == 0)
            {
                ConsoleWorker.Write.Line("info", "CustomFontBuilderWorker: no GUI fonts (IsGui=true) found; skipping chest_screen.json.");
                return;
            }

            string chestGuiRoot = Path.Combine(session.PackRoot, "textures", "ui", "chestgui");
            string uiRoot = Path.Combine(session.PackRoot, "ui");

            BedrockManager.EnsureDir(chestGuiRoot);
            BedrockManager.EnsureDir(uiRoot);

            string chestScreenAbs = Path.Combine(uiRoot, "chest_screen.json");

            JObject root;
            if (File.Exists(chestScreenAbs))
            {
                try
                {
                    root = JObject.Parse(File.ReadAllText(chestScreenAbs));
                }
                catch (Exception ex)
                {
                    ConsoleWorker.Write.Line("warn", "CustomFontBuilderWorker: existing chest_screen.json is invalid JSON, recreating. ex=" + ex.Message);
                    root = new JObject();
                }
            }
            else
            {
                // Minimal skeleton; real layout can be more complex, but we only care
                // that gui_* entries live at the root under the "chest" namespace.
                root = new JObject
                {
                    ["namespace"] = "chest"
                };
            }

            int guiCount = 0;

            foreach (var font in guiFonts)
            {
                try
                {
                    string symbol = font.FontSymbol;
                    if (string.IsNullOrEmpty(symbol))
                        continue;

                    // Use the first char; ItemsAdder GUI glyphs should be 1-char strings.
                    int codepoint = char.ConvertToUtf32(symbol, 0);
                    string hex = codepoint.ToString("X4"); // e.g. E059
                    string guiKey = "gui_" + hex;

                    // Resolve IA texture (we stored FontImagePath as a normalized "textures/..."-style rel path).
                    string srcRel = font.FontImagePath ?? string.Empty;
                    string srcAbs = BuildIaFontTextureAbs(itemsAdderRoot, font.FontNamespace, srcRel);

                    if (!File.Exists(srcAbs))
                    {
                        ConsoleWorker.Write.Line(
                            "warn",
                            "CustomFontBuilderWorker: GUI font source missing for " +
                            font.FontNamespace + ":" + font.FontID +
                            " path=" + srcAbs);
                        continue;
                    }

                    string dstPngName = hex + ".png";
                    string dstAbs = Path.Combine(chestGuiRoot, dstPngName);
                    Directory.CreateDirectory(Path.GetDirectoryName(dstAbs)!);

                    int width = 192;
                    int height = 170;

                    try
                    {
                        using (var srcImg = Image.FromFile(srcAbs))
                        {
                            int srcW = srcImg.Width;
                            int srcH = srcImg.Height;

                            if (srcW <= 0 || srcH <= 0)
                            {
                                width = 192;
                                height = 170;
                                srcImg.Save(dstAbs, ImageFormat.Png);
                            }
                            else
                            {
                                int finalW = srcW;
                                int finalH = srcH;

                                if (font.ScaleRatio.HasValue && font.ScaleRatio.Value > 0)
                                {
                                    int targetH = font.ScaleRatio.Value;
                                    float scale = (float)targetH / srcH;

                                    finalH = targetH;
                                    finalW = Math.Max(1, (int)Math.Round(srcW * scale));
                                }

                                width = finalW;
                                height = finalH;

                                using (var bmp = new Bitmap(finalW, finalH, PixelFormat.Format32bppArgb))
                                using (var g = Graphics.FromImage(bmp))
                                {
                                    g.Clear(Color.Transparent);
                                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                                    g.PixelOffsetMode = PixelOffsetMode.Half;

                                    g.DrawImage(
                                        srcImg,
                                        new Rectangle(0, 0, finalW, finalH),
                                        new Rectangle(0, 0, srcW, srcH),
                                        GraphicsUnit.Pixel
                                    );

                                    bmp.Save(dstAbs, ImageFormat.Png);
                                }
                            }
                        }
                    }
                    catch (Exception exCopy)
                    {
                        ConsoleWorker.Write.Line(
                            "warn",
                            "CustomFontBuilderWorker: failed scaling GUI font texture " +
                            srcAbs + " → " + dstAbs + " ex=" + exCopy.Message + " (attempting direct copy)");

                        try
                        {
                            File.Copy(srcAbs, dstAbs, true);
                            using (var img = Image.FromFile(dstAbs))
                            {
                                width = img.Width;
                                height = img.Height;
                            }
                        }
                        catch (Exception exSize)
                        {
                            ConsoleWorker.Write.Line(
                                "warn",
                                "CustomFontBuilderWorker: failed reading size for " +
                                dstAbs + " ex=" + exSize.Message + " (using default 192x170)");
                            width = 192;
                            height = 170;
                        }
                    }

                    // Compute Bedrock Y offset from ItemsAdder y_position
                    // Formula from reverse engineering:
                    // offset_y ≈ round(-0.5 * y_position - 36.5)
                    int offsetY = (int)Math.Round(-0.5 * font.YPosition - 36.5);

                    // Build the gui_<HEX> JSON node
                    string texturePath = "textures/ui/chestgui/" + hex;
                    string visibleExpr = "(not (($chest_title - '\\u" + hex + "') = $chest_title))";

                    var guiObj = new JObject
                    {
                        ["type"] = "image",
                        ["layer"] = 13,
                        ["texture"] = texturePath,
                        ["$chest_title"] = "$container_title",
                        ["visible"] = visibleExpr,
                        ["offset"] = new JArray(0, offsetY),
                        ["size"] = new JArray(width, height)
                    };

                    root[guiKey] = guiObj;
                    guiCount++;

                    ConsoleWorker.Write.Line(
                        "info",
                        "CustomFontBuilderWorker: GUI font mapped → " +
                        guiKey + " char='\\u" + hex + "' tex=" + texturePath +
                        " offsetY=" + offsetY + " size=" + width + "x" + height +
                        " scale=" + (font.ScaleRatio?.ToString() ?? "null"));
                }
                catch (Exception ex)
                {
                    ConsoleWorker.Write.Line(
                        "warn",
                        "CustomFontBuilderWorker: GUI font build failed for " +
                        font.FontNamespace + ":" + font.FontID + " ex=" + ex.Message);
                }
            }

            // Write chest_screen.json back
            try
            {
                File.WriteAllText(chestScreenAbs, root.ToString(Formatting.Indented), System.Text.Encoding.UTF8);
                ConsoleWorker.Write.Line("info", "CustomFontBuilderWorker: chest_screen.json updated. GUI entries=" + guiCount);
            }
            catch (Exception ex)
            {
                ConsoleWorker.Write.Line("warn", "CustomFontBuilderWorker: failed writing chest_screen.json: " + ex.Message);
            }
        }

        // --------------------------------------------------------------------
        // 2) HUD / EMOTE FONTS → font/glyph_XX.png bitmap pages
        // --------------------------------------------------------------------

        private static void BuildBitmapFontPages(PackSession session, string itemsAdderRoot)
        {
            var normalFonts = Lists.CustomFonts
                .Where(f => f != null && !f.IsGui && !string.IsNullOrWhiteSpace(f.FontSymbol))
                .ToList();

            if (normalFonts.Count == 0)
            {
                ConsoleWorker.Write.Line("info", "CustomFontBuilderWorker: no non-GUI fonts to pack into glyph pages.");
                return;
            }

            string fontRoot = Path.Combine(session.PackRoot, "font");
            BedrockManager.EnsureDir(fontRoot);

            // Group by "page" = high byte (so U+E059 → page 0xE0 → glyph_E0.png)
            var groups = normalFonts.GroupBy(f =>
            {
                int cp = char.ConvertToUtf32(f.FontSymbol, 0);
                return (cp >> 8) & 0xFF;
            });

            foreach (var group in groups)
            {
                int page = group.Key; // e.g. 0xE0
                string pageHex = page.ToString("X2"); // E0
                string atlasName = "glyph_" + pageHex + ".png";
                string atlasAbs = Path.Combine(fontRoot, atlasName);

                try
                {
                    BuildOneBitmapPage(group.ToList(), page, atlasAbs, itemsAdderRoot);
                }
                catch (Exception ex)
                {
                    ConsoleWorker.Write.Line(
                        "warn",
                        "CustomFontBuilderWorker: BuildOneBitmapPage failed for page 0x" + pageHex +
                        " ex=" + ex.Message);
                }
            }
        }

        /// <summary>
        /// Build a single bitmap font page (glyph_XX.png) as a 16x16 grid.
        /// Cell size is the max width/height of FINAL glyph sizes (after scale_ratio).
        /// Glyph position is determined by low byte of the codepoint:
        ///   low = cp & 0xFF;  row = low / 16; col = low % 16
        /// </summary>
        private static void BuildOneBitmapPage(
            List<CustomFont> fonts,
            int page,
            string atlasAbs,
            string itemsAdderRoot)
        {
            if (fonts == null || fonts.Count == 0)
                return;

            string pageHex = page.ToString("X2");

            // Collect glyph info + determine cell size
            var glyphInfos = new List<GlyphInfo>();
            int cellW = 0;
            int cellH = 0;

            foreach (var font in fonts)
            {
                try
                {
                    string symbol = font.FontSymbol;
                    if (string.IsNullOrEmpty(symbol))
                        continue;

                    int cp = char.ConvertToUtf32(symbol, 0);
                    int high = (cp >> 8) & 0xFF;
                    if (high != page)
                        continue; // safety

                    int low = cp & 0xFF;
                    string srcRel = font.FontImagePath ?? string.Empty;
                    string srcAbs = BuildIaFontTextureAbs(itemsAdderRoot, font.FontNamespace, srcRel);

                    if (!File.Exists(srcAbs))
                    {
                        ConsoleWorker.Write.Line(
                            "warn",
                            "CustomFontBuilderWorker: bitmap font source missing for " +
                            font.FontNamespace + ":" + font.FontID +
                            " page=0x" + pageHex + " path=" + srcAbs);
                        continue;
                    }

                    int srcW;
                    int srcH;
                    using (var img = Image.FromFile(srcAbs))
                    {
                        srcW = img.Width;
                        srcH = img.Height;
                    }

                    if (srcW <= 0 || srcH <= 0)
                        continue;

                    int finalW = srcW;
                    int finalH = srcH;

                    if (font.ScaleRatio.HasValue && font.ScaleRatio.Value > 0)
                    {
                        int targetH = font.ScaleRatio.Value;
                        float scale = (float)targetH / srcH;

                        finalH = targetH;
                        finalW = Math.Max(1, (int)Math.Round(srcW * scale));
                    }

                    cellW = Math.Max(cellW, finalW);
                    cellH = Math.Max(cellH, finalH);

                    glyphInfos.Add(new GlyphInfo
                    {
                        Codepoint = cp,
                        LowByte = low,
                        SourcePath = srcAbs,
                        Width = finalW,
                        Height = finalH
                    });
                }
                catch (Exception ex)
                {
                    ConsoleWorker.Write.Line(
                        "warn",
                        "CustomFontBuilderWorker: failed reading glyph for bitmap page 0x" +
                        pageHex + " ex=" + ex.Message);
                }
            }

            if (glyphInfos.Count == 0)
            {
                ConsoleWorker.Write.Line(
                    "info",
                    "CustomFontBuilderWorker: no valid glyphs for bitmap page 0x" + pageHex + "; skipping.");
                return;
            }

            int cellSize = Math.Max(cellW, cellH);
            if (cellSize <= 0)
            {
                ConsoleWorker.Write.Line(
                    "warn",
                    "CustomFontBuilderWorker: invalid cell size for bitmap page 0x" + pageHex + "; skipping.");
                return;
            }

            int gridSize = 16; // 16x16 cells → 256 glyphs
            int atlasW = cellSize * gridSize;
            int atlasH = cellSize * gridSize;

            Directory.CreateDirectory(Path.GetDirectoryName(atlasAbs)!);

            using (var bmp = new Bitmap(atlasW, atlasH, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;

                foreach (var gi in glyphInfos)
                {
                    try
                    {
                        int col = gi.LowByte % gridSize;
                        int row = gi.LowByte / gridSize;

                        int x = col * cellSize;
                        int y = row * cellSize;

                        using (var glyphImg = Image.FromFile(gi.SourcePath))
                        {
                            // Draw scaled into the top-left of the cell.
                            g.DrawImage(
                                glyphImg,
                                new Rectangle(x, y, gi.Width, gi.Height),
                                new Rectangle(0, 0, glyphImg.Width, glyphImg.Height),
                                GraphicsUnit.Pixel
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleWorker.Write.Line(
                            "warn",
                            "CustomFontBuilderWorker: failed drawing glyph U+" +
                            gi.Codepoint.ToString("X4") + " on page 0x" + pageHex +
                            " ex=" + ex.Message);
                    }
                }

                try
                {
                    bmp.Save(atlasAbs, ImageFormat.Png);
                    ConsoleWorker.Write.Line(
                        "info",
                        "CustomFontBuilderWorker: bitmap font page written → " +
                        atlasAbs.Replace(Path.DirectorySeparatorChar, '/')
                    );
                }
                catch (Exception ex)
                {
                    ConsoleWorker.Write.Line(
                        "warn",
                        "CustomFontBuilderWorker: failed saving bitmap page 0x" +
                        pageHex + " → " + atlasAbs + " ex=" + ex.Message);
                }
            }
        }

        private sealed class GlyphInfo
        {
            public int Codepoint { get; set; }
            public int LowByte { get; set; }
            public string SourcePath { get; set; } = string.Empty;
            public int Width { get; set; }  // final (scaled) width
            public int Height { get; set; } // final (scaled) height
        }

        // --------------------------------------------------------------------
        // 3) ui_common.json helper
        // --------------------------------------------------------------------

        private static void EnsureUiCommon(PackSession session)
        {
            string uiRoot = Path.Combine(session.PackRoot, "ui");
            BedrockManager.EnsureDir(uiRoot);

            string uiCommonAbs = Path.Combine(uiRoot, "ui_common.json");

            if (File.Exists(uiCommonAbs))
            {
                // Don't overwrite user/custom template.
                ConsoleWorker.Write.Line("info", "CustomFontBuilderWorker: ui_common.json already exists; leaving as-is.");
                return;
            }

            var root = new JObject
            {
                ["namespace"] = "common",
                ["common_panel/close_button_holder"] = new JObject
                {
                    ["layer"] = 15
                }
            };

            File.WriteAllText(uiCommonAbs, root.ToString(Formatting.Indented), System.Text.Encoding.UTF8);
            ConsoleWorker.Write.Line("info", "CustomFontBuilderWorker: created minimal ui_common.json.");
        }

        // --------------------------------------------------------------------
        // 4) Utility: resolve IA font texture absolute path
        // --------------------------------------------------------------------

        /// <summary>
        /// Build the absolute path to an ItemsAdder font texture from namespace + normalized rel path.
        /// Mirrors the structure:
        /// {itemsAdderRoot}/contents/{ns}/resourcepack/{ns}/textures/{rel}
        /// </summary>
        private static string BuildIaFontTextureAbs(string itemsAdderRoot, string? fontNamespace, string rel)
        {
            string ns = string.IsNullOrWhiteSpace(fontNamespace) ? "_unknown" : fontNamespace.Trim();
            rel = rel.Replace('\\', '/').TrimStart('/');

            string abs = Path.Combine(
                itemsAdderRoot,
                "contents",
                ns,
                "resourcepack",
                ns,
                "textures",
                rel.Replace('/', Path.DirectorySeparatorChar)
            );
            return abs;
        }
    }
}