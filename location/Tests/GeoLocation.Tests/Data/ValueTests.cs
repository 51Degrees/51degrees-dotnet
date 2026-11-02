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

using FiftyOne.Pipeline.Engines;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FiftyOne.GeoLocation.Tests.Data
{
    public class ValueTests
    {
        /// <summary>
        /// Validate that the properties returned in the Geo-Location element
        /// data are of the correct type as specified by the engine's property
        /// meta data.
        /// </summary>
        /// <param name="wrapper"></param>
        public static void ValueTypes(IWrapper wrapper)
        {
            using (var data = wrapper.Pipeline.CreateFlowData())
            {
                data.AddEvidence(FiftyOne.GeoLocation.Constants.EVIDENCE_GEO_LAT, Constants.TEST_LAT)
                    .AddEvidence(FiftyOne.GeoLocation.Constants.EVIDENCE_GEO_LON, Constants.TEST_LON)
                    .Process();

                var elementData = data.Get(wrapper.GetEngine().ElementDataKey);
                foreach (var property in wrapper.GetEngine().Properties
                    .Where(p => p.Available))
                {
                    Type expectedType = property.Type;
                    var value = elementData[property.Name];
                    Assert.IsNotNull(value);

                    if (value != null)
                    {
                        Assert.IsTrue(expectedType.IsAssignableFrom(
                            value.GetType()),
                            $"Value of '{property.Name}' was of type " +
                            $"{value.GetType()} but should have been " +
                            $"{expectedType}.");
                    }
                }
            }
        }

        /// <summary>
        /// Validate that the available properties as specified in the engine's
        /// property meta data are returned as a result of processing evidence.
        /// </summary>
        /// <param name="wrapper"></param>
        public static void AvailableProperties(IWrapper wrapper)
        {
            using (var data = wrapper.Pipeline.CreateFlowData())
            {
                data.AddEvidence(FiftyOne.GeoLocation.Constants.EVIDENCE_GEO_LAT, Constants.TEST_LAT)
                    .AddEvidence(FiftyOne.GeoLocation.Constants.EVIDENCE_GEO_LON, Constants.TEST_LON)
                    .Process();

                var elementData = data.Get(wrapper.GetEngine().ElementDataKey);
                foreach (var property in wrapper.GetEngine().Properties)
                {
                    var dict = elementData.AsDictionary();

                    Assert.AreEqual(property.Available, dict.ContainsKey(property.Name),
                        $"Property '{property.Name}' " +
                        $"{(property.Available ? "should" : "should not")} be in the results.");
                }
            }
        }

        /// <summary>
        /// Validate that the element data typed getters can be used to access
        /// properties in the engine's aspect data.
        /// </summary>
        /// <param name="wrapper"></param>
        public static void TypedGetters(IWrapper wrapper)
        {
            using (var data = wrapper.Pipeline.CreateFlowData())
            {
                data.AddEvidence(FiftyOne.GeoLocation.Constants.EVIDENCE_GEO_LAT, Constants.TEST_LAT)
                    .AddEvidence(FiftyOne.GeoLocation.Constants.EVIDENCE_GEO_LON, Constants.TEST_LON)
                    .Process();

                var elementData = data.Get(wrapper.GetEngine().ElementDataKey);
                var missingGetters = new List<string>();
                foreach (var property in wrapper.GetEngine().Properties)
                {
                    var cleanPropertyName = property.Name
                        .Replace("/", "")
                        .Replace("-", "");
                    var classProperty = elementData.GetType().GetProperties()
                        .Where(p => p.Name == cleanPropertyName)
                        .FirstOrDefault();
                    if (classProperty != null)
                    {
                        var accessor = classProperty.GetAccessors().FirstOrDefault();
                        if (property.Available == true)
                        {
                            var value = accessor.Invoke(elementData, null);
                            Assert.IsNotNull(value,
                                $"The typed getter for '{property.Name}' should " +
                                $"not have returned a null value.");
                        }
                        else
                        {
                            try
                            {
                                var value = accessor.Invoke(elementData, null);
                                Assert.Fail(
                                    $"The property getter for '{property.Name}' " +
                                    $"should have thrown a " +
                                    $"PropertyMissingException.");
                            }
                            catch (TargetInvocationException e)
                            {
                                Assert.IsInstanceOfType(
                                    e.InnerException,
                                    typeof(PropertyMissingException),
                                    $"The property getter for '{property.Name}' " +
                                    $"should have thrown a " +
                                    $"PropertyMissingException, but the exception " +
                                    $"was of type '{e.InnerException.GetType()}'.");
                            }
                        }
                    }
                    else
                    {
                        missingGetters.Add(property.Name);
                    }
                }
                if (missingGetters.Count > 0)
                {
                    if (missingGetters.Count == 1)
                    {
                        Assert.Fail($"The property '{missingGetters[0]}' " +
                        $"is missing a getter in the GeoData class.");
                    }
                    else
                    {
                        Assert.Fail($"The properties " +
                        $"{string.Join(", ", missingGetters.Select(p => "'" + p + "'"))} " +
                        $"are missing getters in the GeoData class.");
                    }
                }
            }
        }
    }
}
