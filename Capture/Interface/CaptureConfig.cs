﻿using System;
using System.Reflection;
using System.IO;

namespace Capture.Interface
{
    [Serializable]
    public class CaptureConfig
    {
        public Direct3DVersion Direct3DVersion { get; set; }
        public bool ShowOverlay { get; set; }
        
        public bool CaptureMouseEvents { get; set; }
        
        public bool CaptureKeyboardEvents { get; set; }
        public int TargetFramesPerSecond { get; set; }
        public string TargetFolder { get; set; }

        public CaptureConfig()
        {
            Direct3DVersion = Direct3DVersion.AUTO_DETECT;
            ShowOverlay = false;
            TargetFramesPerSecond = 5;
            TargetFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
    }
}
