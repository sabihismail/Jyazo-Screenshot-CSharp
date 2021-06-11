using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local

namespace ScreenShot.src.tools.util
{
    public static class NativeUtils
    {
        public static bool IsInFocus(IntPtr hwnd)
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero) 
            {
                return false;
            }

            GetWindowThreadProcessId(hwnd, out var expectedProcessID);
            GetWindowThreadProcessId(activatedHandle, out var activeProcessID);

            return expectedProcessID == activeProcessID;
        }
        
        public static IEnumerable<string> CollectModules(Process process)
        {
            var collectedModules = new List<string>();
            var modulePointers = new IntPtr[0];

            // Determine number of modules
            if (!EnumProcessModulesEx(process.Handle, modulePointers, 0, out var bytesNeeded, (uint)ModuleFilter.LIST_MODULES_ALL)) return collectedModules;

            var totalNumberOfModules = bytesNeeded / IntPtr.Size;
            modulePointers = new IntPtr[totalNumberOfModules];

            // Collect modules from the process
            if (!EnumProcessModulesEx(process.Handle, modulePointers, bytesNeeded, out bytesNeeded, (uint) ModuleFilter.LIST_MODULES_ALL)) return collectedModules;
            
            for (var index = 0; index < totalNumberOfModules; index++)
            {
                var moduleFilePath = new StringBuilder(1024);
                GetModuleFileNameEx(process.Handle, modulePointers[index], moduleFilePath, (uint)moduleFilePath.Capacity);

                var moduleName = Path.GetFileName(moduleFilePath.ToString());
                collectedModules.Add(moduleName);
            }

            return collectedModules;
        }
        
        [DllImport("psapi.dll")]
        private static extern bool EnumProcessModulesEx(IntPtr hProcess, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U4)] [In][Out] IntPtr[] lphModule, int cb, 
            [MarshalAs(UnmanagedType.U4)] out int lpcbNeeded, uint dwFilterFlag);

        [DllImport("psapi.dll")]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In] [MarshalAs(UnmanagedType.U4)] uint nSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll", SetLastError=true)]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Win32Point pt);
        
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("shell32.dll")]
        public static extern int SHQueryUserNotificationState(out QueryUserNotificationState pquns);

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct Win32Point
        {
            public readonly int X;
            public readonly int Y;
        }
        
        public enum QueryUserNotificationState
        {
            QUNS_NOT_PRESENT = 1,
            QUNS_BUSY = 2,
            QUNS_RUNNING_DIRECT_3D_FULL_SCREEN = 3,
            QUNS_PRESENTATION_MODE = 4,
            QUNS_ACCEPTS_NOTIFICATIONS = 5,
            QUNS_QUIET_TIME = 6
        };

        private enum ModuleFilter
        {
            LIST_MODULES_DEFAULT = 0x0,
            LIST_MODULES_32_BIT = 0x01,
            LIST_MODULES_64_BIT = 0x02,
            LIST_MODULES_ALL = 0x03
        }
    }
}
