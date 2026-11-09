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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FiftyOne.DeviceDetection.Tests.Core
{
    /// <summary>
    /// Checks that the Device Detection sample configuration and message
    /// resources name nothing that does not exist in the source.
    /// </summary>
    [TestClass]
    public class UnusedCodeTests
    {
        private static readonly string[] SourceFolders =
            { "device-detection", "pipeline", "ip-intelligence" };

        private const string SampleConfiguration =
            "examples/device-detection/sample-configuration.json";

        private const string Designer =
            "device-detection/DeviceDetection/Messages.Designer.cs";

        /// <summary>
        /// Every builder the sample configuration names is a class in the
        /// source, so a person copying the file does not ask for a builder
        /// that cannot be found.
        /// </summary>
        [TestMethod]
        public void SampleConfigurationNamesOnlyBuildersThatExist()
        {
            var root = FindRoot();
            var json = File.ReadAllText(
                Path.Combine(root, SampleConfiguration));
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                });
            var names = document.RootElement
                .GetProperty("PipelineOptions")
                .GetProperty("Elements")
                .EnumerateArray()
                .Select(e => e.GetProperty("BuilderName").GetString())
                .ToList();
            Assert.IsNotEmpty(
                names,
                "The sample configuration names no builder.");

            var source = ReadSource(root);
            var missing = names
                .Where(n => Regex.IsMatch(
                    source,
                    @"\bclass\s+" + Regex.Escape(n) + @"\b") == false)
                .ToList();

            Assert.IsEmpty(
                missing,
                "The sample configuration names builders with no class: " +
                string.Join(", ", missing));
        }

        /// <summary>
        /// Every message in the Device Detection resources is used by some
        /// code, so none is left behind when the code that used it goes.
        /// </summary>
        [TestMethod]
        public void EveryMessageIsUsed()
        {
            var root = FindRoot();
            var designer = Path.GetFullPath(Path.Combine(root, Designer));
            var messages = Regex.Matches(
                    File.ReadAllText(designer),
                    @"internal static string (\w+)")
                .Select(m => m.Groups[1].Value)
                .ToList();
            Assert.IsNotEmpty(messages, "The designer holds no message.");

            var source = ReadSource(root, except: designer);
            var unused = messages
                .Where(m => Regex.IsMatch(source, @"\b" + m + @"\b") == false)
                .ToList();

            Assert.IsEmpty(
                unused,
                "Messages that no code uses: " + string.Join(", ", unused));
        }

        private static string ReadSource(string root, string except = null)
        {
            var files = new List<string>();
            foreach (var folder in SourceFolders)
            {
                files.AddRange(Directory.EnumerateFiles(
                    Path.Combine(root, folder),
                    "*.cs",
                    SearchOption.AllDirectories));
            }
            return string.Join(
                "\n",
                files
                    .Where(f => except == null ||
                        Path.GetFullPath(f) != except)
                    .Where(f => f.Contains(
                        Path.DirectorySeparatorChar + "obj" +
                        Path.DirectorySeparatorChar) == false)
                    .Select(File.ReadAllText));
        }

        private static string FindRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(
                    directory.FullName,
                    SampleConfiguration)))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            Assert.Fail("The repository root was not found above " +
                AppContext.BaseDirectory + ".");
            return null;
        }
    }
}
