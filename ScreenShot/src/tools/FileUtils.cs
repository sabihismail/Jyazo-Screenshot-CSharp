using System;
using WK.Libraries.BetterFolderBrowserNS;

namespace ScreenShot.src.tools
{
    public static class FileUtils
    {
        public static string BrowseForDirectory(string title = "Select folder...", string RootFolder = null)
        {
            var betterFolderBrowser = new BetterFolderBrowser
            {
                Title = title,
                RootFolder = RootFolder?.Replace("/", "\\"),
                Multiselect = false
            };

            var result = betterFolderBrowser.ShowDialog();

            if (result != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(betterFolderBrowser.SelectedPath))
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
