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

namespace Examples.OnPremise.Areas;

/// <summary>
/// The result of an area calculation.
/// </summary>
/// <remarks>
/// Constructs a new instance of <see cref="Result"/>.
/// </remarks>
/// <param name="squareKms"></param>
/// <param name="geometries"></param>
/// <param name="contains"></param>
public class Result(int squareKms, int geometries, bool contains)
{

    /// <summary>
    /// Area in square kilometers rounded to nearest integer.
    /// </summary>
    public int SquareKms { get; set; } = squareKms;

    /// <summary>
    /// Number of irregular polygons that form the area.
    /// </summary>
    public int Geometries { get; set; } = geometries;

    /// <summary>
    /// True if the area contains the point passed, otherwise false.
    /// </summary>
    public bool Contains { get; set; } = contains;
}
