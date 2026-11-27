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
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Constants = FiftyOne.IpIntelligence.TestHelpers.Constants;

namespace FiftyOne.IpIntelligence.OnPremise.Tests.Core.Data;

/// <summary>
/// Latitude and Longitude from the Enterprise data file, which stores them
/// as a declination and an azimuth, are given rounded to 3 decimal places in
/// every form a caller can read them in, as the value types specification
/// says.
/// </summary>
[TestClass]
[TestCategory("Core")]
[TestCategory("Values")]
public class CoordinateDecimalPlacesTests : TestsBase
{
    private const int DecimalPlaces = 3;

    private static readonly string[] Coordinates = { "Latitude", "Longitude" };

    public static IEnumerable<object[]> Addresses => new[]
    {
        new object[] { Constants.Ipv4Address },
        new object[] { Constants.Ipv6Address }
    };

    [TestMethod]
    [DynamicData(nameof(Addresses))]
    public void Coordinates_EveryForm_HaveThreePlaces(string address)
    {
        TestInitialize(PerformanceProfiles.LowMemory);
        using var data = Wrapper.Pipeline.CreateFlowData();
        data.AddEvidence("query.client-ip-51d", address).Process();
        var ip = data.Get<IIpIntelligenceData>();
        var dictionary = ip.AsDictionary();

        AssertRounded($"Latitude from its getter for {address}", ip.Latitude);
        AssertRounded($"Longitude from its getter for {address}", ip.Longitude);
        foreach (var name in Coordinates)
        {
            AssertRounded($"{name} by name for {address}", ip[name]);
            AssertRounded(
                $"{name} in the dictionary for {address}", dictionary[name]);
        }
    }

    /// <summary>
    /// Asserts the value has numbers and that each has at most 3 decimal
    /// places, a float being the float nearest to them.
    /// </summary>
    private static void AssertRounded(string form, object value)
    {
        Assert.IsTrue((value as IAspectPropertyValue)?.HasValue ?? false,
            $"{form} has a value.");
        switch (value)
        {
            case IAspectPropertyValue<double> number:
                AssertRounded(form, number.Value);
                break;
            case IAspectPropertyValue<float> number:
                AssertRounded(form, number.Value);
                break;
            case IAspectPropertyValue<IReadOnlyList<IWeightedValue<double>>> list:
                Assert.IsNotEmpty(list.Value, $"{form} has numbers.");
                foreach (var item in list.Value)
                {
                    AssertRounded(form, item.Value);
                }
                break;
            case IAspectPropertyValue<IReadOnlyList<IWeightedValue<float>>> list:
                Assert.IsNotEmpty(list.Value, $"{form} has numbers.");
                foreach (var item in list.Value)
                {
                    AssertRounded(form, item.Value);
                }
                break;
            default:
                Assert.Fail(
                    $"{form} is a {value.GetType()}, which this test does " +
                    "not read.");
                break;
        }
    }

    private static void AssertRounded(string form, double number) =>
        Assert.AreEqual(
            Math.Round(number, DecimalPlaces, MidpointRounding.AwayFromZero),
            number,
            $"{form} is {number:R}.");

    private static void AssertRounded(string form, float number) =>
        Assert.AreEqual(
            (float)Math.Round(
                number, DecimalPlaces, MidpointRounding.AwayFromZero),
            number,
            $"{form} is {number:R}.");
}
