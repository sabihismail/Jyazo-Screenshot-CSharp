﻿using System;
using System.Windows.Forms;
using WK.Libraries.BetterFolderBrowserNS;

namespace ScreenShot.src.tools.util
{
    public static class FileUtils
    {
        public static string BrowseForDirectory(string title = "Select folder...", string rootFolder = null)
        {
            var betterFolderBrowser = new BetterFolderBrowser
            {
                Title = title,
                RootFolder = rootFolder?.Replace("/", "\\"),
                Multiselect = false
            };

            var result = betterFolderBrowser.ShowDialog();

            if (result != DialogResult.OK || string.IsNullOrWhiteSpace(betterFolderBrowser.SelectedPath))
            {
                return null;
            }

            var path = betterFolderBrowser.SelectedPath.Replace('\\', '/');
            if (path.EndsWith("/"))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }
        
        public static string BrowseForFile(string filter, string title = "Select file...", string rootFolder = null)
        {
            var openFileDialog = new OpenFileDialog()
            {
                Title = title,
                InitialDirectory = rootFolder?.Replace("/", "\\"),
                Multiselect = false,
                Filter = filter
            };

            var result = openFileDialog.ShowDialog();
            if (result != DialogResult.OK || string.IsNullOrWhiteSpace(openFileDialog.FileName))
            {
                return null;
            }

            return openFileDialog.FileName.Replace('\\', '/');
        }

        public static string GetContentType(string ext)
        {
            switch (ext)
            {
                case ".png":
                    return "image/png";

                case ".jpeg":
                case ".jpg":
                    return "image/jpeg";

                case ".gif":
                    return "image/gif";

                default:
                    throw new ArgumentOutOfRangeException("Extension unknown: " + ext);
            }
        }
    }
}
