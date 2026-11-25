using System;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace StormSwitchBox.Services
{
    public static class StormHttpClientFactory
    {
        private static readonly SocketsHttpHandler SharedHandler = new()
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(4),
            MaxConnectionsPerServer = 64,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            AllowAutoRedirect = true
        };

        private static readonly ConditionalWeakTable<CookieContainer, SocketsHttpHandler> CookieHandlers = new();

        public static HttpClient Shared { get; } = CreateDefaultSharedClient();

        private static HttpClient CreateDefaultSharedClient()
        {
            var client = new HttpClient(SharedHandler, disposeHandler: false)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            return client;
        }

        public static HttpClient CreateClient(TimeSpan? timeout = null)
        {
            var client = new HttpClient(SharedHandler, disposeHandler: false)
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            return client;
        }

        public static HttpClient CreateCookieClient(CookieContainer cookies, TimeSpan? timeout = null, bool allowAutoRedirect = true)
        {
            if (!allowAutoRedirect)
            {
                var nonRedirectHandler = new SocketsHttpHandler
                {
                    CookieContainer = cookies,
                    UseCookies = true,
                    AutomaticDecompression = DecompressionMethods.All,
                    AllowAutoRedirect = false,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(3),
                    ConnectTimeout = TimeSpan.FromSeconds(10)
                };
                var c = new HttpClient(nonRedirectHandler, disposeHandler: true)
                {
                    Timeout = timeout ?? TimeSpan.FromSeconds(25)
                };
                c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
                return c;
            }

            var handler = CookieHandlers.GetValue(cookies, cc => new SocketsHttpHandler
            {
                CookieContainer = cc,
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = true,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 24,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });

            var client = new HttpClient(handler, disposeHandler: false)
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(25)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            return client;
        }

        public static bool IsValidImageBytes(ReadOnlySpan<byte> data)
        {
            if (data.Length < 12) return false;
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return true;
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return true;
            if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
                data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
                return true;
            if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
                return true;
            if (data[0] == 0x42 && data[1] == 0x4D)
                return true;
            return false;
        }
    }
}
