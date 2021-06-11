using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using Capture;
using Capture.Hook.Common;
using Capture.Interface;
using ScreenShot.src.tools.util;

namespace ScreenShot.src.tools.gpu
{
    public static class CaptureScreenDirectX
    {
        public static void Capture(IntPtr hwnd, Direct3DVersion direct3DVersion = Direct3DVersion.AUTO_DETECT)
        {
            NativeUtils.GetWindowThreadProcessId(hwnd, out var processID);
            var process = Process.GetProcessById(processID);
            
            var captureConfig = new CaptureConfig
            {
                Direct3DVersion = direct3DVersion,
                ShowOverlay = true,
                CaptureMouseEvents = true,
                CaptureKeyboardEvents = true
            };
            
            var captureInterface = new CaptureInterface();
            captureInterface.RemoteMessage += e =>
            {
                Debug.WriteLine(e.Message);
            };

            captureInterface.Connected += () =>
            {
                captureInterface.DrawOverlayInGame(new Overlay
                {
                    Elements = new List<IOverlayElement>
                    {
                        new RectangleElement
                        {
                            Colour = Color.FromArgb(Color.Gray.ToArgb() ^ (0x33 << 24)),
                            Location = new Point(0, 0),
                            Width = -1,
                            Height = -1
                        },
                        new RectangleMouseHookElement
                        {
                            Colour = Color.FromArgb(Color.Green.ToArgb() ^ (0x33 << 24)),
                            Width = 0,
                            Height = 0
                        },
                        new FramesPerSecond(new Font("Arial", 16, FontStyle.Bold))
                        {
                            Location = new Point(0, 0),
                            Color = Color.Red,
                            AntiAliased = true,
                            Text = "{0:N0} FPS"
                        }
                    },
                    Hidden = false
                });
            };
            
            var captureProcess = new CaptureProcess(process, captureConfig, captureInterface);
            while (!captureProcess.IsDisposed)
            {
                Thread.Sleep(500);
            }
            
            Debug.WriteLine("Disconnected from DirectX Hook");
        }
        
        /*
        public static void Capture(IntPtr hwnd)
        {
            var test = new DirectXOverlayWindow(hwnd, false);
            
            var font = test.Graphics.CreateFont("Arial", 20);
            var uBrush = test.Graphics.CreateBrush(Color.FromArgb(50, 80, 80, 80));
            var redBrush = test.Graphics.CreateBrush(0x7FFF0000);
            
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000 / 144.0),
            };

            timer.Tick += (_, _) =>
            {
                if (!test.IsVisible) return;
                test.Update();
                
                test.Graphics.BeginScene();
                test.Graphics.ClearScene();

                test.Graphics.FillRectangle(0, 0, test.Width, test.Height, uBrush);
                test.Graphics.DrawText("Test 123", font, redBrush, 0, 0);
                        
                test.Graphics.EndScene();
            };
            
            test.Show();
            timer.Start();
        }
        */
    }
}
