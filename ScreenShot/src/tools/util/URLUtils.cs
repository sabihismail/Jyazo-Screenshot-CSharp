using System;
using System.Net;
using System.Net.Sockets;

namespace ScreenShot.src.tools.util
{
    public static class URLUtils
    {
        public static string JoinURL(string path, string endpoint)
        {
            if (path.EndsWith("/") && endpoint.StartsWith("/"))
            {
                endpoint = endpoint.TrimStart('/');
            }
            else if (!path.EndsWith("/") && !endpoint.StartsWith("/"))
            {
                endpoint = "/" + endpoint;
            }

            return path + endpoint;
        }

        public static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
