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

using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using System;

namespace Examples.OnPremise.Areas;

public class Rectangle
{
    /// <summary>
    /// Used to handle area calculations that are aware the earth is not a
    /// sphere and has more complex mappings between WGS84 latitudes and
    /// longitudes are geographic areas.
    /// </summary>
    private static readonly CoordinateTransformationFactory
        _transformFactory = new();

    public ICoordinateTransformation Transformation { get; private set; }

    public Polygon Polygon { get; private set; }

    public Rectangle(Polygon polygon)
    {
        Polygon = polygon;
        Transformation = CreateTransform(
            polygon.InteriorPoint.X,
            polygon.InteriorPoint.Y);
    }

    public static ICoordinateTransformation CreateTransform(double x, double y)
    {
        // Create UTM projected coordinate system for a specific zone
        var utmZone = (int)Math.Floor((x + 180) / 6) + 1;
        var isNorthernHemisphere = y >= 0;

        // Create a coordinate transformation
        return _transformFactory.CreateFromCoordinateSystems(
            // Define the source (WGS84) and target (Projected) coordinate
            // systems
            GeographicCoordinateSystem.WGS84,
            // Adjust UTM zone and hemisphere as needed
            ProjectedCoordinateSystem.WGS84_UTM(
                utmZone,
                isNorthernHemisphere));
    }
}
