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

using FiftyOne.IpIntelligence.Engine.OnPremise.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Reflection;

namespace FiftyOne.IpIntelligence.OnPremise.Tests
{
    /// <summary>
    /// Tests of the proxies for the collections of the native configuration.
    /// Each collection is a member of the native configuration, so a proxy
    /// that owned one would free it when finalized and corrupt the heap.
    /// </summary>
    [TestClass]
    [TestCategory("IpIntelligence")]
    [TestCategory("OnPremise")]
    public class ConfigIpiSwigTests
    {
        private static readonly FieldInfo Owned = typeof(CollectionConfigSwig)
            .GetField("swigCMemOwn", BindingFlags.Instance | BindingFlags.NonPublic);

        [TestMethod]
        public void CollectionConfigsAreNotOwned()
        {
            var getters = typeof(ConfigIpiSwig)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.ReturnType == typeof(CollectionConfigSwig) &&
                    m.GetParameters().Length == 0)
                .OrderBy(m => m.Name)
                .ToList();
            Assert.IsTrue(getters.Count > 0, "ConfigIpiSwig has no collection getters.");
            using (var config = new ConfigIpiSwig())
            {
                var owned = getters.Where(getter =>
                {
                    var collection = getter.Invoke(config, null);
                    var owns = (bool)Owned.GetValue(collection);
                    // Ownership is taken back before asserting, so that a
                    // failing test does not also free the configuration's
                    // memory.
                    Owned.SetValue(collection, false);
                    return owns;
                }).Select(getter => getter.Name).ToList();
                Assert.AreEqual(0, owned.Count,
                    "These getters give a proxy that frees a member of the " +
                    "configuration: " + string.Join(", ", owned));
            }
        }
    }
}
