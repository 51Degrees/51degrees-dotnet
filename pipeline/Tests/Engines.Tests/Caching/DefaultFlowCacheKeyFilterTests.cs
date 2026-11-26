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

using FiftyOne.Caching;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Caching;
using FiftyOne.Pipeline.Engines.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.Tests.Caching
{
    /// <summary>
    /// The default flow cache makes its key from the flow element's cache
    /// key filter where the element has one, and from its evidence key
    /// filter otherwise.
    /// </summary>
    [TestClass]
    public class DefaultFlowCacheKeyFilterTests
    {
        private static IEvidenceKeyFilter KeyFilterUsed(IFlowElement element)
        {
            using var cache = new DefaultFlowCache(new CacheConfiguration()
            {
                Builder = new LruPutCacheBuilder(),
                Size = 10,
            });
            cache.FlowElement = element;
            IEvidenceKeyFilter used = null;
            var data = new Mock<IFlowData>();
            data.Setup(d => d.GenerateKey(It.IsAny<IEvidenceKeyFilter>()))
                .Callback<IEvidenceKeyFilter>(f => used = f)
                .Returns(new DataKeyBuilder().Add(0, "k", "v").Build());
            _ = cache[data.Object];
            return used;
        }

        [TestMethod]
        public void DefaultFlowCache_KeyFilter_FromProvider()
        {
            var accepted = new EvidenceKeyFilterWhitelist(
                new List<string> { "a", "b" });
            var keyed = new EvidenceKeyFilterWhitelist(
                new List<string> { "a" });
            var element = new Mock<IFlowElement>();
            element.SetupGet(e => e.EvidenceKeyFilter).Returns(accepted);
            element.As<ICacheKeyFilterProvider>()
                .SetupGet(e => e.CacheKeyFilter).Returns(keyed);

            Assert.AreSame(keyed, KeyFilterUsed(element.Object));
        }

        [TestMethod]
        public void DefaultFlowCache_KeyFilter_EvidenceKeyFilterOtherwise()
        {
            var accepted = new EvidenceKeyFilterWhitelist(
                new List<string> { "a", "b" });
            var element = new Mock<IFlowElement>();
            element.SetupGet(e => e.EvidenceKeyFilter).Returns(accepted);

            Assert.AreSame(accepted, KeyFilterUsed(element.Object));
        }
    }
}
