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


// Ignore Spelling: ip API

using FiftyOne.IpIntelligence.Examples;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

[assembly: Parallelize]
namespace FiftyOne.IpIntelligence.Example.Tests.Cloud
{
    /// <summary>
    /// Runs the Cloud examples against the real 51Degrees Cloud service
    /// (cloud.51degrees.com by default). Requires the 51DEGREES_RESOURCE_KEY
    /// (or legacy SUPER_RESOURCE_KEY) environment variable to be set.
    /// </summary>
    /// <remarks>
    /// Note that these tests do not generally ensure the correctness
    /// of the example, only that the example will run without
    /// crashing or throwing any unhandled exceptions.
    /// </remarks>
    [TestClass]
    public class TestCloudExamples
    {
        private string _resourceKey;

        [TestInitialize]
        public void Init()
        {
            _resourceKey = ExampleUtils.GetResourceKeyFromEnv();

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(_resourceKey),
                $"Environment variable '{ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR}' " +
                $"is not set. Obtain a resource key at " +
                $"https://configure.51degrees.com and export it to run this test.");
        }

        private static readonly string RepoRootPath =
            Path.GetFullPath(
                Path.GetDirectoryName(
                    ExampleUtils.FindFile("FiftyOne.IpIntelligence.Examples.sln"))!);

        private static readonly string CloudExamplePath =
            Path.Combine(RepoRootPath, "Examples", "Cloud", "GettingStarted-Console");
        private static readonly string MixedCloudExamplePath =
            Path.Combine(RepoRootPath, "Examples", "Cloud", "Mixed", "GettingStarted-Console");
        private static readonly string GetAllPropertiesExamplePath =
            Path.Combine(RepoRootPath, "Examples", "Cloud", "GetAllProperties-Console");
        private static readonly string MetadataExamplePath =
            Path.Combine(RepoRootPath, "Examples", "Cloud", "Metadata-Console");

        public static IEnumerable<object[]> CloudExamplesToTest =>
        [
            [
                CloudExamplePath,
                new string[]
                {
                    "VMCBBUK",
                },
                new string[]
                {
                    "United Kingdom",
                    "GB",
                },
                new string[]
                {
                    "82.12.34.0",
                    "82.12.34.255",
                },
                "82.12.34.x",
            ],
            [
                MixedCloudExamplePath,
                new string[]
                {
                    "VMCBBUK",
                },
                new string[]
                {
                    "United Kingdom",
                    "GB",
                },
                new string[]
                {
                    "82.12.34.0",
                    "82.12.34.255",
                },
                "82.12.34.x",
            ],
        ];

        /// <summary>
        /// Runs a Cloud example as a separate process (matching how a user
        /// would run it) and verifies the output contains the expected
        /// IP intelligence values for one of the IPs in
        /// <see cref="ExampleUtils.EvidenceValues"/>.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(CloudExamplesToTest))]
        public void Example_Cloud_GettingStarted(
            string examplePath,
            string[] networkNames,
            string[] countryNames,
            string[] ipRanges,
            string rangeDescriptor)
        {
            var (exitCode, result, errorOutput) = RunCloudExampleProcess(examplePath);

            Assert.AreEqual(0, exitCode,
                $"Cloud example should exit successfully. Error output: {errorOutput}");

            Assert.Contains("Input values:", result,
                "Output should contain input values section");
            Assert.Contains("Results:", result,
                "Output should contain results section");

            Assert.IsTrue(networkNames.Any(x => result.Contains(x)),
                $"Output should contain {networkNames[0]} as registered name");
            Assert.IsTrue(countryNames.Any(x => result.Contains(x)),
                $"Output should contain {countryNames[0]} as country or code");
            Assert.IsTrue(ipRanges.Any(x => result.Contains(x)),
                $"Output should contain IP range values {rangeDescriptor}");

            Assert.IsTrue(Regex.IsMatch(result, @"\d+\.\d+\.\d+\.\d+"),
                "Output should contain IP address values in dotted decimal format");

            Assert.IsTrue(Regex.IsMatch(result, @"[\d\-]\d+\.\d+"),
                "Output should contain numeric values (coordinates, ranges, etc.)");

            // Only unhandled exceptions indicate a broken example. The engine
            // logs recoverable problems (such as a transient cloud warm-up
            // failure that it then retries) to stdout, and those log lines
            // contain the words 'Exception' and 'Error' without the example
            // itself having failed - the exit code assertion above covers that.
            Assert.DoesNotContain("Unhandled exception", result,
                StringComparison.OrdinalIgnoreCase,
                "Output should not contain an unhandled exception");
        }

        /// <summary>
        /// Runs the GetAllProperties Cloud example and verifies it lists the
        /// property values returned for the analysed IP address.
        /// </summary>
        [TestMethod]
        public void Example_Cloud_GetAllProperties()
        {
            var (exitCode, result, errorOutput) =
                RunCloudExampleProcess(GetAllPropertiesExamplePath);

            Assert.AreEqual(0, exitCode,
                $"GetAllProperties example should exit successfully. " +
                $"Error output: {errorOutput}");

            Assert.Contains("What property values are associated with", result,
                "Output should introduce the property values for an IP address.");
            Assert.IsTrue(
                Regex.IsMatch(result, @".+ = .+"),
                "Output should list properties as 'name = value' pairs.");
            Assert.DoesNotContain("Unhandled exception", result,
                "Output should not contain an unhandled exception.");
        }

        /// <summary>
        /// Runs the Metadata Cloud example and verifies it prints the engine
        /// metadata sections (accepted evidence keys and the property listing).
        /// </summary>
        [TestMethod]
        public void Example_Cloud_Metadata()
        {
            var (exitCode, result, errorOutput) =
                RunCloudExampleProcess(MetadataExamplePath);

            Assert.AreEqual(0, exitCode,
                $"Metadata example should exit successfully. " +
                $"Error output: {errorOutput}");

            Assert.Contains("Accepted evidence keys:", result,
                "Metadata output should list the accepted evidence keys.");
            Assert.Contains("Property -", result,
                "Metadata output should list the available properties.");
            Assert.DoesNotContain("Unhandled exception", result,
                "Output should not contain an unhandled exception.");
        }

        /// <summary>
        /// Runs a Cloud console example exactly as a user would (via
        /// <c>dotnet run</c> in the example's own directory), passing the
        /// resource key and optional custom endpoint through the same
        /// environment variables the example reads. Standard input is closed so
        /// examples that pause for a key press (guarded by
        /// <see cref="Console.IsInputRedirected"/>) do not block the test.
        /// </summary>
        private (int ExitCode, string Output, string Error) RunCloudExampleProcess(
            string examplePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                WorkingDirectory = examplePath,
                CreateNoWindow = true,
            };
            startInfo.Environment[ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR] = _resourceKey;
            var endPoint = Environment.GetEnvironmentVariable(
                ExampleUtils.CLOUD_END_POINT_ENV_VAR);
            if (string.IsNullOrWhiteSpace(endPoint) == false)
            {
                startInfo.Environment[ExampleUtils.CLOUD_END_POINT_ENV_VAR] = endPoint;
            }

            using var process = new Process { StartInfo = startInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    output.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    error.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            // Signal end-of-input so the example never waits for a key press.
            process.StandardInput.Close();

            Assert.IsTrue(process.WaitForExit(60000),
                "Cloud example should complete within 60 seconds");
            // Block until the async output handlers have flushed everything.
            process.WaitForExit();

            var result = output.ToString();
            var errorOutput = error.ToString();

            Console.WriteLine("Cloud Example Output:");
            Console.WriteLine(result);

            if (!string.IsNullOrEmpty(errorOutput))
            {
                Console.WriteLine("Cloud Example Error Output:");
                Console.WriteLine(errorOutput);
            }

            return (process.ExitCode, result, errorOutput);
        }
    }
}
