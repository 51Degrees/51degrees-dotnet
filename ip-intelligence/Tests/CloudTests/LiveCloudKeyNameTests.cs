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
using System.Collections.Generic;

namespace FiftyOne.IpIntelligence.CloudTests;

/// <summary>
/// A live cloud test with no resource key skips, naming every variable it
/// reads the key from, and never fails. The tests change the environment
/// of the whole process, so they run apart from the others.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LiveCloudKeyNameTests
{
    private readonly Dictionary<string, string> _saved =
        new Dictionary<string, string>();

    [TestInitialize]
    public void Init()
    {
        foreach (var name in LiveCloudKey.Names)
        {
            _saved[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var saved in _saved)
        {
            Environment.SetEnvironmentVariable(saved.Key, saved.Value);
        }
    }

    [TestMethod]
    public void WithoutAKeyTheTestIsSkippedNamingTheVariables()
    {
        string message = null;
        try
        {
            LiveCloudKey.Require("ip", "an address");
        }
        catch (AssertInconclusiveException ex)
        {
            message = ex.Message;
        }
        Assert.IsNotNull(message, "The test was not skipped.");
        foreach (var name in LiveCloudKey.Names)
        {
            StringAssert.Contains(message, name);
        }
    }
}
