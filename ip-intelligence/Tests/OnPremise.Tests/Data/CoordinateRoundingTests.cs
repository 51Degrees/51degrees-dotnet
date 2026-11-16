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

using FiftyOne.IpIntelligence.Shared.Data;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace FiftyOne.IpIntelligence.OnPremise.Tests.Core.Data;

/// <summary>
/// Latitude and Longitude, which the data file stores as a declination
/// and an azimuth, are given rounded to 3 decimal places in every
/// numeric form, as the value types specification says. The numbers
/// come from a table rather than native results, so no data file is
/// needed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class CoordinateRoundingTests
{
    private const string ElementDataKey = "ip";

    /// <summary>
    /// The specification's example, a stored declination of 6751, and a
    /// second number with a weight, as the data file gives a list.
    /// </summary>
    private static readonly IReadOnlyList<IWeightedValue<double>> Unrounded =
        new List<IWeightedValue<double>>
        {
            new WeightedValue<double>(65535, 18.542741172521133),
            new WeightedValue<double>(100, -73.91278076171875)
        };

    private static readonly double[] Rounded = { 18.543, -73.913 };

    private const string OtherNumber = "OtherNumber";

    [TestMethod]
    [DataRow("Latitude")]
    [DataRow("Longitude")]
    public void WeightedDoubles_Coordinate_AreRounded(string name)
    {
        var data = NewData(typeof(IReadOnlyList<IWeightedValue<double>>));

        var value = (IAspectPropertyValue<IReadOnlyList<IWeightedValue<double>>>)
            data[name];

        CollectionAssert.AreEqual(Rounded,
            value.Value.Select(v => v.Value).ToArray(),
            $"Each number of {name} has 3 decimal places.");
        CollectionAssert.AreEqual(new ushort[] { 65535, 100 },
            value.Value.Select(v => v.RawWeighting).ToArray(),
            "The weights are unchanged.");
    }

    [TestMethod]
    [DataRow("Latitude")]
    [DataRow("Longitude")]
    public void WeightedFloats_Coordinate_AreRounded(string name)
    {
        var data = NewData(typeof(IReadOnlyList<IWeightedValue<float>>));

        var value = (IAspectPropertyValue<IReadOnlyList<IWeightedValue<float>>>)
            data[name];

        CollectionAssert.AreEqual(Rounded.Select(r => (float)r).ToArray(),
            value.Value.Select(v => v.Value).ToArray(),
            $"Each number of {name} is the float nearest to 3 places.");
    }

    [TestMethod]
    [DataRow("Latitude")]
    [DataRow("Longitude")]
    public void Double_Coordinate_IsRounded(string name)
    {
        var data = NewData(typeof(double));

        Assert.AreEqual(Rounded[0],
            ((IAspectPropertyValue<double>)data[name]).Value,
            $"{name} has 3 decimal places.");
    }

    [TestMethod]
    public void TypedGetters_Coordinates_AreRounded()
    {
        var data = NewData(typeof(float));

        Assert.AreEqual((float)Rounded[0], data.Latitude.Value,
            "Latitude is the float nearest to 3 places.");
        Assert.AreEqual((float)Rounded[0], data.Longitude.Value,
            "Longitude is the float nearest to 3 places.");
    }

    [TestMethod]
    public void Dictionary_Coordinates_AreRounded()
    {
        var data = NewData(typeof(IReadOnlyList<IWeightedValue<double>>));

        var dictionary = data.AsDictionary();

        foreach (var name in new[] { "latitude", "longitude" })
        {
            var value = (IAspectPropertyValue<IReadOnlyList<IWeightedValue<double>>>)
                dictionary[name];
            CollectionAssert.AreEqual(Rounded,
                value.Value.Select(v => v.Value).ToArray(),
                $"Each number of {name} has 3 decimal places.");
        }
    }

    [TestMethod]
    public void WeightedDoubles_OtherNumber_KeepEveryPlace()
    {
        var data = NewData(typeof(IReadOnlyList<IWeightedValue<double>>));

        var value = (IAspectPropertyValue<IReadOnlyList<IWeightedValue<double>>>)
            data[OtherNumber];

        CollectionAssert.AreEqual(
            Unrounded.Select(v => v.Value).ToArray(),
            value.Value.Select(v => v.Value).ToArray(),
            "A number that is not a coordinate is not rounded.");
    }

    [TestMethod]
    public void Double_RoundsToZero_HasNoSign()
    {
        var data = NewData(
            typeof(double),
            new List<IWeightedValue<double>>
            {
                new WeightedValue<double>(65535, -0.0004)
            });

        var value = ((IAspectPropertyValue<double>)data["Latitude"]).Value;

        Assert.AreEqual(0.0, value, "The value rounds to zero.");
        Assert.IsFalse(double.IsNegative(value),
            "The zero has no sign, as 0.000 has none.");
    }

    /// <summary>
    /// Data holding Latitude, Longitude and one other number, each of the
    /// given type in the property meta-data.
    /// </summary>
    private static StubData NewData(
        Type type,
        IReadOnlyList<IWeightedValue<double>> numbers = null)
    {
        var engine = new Mock<IAspectEngine>();
        engine.SetupGet(e => e.ElementDataKey).Returns(ElementDataKey);
        var names = new[] { "Latitude", "Longitude", OtherNumber };
        var properties = names.ToDictionary(
            n => n,
            n => (IElementPropertyMetaData)new ElementPropertyMetaData(
                engine.Object, n, type, true, "Location"),
            StringComparer.OrdinalIgnoreCase);
        var pipeline = new Mock<IPipeline>();
        pipeline.SetupGet(p => p.ElementAvailableProperties).Returns(
            new Dictionary<string, IReadOnlyDictionary<string, IElementPropertyMetaData>>
            {
                { ElementDataKey, properties }
            });
        return new StubData(
            pipeline.Object,
            engine.Object,
            names.ToDictionary(
                n => n, n => numbers ?? Unrounded,
                StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Data whose numbers come from a table, each property holding a
    /// list of weighted numbers as the data file does. The single number
    /// forms take the first.
    /// </summary>
    private class StubData : IpDataBaseOnPremise<IDisposable>
    {
        private readonly IReadOnlyDictionary<string,
            IReadOnlyList<IWeightedValue<double>>> _numbers;

        internal StubData(
            IPipeline pipeline,
            IAspectEngine engine,
            IReadOnlyDictionary<string,
                IReadOnlyList<IWeightedValue<double>>> numbers)
            : base(
                new Mock<ILogger<AspectDataBase>>().Object,
                pipeline,
                engine,
                new Mock<IMissingPropertyService>().Object)
        {
            _numbers = numbers;
            // The values are read only once the data has results.
            Results.AddResult(null);
        }

        private IReadOnlyList<IWeightedValue<double>> Numbers(
            string propertyName) =>
            _numbers.TryGetValue(propertyName, out var numbers) ?
            numbers : throw new PropertyMissingException();

        protected override bool PropertyIsAvailable(string propertyName) =>
            _numbers.ContainsKey(propertyName);

        protected override IAspectPropertyValue<double> GetValueAsDouble(
            string propertyName) =>
            new AspectPropertyValue<double>(Numbers(propertyName)[0].Value);

        protected override IAspectPropertyValue<float> GetValueAsFloat(
            string propertyName) =>
            new AspectPropertyValue<float>(
                (float)Numbers(propertyName)[0].Value);

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<double>>>
            GetValuesAsWeightedDoubleList(string propertyName) =>
            new AspectPropertyValue<IReadOnlyList<IWeightedValue<double>>>(
                Numbers(propertyName));

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<float>>>
            GetValuesAsWeightedFloatList(string propertyName) =>
            new AspectPropertyValue<IReadOnlyList<IWeightedValue<float>>>(
                Numbers(propertyName)
                .Select(v => (IWeightedValue<float>)new WeightedValue<float>(
                    v.RawWeighting, (float)v.Value))
                .ToList());

        // The tests read numbers only.
        protected override IAspectPropertyValue<string> GetValueAsString(
            string propertyName) => throw new NotSupportedException();

        protected override IAspectPropertyValue<WktString> GetValueAsWktString(
            string propertyName) => throw new NotSupportedException();

        protected override IAspectPropertyValue<int> GetValueAsInteger(
            string propertyName) => throw new NotSupportedException();

        protected override IAspectPropertyValue<bool> GetValueAsBool(
            string propertyName) => throw new NotSupportedException();

        protected override IAspectPropertyValue<IPAddress> GetValueAsIp(
            string propertyName) => throw new NotSupportedException();

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<bool>>>
            GetValuesAsWeightedBoolList(string propertyName) =>
            throw new NotSupportedException();

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<int>>>
            GetValuesAsWeightedIntegerList(string propertyName) =>
            throw new NotSupportedException();

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>
            GetValuesAsWeightedStringList(string propertyName) =>
            throw new NotSupportedException();

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<IPAddress>>>
            GetValuesAsWeightedIPList(string propertyName) =>
            throw new NotSupportedException();

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>
            GetValuesAsWeightedWKTStringList(
                string propertyName, byte decimalPlaces) =>
            throw new NotSupportedException();

        public override IAspectPropertyValue<IReadOnlyList<string>> GetValues(
            string propertyName) => throw new NotSupportedException();
    }
}
