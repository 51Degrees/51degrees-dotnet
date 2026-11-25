/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Cloud
{
    /// <summary>
    /// Sends requests as the default handler does, and writes to the build
    /// log how long each request to the cloud service took and, for each
    /// new connection, how long the host name lookup and the connection
    /// took. A request that fails is logged with the step it stopped in.
    /// Only the path is written, never the query, and a last part ending in
    /// ".json" is replaced, as it is the resource key.
    /// </summary>
    internal sealed class TimedHttpHandler : DelegatingHandler
    {
        public TimedHttpHandler()
            : base(new SocketsHttpHandler { ConnectCallback = ConnectAsync })
        {
        }

        /// <summary>
        /// A client that sends its requests through a new handler.
        /// </summary>
        public static HttpClient CreateClient() =>
            new HttpClient(new TimedHttpHandler());

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = Path(request.RequestUri);
            var timer = Stopwatch.StartNew();
            try
            {
                var response = await base.SendAsync(
                    request, cancellationToken).ConfigureAwait(false);
                Log($"Cloud request {path} answered " +
                    $"{(int)response.StatusCode} in " +
                    $"{timer.ElapsedMilliseconds} ms");
                return response;
            }
            catch (Exception e)
            {
                Log($"Cloud request {path} failed in " +
                    $"{timer.ElapsedMilliseconds} ms, " +
                    $"stopped in {Step(e)}: {Causes(e)}");
                throw;
            }
        }

        /// <summary>
        /// Opens the connection as the default handler does, timing the host
        /// name lookup and the connection apart.
        /// </summary>
        private static async ValueTask<System.IO.Stream> ConnectAsync(
            SocketsHttpConnectionContext context,
            CancellationToken cancellationToken)
        {
            var host = context.DnsEndPoint.Host;
            var timer = Stopwatch.StartNew();
            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(
                    host, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log($"Host name lookup of {host} failed in " +
                    $"{timer.ElapsedMilliseconds} ms: {Causes(e)}");
                throw;
            }
            var lookup = timer.ElapsedMilliseconds;
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(
                    addresses, context.DnsEndPoint.Port, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                socket.Dispose();
                Log($"Connection to {host} failed " +
                    $"{timer.ElapsedMilliseconds - lookup} ms after a host " +
                    $"name lookup of {lookup} ms: {Causes(e)}");
                throw;
            }
            Log($"Connected to {host}: host name lookup {lookup} ms, " +
                $"connection {timer.ElapsedMilliseconds - lookup} ms");
            return new NetworkStream(socket, ownsSocket: true);
        }

        /// <summary>
        /// The step a failed request stopped in, from the failure's causes.
        /// </summary>
        private static string Step(Exception failure)
        {
            for (var e = failure; e != null; e = e.InnerException)
            {
                if (e is SocketException socket)
                {
                    return socket.SocketErrorCode == SocketError.HostNotFound ||
                        socket.SocketErrorCode == SocketError.TryAgain ?
                        "the host name lookup" : "the connection";
                }
                if (e is AuthenticationException)
                {
                    return "the TLS handshake";
                }
            }
            return failure is TaskCanceledException ?
                "the wait for the answer, or a step the log above shows " +
                "did not finish" :
                "a step not named by the failure";
        }

        private static string Causes(Exception failure)
        {
            var text = failure.GetType().Name + ": " + failure.Message;
            for (var e = failure.InnerException; e != null;
                e = e.InnerException)
            {
                text += " caused by " + e.GetType().Name + ": " + e.Message;
            }
            return text;
        }

        private static string Path(Uri uri)
        {
            var path = uri?.AbsolutePath ?? "";
            var last = path.LastIndexOf('/');
            return path.EndsWith(".json", StringComparison.Ordinal) ?
                path.Substring(0, last + 1) + "<resource key>.json" :
                path;
        }

        private static void Log(string message) =>
            Console.WriteLine("Build log: " + message);
    }
}
