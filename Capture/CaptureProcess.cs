using System;
using EasyHook;
using System.Runtime.Remoting;
using Capture.Interface;
using System.Diagnostics;
using System.Threading;
using Capture.Hook;

namespace Capture
{
    public sealed class CaptureProcess : IDisposable
    {
        /// <summary>
        /// Must be null to allow a random channel name to be generated
        /// </summary>
        private readonly string channelName;

        private Process Process { get; }

        /// <summary>
        /// Prepares capturing in the target process. Note that the process must not already be hooked, and must have a MainWindowHandle/>.
        /// </summary>
        /// <param name="process">The process to inject into</param>
        /// <param name="config"></param>
        /// <param name="captureInterface"></param>
        /// <exception cref="ProcessHasNoWindowHandleException">Thrown if the <paramref name="process"/> does not have a window handle. This could mean that the process does not have a UI, or that the process has not yet finished starting.</exception>
        /// <exception cref="ProcessAlreadyHookedException">Thrown if the <paramref name="process"/> is already hooked</exception>
        /// <exception cref="InjectionFailedException">Thrown if the injection failed - see the InnerException for more details.</exception>
        /// <remarks>The target process will have its main window brought to the foreground after successful injection.</remarks>
        public CaptureProcess(Process process, CaptureConfig config, CaptureInterface captureInterface)
        {
            // If the process doesn't have a MainWindowHandle yet, skip it (we need to be able to get the hwnd to set foreground etc)
            if (process.MainWindowHandle == IntPtr.Zero)
            {
                throw new ProcessHasNoWindowHandleException();
            }

            // Skip if the process is already hooked (and we want to hook multiple applications)
            if (HookManager.IsHooked(process.Id))
            {
                throw new ProcessAlreadyHookedException();
            }
            
            Process = process;
            Process.EnableRaisingEvents = true;

            captureInterface.ProcessId = Process.Id;
            CaptureInterface = captureInterface;
            //_serverInterface = new CaptureInterface() { ProcessId = process.Id };

            // Initialise the IPC server (with our instance of _serverInterface)
            RemoteHooking.IpcCreateServer(
                ref channelName,
                WellKnownObjectMode.Singleton,
                CaptureInterface);

            try
            {
                // Inject DLL into target process
                RemoteHooking.Inject(
                    Process.Id,
                    InjectionOptions.Default,
                    typeof(CaptureInterface).Assembly.Location, // 32-bit (the same because AnyCPU) could use different assembly that links to 32-bit C++ helper dll
                    typeof(CaptureInterface).Assembly.Location, // 64-bit (the same because AnyCPU) could use different assembly that links to 64-bit C++ helper dll
                    // the optional parameter list...
                    channelName, // The name of the IPC channel for the injected assembly to connect to
                    config
                );
            }
            catch (Exception e)
            {
                throw new InjectionFailedException(e);
            }

            HookManager.AddHookedProcess(Process.Id);

            // Ensure the target process is in the foreground,
            // this prevents an issue where the target app appears to be in 
            // the foreground but does not receive any user inputs.
            // Note: the first Alt+Tab out of the target application after injection
            //       may still be an issue - switching between windowed and 
            //       fullscreen fixes the issue however (see ScreenshotInjection.cs for another option)
            BringProcessWindowToFront();

            Process.Exited += (_, _) =>
            {
                Dispose();
            };
        }

        private CaptureInterface CaptureInterface { get; }

        ~CaptureProcess()
        {
            Dispose(false);
        }

        #region Private methods

        /// <summary>
        /// Bring the target window to the front and wait for it to be visible
        /// </summary>
        /// <remarks>If the window does not come to the front within approx. 30 seconds an exception is raised</remarks>
        private void BringProcessWindowToFront()
        {
            if (Process == null)
                return;
            var handle = Process.MainWindowHandle;
            var i = 0;

            while (!NativeMethods.IsWindowInForeground(handle))
            {
                if (i == 0)
                {
                    // Initial sleep if target window is not in foreground - just to let things settle
                    Thread.Sleep(250);
                }

                if (NativeMethods.IsIconic(handle))
                {
                    // Minimized so send restore
                    NativeMethods.ShowWindow(handle, NativeMethods.WindowShowStyle.Restore);
                }
                else
                {
                    // Already Maximized or Restored so just bring to front
                    NativeMethods.SetForegroundWindow(handle);
                }
                Thread.Sleep(250);

                // Check if the target process main window is now in the foreground
                if (NativeMethods.IsWindowInForeground(handle))
                {
                    // Leave enough time for screen to redraw
                    Thread.Sleep(1000);
                    return;
                }

                // Prevent an infinite loop
                if (i > 120) // about 30secs
                {
                    throw new Exception("Could not set process window to the foreground");
                }
                i++;
            }
        }

        #endregion

        #region IDispose

        public bool IsDisposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (IsDisposed) return;
            
            if (disposing)
            {
                // Disconnect the IPC (which causes the remote entry point to exit)
                CaptureInterface.Disconnect();
            }

            IsDisposed = true;
        }

        #endregion
    }
}
