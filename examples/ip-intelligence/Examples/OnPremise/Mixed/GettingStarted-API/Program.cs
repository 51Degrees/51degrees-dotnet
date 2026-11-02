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

using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.IpIntelligence.Examples.Mixed.OnPremise;
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using FiftyOne.Pipeline.JsonBuilder.Data;
using FiftyOne.Pipeline.Web.Shared;
using Microsoft.AspNetCore.HttpOverrides;
using System.IO.Compression;
using System.Security.Cryptography;

/// <summary>
/// @example OnPremise/GettingStarted-API/Program.cs
/// 
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/master/Examples/OnPremise/GettingStarted-API/Program.cs). 
/// 
/// 
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.OnPremise.GettingStartedAPI
{
    /// <summary>
    /// Local version of IPI API.
    /// </summary>
    /// <seealso cref="https://cloud.51degrees.com/api-docs/index.html"/>
    public class Program
    {
        #region Customization Properties
        // ReSharper disable MemberCanBePrivate.Global
        // Properties are made public to enable reusing in other repos.
        
        /// <summary>
        /// Program args.
        /// Passed to <see cref="WebApplication.CreateBuilder()"/>.
        /// </summary>
        public string[] Args { get; init; } = Array.Empty<string>();
        
        /// <summary>
        /// Intended to injecting additional evidence
        /// that might be present in more real environment
        /// (e.g. API Version). 
        /// </summary>
        public Action<IDictionary<string, object>>? UnconditionalEvidenceInjector { get; init; }
        
        /// <summary>
        /// Allows injecting additional element builders into
        /// <see cref="WebApplicationBuilder.Services"/>
        /// before building the <see cref="Pipeline"/>.
        /// </summary>
        public Action<IServiceCollection>? ServiceInjector { get; init; }
        
        // ReSharper restore MemberCanBePrivate.Global
        #endregion
        
        public static void Main(string[] args)
        {
            var app = new Program
            {
                Args = args,
            }.BuildWebApp();
            app.Run();
        }

        /// <summary>
        /// Builds Cloud service emulator.
        /// </summary>
        /// <returns>
        /// Application that can be
        /// <see cref="WebApplication.Run(string?)"/> directly
        /// or started asynchronously via
        /// <see cref="WebApplication.StartAsync(CancellationToken)"/>
        /// and stopped later via
        /// <see cref="HostingAbstractionsHostExtensions.WaitForShutdownAsync(IHost, CancellationToken)"/>
        /// or <see cref="WebApplication.StopAsync(CancellationToken)"/>.
        /// </returns>
        // ReSharper disable MemberCanBePrivate.Global
        // Made public to enable reusing in other repos.
        public WebApplication BuildWebApp()
        {
            string? ddDataFileOverride = null;
            string? ipiDataFileOverride = null;
            if ((Args.Length > 0) && (Args[0].StartsWith("--") == false))
            {
                ddDataFileOverride = Args.Length > 0 ? Args[0] : null;
                ipiDataFileOverride = Args.Length > 1 ? Args[1] : null;
            }

            var builder = WebApplication.CreateBuilder(Args);
            builder.WebHost.UseUrls("http://0.0.0.0:5225");
            AppendConfigOverrides(
                builder.Configuration,
                out var rawOptions,
                ipiDataFileOverride,
                ddDataFileOverride);

            // Add services to the container.
            builder.Services.AddAuthorization();


            // Add the hash engine builder to services so that the system can find the builder
            // when it needs to.
            // builder.Services.AddTransient<IDataUpdateService, DataUpdateService>();
            builder.Services.AddHttpClient<IDataUpdateService, DataUpdateService>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(20);
            });
            builder.Services.AddSingleton<IpiOnPremiseEngineBuilder>();
            builder.Services.AddSingleton<DeviceDetectionHashEngineBuilder>();
            ServiceInjector?.Invoke(builder.Services);

            // Configure the services needed by IP Intelligence and create the 51Degrees Pipeline
            // instance that will be used to process requests.
            builder.Services.AddFiftyOne(builder.Configuration);

            var app = builder.Build();

            // Enable forwarded headers so that the real client IP is used when behind
            // a reverse proxy (e.g. ngrok, Azure App Service, nginx).
            // This updates HttpContext.Connection.RemoteIpAddress from X-Forwarded-For,
            // which the 51Degrees pipeline then uses as server.client-ip evidence.
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseHttpsRedirection();
            app.UseAuthorization();

            app.Map("/accessibleproperties", AccessibleProperties).WithName(nameof(AccessibleProperties));
            app.Map("/accessibleproperties/{resource}", AccessibleProperties).WithName(nameof(AccessibleProperties) + "WithResource");

            app.Map("/evidencekeys", EvidenceKeys).WithName(nameof(EvidenceKeys));
            app.Map("/evidencekeys/{resource}", EvidenceKeys).WithName(nameof(EvidenceKeys) + "WithResource");

            app.Map("/json", ProcessEvidence).WithName(nameof(ProcessEvidence));
            app.Map("/{resource}.json", ProcessEvidence).WithName(nameof(ProcessEvidence) + "WithResource");
            
            if (rawOptions.TryGetElementConfig(
                    nameof(IpiOnPremiseEngine),
                    out _))
            {
                app.MapGet("/download-ipi-gz", GetDataFile).WithName(nameof(GetDataFile));
            } 
            
            // Force pipeline initialization before accepting first request.
            app.Services.GetService<IPipeline>();

            return app;
        }

        private static async Task GetDataFile(
            HttpContext context,
            IPipeline pipeline,
            ILogger<Program> logger)
        {
            var token = context.RequestAborted;
            var sourcePath = pipeline.GetElement<IpiOnPremiseEngine>().DataFiles[0].DataFilePath;

            try
            {
                // Buffer the compressed content
                logger.LogInformation("({TS:O}) [{FUNC}] compressing the file...",
                    DateTime.Now, nameof(GetDataFile));

                await using var sourceStream = File.OpenRead(sourcePath);
                await using var memoryStream = new MemoryStream();
                await using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
                {
                    await sourceStream.CopyToAsync(gzipStream, token);
                }

                logger.LogInformation("({TS:O}) [{FUNC}] calculating MD5...",
                    DateTime.Now, nameof(GetDataFile));

                // Reset position to read from beginning
                memoryStream.Position = 0;

                // Compute MD5 of compressed content
                using var md5 = MD5.Create();
                var hashBytes = await md5.ComputeHashAsync(memoryStream, token);
                // var hashBase64 = Convert.ToBase64String(hashBytes);
                // var hashHex = Convert.ToHexStringLower(hashBytes);
                var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
                string md5Hash = hashHex;

                logger.LogInformation("({TS:O}) [{FUNC}] MD5 = {MD5}",
                    DateTime.Now, nameof(GetDataFile), md5Hash);

                // Reset again for streaming
                memoryStream.Position = 0;

                // Set headers
                context.Response.ContentType = "application/gzip";
                context.Response.Headers.ContentDisposition =
                    $"attachment; filename=\"{Path.GetFileName(sourcePath)}.gz\"";
                context.Response.Headers["Content-MD5"] = md5Hash;

                // Stream the buffered content
                await memoryStream.CopyToAsync(context.Response.Body, token);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("({TS:O}) [{FUNC}] delivery finished ({Outcome}).",
                    DateTime.Now, nameof(GetDataFile), "cancelled");
                throw;
            }

            logger.LogInformation("({TS:O}) [{FUNC}] delivery finished ({Outcome}).",
                DateTime.Now, nameof(GetDataFile), "done");
        }

        private static IResult AccessibleProperties(string? resource, HttpContext httpContext, IPipeline pipeline)
        {
            Dictionary<string, ProductMetaData> products = new(
                pipeline.FlowElements
                .SelectMany(x => x is IAspectEngine eng ? [eng] : (IEnumerable<IAspectEngine>)[])
                .Select(eng => new KeyValuePair<string, ProductMetaData>(eng.ElementDataKey, new ProductMetaData
                {
                    Properties = GetEngineProperties(eng).ToList(),
                })));

            return Results.Json(new
            {
                Products = products,
            });
        }

        private static IEnumerable<PropertyMetaData> GetEngineProperties(IAspectEngine aspectEngine)
        {
            if (aspectEngine is IpiOnPremiseEngine ipiEngine)
            {
                return ipiEngine.Components.SelectMany(comp
                    => comp.Properties.Select(prop => PropMetadataForIpiComponent(prop, comp)));
            }
            return aspectEngine.Properties.Select(prop => new PropertyMetaData(prop));
        }

        private static PropertyMetaData PropMetadataForIpiComponent(
            IFiftyOneAspectPropertyMetaData property,
            IComponentMetaData component)
        {
            var result = new PropertyMetaData(property)
            {
                Category = component.Name
            };
            if (GetTypeOverrideForPropertyType(property.Type) is { } typeOverride)
            {
                result.Type = typeOverride;
            }
            return result;
        }

        private static string? GetTypeOverrideForPropertyType(Type type)
        {
            if (!type.IsGenericType)
                return null;
            var enumerableType = type.GetGenericTypeDefinition() == typeof(IWeightedValue<>)
                ? type
                : type.GetInterfaces().FirstOrDefault(
                x => x.IsGenericType
                && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (enumerableType is null)
                return null;
            var topTypes = enumerableType.GetGenericArguments();
            if (topTypes is null)
                return null;
            if (topTypes.Length != 1)
                return null;
            if (!topTypes[0].IsGenericType)
                return null;
            var weightedType = topTypes[0].GetGenericTypeDefinition() == typeof(IWeightedValue<>)
                ? topTypes[0]
                : topTypes[0].GetInterfaces().FirstOrDefault(
                    x => x.IsGenericType
                    && x.GetGenericTypeDefinition() == typeof(IWeightedValue<>));
            if (weightedType is null)
                return null;
            var deepTypes = weightedType.GetGenericArguments();
            if (weightedType is null)
                return null;
            if (deepTypes.Length != 1)
                return null;
            if (deepTypes[0].IsGenericType)
                return null;
            return "Weighted" + deepTypes[0].Name;
        }

        private static IResult EvidenceKeys(string? resource, HttpContext httpContext, IPipeline pipeline)
        {
            var keys = pipeline.FlowElements
            .Select(x => x.EvidenceKeyFilter as EvidenceKeyFilterWhitelist)
            .Where(x => x is not null)
            .SelectMany(x => x?.Whitelist.Keys ?? [])
            .Distinct()
            .ToList();
            return Results.Json(keys);
        }

        private IResult ProcessEvidence(string? resource, HttpContext context, IPipeline pipeline) 
        {
            var aggregated = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            UnconditionalEvidenceInjector?.Invoke(aggregated);
            
            foreach (var kvp in context.Request.Query)
            {
                var effectiveValue = kvp.Value.LastOrDefault() ?? string.Empty;
                aggregated["query." + kvp.Key] = effectiveValue;
                if (string.Compare(
                        kvp.Key,
                        "resource",
                        StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    aggregated["fiftyone.resource-key"] = effectiveValue;
                }
            }
            foreach (var kvp in context.Request.Headers)
            {
                aggregated["header." + kvp.Key] = kvp.Value.LastOrDefault() ?? string.Empty;
            }
            foreach (var kvp in context.Request.Cookies)
            {
                aggregated["cookie." + kvp.Key] = kvp.Value;
            }
            if (context.Request.HasFormContentType)
            {
                foreach (var kvp in context.Request.Form)
                {
                    var effectiveValue = kvp.Value.LastOrDefault() ?? string.Empty;
                    aggregated["query." + kvp.Key] = effectiveValue;
                    if (string.Compare(
                            kvp.Key,
                            "resource",
                            StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        aggregated["fiftyone.resource-key"] = effectiveValue;
                    }
                }
            }
            if (resource is not null)
            {
                aggregated["fiftyone.resource-key"] = resource;
            }

            using var flowData = pipeline.CreateFlowData();
            flowData.AddEvidence(aggregated);
            flowData.Process();

            if (flowData.Get<IJsonBuilderElementData>()?.Json is { } json)
            {
                return Results.Text(json, "application/json");
            }
            return Results.NotFound("No results.");
        }

        /// <summary>
        /// Typically, something like this will not be necessary.
        /// The IP Intelligence API will accept an absolute or relative path for the data file.
        /// However, if a relative path is specified, it will only look in the current working 
        /// directory.
        /// In our examples, we have many different projects and we don't want to have a copy of 
        /// the data file for every single one.
        /// In order to handle this, we dynamically search the project directories for the data 
        /// file location and then override the configured setting with the absolute path if 
        /// necessary.
        /// In a real-world scenario, you can just put the data file in your working directory
        /// or use an absolute path in the configuration file.
        /// </summary>
        private static void AppendConfigOverrides(
            ConfigurationManager configurationManager,
            out PipelineOptions rawOptions,
            string? ddDataFileOverride = null,
            string? ipiDataFileOverride = null)
        {
            var overrides = new Dictionary<string, string?>();

            // Bind the configuration to a pipeline options instance
            rawOptions = new PipelineWebIntegrationOptions();
            var section = configurationManager.GetRequiredSection("PipelineOptions");
            // Use the 'ErrorOnUnknownConfiguration' option to warn us if we've got any
            // misnamed configuration keys.
            section.Bind(rawOptions, (o) => { o.ErrorOnUnknownConfiguration = true; });

            AddOverrides_IPI(rawOptions, overrides, ipiDataFileOverride);
            AddOverrides_DD(rawOptions, overrides, ddDataFileOverride);

            configurationManager.AddInMemoryCollection(overrides);
        }

        private static void AddOverrides_IPI(PipelineOptions options, Dictionary<string, string?> overrides,
            string? dataFileOverride = null)
        {
            // Get the index of the IP Intelligence engine element in the config file so that
            // we can create an override key for it.
            if (options.TryGetElementConfig(
                    nameof(IpiOnPremiseEngine),
                    out var ipiEngineOptions) == false)
            {
                return;
            } 
            var ipiEngineIndex = options.Elements.IndexOf(ipiEngineOptions);
            var dataFileConfigKey = $"PipelineOptions:Elements:{ipiEngineIndex}" +
                                    $":BuildParameters:DataFile";

            // Use the command line argument if provided. Otherwise, an explicit data file
            // path supplied in an environment variable takes precedence over the value in
            // the configuration file. The aligned '51DEGREES_IPI_PATH' variable is checked
            // first, followed by the legacy 'IPINTELLIGENCEDATAFILE' variable.
            var dataFile = dataFileOverride;
            if (string.IsNullOrWhiteSpace(dataFile))
            {
                dataFile = Environment.GetEnvironmentVariable(
                    Examples.Constants.IP_INTELLIGENCE_DATA_FILE_ENV_VAR);
            }
            if (string.IsNullOrWhiteSpace(dataFile))
            {
                dataFile = Environment.GetEnvironmentVariable(
                    Examples.Constants.LEGACY_IP_INTELLIGENCE_DATA_FILE_ENV_VAR);
            }
            if (string.IsNullOrWhiteSpace(dataFile))
            {
                dataFile = options.GetIpiDataFile();
            }
            var foundDataFile = false;
            if (string.IsNullOrEmpty(dataFile))
            {
                throw new Exception($"A data file must be specified as a command line " +
                    $"argument, in the appsettings.json file, or in the " +
                    $"'{Examples.Constants.IP_INTELLIGENCE_DATA_FILE_ENV_VAR}' environment " +
                    $"variable.");
            }
            // The data file location provided in the configuration may be using an absolute or
            // relative path. If it is relative then search for a matching file using the
            // ExampleUtils.FindFile function.
            if (Path.IsPathRooted(dataFile) == false)
            {
                var newPath = Examples.ExampleUtils.FindFile(dataFile);
                if (newPath != null)
                {
                    // Add an override for the absolute path to the data file.
                    overrides.Add(dataFileConfigKey, newPath);
                    foundDataFile = true;
                }
            }
            else
            {
                if (File.Exists(dataFile))
                {
                    overrides[dataFileConfigKey] = dataFile;
                    foundDataFile = true;
                }
            }

            if (foundDataFile == false)
            {
                throw new Exception($"Failed to find a IP Intelligence data file matching " +
                                    $"'{dataFile}'. If using the lite file, then make sure the " +
                                    $"ip-intelligence-data submodule has been updated by running " +
                                    "`git submodule update --recursive`. Otherwise, ensure that the filename " +
                                    "is correct in appsettings.json.");
            }
        }

        private static void AddOverrides_DD(PipelineOptions options, Dictionary<string, string?> overrides,
            string? dataFileOverride = null)
        {
            // Get the index of the device detection engine element in the config file so that
            // we can create an override key for it.
            
            if (options.TryGetElementConfig(
                    nameof(DeviceDetectionHashEngine),
                    out var hashEngineOptions) == false)
            {
                return;
            }
            var hashEngineIndex = options.Elements.IndexOf(hashEngineOptions);
            var dataFileConfigKey = $"PipelineOptions:Elements:{hashEngineIndex}" +
                                    $":BuildParameters:DataFile";

            // Use the command line argument if provided, otherwise use the appsettings.json value.
            var dataFile = dataFileOverride ?? options.GetHashDataFile();
            var foundDataFile = false;
            if (string.IsNullOrEmpty(dataFile))
            {
                throw new Exception($"A data file must be specified as a command line argument " +
                    $"or in the appsettings.json file.");
            }
            // The data file location provided in the configuration may be using an absolute or
            // relative path. If it is relative then search for a matching file using the
            // ExampleUtils.FindFile function.
            if (Path.IsPathRooted(dataFile) == false)
            {
                var newPath = Examples.ExampleUtils.FindFile(dataFile);
                if (newPath != null)
                {
                    // Add an override for the absolute path to the data file.
                    overrides.Add(dataFileConfigKey, newPath);
                    foundDataFile = true;
                }
            }
            else
            {
                if (File.Exists(dataFile))
                {
                    overrides[dataFileConfigKey] = dataFile;
                    foundDataFile = true;
                }
            }

            if (foundDataFile == false)
            {
                throw new Exception($"Failed to find a device detection data file matching " +
                                    $"'{dataFile}'. If using the lite file, then make sure the " +
                                    $"device-detection-data submodule has been updated by running " +
                                    "`git submodule update --recursive`. Otherwise, ensure that the filename " +
                                    "is correct in appsettings.json.");
            }
        }
    }
}
