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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Cloud;

/// <summary>
/// A stand-in for the cloud service on this machine. It answers as the
/// service does for a resource key that does not include the hardware
/// lookup, or for one that does where every lookup matches no device,
/// answers anything else with 404, and records the path of every request
/// so a test can check what an example asked for.
/// </summary>
internal sealed class CloudStandIn : IDisposable
{
    /// <summary>
    /// The service's answer for a key without the hardware element, cut
    /// down to one device property.
    /// </summary>
    private const string AccessibleProperties =
        "{\"Products\":{\"device\":{\"DataTier\":\"CloudV5Basic\"," +
        "\"Properties\":[{\"Name\":\"IsMobile\",\"Type\":\"Boolean\"," +
        "\"Category\":\"Device\",\"DelayExecution\":false}]}}}";

    /// <summary>
    /// The service's answer for a key with the hardware element, cut down
    /// to the profile properties the examples print.
    /// </summary>
    private const string AccessiblePropertiesWithHardware =
        "{\"Products\":{\"hardware\":{\"DataTier\":\"CloudV5Bespoke\"," +
        "\"Properties\":[{\"Name\":\"Profiles\",\"Type\":\"Array\"," +
        "\"Category\":\"\",\"DelayExecution\":false," +
        "\"EvidenceProperties\":[],\"ItemProperties\":[" +
        "{\"Name\":\"HardwareVendor\",\"Type\":\"String\"," +
        "\"Category\":\"Name\",\"DelayExecution\":false}," +
        "{\"Name\":\"HardwareName\",\"Type\":\"Array\"," +
        "\"Category\":\"Name\",\"DelayExecution\":false}," +
        "{\"Name\":\"HardwareModel\",\"Type\":\"String\"," +
        "\"Category\":\"Name\",\"DelayExecution\":false}]}]}}}";

    /// <summary>
    /// The service's answer to a lookup that matches no device.
    /// </summary>
    private const string NoDevices = "{\"hardware\":{\"profiles\":[]}}";

    /// <summary>
    /// Part of the service's evidence key list, including the keys the
    /// hardware lookups use.
    /// </summary>
    private const string EvidenceKeys =
        "[\"query.tac\",\"query.nativemodel\",\"header.user-agent\"]";

    private readonly HttpListener _listener = new HttpListener();

    private readonly List<string> _paths = new List<string>();

    private readonly Task _serving;

    private readonly bool _withHardware;

    /// <param name="withHardware">
    /// True to answer as for a key with the hardware lookup, where every
    /// lookup matches no device.
    /// </param>
    public CloudStandIn(bool withHardware = false)
    {
        _withHardware = withHardware;
        var port = FreePort();
        EndPoint = $"http://localhost:{port}/api/v4/";
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();
        _serving = Task.Run(Serve);
    }

    /// <summary>
    /// The endpoint to give an example in place of the cloud service's.
    /// </summary>
    public string EndPoint { get; }

    /// <summary>
    /// The path of every request received so far.
    /// </summary>
    public IReadOnlyList<string> Paths
    {
        get
        {
            lock (_paths)
            {
                return _paths.ToArray();
            }
        }
    }

    public void Dispose()
    {
        _listener.Stop();
        _listener.Close();
        _serving.Wait();
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
            catch (Exception) when (_listener.IsListening == false)
            {
                // Stopping the listener ends the wait for a request.
                return;
            }
            var path = context.Request.Url.AbsolutePath;
            lock (_paths)
            {
                _paths.Add(path);
            }
            var body =
                path.EndsWith("/accessibleproperties",
                    StringComparison.Ordinal) ?
                    (_withHardware ?
                        AccessiblePropertiesWithHardware :
                        AccessibleProperties) :
                path.EndsWith("/evidencekeys",
                    StringComparison.Ordinal) ? EvidenceKeys :
                path.EndsWith("/json",
                    StringComparison.Ordinal) && _withHardware ? NoDevices :
                null;
            if (body == null)
            {
                context.Response.StatusCode = 404;
            }
            else
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
            }
            context.Response.Close();
        }
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
