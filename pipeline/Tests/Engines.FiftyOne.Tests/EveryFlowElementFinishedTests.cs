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

using FiftyOne.Did.Cloud.FlowElements;
using FiftyOne.Did.Core.FlowElements;
using FiftyOne.Pipeline.AgentSignature.FlowElement;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using CloudRequestEngineElement =
    FiftyOne.Pipeline.CloudRequestEngine.FlowElements.CloudRequestEngine;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.DerivedProperty.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.TestHelpers;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.Translation.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests;

/// <summary>
/// thread-safety.md, Element Data: "Each implementation MUST include a
/// check, run by its automated tests, that processes every Flow Element in
/// the repository, then writes to its Element Data and expects the error."
/// This covers the Flow Elements of the Pipeline packages. The Device
/// Detection, IP Intelligence and location packages have their own.
/// </summary>
[TestClass]
public class EveryFlowElementFinishedTests
{
    /// <summary>
    /// Flow Elements of the Pipeline packages that have no Element Data of
    /// their own to write, so there is nothing to check.
    /// </summary>
    private static readonly Dictionary<Type, string> NoElementData =
        new Dictionary<Type, string>
        {
            { typeof(ShareUsageElement), "it adds no Element Data" },
        };

    private static readonly Assembly[] Assemblies =
    {
        typeof(FlowElementBase<,>).Assembly,
        typeof(AspectEngineBase<,>).Assembly,
        typeof(SequenceElement).Assembly,
        typeof(EmptyEngine).Assembly,
        typeof(JsonBuilderElement).Assembly,
        typeof(JavaScriptBuilderElement).Assembly,
        typeof(CloudRequestEngineElement).Assembly,
        typeof(DidCloudEngine).Assembly,
        typeof(AgentSignatureElement).Assembly,
        typeof(DerivedPropertyElement).Assembly,
        typeof(TranslationEngine).Assembly,
    };

    /// <summary>
    /// Answers the cloud service's three requests with fixed responses.
    /// </summary>
    private class FakeCloud : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri.AbsolutePath.ToLowerInvariant();
            string body;
            if (path.EndsWith("evidencekeys"))
            {
                body = "['query.User-Agent']";
            }
            else if (path.EndsWith("accessibleproperties"))
            {
                body =
                    "{'Products': {'" +
                    DidBaseEnginePropertiesBuilder.ComponentName +
                    "': {'DataTier': 'tier', 'Properties': []}}}";
            }
            else
            {
                body = "{'" + DidBaseEnginePropertiesBuilder.ComponentName +
                    "': {}}";
            }
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(body),
            });
        }
    }

    private const string ScriptText =
        "Format: 1\n" +
        "Name: Finished\n" +
        "Version: 1.0.0\n" +
        "Output:\n" +
        "  Name: Finished\n" +
        "  Description: A value for the test.\n" +
        "  ValueType: string\n" +
        "  DefaultValue: Low\n" +
        "  IsList: false\n" +
        "  Category: General\n" +
        "  Values:\n" +
        "    - Name: High\n" +
        "      Description: High.\n" +
        "    - Name: Low\n" +
        "      Description: Low.\n" +
        "Checks:\n" +
        "  Positive: { Property: empty-aspect.valueone, Gt: 0 }\n" +
        "Rules:\n" +
        "  - When: { Failed: Checks, Eq: 0 }\n" +
        "    Then: High\n" +
        "  - Else: Low\n";

    /// <summary>
    /// Each Flow Element checked, with the elements a Pipeline needs before
    /// it, the last being the one checked.
    /// </summary>
    private static IEnumerable<(Type Type, Func<IFlowElement[]> Build)>
        Cases()
    {
        var logger = NullLoggerFactory.Instance;
        IFlowElement Cloud() => new CloudRequestEngineBuilder(
                logger,
                new HttpClient(new FakeCloud()))
            .SetResourceKey("key")
            .Build();
        yield return (typeof(SequenceElement), () => new IFlowElement[]
        {
            new SequenceElementBuilder(logger).Build(),
        });
        yield return (typeof(SetHeadersElement), () => new IFlowElement[]
        {
            new SetHeadersElementBuilder(logger).Build(),
        });
        yield return (typeof(JsonBuilderElement), () => new IFlowElement[]
        {
            new SequenceElementBuilder(logger).Build(),
            new JsonBuilderElementBuilder(logger).Build(),
        });
        yield return (typeof(JavaScriptBuilderElement), () =>
            new IFlowElement[]
            {
                new SequenceElementBuilder(logger).Build(),
                new JsonBuilderElementBuilder(logger).Build(),
                new JavaScriptBuilderElementBuilder(logger).Build(),
            });
        yield return (typeof(EmptyEngine), () => new IFlowElement[]
        {
            new EmptyEngineBuilder(logger).Build(),
        });
        yield return (typeof(CloudRequestEngineElement), () => new IFlowElement[]
        {
            Cloud(),
        });
        yield return (typeof(DidCloudEngine), () => new IFlowElement[]
        {
            Cloud(),
            new DidCloudEngineBuilder(logger).Build(),
        });
        yield return (typeof(AgentSignatureElement), () => new IFlowElement[]
        {
            new AgentSignatureElementBuilder(logger).Build(),
        });
        yield return (typeof(DerivedPropertyElement), () =>
            new IFlowElement[]
            {
                new EmptyEngineBuilder(logger).Build(),
                new DerivedPropertyElementBuilder(logger)
                    .AddScript("Finished", ScriptText)
                    .Build(),
            });
        yield return (typeof(TranslationEngine), () =>
        {
            var source = Path.Combine(
                Path.GetTempPath(),
                $"finished-{Guid.NewGuid():N}.fr_FR.yml");
            File.WriteAllText(source, "dog: chien\n");
            try
            {
                return new IFlowElement[]
                {
                    new SequenceElementBuilder(logger).Build(),
                    new TranslationEngineBuilder(logger)
                        .SetSourceElementDataKey(
                            SequenceElement.DEFAULT_ELEMENT_DATA_KEY)
                        .AddSource(source)
                        .AddTranslation("sessionid", "translated")
                        .Build(),
                };
            }
            finally
            {
                File.Delete(source);
            }
        });
    }

    /// <summary>
    /// Every Flow Element is processed, then a write to its Element Data
    /// fails.
    /// </summary>
    [TestMethod]
    public void EveryFlowElement_WriteAfterProcessingFails()
    {
        var failures = new List<string>();
        foreach (var (type, build) in Cases())
        {
            var elements = build();
            var element = elements.Last();
            Assert.AreEqual(type, element.GetType());
            using var pipeline = elements
                .Aggregate(
                    new PipelineBuilder(NullLoggerFactory.Instance)
                        .SetSuppressProcessExceptions(true),
                    (b, e) => b.AddFlowElement(e))
                .Build();
            using var data = pipeline.CreateFlowData();
            data.AddEvidence("query.User-Agent", "test");
            data.AddEvidence("header.host", "localhost");
            data.Process();
            if (data.TryGet(element.ElementDataKey, out var elementData) ==
                false || elementData == null)
            {
                failures.Add($"{type.Name} added no Element Data");
                continue;
            }
            try
            {
                elementData["written.after.processing"] = "value";
                failures.Add($"{type.Name}: a write after processing " +
                    "succeeded");
            }
            catch (PipelineException)
            {
                // Expected.
            }
        }
        Assert.IsEmpty(failures,
            string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Every Flow Element in the Pipeline packages is either checked by
    /// <see cref="EveryFlowElement_WriteAfterProcessingFails"/> or has no
    /// Element Data of its own, so a new Flow Element is checked too.
    /// </summary>
    [TestMethod]
    public void EveryFlowElement_IsChecked()
    {
        var checkedTypes = new HashSet<Type>(Cases().Select(c => c.Type));
        var missing = Assemblies
            .Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass &&
                t.IsAbstract == false &&
                t.IsGenericTypeDefinition == false &&
                t.IsPublic &&
                IsFlowElement(t))
            .Where(t => checkedTypes.Contains(t) == false &&
                NoElementData.ContainsKey(t) == false)
            .Select(t => t.FullName)
            .OrderBy(n => n)
            .ToList();
        Assert.IsEmpty(missing,
            "These Flow Elements are not checked: " +
            string.Join(", ", missing));
    }

    private static bool IsFlowElement(Type type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.IsGenericType &&
                t.GetGenericTypeDefinition() == typeof(FlowElementBase<,>))
            {
                return true;
            }
        }
        return false;
    }
}
