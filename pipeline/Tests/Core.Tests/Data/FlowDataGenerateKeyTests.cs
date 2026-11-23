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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.Tests.Data;

/// <summary>
/// The order <see cref="FlowData.GenerateKey"/> puts evidence values in.
/// </summary>
[TestClass]
public class FlowDataGenerateKeyTests
{
    /// <summary>
    /// Includes every key but one, and gives some keys the same order and
    /// some no order at all.
    /// </summary>
    private class Filter : IEvidenceKeyFilter
    {
        private readonly Dictionary<string, int?> _orders;

        public Filter(Dictionary<string, int?> orders)
        {
            _orders = orders;
        }

        public bool Include(string key) => key != "query.excluded";

        public int? Order(string key) =>
            _orders.TryGetValue(key, out var order) ? order : null;
    }

    /// <summary>
    /// The key holds the included values ordered by the filter's order and
    /// then by key, which is what the LINQ query the key was built with
    /// before gives.
    /// </summary>
    [TestMethod]
    public void FlowData_GenerateKey_OrdersByFilterOrderThenKey()
    {
        var evidence = new Dictionary<string, object>()
        {
            { "header.user-agent", "a" },
            { "query.excluded", "b" },
            { "header.accept", "c" },
            { "cookie.zzz", "d" },
            { "query.client-ip", "e" },
            { "header.Sec-CH-UA", "f" },
            { "server.client-ip", "g" },
        };
        var filter = new Filter(new Dictionary<string, int?>()
        {
            { "header.user-agent", 2 },
            { "header.accept", 2 },
            { "query.client-ip", 0 },
            { "header.Sec-CH-UA", 1 },
            { "server.client-ip", null },
        });
        var pipeline = new Mock<IPipelineInternal>();
        using var data = new FlowData(
            new Mock<ILogger<FlowData>>().Object,
            pipeline.Object,
            new Evidence(new Mock<ILogger<Evidence>>().Object));
        data.AddEvidence(evidence);

        var expected = new DataKey(data.GetEvidence().AsDictionary()
            .Where(e => filter.Include(e.Key))
            .OrderBy(e => filter.Order(e.Key))
                .ThenBy(e => e.Key)
            .Select(e => e.Value)
            .ToList());

        Assert.AreEqual(expected, data.GenerateKey(filter));
        Assert.AreNotEqual(
            new DataKey(evidence
                .Where(e => filter.Include(e.Key))
                .Select(e => e.Value)
                .ToList()),
            data.GenerateKey(filter),
            "The evidence must be added in a different order from the " +
            "key's for the test to show anything.");
    }
}
