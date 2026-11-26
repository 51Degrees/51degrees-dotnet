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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FiftyOne.Example.Tests.Shared;

/// <summary>
/// A request the stand-in received, with the product it named, or null
/// for none, and the status the stand-in answered with.
/// </summary>
internal sealed record DataUpdateRequest(string Product, int Status);

/// <summary>
/// A stand-in for the Distributor on this machine, so that a test of a data
/// file update example never reaches the Distributor. It answers as the
/// Distributor does for license keys that hold several products, whatever
/// key it is sent:
/// <list type="bullet">
/// <item>a request that names no product gets 300 (Multiple choices) with
/// the list of products and a Location header naming the first</item>
/// <item>a request for a product not on the list gets 403 (Forbidden)</item>
/// <item>a request for a product on the list gets the data file to serve,
/// compressed and with its MD5 hash, unless there is no file to serve or
/// the request's If-Modified-Since is not earlier than the file's published
/// date, when it gets 304 (Not modified)</item>
/// </list>
/// Every request is recorded, so a test can check what the example asked
/// for.
/// </summary>
internal sealed class DataUpdateStandIn : IDisposable
{
    /// <summary>
    /// Where the published date starts in the header of a Hash or an IP
    /// Intelligence data file. The date is a 16 bit year, little endian,
    /// then a byte each for the month and the day.
    /// </summary>
    private const int PublishedOffset = 66;

    private const string NotOffered =
        "{\"Error\":\"The license key provided does not contain the " +
        "requested product.\"}";

    private const string SeveralProducts =
        "There are multiple products associated with this license key. " +
        "Please use the URI returned in the Location header.";

    private readonly HttpListener _listener = new HttpListener();

    private readonly List<DataUpdateRequest> _requests =
        new List<DataUpdateRequest>();

    private readonly string[] _products;

    private readonly string _compressed;

    private readonly string _md5;

    private readonly DateTime _published;

    private readonly Task _serving;

    private volatile bool _stopping;

    /// <param name="products">
    /// The products the license key holds, in the order the Distributor
    /// lists them.
    /// </param>
    /// <param name="dataFile">
    /// The data file to serve, or null to answer every request for a
    /// product on the list with 304 (Not modified).
    /// </param>
    public DataUpdateStandIn(IEnumerable<string> products, string dataFile)
    {
        _products = products.ToArray();
        if (dataFile != null)
        {
            _published = ReadPublished(dataFile);
            _compressed = dataFile + ".gz";
            using (var source = File.OpenRead(dataFile))
            using (var target = File.Create(_compressed))
            using (var gzip = new GZipStream(
                target, CompressionLevel.Fastest))
            {
                source.CopyTo(gzip);
            }
            // The Distributor sends the MD5 hash of the compressed file as
            // lower case hexadecimal, which the data update service checks.
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
            using (var stream = File.OpenRead(_compressed))
            {
                _md5 = Convert.ToHexString(MD5.HashData(stream))
                    .ToLowerInvariant();
            }
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
        }
        var port = FreePort();
        Url = $"http://localhost:{port}/api/v2/download";
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();
        _serving = Task.Run(Serve);
    }

    /// <summary>
    /// The data update URL to give an example in place of the
    /// Distributor's.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Every request received so far, in the order received.
    /// </summary>
    public IReadOnlyList<DataUpdateRequest> Requests
    {
        get
        {
            lock (_requests)
            {
                return _requests.ToArray();
            }
        }
    }

    /// <summary>
    /// The published date in a data file's header.
    /// </summary>
    public static DateTime ReadPublished(string dataFile)
    {
        using (var reader = new BinaryReader(File.OpenRead(dataFile)))
        {
            reader.BaseStream.Seek(PublishedOffset, SeekOrigin.Begin);
            var year = reader.ReadInt16();
            var month = reader.ReadByte();
            var day = reader.ReadByte();
            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Sets the published date in a data file's header, so that a copy of
    /// a data file can stand for a newer one.
    /// </summary>
    public static void WritePublished(string dataFile, DateTime date)
    {
        using (var writer = new BinaryWriter(File.OpenWrite(dataFile)))
        {
            writer.Seek(PublishedOffset, SeekOrigin.Begin);
            writer.Write((short)date.Year);
            writer.Write((byte)date.Month);
            writer.Write((byte)date.Day);
        }
    }

    public void Dispose()
    {
        // Set first, because stopping the listener can end the wait for a
        // request before the listener reports it has stopped.
        _stopping = true;
        _listener.Stop();
        _listener.Close();
        _serving.Wait();
        if (_compressed != null)
        {
            File.Delete(_compressed);
        }
    }

    private async Task Serve()
    {
        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (_stopping)
            {
                // Stopping the listener ends the wait for a request.
                return;
            }
            try
            {
                await Answer(context);
            }
            catch (Exception exception) when (
                exception is HttpListenerException ||
                exception is IOException ||
                exception is ObjectDisposedException)
            {
                // An example that ends before its answer is written, or
                // a listener that is stopping, ends only this answer.
            }
        }
    }

    private async Task Answer(HttpListenerContext context)
    {
        var product = ProductOf(context.Request.Url.Query);
        var response = context.Response;
        if (product == null)
        {
            var uris = _products
                .Select(p => WithProduct(context.Request.Url, p))
                .ToArray();
            Record(product, 300);
            response.StatusCode = 300;
            response.RedirectLocation = uris[0];
            await WriteJson(response, JsonSerializer.Serialize(new
            {
                AvailableUris = uris,
                Error = SeveralProducts,
            }));
        }
        else if (_products.Contains(
            product, StringComparer.OrdinalIgnoreCase) == false)
        {
            Record(product, 403);
            response.StatusCode = 403;
            await WriteJson(response, NotOffered);
        }
        else if (_compressed == null || IsNotModified(context.Request))
        {
            Record(product, 304);
            response.StatusCode = 304;
        }
        else
        {
            Record(product, 200);
            response.ContentType = "application/octet-stream";
            response.AddHeader("Content-MD5", _md5);
            using (var file = File.OpenRead(_compressed))
            {
                response.ContentLength64 = file.Length;
                await file.CopyToAsync(response.OutputStream);
            }
        }
        response.Close();
    }

    private void Record(string product, int status)
    {
        lock (_requests)
        {
            _requests.Add(new DataUpdateRequest(product, status));
        }
    }

    /// <summary>
    /// True when the request's If-Modified-Since is not earlier than the
    /// published date of the file served, so the file is not newer.
    /// </summary>
    private bool IsNotModified(HttpListenerRequest request)
    {
        var since = request.Headers["If-Modified-Since"];
        return since != null &&
            DateTimeOffset.TryParse(
                since,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var date) &&
            date.UtcDateTime >= _published;
    }

    /// <summary>
    /// The value of the Product parameter of a query, its name matched
    /// ignoring case, or null when there is none or it is empty.
    /// </summary>
    private static string ProductOf(string query)
    {
        foreach (var parameter in query.TrimStart('?').Split('&'))
        {
            var equals = parameter.IndexOf('=');
            if (equals > 0 && string.Equals(
                parameter.Substring(0, equals),
                "Product",
                StringComparison.OrdinalIgnoreCase))
            {
                var value = Uri.UnescapeDataString(
                    parameter.Substring(equals + 1));
                return value.Length > 0 ? value : null;
            }
        }
        return null;
    }

    /// <summary>
    /// The URL requested with Product set to the product, as each URI the
    /// Distributor lists is.
    /// </summary>
    private static string WithProduct(Uri url, string product)
    {
        var parameters = url.Query.TrimStart('?').Split('&')
            .Where(p => p.Length > 0 && p.StartsWith(
                "Product=", StringComparison.OrdinalIgnoreCase) == false)
            .Append("Product=" + Uri.EscapeDataString(product));
        return url.GetLeftPart(UriPartial.Path) + "?" +
            string.Join("&", parameters);
    }

    private static async Task WriteJson(
        HttpListenerResponse response,
        string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
