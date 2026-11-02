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

using FiftyOne.Common.TestHelpers;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Common.Tests;

[TestClass]
public class LoggerFactoryTests
{
    private TestLoggerFactory _loggerFactory;
    private ILogger _logger;
            
    [TestInitialize]
    public void Init()
    {
        _loggerFactory = new TestLoggerFactory();
        _logger = _loggerFactory.CreateLogger("Test");
        _logger.LogInformation("Hello");
    }

    [TestMethod]
    public void NoWarnings()
    {
        _loggerFactory.AssertMaxWarnings(0);
    }

    [TestMethod]
    public void NoErrors()
    {
        _loggerFactory.AssertMaxErrors(0);
    }

    [TestMethod]
    public void WarningsOne()
    {
        _logger.LogWarning("Test");
        _loggerFactory.AssertMaxWarnings(1);
    }

    [TestMethod]
    public void ErrorsOne()
    {
        _logger.LogError("Test");
        _loggerFactory.AssertMaxErrors(1);
    }

    [TestMethod]
    public void Warnings()
    {
        _logger.LogWarning("Test");
        Assert.ThrowsExactly<AssertFailedException>(
            () => _loggerFactory.AssertMaxWarnings(0));
    }

    [TestMethod]
    public void Errors()
    {
        _logger.LogError("Test");
        Assert.ThrowsExactly<AssertFailedException>(
            () => _loggerFactory.AssertMaxErrors(0));
    }
}