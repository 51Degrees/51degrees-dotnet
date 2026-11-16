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

using System;
using System.IO;
using System.Linq;

namespace FiftyOne.Common.Tests;

/// <summary>
/// A pull request built at standard depth runs only some categories of the
/// IP Intelligence on-premise example tests, because the others load the
/// Enterprise data file (ci/run-integration-tests.ps1 and
/// ci/run-test-lanes.ps1). A test class there that names no category is in
/// none of those categories, so no pull request runs it and nothing says
/// so. The test reads the files git tracks.
/// </summary>
[TestClass]
public class ExampleTestCategoriesTests
{
    /// <summary>
    /// The test assembly a pull request runs by category.
    /// </summary>
    private const string NarrowedTests =
        "examples/ip-intelligence/Tests/Example.Tests.OnPremise/";

    [TestMethod]
    public void NarrowedExampleTests_NameACategory()
    {
        var root = Repository.Root();
        var without = Repository.TrackedFiles(root)
            .Where(f => f.StartsWith(NarrowedTests, StringComparison.Ordinal))
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal))
            .Where(f =>
            {
                var text = File.ReadAllText(Path.Combine(root, f));
                return text.Contains(
                        "[TestClass]", StringComparison.Ordinal) &&
                    text.Contains(
                        "[TestCategory(", StringComparison.Ordinal) == false;
            })
            .ToList();

        Assert.AreEqual(0, without.Count,
            "These test classes name no category, so a pull request built " +
            "at standard depth runs none of their tests: " +
            string.Join(", ", without));
    }
}
