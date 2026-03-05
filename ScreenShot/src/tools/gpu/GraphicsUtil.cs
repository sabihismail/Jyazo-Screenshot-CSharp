using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ScreenShot.src.tools.util;

namespace ScreenShot.src.tools.gpu
{
    public static class GraphicsUtil
    {
        private static readonly Dictionary<string, GraphicsPipeline> GRAPHICS_MAPPING = new(StringComparer.InvariantCultureIgnoreCase)
        {
            { "d3d9.dll", GraphicsPipeline.DIRECT_X_9 },
            { "d3d10.dll", GraphicsPipeline.DIRECT_X_10 },
            { "d3d10_1.dll", GraphicsPipeline.DIRECT_X_10_1 },
            { "d3d11.dll", GraphicsPipeline.DIRECT_X_11 },
            { "d3d11_1.dll", GraphicsPipeline.DIRECT_X_11_1 },
            { "d3d11_2.dll", GraphicsPipeline.DIRECT_X_11_2 },
            { "d3d11_3.dll", GraphicsPipeline.DIRECT_X_11_3 },
            { "d3d11_4.dll", GraphicsPipeline.DIRECT_X_11_4 }
            // { "d3d12.dll", GraphicsPipeline.DIRECT_X_12 },
        };

        public static GraphicsPipeline? IsFullscreenGameWindow(IntPtr hwnd)
        {
            // First check if it's actually exclusive fullscreen (covers entire screen)
            if (!IsExclusiveFullscreen(hwnd))
                return null;

            // Then check if it uses DirectX
            NativeUtils.GetWindowThreadProcessId(hwnd, out var processID);
            var process = Process.GetProcessById(processID);
            var modules = NativeUtils.CollectModules(process)
                .Distinct()
                .Where(x => x.StartsWith("d"))
                .ToList();

            var isDirectX = GRAPHICS_MAPPING.Keys.FirstOrDefault(x => modules.Contains(x, StringComparer.InvariantCultureIgnoreCase));

            return !string.IsNullOrWhiteSpace(isDirectX) ? GRAPHICS_MAPPING[isDirectX] : null;
        }

        /// <summary>
        /// Checks if a window is in exclusive fullscreen mode by verifying it covers the entire monitor.
        /// </summary>
        private static bool IsExclusiveFullscreen(IntPtr hwnd)
        {
            try
            {
                NativeUtils.GetWindowRect(hwnd, out var windowRect);

                // Get the screen dimensions
                var screenWidth = NativeUtils.GetSystemMetrics(0); // SM_CXSCREEN
                var screenHeight = NativeUtils.GetSystemMetrics(1); // SM_CYSCREEN

                // Check if window covers the entire screen
                var coversFullScreen =
                    windowRect.Left == 0 &&
                    windowRect.Top == 0 &&
                    (windowRect.Right - windowRect.Left) == screenWidth &&
                    (windowRect.Bottom - windowRect.Top) == screenHeight;

                Debug.WriteLine($"[GraphicsUtil] Window bounds: {windowRect.Left},{windowRect.Top} -> {windowRect.Right},{windowRect.Bottom}");
                Debug.WriteLine($"[GraphicsUtil] Screen size: {screenWidth}x{screenHeight}");
                Debug.WriteLine($"[GraphicsUtil] Covers full screen: {coversFullScreen}");

                return coversFullScreen;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GraphicsUtil] Error checking fullscreen: {ex}");
                return false;
            }
        }

        public enum GraphicsPipeline
        {
            DIRECT_X_9,
            DIRECT_X_10,
            DIRECT_X_10_1,
            DIRECT_X_11,
            DIRECT_X_11_1,
            DIRECT_X_11_2,
            DIRECT_X_11_3,
            DIRECT_X_11_4
        }
    }
}