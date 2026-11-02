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

namespace FiftyOne.IpIntelligence.Example.Tests.OnPremise;

/// <summary>
/// Test category names used with <c>[TestCategory(...)]</c> so that CI can
/// filter test runs, e.g. <c>dotnet test --filter TestCategory=Unit</c> for a
/// fast feedback loop that does not need the (large) enterprise data file, or
/// <c>--filter TestCategory=Integration</c> for the slower end-to-end checks
/// that exercise the real IP Intelligence engine.
/// </summary>
public static class TestCategories
{
    /// <summary>
    /// Fast, self-contained tests with no external dependencies (no data file,
    /// no network). Safe to run on every commit.
    /// </summary>
    public const string Unit = "Unit";

    /// <summary>
    /// Tests that build a real on-premise IP Intelligence engine against the
    /// enterprise data file (and therefore require that file to be present and
    /// are comparatively slow / memory hungry).
    /// </summary>
    public const string Integration = "Integration";
}
