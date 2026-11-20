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

using FiftyOne.Did.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests that <see cref="DidClient"/> reads the cloud endpoint from its
    /// environment variable, and that the old constant keeps its name and
    /// value although the variable it names is no longer read. Nothing here
    /// touches the network.
    /// </summary>
    [TestClass]
    public class DidClientOldEndpointNameTests
    {
        private const string Resource = "AQAAAresourcekey";
        private const string NewName = "_51DEGREES_CLOUD_ENDPOINT";
        private const string OldName = "FOD_CLOUD_API_URL";
        private const string NewConstant = "CloudEndpointEnvironmentVariable";
        private const string NewUrl = "https://new.example/api/v4/";

        private FakeHttpHandler _handler = null!;
        private string? _previousNew;

        [TestInitialize]
        public void TestInitialize()
        {
            _handler = new FakeHttpHandler();
            _previousNew = Environment.GetEnvironmentVariable(NewName);
            Environment.SetEnvironmentVariable(NewName, null);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Environment.SetEnvironmentVariable(NewName, _previousNew);
        }

        /// <summary>
        /// A client given no endpoint, so it takes one from the
        /// environment or the default.
        /// </summary>
        private DidClient NewClient() =>
            new DidClient(Resource, null, null, _handler.Client);

        [TestMethod]
        public void NewNameSet_GivesThatEndpoint()
        {
            Environment.SetEnvironmentVariable(NewName, NewUrl);

            using var client = NewClient();

            Assert.AreEqual(NewUrl, client.Endpoint);
        }

#if NET9_0_OR_GREATER
        // .NET 8 deletes a variable that is set to an empty value, so an
        // empty value can only be set up from .NET 9.
        [TestMethod]
        public void NewNameEmpty_GivesTheDefault()
        {
            Environment.SetEnvironmentVariable(NewName, string.Empty);

            using var client = NewClient();

            Assert.AreEqual(DidClient.DefaultEndpoint, client.Endpoint);
        }
#endif

        [TestMethod]
        public void NewNameNotSet_GivesTheDefault()
        {
            using var client = NewClient();

            Assert.AreEqual(DidClient.DefaultEndpoint, client.Endpoint);
        }

        [TestMethod]
        public void EndpointGiven_WinsOverTheNewName()
        {
            Environment.SetEnvironmentVariable(NewName, NewUrl);

            using var client = new DidClient(
                Resource, null, "https://given.example/api/v4/",
                _handler.Client);

            Assert.AreEqual("https://given.example/api/v4/", client.Endpoint);
        }

        [TestMethod]
        public void OldConstant_HoldsTheOldName()
        {
#pragma warning disable CS0618 // The deprecated constant is the subject.
            Assert.AreEqual(OldName, DidClient.EndpointEnvironmentVariable);
#pragma warning restore CS0618
        }

        [TestMethod]
        public void NewConstant_HoldsTheNewName()
        {
            var field = typeof(DidClient).GetField(NewConstant);

            Assert.IsNotNull(field, $"DidClient has no {NewConstant}.");
            Assert.AreEqual(NewName, field.GetRawConstantValue());
        }

        [TestMethod]
        public void OldConstant_IsObsoleteAndPointsAtTheNewOne()
        {
            var obsolete = typeof(DidClient)
                .GetField("EndpointEnvironmentVariable")!
                .GetCustomAttribute<ObsoleteAttribute>();

            Assert.IsNotNull(
                obsolete, "EndpointEnvironmentVariable is not obsolete.");
            Assert.IsFalse(
                obsolete.IsError,
                "Code that uses the old constant must still compile.");
            StringAssert.Contains(obsolete.Message, NewConstant);
        }
    }
}