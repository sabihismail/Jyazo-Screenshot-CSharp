using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
// ReSharper disable UnusedAutoPropertyAccessor.Local

// retrieved from https://stackoverflow.com/questions/604410/global-keyboard-capture-in-c-sharp-application
namespace ScreenShot.src.tools.hooks
{
    public class GlobalKeyboardHookEventArgs : HandledEventArgs
    {
        private GlobalKeyboardHook.KeyboardState KeyboardState { get; }

        public GlobalKeyboardHook.LowLevelKeyboardInputEvent KeyboardData { get; }

        public GlobalKeyboardHookEventArgs(GlobalKeyboardHook.LowLevelKeyboardInputEvent keyboardData, GlobalKeyboardHook.KeyboardState keyboardState)
        {
            KeyboardData = keyboardData;
            KeyboardState = keyboardState;
        }
    }

    //Based on https://gist.github.com/Stasonix
    public sealed class GlobalKeyboardHook : IDisposable
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool FreeLibrary(IntPtr hModule);

        /// <summary>
        /// The SetWindowsHookEx function installs an application-defined hook procedure into a hook chain.
        /// You would install a hook procedure to monitor the system for certain types of events. These events are
        /// associated either with a specific thread or with all threads in the same desktop as the calling thread.
        /// </summary>
        /// <param name="idHook">hook type</param>
        /// <param name="lpfn">hook procedure</param>
        /// <param name="hMod">handle to application instance</param>
        /// <param name="dwThreadId">thread identifier</param>
        /// <returns>If the function succeeds, the return value is the handle to the hook procedure.</returns>
        [DllImport("USER32", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

        /// <summary>
        /// The UnhookWindowsHookEx function removes a hook procedure installed in a hook chain by the SetWindowsHookEx function.
        /// </summary>
        /// <returns>If the function succeeds, the return value is true.</returns>
        [DllImport("USER32", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hHook);

        /// <summary>
        /// The CallNextHookEx function passes the hook information to the next hook procedure in the current hook chain.
        /// A hook procedure can call this function either before or after processing the hook information.
        /// </summary>
        /// <param name="hHook">handle to current hook</param>
        /// <param name="code">hook code passed to hook procedure</param>
        /// <param name="wParam">value passed to hook procedure</param>
        /// <param name="lParam">value passed to hook procedure</param>
        /// <returns>If the function succeeds, the return value is true.</returns>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hHook, int code, IntPtr wParam, IntPtr lParam);

        public event EventHandler<GlobalKeyboardHookEventArgs> KeyboardPressed;
        public event EventHandler<GlobalKeyboardHookEventArgs> KeyboardReleased;

        public GlobalKeyboardHook()
        {
            windowsHookHandle = IntPtr.Zero;
            user32LibraryHandle = IntPtr.Zero;
            hookProc = LowLevelKeyboardProc; // we must keep alive _hookProc, because GC is not aware about SetWindowsHookEx behaviour.

            user32LibraryHandle = LoadLibrary("User32");
            if (user32LibraryHandle == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"Failed to load library 'User32.dll'. Error {errorCode}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}.");
            }

            windowsHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, user32LibraryHandle, 0);
            if (windowsHookHandle != IntPtr.Zero) return;
            
            var errorCode2 = Marshal.GetLastWin32Error();
            throw new Win32Exception(errorCode2, $"Failed to adjust keyboard hooks for '{System.Diagnostics.Process.GetCurrentProcess().ProcessName}'. " +
                                                 $"Error {errorCode2}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}."); 
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // because we can unhook only in the same thread, not in garbage collector thread
                if (windowsHookHandle != IntPtr.Zero)
                {
                    if (!UnhookWindowsHookEx(windowsHookHandle))
                    {
                        var errorCode = Marshal.GetLastWin32Error();
                        throw new Win32Exception(errorCode, $"Failed to remove keyboard hooks for '{System.Diagnostics.Process.GetCurrentProcess().ProcessName}'. Error {errorCode}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}.");
                    }
                    windowsHookHandle = IntPtr.Zero;

                    // ReSharper disable once DelegateSubtraction
                    hookProc -= LowLevelKeyboardProc;
                }
            }

            if (user32LibraryHandle == IntPtr.Zero) return;
            
            if (!FreeLibrary(user32LibraryHandle)) // reduces reference to library by 1.
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"Failed to unload library 'User32.dll'. Error {errorCode}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}.");
            }
            user32LibraryHandle = IntPtr.Zero;
        }

        ~GlobalKeyboardHook()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private IntPtr windowsHookHandle;
        private IntPtr user32LibraryHandle;
        private HookProc hookProc;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct LowLevelKeyboardInputEvent
        {
            /// <summary>
            /// A virtual-key code. The code must be a value in the range 1 to 254.
            /// </summary>
            public readonly int VirtualCode;

            /// <summary>
            /// A hardware scan code for the key. 
            /// </summary>
            private readonly int HardwareScanCode;

            /// <summary>
            /// The extended-key flag, event-injected Flags, context code, and transition-state flag. This member is specified as follows. An application can use the following values to test the keystroke Flags. Testing LLKHF_INJECTED (bit 4) will tell you whether the event was injected. If it was, then testing LLKHF_LOWER_IL_INJECTED (bit 1) will tell you whether or not the event was injected from a process running at lower integrity level.
            /// </summary>
            private readonly int Flags;

            /// <summary>
            /// The time stamp stamp for this message, equivalent to what GetMessageTime would return for this message.
            /// </summary>
            private readonly int TimeStamp;

            /// <summary>
            /// Additional information associated with the message. 
            /// </summary>
            private readonly IntPtr AdditionalInformation;
        }

        private const int WH_KEYBOARD_LL = 13;
        //const int HC_ACTION = 0;

        public enum KeyboardState
        {
            KeyDown = 0x0100,
            KeyUp = 0x0101,
            SysKeyDown = 0x0104,
            SysKeyUp = 0x0105
        }

        //public const int VkSnapshot = 0x2c;
        //const int VkLwin = 0x5b;
        //const int VkRwin = 0x5c;
        //const int VkTab = 0x09;
        //const int VkEscape = 0x18;
        //const int VkControl = 0x11;
        //const int KfAltdown = 0x2000;
        //public const int LlkhfAltdown = (KfAltdown >> 8);

        private IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            var wParamTyped = wParam.ToInt32();
            if (!Enum.IsDefined(typeof(KeyboardState), wParamTyped)) return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            
            var o = Marshal.PtrToStructure(lParam, typeof(LowLevelKeyboardInputEvent));
            var p = (LowLevelKeyboardInputEvent)o;

            var keyboardState = (KeyboardState)wParamTyped;
            var eventArguments = new GlobalKeyboardHookEventArgs(p, keyboardState);

            switch (keyboardState)
            {
                case KeyboardState.KeyDown:
                    KeyboardPressed?.Invoke(this, eventArguments);
                    break;
                
                case KeyboardState.KeyUp:
                    KeyboardReleased?.Invoke(this, eventArguments);
                    break;
                
                case KeyboardState.SysKeyDown:
                    break;
                
                case KeyboardState.SysKeyUp:
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var fEatKeyStroke = eventArguments.Handled;

            return fEatKeyStroke ? (IntPtr)1 : CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }
    }
}
