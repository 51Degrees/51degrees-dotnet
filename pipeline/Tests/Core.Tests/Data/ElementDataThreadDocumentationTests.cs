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

using FiftyOne.Pipeline.Core.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FiftyOne.Pipeline.Core.Tests.Data;

/// <summary>
/// The documentation of Element Data says how it may be used from several
/// threads. Each test reads the XML documentation that ships beside the
/// assembly, which is what an IDE and the API reference show.
/// </summary>
[TestClass]
public class ElementDataThreadDocumentationTests
{
    private static readonly string[] Statements =
    {
        "not intended to be written from several threads at once",
        "nor read while it is being written",
        "safe to read from several threads at once",
    };

    [TestMethod]
    public void IElementData_DocumentsUseFromSeveralThreads()
    {
        AssertDocumented(typeof(IElementData));
    }

    [TestMethod]
    public void ElementDataBase_DocumentsUseFromSeveralThreads()
    {
        AssertDocumented(typeof(ElementDataBase));
    }

    private static void AssertDocumented(Type type)
    {
        var path = Path.ChangeExtension(type.Assembly.Location, ".xml");
        Assert.IsTrue(File.Exists(path),
            $"No XML documentation at '{path}'.");
        var member = XDocument.Load(path)
            .Descendants("member")
            .SingleOrDefault(m =>
                (string)m.Attribute("name") == "T:" + type.FullName);
        Assert.IsNotNull(member, $"'{type.FullName}' is not documented.");
        var text = Regex.Replace(member.Value, @"\s+", " ");
        foreach (var statement in Statements)
        {
            Assert.IsTrue(
                text.Contains(statement, StringComparison.Ordinal),
                $"The documentation of '{type.Name}' does not say that an " +
                $"instance is '{statement}'.");
        }
    }
}
