using System;
using Capture.Hook;
using Capture.Interface;

namespace Capture
{
    // ReSharper disable once UnusedType.Global
    public class EntryPoint : EasyHook.IEntryPoint
    {
        private readonly System.Collections.Generic.List<IDXHook> directXHooks = new();
        private readonly ClientCaptureInterfaceEventProxy clientEventProxy = new();
        private readonly System.Runtime.Remoting.Channels.Ipc.IpcServerChannel clientServerChannel = null;
        private readonly CaptureInterface captureInterface;
        private IDXHook directXHook;
        private System.Threading.ManualResetEvent runWait;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedParameter.Local")]
        public EntryPoint(EasyHook.RemoteHooking.IContext context, string channelName, CaptureConfig config)
        {
            // Get reference to IPC to host application
            // Note: any methods called or events triggered against _interface will execute in the host process.
            captureInterface = EasyHook.RemoteHooking.IpcConnectClient<CaptureInterface>(channelName);

            // We try to ping immediately, if it fails then injection fails
            captureInterface.Ping();

            #region Allow client event handlers (bi-directional IPC)
            
            // Attempt to create a IpcServerChannel so that any event handlers on the client will function correctly
            System.Collections.IDictionary properties = new System.Collections.Hashtable();
            properties["name"] = channelName;
            properties["portName"] = channelName + Guid.NewGuid().ToString("N"); // random portName so no conflict with existing channels of channelName

            var binaryProv = new System.Runtime.Remoting.Channels.BinaryServerFormatterSinkProvider
            {
                TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full
            };

            var clientServerChannelIn = new System.Runtime.Remoting.Channels.Ipc.IpcServerChannel(properties, binaryProv);
            System.Runtime.Remoting.Channels.ChannelServices.RegisterChannel(clientServerChannelIn, false);
            
            #endregion
        }

        // ReSharper disable once UnusedMember.Global
        public void Run(EasyHook.RemoteHooking.IContext context, string channelName, CaptureConfig config)
        {
            // When not using GAC there can be issues with remoting assemblies resolving correctly
            // this is a workaround that ensures that the current assembly is correctly associated
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += (_, args) => GetType().Assembly.FullName == args.Name ? GetType().Assembly : null;

            // NOTE: This is running in the target process
            captureInterface.Message(MessageType.Information, "Injected into Process ID: {0}.",  EasyHook.RemoteHooking.GetCurrentProcessId());

            runWait = new System.Threading.ManualResetEvent(false);
            runWait.Reset();
            try
            {
                // Initialise the Hook
                if (!InitialiseDirectXHook(config))
                {
                    return;
                }
                captureInterface.Disconnected += clientEventProxy.DisconnectedProxyHandler;

                // Important Note:
                // accessing the _interface from within a _clientEventProxy event handler must always 
                // be done on a different thread otherwise it will cause a deadlock
                clientEventProxy.Disconnected += () =>
                {
                    // We can now signal the exit of the Run method
                    runWait.Set();
                };

                // We start a thread here to periodically check if the host is still running
                // If the host process stops then we will automatically uninstall the hooks
                StartCheckHostIsAliveThread();

                // Wait until signaled for exit either when a Disconnect message from the host 
                // or if the the check is alive has failed to Ping the host.
                runWait.WaitOne();

                // we need to tell the check host thread to exit (if it hasn't already)
                StopCheckHostIsAliveThread();

                // Dispose of the DXHook so any installed hooks are removed correctly
                DisposeDirectXHook();
            }
            catch (Exception e)
            {
                captureInterface.Message(MessageType.Error, "An unexpected error occured: {0}", e.ToString());
            }
            finally
            {
                try
                {
                    captureInterface.Message(MessageType.Information, "Disconnecting from process {0}", EasyHook.RemoteHooking.GetCurrentProcessId());
                }
                catch
                {
                    // ignored
                }

                // Remove the client server channel (that allows client event handlers)
                System.Runtime.Remoting.Channels.ChannelServices.UnregisterChannel(clientServerChannel);

                // Always sleep long enough for any remaining messages to complete sending
                System.Threading.Thread.Sleep(100);
            }
        }

        private void DisposeDirectXHook()
        {
            if (directXHooks == null) return;
            
            try
            {
                captureInterface.Message(MessageType.Debug, "Disposing of hooks...");
            }
            catch (System.Runtime.Remoting.RemotingException) { } // Ignore channel remoting errors

            // Dispose of the hooks so they are removed
            foreach (var dxHook in directXHooks)
                dxHook.Dispose();

            directXHooks.Clear();
        }

        private bool InitialiseDirectXHook(CaptureConfig config)
        {
            var version = config.Direct3DVersion;

            var loadedVersions = new System.Collections.Generic.List<Direct3DVersion>();

            var isX64Process = EasyHook.RemoteHooking.IsX64Process(EasyHook.RemoteHooking.GetCurrentProcessId());
            captureInterface.Message(MessageType.Information, "Remote process is a {0}-bit process.", isX64Process ? "64" : "32");

            try
            {
                if (version is Direct3DVersion.AUTO_DETECT or Direct3DVersion.UNKNOWN)
                {
                    // Attempt to determine the correct version based on loaded module.
                    // In most cases this will work fine, however it is perfectly ok for an application to use a D3D10 device along with D3D11 devices
                    // so the version might matched might not be the one you want to use
                    var d3D9Loaded = IntPtr.Zero;
                    var d3D10Loaded = IntPtr.Zero;
                    var d3D101Loaded = IntPtr.Zero;
                    var d3D11Loaded = IntPtr.Zero;
                    var d3D111Loaded = IntPtr.Zero;

                    const int delayTime = 100;
                    var retryCount = 0;
                    while (d3D9Loaded == IntPtr.Zero && d3D10Loaded == IntPtr.Zero && d3D101Loaded == IntPtr.Zero && d3D11Loaded == IntPtr.Zero && d3D111Loaded == IntPtr.Zero)
                    {
                        retryCount++;
                        d3D9Loaded = NativeMethods.GetModuleHandle("d3d9.dll");
                        d3D10Loaded = NativeMethods.GetModuleHandle("d3d10.dll");
                        d3D101Loaded = NativeMethods.GetModuleHandle("d3d10_1.dll");
                        d3D11Loaded = NativeMethods.GetModuleHandle("d3d11.dll");
                        d3D111Loaded = NativeMethods.GetModuleHandle("d3d11_1.dll");
                        System.Threading.Thread.Sleep(delayTime);

                        if (retryCount * delayTime <= 5000) continue;
                        
                        captureInterface.Message(MessageType.Error, "Unsupported Direct3D version, or Direct3D DLL not loaded within 5 seconds.");
                        return false;
                    }

                    if (d3D111Loaded != IntPtr.Zero)
                    {
                        captureInterface.Message(MessageType.Debug, "Autodetect found Direct3D 11.1");
                        version = Direct3DVersion.DIRECT_3D_11_1;
                        loadedVersions.Add(version);
                    }
                    if (d3D11Loaded != IntPtr.Zero)
                    {
                        captureInterface.Message(MessageType.Debug, "Autodetect found Direct3D 11");
                        version = Direct3DVersion.DIRECT_3D_11;
                        loadedVersions.Add(version);
                    }
                    if (d3D101Loaded != IntPtr.Zero)
                    {
                        captureInterface.Message(MessageType.Debug, "Autodetect found Direct3D 10.1");
                        version = Direct3DVersion.DIRECT_3D_10_1;
                        loadedVersions.Add(version);
                    }
                    if (d3D10Loaded != IntPtr.Zero)
                    {
                        captureInterface.Message(MessageType.Debug, "Autodetect found Direct3D 10");
                        version = Direct3DVersion.DIRECT_3D_10;
                        loadedVersions.Add(version);
                    }
                    if (d3D9Loaded != IntPtr.Zero)
                    {
                        captureInterface.Message(MessageType.Debug, "Autodetect found Direct3D 9");
                        version = Direct3DVersion.DIRECT_3D_9;
                        loadedVersions.Add(version);
                    }
                }
                else
                {
                    // If not autodetect, assume specified version is loaded
                    loadedVersions.Add(version);
                }

                foreach (var dxVersion in loadedVersions)
                {
                    version = dxVersion;
                    switch (version)
                    {
                        case Direct3DVersion.DIRECT_3D_9:
                            directXHook = new DXHookD3D9(captureInterface);
                            break;
                        case Direct3DVersion.DIRECT_3D_10:
                            directXHook = new DXHookD3D10(captureInterface);
                            break;
                        case Direct3DVersion.DIRECT_3D_10_1:
                            directXHook = new DXHookD3D101(captureInterface);
                            break;
                        case Direct3DVersion.DIRECT_3D_11:
                            directXHook = new DXHookD3D11(captureInterface);
                            break;
                        //case Direct3DVersion.Direct3D11_1:
                        //    _directXHook = new DXHookD3D11_1(_interface);
                        //    return;
                        case Direct3DVersion.UNKNOWN:
                            captureInterface.Message(MessageType.Error, "Unsupported Direct3D version: {0}", version);
                            break;
                        case Direct3DVersion.AUTO_DETECT:
                            captureInterface.Message(MessageType.Error, "Unsupported Direct3D version: {0}", version);
                            break;
                        case Direct3DVersion.DIRECT_3D_11_1:
                            captureInterface.Message(MessageType.Error, "Unsupported Direct3D version: {0}", version);
                            break;
                        default:
                            captureInterface.Message(MessageType.Error, "Unsupported Direct3D version: {0}", version);
                            return false;
                    }

                    directXHook.Config = config;
                    directXHook.Hook();

                    directXHooks.Add(directXHook);
                }

                return true;
            }
            catch (Exception e)
            {
                // Notify the host/server application about this error
                captureInterface.Message(MessageType.Error, "Error in InitialiseHook: {0}", e.ToString());
                return false;
            }
        }

        #region Check Host Is Alive

        private System.Threading.Tasks.Task checkAlive;
        private long stopCheckAlive;
        
        /// <summary>
        /// Begin a background thread to check periodically that the host process is still accessible on its IPC channel
        /// </summary>
        private void StartCheckHostIsAliveThread()
        {
            checkAlive = new System.Threading.Tasks.Task(() =>
            {
                try
                {
                    while (System.Threading.Interlocked.Read(ref stopCheckAlive) == 0)
                    {
                        System.Threading.Thread.Sleep(1000);

                        // .NET Remoting exceptions will throw RemotingException
                        captureInterface.Ping();
                    }
                }
                catch // We will assume that any exception means that the hooks need to be removed. 
                {
                    // Signal the Run method so that it can exit
                    runWait.Set();
                }
            });

            checkAlive.Start();
        }

        /// <summary>
        /// Tell the _checkAlive thread that it can exit if it hasn't already
        /// </summary>
        private void StopCheckHostIsAliveThread()
        {
            System.Threading.Interlocked.Increment(ref stopCheckAlive);
        }

        #endregion
    }
}
