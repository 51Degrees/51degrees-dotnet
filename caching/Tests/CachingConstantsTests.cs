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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace FiftyOne.Caching.Tests;

/// <summary>
/// The cache builders' default values are in <c>Constants</c>, and a
/// builder that is given no settings uses them.
/// </summary>
[TestClass]
public class CachingConstantsTests
{
    private const string CONSTANTS_TYPE = "FiftyOne.Caching.Constants";
    private const string UPDATE_EXISTING =
        "LRU_PUT_CACHE_DEFAULT_UPDATE_EXISTING";
    private const string CONCURRENCY = "LRU_CACHE_DEFAULT_CONCURRENCY";
    private const int CACHE_SIZE = 2;

    [TestMethod]
    public void Constants_UpdateExistingDefault_IsFalse()
    {
        Assert.AreEqual(false, ReadConstant(UPDATE_EXISTING));
    }

    [TestMethod]
    public void Constants_ConcurrencyDefault_IsTheProcessorCount()
    {
        Assert.AreEqual(
            Environment.ProcessorCount, ReadConstant(CONCURRENCY));
    }

    [TestMethod]
    public void LruPutCacheBuilder_NoSettings_KeepsTheExistingValue()
    {
        var cache = new LruPutCacheBuilder().Build<int, string>(CACHE_SIZE);

        cache.Put(1, "test");
        cache.Put(1, "replacement");

        Assert.AreEqual("test", cache[1],
            "A put cache built with no settings keeps the value a key " +
            "already has.");
    }

    [TestMethod]
    public void LruPutCacheBuilder_NoSettings_HasAListForEachProcessor()
    {
        var cache = (LruPutCache<int, string>)new LruPutCacheBuilder()
            .Build<int, string>(CACHE_SIZE);

        Assert.AreEqual(
            Environment.ProcessorCount, cache._linkedLists.Length);
    }

    [TestMethod]
    public void LruLoadingCacheBuilder_NoSettings_HasAListForEachProcessor()
    {
        var cache = (LruLoadingCache<int, string>)new LruLoadingCacheBuilder()
            .Build<int, string>(CACHE_SIZE);

        Assert.AreEqual(
            Environment.ProcessorCount, cache._linkedLists.Length);
    }

    /// <summary>
    /// Read a public constant or static property of the caching package's
    /// Constants class by name.
    /// </summary>
    private static object ReadConstant(string name)
    {
        var type = typeof(LruPutCacheBuilder).Assembly.GetType(CONSTANTS_TYPE);
        Assert.IsNotNull(type, $"The package has no {CONSTANTS_TYPE} class.");
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        var field = type.GetField(name, flags);
        if (field != null)
        {
            return field.GetValue(null);
        }
        var property = type.GetProperty(name, flags);
        Assert.IsNotNull(property, $"{CONSTANTS_TYPE} has no {name}.");
        return property.GetValue(null);
    }
}
