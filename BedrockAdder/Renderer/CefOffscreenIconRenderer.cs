using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CefSharp;
using CefSharp.OffScreen;
using BedrockAdder.ConverterWorker.ObjectWorker;

namespace BedrockAdder.Renderer
{
    internal sealed class CefOffscreenIconRenderer : IModelIconRenderer, IDisposable
    {
        private static bool _cefInit;
        private bool _disposed;

        private readonly int _size;
        private readonly bool _transparent;
        private readonly string _renderHtmlAbs;

        public CefOffscreenIconRenderer(int size, bool transparent, string renderHtmlAbsolutePath)
        {
            _size = size;
            _transparent = transparent;
            _renderHtmlAbs = renderHtmlAbsolutePath;
            EnsureCefInitialized();
        }

        // PUBLIC ENTRY – COMPLETELY SYNCHRONOUS
        public bool TryRenderIcon(string javaModelPath,
                                  IReadOnlyDictionary<string, string> textureSlotsAbs,
                                  string iconPngAbs)
        {
            try
            {
                return TryRenderIconInternal(javaModelPath, textureSlotsAbs, iconPngAbs);
            }
            catch (Exception ex)
            {
                ConsoleWorker.Write.Line("error", "CefOffscreenIconRenderer fatal error: " + ex);
                return false;
            }
        }

        private bool TryRenderIconInternal(string javaModelPath,
                                           IReadOnlyDictionary<string, string> textureSlotsAbs,
                                           string iconPngAbs)
        {
            if (!File.Exists(_renderHtmlAbs))
            {
                ConsoleWorker.Write.Line("error", "render.html not found: " + _renderHtmlAbs);
                return false;
            }

            if (string.IsNullOrWhiteSpace(javaModelPath))
            {
                ConsoleWorker.Write.Line("error", "Model path is null or empty.");
                return false;
            }

            string modelFullPath = Path.GetFullPath(javaModelPath);
            ConsoleWorker.Write.Line("debug", "CEF model path resolved to: " + modelFullPath);

            if (!File.Exists(modelFullPath))
            {
                ConsoleWorker.Write.Line("error", "Model JSON file does not exist: " + modelFullPath);
                return false;
            }

            if (!modelFullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleWorker.Write.Line("error", "Model path is not a .json file: " + modelFullPath);
                // We still continue, but this is almost certainly wrong.
            }

            // Build texture map for JS (slot -> file:// URL)
            var texMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool anyTextureExists = false;

            foreach (var kv in textureSlotsAbs)
            {
                string slotName = kv.Key ?? "(null-slot)";
                string? texPath = kv.Value;

                if (!string.IsNullOrWhiteSpace(texPath))
                {
                    string fullTexPath = Path.GetFullPath(texPath);
                    if (File.Exists(fullTexPath))
                    {
                        texMap[slotName] = new Uri(fullTexPath).AbsoluteUri;
                        anyTextureExists = true;
                        ConsoleWorker.Write.Line("debug", "Texture for slot '" + slotName + "': " + fullTexPath);
                    }
                    else
                    {
                        ConsoleWorker.Write.Line("error", "Texture for slot '" + slotName + "' does not exist: " + fullTexPath);
                    }
                }
                else
                {
                    ConsoleWorker.Write.Line("error", "Texture path for slot '" + slotName + "' is null/empty.");
                }
            }

            if (!anyTextureExists)
            {
                ConsoleWorker.Write.Line("error", "No existing textures found for model: " + modelFullPath + " – cannot render icon.");
                return false;
            }

            string texMapJson = Newtonsoft.Json.JsonConvert.SerializeObject(texMap);
            string texMapB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texMapJson));

            string modelFileUrl = new Uri(modelFullPath).AbsoluteUri;
            string htmlFileUrl = new Uri(Path.GetFullPath(_renderHtmlAbs)).AbsoluteUri;

            string url = htmlFileUrl
                + "?size=" + _size
                + "&transparent=" + (_transparent ? "1" : "0")
                + "&modelPath=" + Uri.EscapeDataString(modelFileUrl)
                + "&texMap=" + Uri.EscapeDataString(texMapB64);

            ConsoleWorker.Write.Line("debug", "CEF loading URL: " + url);

            using (var browser = new ChromiumWebBrowser())
            {
                browser.Size = new System.Drawing.Size(_size, _size);

                var initialLoad = browser.WaitForInitialLoadAsync();
                browser.BrowserInitialized += (s, e) =>
                {
                    ConsoleWorker.Write.Line("debug", "CEF browser initialized, navigating to render.html URL.");
                    browser.LoadUrl(url);
                };

                // BLOCK UNTIL INITIAL LOAD COMPLETES
                var initialResponse = initialLoad.GetAwaiter().GetResult();
                ConsoleWorker.Write.Line(
                    "debug",
                    "CEF initial load: success=" + initialResponse.Success
                    + " httpStatus=" + initialResponse.HttpStatusCode
                    + " errorCode=" + initialResponse.ErrorCode
                );

                // Wait for JS/three.js to be "ready" (best-effort)
                bool ready = WaitForRenderDone(browser, TimeSpan.FromSeconds(12));
                if (!ready)
                {
                    ConsoleWorker.Write.Line("warn", "Render timeout or page init flag not detected, capturing anyway.");
                }

                ConsoleWorker.Write.Line("debug",
                    "Before screenshot: IsBrowserInitialized=" + browser.IsBrowserInitialized
                    + " CanExecuteJavascriptInMainFrame=" + browser.CanExecuteJavascriptInMainFrame);

                // SYNCHRONOUS SCREENSHOT
                var screenshotTask = browser.CaptureScreenshotAsync();
                screenshotTask.Wait();
                var screenshot = screenshotTask.Result;

                if (screenshot == null || screenshot.Length == 0)
                {
                    ConsoleWorker.Write.Line("error", "CaptureScreenshotAsync returned no data (null or empty buffer).");
                    return false;
                }

                byte[] pngBytes = screenshot;
                string iconDir = Path.GetDirectoryName(iconPngAbs) ?? AppContext.BaseDirectory;
                Directory.CreateDirectory(iconDir);
                File.WriteAllBytes(iconPngAbs, pngBytes);

                bool exists = File.Exists(iconPngAbs);
                ConsoleWorker.Write.Line("info", "Cef icon render → " + iconPngAbs + " (exists=" + exists + ")");
                return exists;
            }
        }

        private static void EnsureCefInitialized()
        {
            if (_cefInit) return;

            var settings = new CefSettings
            {
                WindowlessRenderingEnabled = true,
                PersistSessionCookies = false,
                LogSeverity = LogSeverity.Disable
            };

            // Allow local file textures
            settings.CefCommandLineArgs.Add("allow-file-access-from-files", "1");
            settings.CefCommandLineArgs.Add("disable-gpu", "1");
            settings.CefCommandLineArgs.Add("disable-gpu-compositing", "1");

            Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
            _cefInit = true;
        }

        // COMPLETELY SYNCHRONOUS "READY" LOOP
        private static bool WaitForRenderDone(ChromiumWebBrowser browser, TimeSpan timeout)
        {
            var start = DateTime.UtcNow;

            while (DateTime.UtcNow - start < timeout)
            {
                if (!browser.IsBrowserInitialized || browser.GetBrowser() == null)
                {
                    Thread.Sleep(50);
                    continue;
                }

                if (!browser.CanExecuteJavascriptInMainFrame)
                {
                    Thread.Sleep(50);
                    continue;
                }

                // window.renderDone is optional; we also accept document.readyState === 'complete'
                string js = "(function() {" +
                            "  if (window.renderDone === true) return true;" +
                            "  if (document && document.readyState === 'complete') return true;" +
                            "  return false;" +
                            "})()";

                var evalTask = browser.EvaluateScriptAsync(js);
                evalTask.Wait();
                var resp = evalTask.Result;

                if (resp != null && resp.Success && resp.Result is bool b && b)
                {
                    ConsoleWorker.Write.Line("debug", "CEF page reported renderDone/readyState complete.");
                    return true;
                }

                Thread.Sleep(50);
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}