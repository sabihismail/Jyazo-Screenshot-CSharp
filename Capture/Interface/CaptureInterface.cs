using System;
using System.Drawing;
using System.Threading;
using Capture.Hook.Common;
// ReSharper disable EventNeverSubscribedTo.Global

namespace Capture.Interface
{
    [Serializable]
    public delegate void RecordingStartedEvent(CaptureConfig config);
    [Serializable]
    public delegate void RecordingStoppedEvent();
    [Serializable]
    public delegate void MessageReceivedEvent(MessageReceivedEventArgs message);
    [Serializable]
    public delegate void ScreenshotReceivedEvent(ScreenshotReceivedEventArgs response);
    [Serializable]
    public delegate void ConnectedEvent();
    [Serializable]
    public delegate void DisconnectedEvent();
    [Serializable]
    public delegate void ScreenshotRequestedEvent(ScreenshotRequest request);
    [Serializable]
    public delegate void DisplayTextEvent(DisplayTextEventArgs args);
    [Serializable]
    public delegate void DrawOverlayEvent(DrawOverlayEventArgs args);

    [Serializable]
    public class CaptureInterface : MarshalByRefObject
    {
        /// <summary>
        /// The client process Id
        /// </summary>
        public int ProcessId { get; set; }

        #region Events

        #region Server-side Events
        
        /// <summary>
        /// Server event for sending debug and error information from the client to server
        /// </summary>
        public event MessageReceivedEvent RemoteMessage;
        
        /// <summary>
        /// Server event for receiving screenshot image data
        /// </summary>
        public event ScreenshotReceivedEvent ScreenshotReceived;
        
        /// <summary>
        /// Server event used to notify the client on connect
        /// </summary>
        public event ConnectedEvent Connected;
        
        #endregion

        #region Client-side Events
        
        /// <summary>
        /// Client event used to communicate to the client that it is time to start recording
        /// </summary>
        public event RecordingStartedEvent RecordingStarted;

        /// <summary>
        /// Client event used to communicate to the client that it is time to stop recording
        /// </summary>
        public event RecordingStoppedEvent RecordingStopped;

        /// <summary>
        /// Client event used to communicate to the client that it is time to create a screenshot
        /// </summary>
        public event ScreenshotRequestedEvent ScreenshotRequested;

        /// <summary>
        /// Client event used to notify the hook to exit
        /// </summary>
        public event DisconnectedEvent Disconnected;

        /// <summary>
        /// Client event used to display a piece of text in-game
        /// </summary>
        public event DisplayTextEvent DisplayText;
        
        /// <summary>
        ///     Client event used to (re-)draw an overlay in-game.
        /// </summary>
        public event DrawOverlayEvent DrawOverlay;

        #endregion

        #endregion

        public bool IsRecording { get; set; }

        #region Public Methods

        #region Video Capture

        /// <summary>
        /// If not <see cref="IsRecording"/> will invoke the <see cref="RecordingStarted"/> event, starting a new recording. 
        /// </summary>
        /// <param name="config">The configuration for the recording</param>
        /// <remarks>Handlers in the server and remote process will be be invoked.</remarks>
        public void StartRecording(CaptureConfig config)
        {
            if (IsRecording)
                return;
            SafeInvokeRecordingStarted(config);
            IsRecording = true;
        }

        /// <summary>
        /// If <see cref="IsRecording"/>, will invoke the <see cref="RecordingStopped"/> event, finalising any existing recording.
        /// </summary>
        /// <remarks>Handlers in the server and remote process will be be invoked.</remarks>
        public void StopRecording()
        {
            if (!IsRecording)
                return;
            SafeInvokeRecordingStopped();
            IsRecording = false;
        }

        #endregion

        #region Still image Capture

        private object mutex = new();
        private Guid? requestId;
        private Action<Screenshot> completeScreenshot;
        private ManualResetEvent wait = new(false);

        /// <summary>
        /// Get a fullscreen screenshot with the default timeout of 2 seconds
        /// </summary>
        public Screenshot GetScreenshot()
        {
            return GetScreenshot(Rectangle.Empty, new TimeSpan(0, 0, 2), null, ImageFormat.Bitmap);
        }

        /// <summary>
        /// Get a screenshot of the specified region
        /// </summary>
        /// <param name="region">the region to capture (x=0,y=0 is top left corner)</param>
        /// <param name="timeout">maximum time to wait for the screenshot</param>
        /// <param name="resize"></param>
        /// <param name="format"></param>
        public Screenshot GetScreenshot(Rectangle region, TimeSpan timeout, Size? resize, ImageFormat format)
        {
            lock (mutex)
            {
                Screenshot result = null;
                requestId = Guid.NewGuid();
                wait.Reset();

                SafeInvokeScreenshotRequested(new ScreenshotRequest(requestId.Value, region)
                {
                    Format = format,
                    Resize = resize
                });

                completeScreenshot = sc =>
                {
                    try
                    {
                        Interlocked.Exchange(ref result, sc);
                    }
                    catch
                    {
                        // ignored
                    }

                    wait.Set();
                        
                };

                wait.WaitOne(timeout);
                completeScreenshot = null;
                return result;
            }
        }

        public IAsyncResult BeginGetScreenshot(Rectangle region, TimeSpan timeout, AsyncCallback callback = null, Size? resize = null, ImageFormat format = ImageFormat.Bitmap)
        {
            Func<Rectangle, TimeSpan, Size?, ImageFormat, Screenshot> getScreenshot = GetScreenshot;
            
            return getScreenshot.BeginInvoke(region, timeout, resize, format, callback, getScreenshot);
        }

        public Screenshot EndGetScreenshot(IAsyncResult result)
        {
            var getScreenshot = result.AsyncState as Func<Rectangle, TimeSpan, Size?, ImageFormat, Screenshot>;
            return getScreenshot?.EndInvoke(result);
        }

        public void SendScreenshotResponse(Screenshot screenshot)
        {
            if (requestId != null && screenshot != null && screenshot.RequestId == requestId.Value)
            {
                completeScreenshot?.Invoke(screenshot);
            }
        }

        #endregion

        /// <summary>
        /// Tell the client process to disconnect
        /// </summary>
        public void Disconnect()
        {
            SafeInvokeDisconnected();
        }

        /// <summary>
        /// Send a message to all handlers of <see cref="CaptureInterface.RemoteMessage"/>.
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void Message(MessageType messageType, string format, params object[] args)
        {
            Message(messageType, string.Format(format, args));
        }

        public void Message(MessageType messageType, string message)
        {
            SafeInvokeMessageReceived(new MessageReceivedEventArgs(messageType, message));
        }

        /// <summary>
        /// Display text in-game for the default duration of 5 seconds
        /// </summary>
        /// <param name="text"></param>
        public void DisplayInGameText(string text)
        {
            DisplayInGameText(text, new TimeSpan(0, 0, 5));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="duration"></param>
        public void DisplayInGameText(string text, TimeSpan duration)
        {
            if (duration.TotalMilliseconds <= 0)
                throw new ArgumentException(@"Duration must be larger than 0", nameof(duration));
            
            SafeInvokeDisplayText(new DisplayTextEventArgs(text, duration));
        }

        /// <summary>
        /// Replace the in-game overlay with the one provided.
        /// 
        /// Note: this is not designed for fast updates (i.e. only a couple of times per second)
        /// </summary>
        /// <param name="overlay"></param>
        public bool DrawOverlayInGame(IOverlay overlay)
        {
            return SafeInvokeDrawOverlay(new DrawOverlayEventArgs
            {
                Overlay = overlay
            });
        }

        #endregion

        #region Private: Invoke message handlers

        private void SafeInvokeRecordingStarted(CaptureConfig config)
        {
            if (RecordingStarted == null)
                return;         //No Listeners

            RecordingStartedEvent listener = null;
            var delegates = RecordingStarted.GetInvocationList();

            foreach (var del in delegates)
            {
                try
                {
                    listener = (RecordingStartedEvent)del;
                    listener.Invoke(config);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    RecordingStarted -= listener;
                }
            }
        }

        private void SafeInvokeRecordingStopped()
        {
            if (RecordingStopped == null)
                return;         //No Listeners

            RecordingStoppedEvent listener = null;
            var delegates = RecordingStopped.GetInvocationList();

            foreach (var del in delegates)
            {
                try
                {
                    listener = (RecordingStoppedEvent)del;
                    listener.Invoke();
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    RecordingStopped -= listener;
                }
            }
        }

        private void SafeInvokeMessageReceived(MessageReceivedEventArgs eventArgs)
        {
            if (RemoteMessage == null)
                return;         //No Listeners

            MessageReceivedEvent listener = null;
            var delegates = RemoteMessage.GetInvocationList();

            foreach (var del in delegates)
            {
                try
                {
                    listener = (MessageReceivedEvent)del;
                    listener.Invoke(eventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    RemoteMessage -= listener;
                }
            }
        }

        private void SafeInvokeScreenshotRequested(ScreenshotRequest eventArgs)
        {
            if (ScreenshotRequested == null)
                return;         //No Listeners

            ScreenshotRequestedEvent listener = null;
            var delegates = ScreenshotRequested.GetInvocationList();

            foreach (var del in delegates)
            {
                try
                {
                    listener = (ScreenshotRequestedEvent)del;
                    listener.Invoke(eventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    ScreenshotRequested -= listener;
                }
            }
        }

        private void SafeInvokeScreenshotReceived(ScreenshotReceivedEventArgs eventArgs)
        {
            if (ScreenshotReceived == null)
                return;         //No Listeners

            ScreenshotReceivedEvent listener = null;
            var delegates = ScreenshotReceived.GetInvocationList();

            foreach (var del in delegates)
            {
                try
                {
                    listener = (ScreenshotReceivedEvent)del;
                    listener.Invoke(eventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    ScreenshotReceived -= listener;
                }
            }
        }

        public void SafeInvokeConnected()
        {
            if (Connected == null)
                return;         //No Listeners

            ConnectedEvent listener = null;
            var delegates = Connected.GetInvocationList();

            foreach (var del in delegates)
            {
                try
                {
                    listener = (ConnectedEvent)del;
                    listener.Invoke();
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    Connected -= listener;
                }
            }
        }

        private void SafeInvokeDisconnected()
        {
            if (Disconnected == null)
                return;         //No Listeners

            DisconnectedEvent listener = null;
            var delegates = Disconnected.GetInvocationList();

            foreach (var del in delegates)
            {
                try
                {
                    listener = (DisconnectedEvent)del;
                    listener.Invoke();
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    Disconnected -= listener;
                }
            }
        }

        private void SafeInvokeDisplayText(DisplayTextEventArgs displayTextEventArgs)
        {
            if (DisplayText == null)
                return;         //No Listeners

            DisplayTextEvent listener = null;
            var delegates = DisplayText.GetInvocationList();

            foreach (var del in delegates)
            {
                try
                {
                    listener = (DisplayTextEvent)del;
                    listener.Invoke(displayTextEventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    DisplayText -= listener;
                }
            }
        }

        private bool SafeInvokeDrawOverlay(DrawOverlayEventArgs drawOverlayEventArgs)
        {
            if (DrawOverlay == null)
                return false; //No Listeners

            DrawOverlayEvent listener = null;
            var delegates = DrawOverlay.GetInvocationList();

            foreach (var del in delegates)
            {
                try
                {
                    listener = (DrawOverlayEvent)del;
                    listener.Invoke(drawOverlayEventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    DrawOverlay -= listener;
                }
            }

            return true;
        }

        #endregion

        /// <summary>
        /// Used to confirm connection to IPC server channel
        /// </summary>
        public DateTime Ping()
        {
            return DateTime.Now;
        }
    }

    /// <summary>
    /// Client event proxy for marshalling event handlers
    /// </summary>
    public class ClientCaptureInterfaceEventProxy : MarshalByRefObject
    {
        #region Event Declarations

        /// <summary>
        /// Client event used to communicate to the client that it is time to start recording
        /// </summary>
        public event RecordingStartedEvent RecordingStarted;

        /// <summary>
        /// Client event used to communicate to the client that it is time to stop recording
        /// </summary>
        public event RecordingStoppedEvent RecordingStopped;

        /// <summary>
        /// Client event used to communicate to the client that it is time to create a screenshot
        /// </summary>
        public event ScreenshotRequestedEvent ScreenshotRequested;

        /// <summary>
        /// Client event used to notify the hook to exit
        /// </summary>
        public event DisconnectedEvent Disconnected;

        /// <summary>
        /// Client event used to display in-game text
        /// </summary>
        public event DisplayTextEvent DisplayText;

        /// <summary>
        ///     Client event used to (re-)draw an overlay in-game.
        /// </summary>
        public event DrawOverlayEvent DrawOverlay;

        #endregion

        #region Lifetime Services

        public override object InitializeLifetimeService()
        {
            //Returning null holds the object alive
            //until it is explicitly destroyed
            return null;
        }

        #endregion

        public void RecordingStartedProxyHandler(CaptureConfig config)
        {
            RecordingStarted?.Invoke(config);
        }

        public void RecordingStoppedProxyHandler()
        {
            RecordingStopped?.Invoke();
        }

        public void DisconnectedProxyHandler()
        {
            Disconnected?.Invoke();
        }

        public void ScreenshotRequestedProxyHandler(ScreenshotRequest request)
        {
            ScreenshotRequested?.Invoke(request);
        }

        public void DisplayTextProxyHandler(DisplayTextEventArgs args)
        {
            DisplayText?.Invoke(args);
        }
        
        public void DrawOverlayProxyHandler(DrawOverlayEventArgs args)
        {
            DrawOverlay?.Invoke(args);
        }
    }
}
