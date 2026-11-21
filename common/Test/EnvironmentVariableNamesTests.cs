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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FiftyOne.Common.Tests;

/// <summary>
/// Every 51Degrees environment variable name in this repository starts with
/// an underscore, because a POSIX shell cannot set a name that starts with a
/// digit, and no old name is read. The tests read every file git tracks,
/// which leaves out build output and the submodules, and fail on any name
/// that starts with the digit and on any old name that is read or set,
/// apart from the obsolete constants that still hold an old name. The old
/// key names count too: a key is read only under its _51DEGREES_ name, and
/// a workflow passes only the _51DEGREES_ secrets.
/// </summary>
[TestClass]
public class EnvironmentVariableNamesTests
{
    /// <summary>
    /// The start of a name that a shell cannot set, joined at run time so
    /// this file does not match itself.
    /// </summary>
    private const string DigitInitial = "5" + "1DEGREES_";

    /// <summary>
    /// True when the character can be part of an environment variable name.
    /// </summary>
    private static bool IsNameCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character == '_';

    /// <summary>
    /// Each name in the line that starts with <see cref="DigitInitial"/> and
    /// is not the end of a longer name, such as one with a leading
    /// underscore. The name is matched in any case, because Windows reads an
    /// environment variable name in any case.
    /// </summary>
    private static List<string> DigitInitialNames(string line)
    {
        var names = new List<string>();
        var at = 0;
        while (at < line.Length)
        {
            if (at + DigitInitial.Length <= line.Length &&
                string.Compare(
                    line,
                    at,
                    DigitInitial,
                    0,
                    DigitInitial.Length,
                    StringComparison.OrdinalIgnoreCase) == 0 &&
                (at == 0 || !IsNameCharacter(line[at - 1])))
            {
                var end = at;
                while (end < line.Length && IsNameCharacter(line[end]))
                {
                    end++;
                }
                names.Add(line.Substring(at, end - at));
                at = end;
            }
            else
            {
                at++;
            }
        }
        return names;
    }

    /// <summary>
    /// This file, which names every old name on purpose.
    /// </summary>
    private const string ThisFile =
        "common/Test/EnvironmentVariableNamesTests.cs";

    /// <summary>
    /// An old name read or set as an environment variable, or a constant
    /// held to fall back to one. A .NET read takes the name as a string, so
    /// a quoted name is matched, along with the forms the PowerShell scripts
    /// use. A quoted name inside brackets is a dictionary key, such as a
    /// page template's placeholder, and is left alone. The names keep their
    /// case, because the cloud's query string parameter resource_key is not
    /// an environment variable.
    /// </summary>
    private static readonly Regex OldName = new Regex(
        @"(?:(?<!\[)[""']|\$env:|\$\{env:|Env:|@\{\s*|;\s*)" +
        "(?:RESOURCE_KEY|SUPER_RESOURCE_KEY|LICENSE_KEY|" +
        "DEVICEDETECTIONDATAFILE|IPINTELLIGENCEDATAFILE|FOD_CLOUD_API_URL|" +
        "FIFTYONE_CLOUD_ENDPOINT|FIFTYONE_TEST_FILTER|" +
        "51D_RESOURCE_KEY|51D_CLOUD_ENDPOINT|" +
        "DEVICEDETECTIONLICENSEKEY_DOTNET|IPINTELLIGENCELICENSEKEY_DOTNET|" +
        "RESOURCE_KEY_CLOUD_V5_BESPOKE|PAID_RESOURCE_KEY|" +
        "ACCEPTCH_BROWSER_KEY|ACCEPTCH_HARDWARE_KEY|" +
        "ACCEPTCH_PLATFORM_KEY|ACCEPTCH_NONE_KEY)" +
        "(?![A-Za-z0-9_])|" +
        @"\bLEGACY_[A-Z_]*ENV_VARS?\b",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The cloud endpoint's old name.
    /// </summary>
    private const string OldEndpointName = "FOD_CLOUD_API_URL";

    /// <summary>
    /// The only files that may hold <see cref="OldEndpointName"/>, being
    /// the two packages that keep an obsolete constant holding it, and the
    /// tests of those constants.
    /// </summary>
    private static readonly HashSet<string> OldEndpointNameFiles =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "pipeline/CloudRequestEngine/Constants.cs",
            "pipeline/Did/Client/DidClient.cs",
            "pipeline/Tests/CloudRequestEngine.Tests/" +
                "CloudRequestEngineOldEndpointNameTests.cs",
            "pipeline/Tests/Did.Tests/DidClientOldEndpointNameTests.cs",
        };

    /// <summary>
    /// True when <paramref name="match"/> is the cloud endpoint's old name
    /// as a quoted string in one of the files that may hold it.
    /// </summary>
    private static bool IsKeptOldEndpointName(string relative, string match) =>
        OldEndpointNameFiles.Contains(relative) &&
        match == "\"" + OldEndpointName;

    /// <summary>
    /// The example files that keep obsolete constants holding the other old
    /// names. The examples no longer read those names.
    /// </summary>
    private static readonly HashSet<string> ObsoleteConstantFiles =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "examples/device-detection/Cloud/pmp-web/Settings.cs",
            "examples/device-detection/ExampleBase/Constants.cs",
            "examples/device-detection/ExampleBase/ExampleUtils.cs",
            "examples/ip-intelligence/Examples/Constants.cs",
            "examples/ip-intelligence/Examples/ExampleUtils.cs",
        };

    /// <summary>
    /// The tests that set an old licence key name to show that it is not
    /// read.
    /// </summary>
    private static readonly HashSet<string> OldNameTestFiles =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "examples/device-detection/Tests/Example.Tests.OnPremise/" +
                "LicenceKeyNameTests.cs",
            "examples/ip-intelligence/Tests/Example.Tests.OnPremise/" +
                "LicenceKeyNameTests.cs",
        };

    /// <summary>
    /// The name the external Selenium contract suite reads its resource
    /// key from. That suite is another repository, so the script that runs
    /// it sets the name, from _51DEGREES_RESOURCE_KEY, until the suite
    /// reads a _51DEGREES_ name.
    /// </summary>
    private const string ContractSuiteKeyName = "PAID_RESOURCE_KEY";

    /// <summary>
    /// The script that runs the external Selenium contract suite.
    /// </summary>
    private const string ContractSuiteScript = "ci/run-integration-tests.ps1";

    /// <summary>
    /// True when the file may hold the match, as one of the example files
    /// that keep obsolete constants, as the cloud endpoint's old name where
    /// it is kept, as a test that shows an old licence key name is not
    /// read, or as the contract suite's key name where that suite is run.
    /// </summary>
    private static bool IsKeptOldName(string relative, string match) =>
        ObsoleteConstantFiles.Contains(relative) ||
        IsKeptOldEndpointName(relative, match) ||
        OldNameTestFiles.Contains(relative) ||
        (relative == ContractSuiteScript &&
            match.EndsWith(ContractSuiteKeyName, StringComparison.Ordinal));

    /// <summary>
    /// The secrets a workflow may name that do not start _51DEGREES_: the
    /// token a workflow acts with.
    /// </summary>
    private static readonly HashSet<string> OtherSecrets =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "ACCESS_TOKEN",
            "GITHUB_TOKEN",
        };

    /// <summary>
    /// A secret or repository variable a workflow names.
    /// </summary>
    private static readonly Regex WorkflowName = new Regex(
        @"(?<![A-Za-z0-9_])(?:secrets|vars)[.](?<name>[A-Za-z0-9_]+)",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Each secret the line names that is not a _51DEGREES_ secret or one
    /// of <see cref="OtherSecrets"/>, and each repository variable it
    /// names, because a key is a secret.
    /// </summary>
    private static List<string> OtherWorkflowNames(string line)
    {
        var names = new List<string>();
        foreach (Match match in WorkflowName.Matches(line))
        {
            var name = match.Groups["name"].Value;
            var isVariable = match.Value.StartsWith(
                "vars.", StringComparison.Ordinal);
            if (isVariable ||
                (name.StartsWith("_51DEGREES_", StringComparison.Ordinal)
                    == false &&
                 OtherSecrets.Contains(name) == false))
            {
                names.Add(match.Value);
            }
        }
        return names;
    }

    /// <summary>
    /// Each old name the line reads, sets or holds.
    /// </summary>
    private static List<string> OldNames(string line)
    {
        var names = new List<string>();
        foreach (Match match in OldName.Matches(line))
        {
            names.Add(match.Value);
        }
        return names;
    }

    /// <summary>
    /// Every match in the tracked files apart from <paramref name="skip"/>
    /// and the matches <paramref name="allow"/> accepts for a path, as the
    /// path, the line number and the text matched.
    /// </summary>
    private static List<string> FindInTrackedFiles(
        Func<string, List<string>> find,
        string skip,
        Func<string, string, bool> allow = null)
    {
        var root = Repository.Root();
        var found = new List<string>();
        foreach (var relative in Repository.TrackedFiles(root))
        {
            if (relative == skip)
            {
                continue;
            }
            var path = Path.Combine(
                root, relative.Replace('/', Path.DirectorySeparatorChar));
            // A submodule is tracked as a directory, and its files belong to
            // another repository.
            if (!File.Exists(path))
            {
                continue;
            }
            var text = File.ReadAllText(path, Encoding.UTF8);
            var lines = text.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var name in find(lines[index]))
                {
                    if (allow != null && allow(relative, name))
                    {
                        continue;
                    }
                    found.Add($"{relative}:{index + 1}: {name}");
                }
            }
        }
        return found;
    }

    [TestMethod]
    public void TheNameMatcherFindsOnlyNamesThatStartWithTheDigit()
    {
        var digitInitial = DigitInitial + "DD_PATH";
        var lower = digitInitial.ToLowerInvariant();
        var line =
            $"export {digitInitial}=a _{digitInitial}=b X{digitInitial} " +
            $"{lower}=c";
        CollectionAssert.AreEqual(
            new[] { digitInitial, lower },
            DigitInitialNames(line));
    }

    [TestMethod]
    public void TheOldNameMatcherFindsReadsAndSetsOnly()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "\"SUPER_RESOURCE_KEY",
                "\"51D_RESOURCE_KEY",
                "$env:FIFTYONE_TEST_FILTER",
                "@{ FIFTYONE_CLOUD_ENDPOINT",
                "LEGACY_CLOUD_RESOURCE_KEY_ENV_VAR",
            },
            OldNames(
                "GetEnvironmentVariable(\"SUPER_RESOURCE_KEY\") " +
                "{ \"51D_RESOURCE_KEY\", " +
                "$env:FIFTYONE_TEST_FILTER " +
                "-ExampleEnv @{ FIFTYONE_CLOUD_ENDPOINT = 'x' } " +
                "LEGACY_CLOUD_RESOURCE_KEY_ENV_VAR"));
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            OldNames(
                "[\"RESOURCE_KEY\"] = ResourceKey {{RESOURCE_KEY}} " +
                "\"_51DEGREES_RESOURCE_KEY\" \"RESOURCE_KEYS\" " +
                "LICENSE_KEY_ENV_VAR resource_key=abc"));
    }

    [TestMethod]
    public void TheOldEndpointNameIsKeptOnlyInTheFilesThatHoldItsConstant()
    {
        var quoted = "\"" + OldEndpointName;
        foreach (var file in OldEndpointNameFiles)
        {
            Assert.IsTrue(IsKeptOldEndpointName(file, quoted), file);
            Assert.IsFalse(
                IsKeptOldEndpointName(file, "\"RESOURCE_KEY"), file);
            Assert.IsFalse(
                IsKeptOldEndpointName(file, "$env:" + OldEndpointName),
                file);
        }
        Assert.IsFalse(IsKeptOldEndpointName(
            "pipeline/CloudRequestEngine/FlowElements/" +
                "CloudRequestEngineBuilder.cs",
            quoted));
        Assert.IsFalse(IsKeptOldEndpointName(ThisFile, quoted));
    }

    [TestMethod]
    public void NoFileNamesAVariableThatStartsWithADigit()
    {
        var found = FindInTrackedFiles(
            DigitInitialNames, null, IsKeptOldName);
        Assert.AreEqual(
            0,
            found.Count,
            $"{found.Count} names start with a digit, which a POSIX shell " +
            "cannot set, so each must start with an underscore:\n" +
            string.Join("\n", found));
    }

    [TestMethod]
    public void TheOldKeyNamesAreFound()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "$env:ACCEPTCH_BROWSER_KEY",
                "\"DEVICEDETECTIONLICENSEKEY_DOTNET",
                "\"RESOURCE_KEY_CLOUD_V5_BESPOKE",
                "$env:PAID_RESOURCE_KEY",
            },
            OldNames(
                "$env:ACCEPTCH_BROWSER_KEY = 1 " +
                "GetEnvironmentVariable(\"DEVICEDETECTIONLICENSEKEY_DOTNET\") " +
                "{ \"RESOURCE_KEY_CLOUD_V5_BESPOKE\", " +
                "$env:PAID_RESOURCE_KEY = 2"));
        Assert.IsTrue(IsKeptOldName(
            ContractSuiteScript, "$env:" + ContractSuiteKeyName));
        Assert.IsFalse(IsKeptOldName(
            ContractSuiteScript, "$env:ACCEPTCH_NONE_KEY"));
    }

    [TestMethod]
    public void TheWorkflowNameMatcherFindsOnlyOtherNames()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "secrets.DEVICE_DETECTION_KEY",
                "vars._51DEGREES_RESOURCE_KEY_BASIC",
            },
            OtherWorkflowNames(
                "${{ secrets.DEVICE_DETECTION_KEY }} " +
                "${{ vars._51DEGREES_RESOURCE_KEY_BASIC }} " +
                "${{ secrets._51DEGREES_RESOURCE_KEY_BESPOKE }} " +
                "${{ secrets.ACCESS_TOKEN }} secrets: inherit"));
    }

    [TestMethod]
    public void NoWorkflowNamesAnotherSecretOrAVariable()
    {
        var found = FindInTrackedFiles(
            line => OtherWorkflowNames(line),
            ThisFile,
            (relative, _) => relative.StartsWith(
                ".github/", StringComparison.Ordinal) == false);
        Assert.AreEqual(
            0,
            found.Count,
            $"{found.Count} secrets or variables named in the workflows " +
            "do not start _51DEGREES_, so each must be a _51DEGREES_ " +
            "secret:\n" + string.Join("\n", found));
    }

    /// <summary>
    /// A 51Did key that a workflow fills from a secret. A 51Did license key
    /// reads only a sealed result made with a resource key of its own
    /// account, so a 51Did key is filled from the secret of its own name
    /// and never from another key.
    /// </summary>
    private static readonly Regex DidKeyFill = new Regex(
        @"(?<![A-Za-z0-9_])(?<name>_51DEGREES_[A-Z_]*_51DID)\s*:\s*" +
        @"\$\{\{\s*secrets[.](?<secret>[A-Za-z0-9_]+)\s*\}\}",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Each 51Did key the line fills from a secret of another name.
    /// </summary>
    private static List<string> DidKeysFilledFromAnotherSecret(string line)
    {
        var fills = new List<string>();
        foreach (Match match in DidKeyFill.Matches(line))
        {
            if (match.Groups["name"].Value != match.Groups["secret"].Value)
            {
                fills.Add(match.Value);
            }
        }
        return fills;
    }

    [TestMethod]
    public void TheDidKeyFillMatcherFindsOnlyAnotherSecret()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "_51DEGREES_RESOURCE_KEY_51DID: " +
                    "${{ secrets._51DEGREES_RESOURCE_KEY_BESPOKE }}",
            },
            DidKeysFilledFromAnotherSecret(
                "_51DEGREES_RESOURCE_KEY_51DID: " +
                    "${{ secrets._51DEGREES_RESOURCE_KEY_BESPOKE }}"));
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            DidKeysFilledFromAnotherSecret(
                "_51DEGREES_RESOURCE_KEY_51DID: " +
                    "${{ secrets._51DEGREES_RESOURCE_KEY_51DID }}"));
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            DidKeysFilledFromAnotherSecret(
                "_51DEGREES_LICENSE_KEY_51DID: " +
                    "${{ secrets._51DEGREES_LICENSE_KEY_51DID }}"));
    }

    [TestMethod]
    public void EveryWorkflowFillsADidKeyFromTheSecretOfItsName()
    {
        var found = FindInTrackedFiles(
            DidKeysFilledFromAnotherSecret,
            ThisFile,
            (relative, _) => relative.StartsWith(
                ".github/", StringComparison.Ordinal) == false);
        Assert.AreEqual(
            0,
            found.Count,
            $"{found.Count} 51Did keys are filled from another secret. A " +
            "51Did license key reads only a result sealed with the 51Did " +
            "resource key, so each must be filled from the secret of its " +
            "own name:\n" + string.Join("\n", found));
    }

    [TestMethod]
    public void NoFileReadsAnOldName()
    {
        var found = FindInTrackedFiles(
            OldNames, ThisFile, IsKeptOldName);
        Assert.AreEqual(
            0,
            found.Count,
            $"{found.Count} old names are still read, set or held, so " +
            "each must use its _51DEGREES_ name alone:\n" +
            string.Join("\n", found));
    }
}
