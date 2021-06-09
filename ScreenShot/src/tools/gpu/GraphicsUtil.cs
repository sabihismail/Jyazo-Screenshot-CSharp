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
            { "d3d11_4.dll", GraphicsPipeline.DIRECT_X_11_4 },
            // { "d3d12.dll", GraphicsPipeline.DIRECT_X_12 },
        };

        private static readonly List<NativeUtils.QueryUserNotificationState> FULLSCREEN_APPLICATION_MODES = new()
        {
            NativeUtils.QueryUserNotificationState.QUNS_BUSY,
            NativeUtils.QueryUserNotificationState.QUNS_RUNNING_DIRECT_3D_FULL_SCREEN
        };

        public static GraphicsPipeline? IsFullscreenGameWindow(IntPtr hwnd)
        {
            NativeUtils.GetWindowThreadProcessId(hwnd, out var processID);
            NativeUtils.SHQueryUserNotificationState(out var result);

            //if (!FULLSCREEN_APPLICATION_MODES.Contains(result)) return null;
            
            var process = Process.GetProcessById(processID);
            var modules = NativeUtils.CollectModules(process)
                .Distinct()
                .Where(x => x.StartsWith("d"))
                .ToList();

            var isDirectX = GRAPHICS_MAPPING.Keys.FirstOrDefault(x => modules.Contains(x, StringComparer.InvariantCultureIgnoreCase));

            return !string.IsNullOrWhiteSpace(isDirectX) ? GRAPHICS_MAPPING[isDirectX] : null;
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
            DIRECT_X_11_4,
            DIRECT_X_12,
        }
    }
}