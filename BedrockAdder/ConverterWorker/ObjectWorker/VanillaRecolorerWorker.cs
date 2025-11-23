using BedrockAdder.Library;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace BedrockAdder.ConverterWorker.ObjectWorker
{
    internal static class VanillaRecolorerWorker
    {
        /// <summary>
        /// Build a recolored icon for a vanilla-based item using SkiaSharp.
        /// Uses CustomItem.VanillaTextureId (e.g. "minecraft:item/gold_nugget.png")
        /// and CustomItem.RecolorTint. Reads from the Minecraft JAR for the selected
        /// version and writes the tinted result to outputPngAbs.
        /// </summary>
        internal static bool TryBuildRecoloredItemVanillaTexture(
            CustomItem item,
            string selectedVersion,
            string outputPngAbs,
            out string? error
        )
        {
            error = null;

            if (item == null)
            {
                error = "VanillaRecolorerWorker: item is null";
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.VanillaTextureId))
            {
                error = "VanillaRecolorerWorker: VanillaTextureId is empty";
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.RecolorTint))
            {
                error = "VanillaRecolorerWorker: item has no RecolorTint";
                return false;
            }

            if (string.IsNullOrWhiteSpace(selectedVersion) ||
                selectedVersion.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                error = "VanillaRecolorerWorker: no Minecraft version selected for vanilla recolor";
                return false;
            }

            if (!TryParseTint(item.RecolorTint, out var tintRgba))
            {
                error = "VanillaRecolorerWorker: failed to parse tint " + item.RecolorTint;
                return false;
            }

            // Resolve JAR path: %APPDATA%\.minecraft\versions\<version>\<version>.jar
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string jarPath = Path.Combine(appData, ".minecraft", "versions", selectedVersion, selectedVersion + ".jar");

            if (!File.Exists(jarPath))
            {
                error = "VanillaRecolorerWorker: Minecraft JAR not found at " + jarPath;
                ConsoleWorker.Write.Line(
                    "warn",
                    item.ItemNamespace + ":" + item.ItemID + " vanilla jar missing: " + jarPath
                );
                return false;
            }

            // Determine the internal PNG path inside the jar
            string jarRelPath = BuildVanillaTextureJarPath(item.VanillaTextureId);
            if (string.IsNullOrWhiteSpace(jarRelPath))
            {
                error = "VanillaRecolorerWorker: could not normalize VanillaTextureId " + item.VanillaTextureId;
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPngAbs) ?? ".");

                using (var archive = ZipFile.OpenRead(jarPath))
                {
                    var entry = archive.GetEntry(jarRelPath.Replace('\\', '/'));
                    if (entry == null)
                    {
                        error = "VanillaRecolorerWorker: entry not found in jar: " + jarRelPath;
                        ConsoleWorker.Write.Line(
                            "warn",
                            item.ItemNamespace + ":" + item.ItemID +
                            " vanilla texture entry not found in jar: " + jarRelPath
                        );
                        return false;
                    }

                    // Extract into memory for Skia
                    using (var entryStream = entry.Open())
                    using (var ms = new MemoryStream())
                    {
                        entryStream.CopyTo(ms);
                        ms.Position = 0;

                        using (var bitmap = SKBitmap.Decode(ms))
                        {
                            if (bitmap == null)
                            {
                                error = "VanillaRecolorerWorker: Skia failed to decode PNG from jar: " + jarRelPath;
                                return false;
                            }

                            int width = bitmap.Width;
                            int height = bitmap.Height;

                            // Multiply-style tint (same idea as ApplyMultiplyTintInternal)
                            float tfR = tintRgba.R / 255f;
                            float tfG = tintRgba.G / 255f;
                            float tfB = tintRgba.B / 255f;

                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    var p = bitmap.GetPixel(x, y);

                                    if (p.Alpha == 0)
                                        continue;

                                    byte r = (byte)(p.Red * tfR);
                                    byte g = (byte)(p.Green * tfG);
                                    byte b = (byte)(p.Blue * tfB);

                                    var outColor = new SKColor(r, g, b, p.Alpha);
                                    bitmap.SetPixel(x, y, outColor);
                                }
                            }

                            // Save final icon
                            using (var image = SKImage.FromBitmap(bitmap))
                            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                            using (var fs = File.Open(outputPngAbs, FileMode.Create, FileAccess.Write))
                            {
                                data.SaveTo(fs);
                            }
                        }
                    }
                }

                ConsoleWorker.Write.Line(
                    "info",
                    item.ItemNamespace + ":" + item.ItemID +
                    " recolored vanilla texture (Skia) " + item.VanillaTextureId +
                    " → " + outputPngAbs
                );

                return true;
            }
            catch (Exception ex)
            {
                error = "VanillaRecolorerWorker: Skia exception while recoloring: " + ex.Message;
                ConsoleWorker.Write.Line(
                    "warn",
                    item.ItemNamespace + ":" + item.ItemID +
                    " Skia exception while recoloring vanilla texture: " + ex.Message
                );
                return false;
            }
        }

        /// <summary>
        /// Normalize a VanillaTextureId like "minecraft:item/gold_nugget.png"
        /// to a jar path like "assets/minecraft/textures/item/gold_nugget.png".
        /// </summary>
        private static string BuildVanillaTextureJarPath(string vanillaId)
        {
            if (string.IsNullOrWhiteSpace(vanillaId))
                return string.Empty;

            string id = vanillaId.Trim();

            if (id.StartsWith("minecraft:", StringComparison.OrdinalIgnoreCase))
                id = id.Substring("minecraft:".Length);

            id = id.Replace("\\", "/");

            if (!id.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                id += ".png";

            // e.g. "item/gold_nugget.png", "block/stone.png", etc.
            return "assets/minecraft/textures/" + id.TrimStart('/');
        }

        /// <summary>
        /// Parse hex tint like "FFE3E3" or "#FFE3E3" or "FFE3E3FF".
        /// </summary>
        internal static bool TryParseTint(string? hex, out Rgba32 tint)
        {
            tint = new Rgba32(255, 255, 255, 255);

            if (string.IsNullOrWhiteSpace(hex))
                return false;

            string s = hex.Trim().TrimStart('#');

            try
            {
                byte r = 255, g = 255, b = 255, a = 255;

                if (s.Length == 6)
                {
                    r = byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    g = byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    b = byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                }
                else if (s.Length == 8)
                {
                    r = byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    g = byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    b = byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    a = byte.Parse(s.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                }
                else
                {
                    return false;
                }

                tint = new Rgba32(r, g, b, a);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Apply a multiplicative tint from source file → destination file.
        /// Used by CustomRecolorerWorker as well.
        /// </summary>
        internal static void ApplyMultiplyTintExternal(string sourcePngAbs, string destPngAbs, Rgba32 tint)
        {
            using (var image = Image.Load<Rgba32>(sourcePngAbs))
            {
                ApplyMultiplyTintInternal(image, tint);
                image.Save(destPngAbs);
            }
        }

        /// <summary>
        /// Apply a multiplicative tint from source file → destination file.
        /// Used by CustomRecolorerWorker as well.
        /// </summary>
        private static void ApplyMultiplyTintInternal(Image<Rgba32> image, Rgba32 tint)
        {
            int width = image.Width;
            int height = image.Height;

            for (int y = 0; y < height; y++)
            {
                var rowSpan = image.GetPixelRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    var p = rowSpan[x];

                    p.R = (byte)((p.R * tint.R) / 255);
                    p.G = (byte)((p.G * tint.G) / 255);
                    p.B = (byte)((p.B * tint.B) / 255);
                    // preserve original alpha but modulate with tint alpha
                    p.A = (byte)((p.A * tint.A) / 255);

                    rowSpan[x] = p;
                }
            }
        }
        /// <summary>
        /// Build a recolored icon for a vanilla-based armor item using SkiaSharp.
        /// Uses CustomArmor.VanillaTextureId (e.g. "minecraft:item/iron_helmet.png")
        /// and CustomArmor.RecolorTint. Reads from the Minecraft JAR for the selected
        /// version and writes the tinted result to outputPngAbs.
        /// </summary>
        internal static bool TryBuildRecoloredArmorVanillaTexture(
            CustomArmor armor,
            string selectedVersion,
            string outputPngAbs,
            out string? error
        )
        {
            error = null;

            // ---------- VALIDATION ----------
            if (armor == null)
            {
                error = "VanillaRecolorerWorker: item is null";
                return false;
            }

            if (string.IsNullOrWhiteSpace(armor.VanillaTextureId))
            {
                error = "VanillaRecolorerWorker: VanillaTextureId is empty";
                return false;
            }

            if (string.IsNullOrWhiteSpace(armor.RecolorTint))
            {
                error = "VanillaRecolorerWorker: item has no RecolorTint";
                return false;
            }

            if (string.IsNullOrWhiteSpace(selectedVersion) ||
                selectedVersion.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                error = "VanillaRecolorerWorker: no Minecraft version selected for vanilla recolor";
                return false;
            }

            if (!TryParseTint(armor.RecolorTint, out var tintRgba))
            {
                error = "VanillaRecolorerWorker: failed to parse tint " + armor.RecolorTint;
                return false;
            }

            // ---------- LOCATE VANILLA JAR ----------
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string jarPath = Path.Combine(appData, ".minecraft", "versions", selectedVersion, selectedVersion + ".jar");

            if (!File.Exists(jarPath))
            {
                error = "VanillaRecolorerWorker: Minecraft JAR not found at " + jarPath;
                ConsoleWorker.Write.Line("warn",
                    armor.ArmorNamespace + ":" + armor.ArmorID +
                    " vanilla jar missing: " + jarPath);
                return false;
            }

            string jarRelPath = BuildVanillaTextureJarPath(armor.VanillaTextureId);
            if (string.IsNullOrWhiteSpace(jarRelPath))
            {
                error = "VanillaRecolorerWorker: could not normalize VanillaTextureId " + armor.VanillaTextureId;
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPngAbs) ?? ".");

                using (var archive = ZipFile.OpenRead(jarPath))
                {
                    var entry = archive.GetEntry(jarRelPath.Replace('\\', '/'));
                    if (entry == null)
                    {
                        error = "VanillaRecolorerWorker: entry not found in jar: " + jarRelPath;
                        ConsoleWorker.Write.Line(
                            "warn",
                            armor.ArmorNamespace + ":" + armor.ArmorID +
                            " vanilla texture entry not found in jar: " + jarRelPath
                        );
                        return false;
                    }

                    // Extract into memory for Skia
                    using (var entryStream = entry.Open())
                    using (var ms = new MemoryStream())
                    {
                        entryStream.CopyTo(ms);
                        ms.Position = 0;

                        using (var bitmap = SKBitmap.Decode(ms))
                        {
                            if (bitmap == null)
                            {
                                error = "VanillaRecolorerWorker: Skia failed to decode PNG from jar: " + jarRelPath;
                                return false;
                            }

                            int width = bitmap.Width;
                            int height = bitmap.Height;

                            float tfR = tintRgba.R / 255f;
                            float tfG = tintRgba.G / 255f;
                            float tfB = tintRgba.B / 255f;

                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    var p = bitmap.GetPixel(x, y);

                                    if (p.Alpha == 0)
                                        continue;

                                    byte r = (byte)(p.Red * tfR);
                                    byte g = (byte)(p.Green * tfG);
                                    byte b = (byte)(p.Blue * tfB);

                                    var outColor = new SKColor(r, g, b, p.Alpha);
                                    bitmap.SetPixel(x, y, outColor);
                                }
                            }

                            // Save final icon
                            using (var image = SKImage.FromBitmap(bitmap))
                            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                            using (var fs = File.Open(outputPngAbs, FileMode.Create, FileAccess.Write))
                            {
                                data.SaveTo(fs);
                            }
                        }
                    }
                }

                ConsoleWorker.Write.Line(
                    "info",
                    armor.ArmorNamespace + ":" + armor.ArmorID +
                    " recolored vanilla armor icon (Skia) " + armor.VanillaTextureId +
                    " → " + outputPngAbs
                );

                return true;
            }
            catch (Exception ex)
            {
                error = "VanillaRecolorerWorker: Skia exception while recoloring: " + ex.Message;
                ConsoleWorker.Write.Line(
                    "warn",
                    armor.ArmorNamespace + ":" + armor.ArmorID +
                    " Skia exception while recoloring vanilla texture: " + ex.Message
                );
                return false;
            }
        }
    }
}