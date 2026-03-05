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
                endpoint = endpoint.Substring(0, endpoint.Length - 1);
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

            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }
    }
}
