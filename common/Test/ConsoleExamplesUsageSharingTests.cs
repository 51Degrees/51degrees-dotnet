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
/// Console examples run on evidence made up for the example, so they leave
/// the Usage Sharing Element out of their Pipeline, as the required
/// examples pages of the device detection and IP Intelligence
/// specifications ask. Web examples keep it, and the usage sharing example
/// in the pipeline folder exists to show it, so neither is checked. The
/// test reads the files git tracks in every folder whose name ends
/// -Console.
/// </summary>
[TestClass]
public class ConsoleExamplesUsageSharingTests
{
    /// <summary>
    /// The folders whose console examples are checked.
    /// </summary>
    private static readonly string[] ExampleRoots =
    {
        "examples/device-detection/",
        "examples/ip-intelligence/",
        "location/Examples/",
    };

    /// <summary>
    /// What a console example would write to add the element.
    /// </summary>
    private static readonly string[] Adds =
    {
        "ShareUsageBuilder",
        "SetShareUsage(true)",
    };

    [TestMethod]
    public void ConsoleExamples_DoNotShareUsage()
    {
        var root = Repository.Root();
        var sharing = Repository.TrackedFiles(root)
            .Where(f => ExampleRoots.Any(r =>
                f.StartsWith(r, StringComparison.Ordinal)))
            .Where(f => f.Split('/').Any(part =>
                part.EndsWith("-Console", StringComparison.Ordinal)))
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal))
            .Where(f => Adds.Any(a => File
                .ReadAllText(Path.Combine(root, f))
                .Contains(a, StringComparison.Ordinal)))
            .ToList();

        Assert.AreEqual(0, sharing.Count,
            "These console examples add the Usage Sharing Element: " +
            string.Join(", ", sharing));
    }
}
