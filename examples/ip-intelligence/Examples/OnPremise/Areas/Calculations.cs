// Ignore Spelling: wkt

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
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Valid;
using ProjNet.CoordinateSystems.Transformations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Examples.OnPremise.Areas;

/// <summary>
/// Used to work out the common values for areas in the examples.
/// 
/// As the areas involved can be quite large a grid is used to break down areas
/// into smaller areas of no more than 1 degree of latitude and longitude
/// before applying a UTM calculation to work out the area. The area of these
/// smaller areas are then combined to provide the result.
/// 
/// This approach handles the differences in area calculation nearer the
/// equator or the poles.
/// </summary>
public static class Calculations
{
    /// <summary>
    /// Parses the WKT string into geometries.
    /// </summary>
    private static readonly WKTReader _wktReader = new();

    /// <summary>
    /// Parsed geometry and area in square meters for each WKT string seen,
    /// so that an area is only worked out once. When the WKT can not be
    /// measured the geometry is null and the error explains why, so that a
    /// bad area is only examined once however many records refer to it.
    /// </summary>
    private static readonly ConcurrentDictionary<
        string,
        (Geometry Geometry, double Area, string Error)> _wktAreas = new();

    /// <summary>
    /// Number of WKT strings with a cached area.
    /// </summary>
    public static int CachedWktCount => _wktAreas.Count;

    /// <summary>
    /// A grid of latitude and longitude rectangles. Used to work out the
    /// geographic area using individual polygons no larger than 1 unit of 
    /// latitude and longitude to avoid distortions due to the differences in
    /// calculation near the equator or the poles.
    /// </summary>
    private static readonly Rectangle[][] _grid = CreateGrid();

    /// <summary>
    /// Returns the result for the WKT string, and geographic point.
    /// </summary>
    /// <param name="wkt">
    /// WKT format geometric area(s).
    /// </param>
    /// <param name="latitude">
    /// Of the point being tested for inclusion in the geographic area.
    /// </param>
    /// <param name="longitude">
    /// Of the point being tested for inclusion in the geographic area.
    /// </param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException">
    /// The WKT can not be parsed, or its area can not be worked out.
    /// </exception>
    public static Result GetAreas(
        string wkt, 
        double latitude,
        double longitude)
    {
        var (geo, area, error) = _wktAreas.GetOrAdd(wkt, Measure);
        if (error != null)
        {
            throw new InvalidDataException(error);
        }
        if (geo != null)
        {
            return new(
                (int)Math.Round(area / 1_000_000),
                geo.NumGeometries,
                geo.Contains(new Point(longitude, latitude)));
        }
        return new(0,0,false);
    }

    /// <summary>
    /// Parses and measures the WKT. Any failure is returned as the error
    /// rather than thrown so that it is cached with the WKT.
    /// </summary>
    /// <param name="wkt">
    /// WKT format geometric area(s).
    /// </param>
    /// <returns>
    /// The geometry and area in square meters, or the reason the WKT can
    /// not be measured.
    /// </returns>
    private static (Geometry, double, string) Measure(string wkt)
    {
        Geometry parsed;
        try
        {
            parsed = _wktReader.Read(wkt);
        }
        catch (Exception ex)
        {
            return (null, 0, $"WKT could not be parsed. {ex.Message}");
        }
        if (parsed == null)
        {
            return (null, 0, null);
        }

        try
        {
            return (parsed, GetAreas(parsed), null);
        }
        catch (Exception ex)
        {
            // Many areas that are not strictly valid, for example where a
            // ring touches itself, can still be measured, so validity is only
            // checked to explain a failure.
            var root = ex.GetBaseException();
            var validation = new IsValidOp(parsed).ValidationError;
            return (
                null,
                0,
                $"Area could not be worked out. {root.GetType().Name}: " +
                root.Message +
                (validation == null
                    ? ""
                    : $". Geometry is not valid. {validation.Message} at " +
                        $"{validation.Coordinate}"));
        }
    }

    /// <summary>
    /// Returns the result for the geometric area, and geographic point.
    /// </summary>
    /// <param name="geo">
    /// Geometric area(s).
    /// </param>
    /// <param name="latitude">
    /// Of the point being tested for inclusion in the geographic area.
    /// </param>
    /// <param name="longitude">
    /// Of the point being tested for inclusion in the geographic area.
    /// </param>
    /// <returns></returns>
    public static Result GetAreas(
        Geometry geo,
        double latitude, 
        double longitude)
    {
        // True if the area contains the point. This must be done before
        // the geo instance is manipulated by GetAreas and converted to
        // different coordinate units.
        var contains = geo.Contains(new Point(longitude, latitude));
        var area = GetAreas(geo);
        return new(
            // The total area in square kms.
            (int)Math.Round(area / 1_000_000),
            // Number of polygons in the area.
            geo.NumGeometries,
            // Whether the geographic area contains the point.
            contains);
    }

    private static double GetAreas(Geometry geo)
    {
        var area = 0.0;
        if (geo.NumGeometries > 1)
        {
            for (var i = 0; i < geo.NumGeometries; i++)
            {
                area += GetAreas(geo.GetGeometryN(i));
            }
        }
        else if (geo.IsEmpty == false)
        {
            area += GetArea(geo);
        }

        // Calculate area in square meters and convert to square kilometers
        return area;
    }

    private static double GetArea(Geometry geo)
    {
        var area = 0.0;
        foreach(var rectangle in GetRectangles(geo))
        {
            try 
            { 
                area += GetArea(geo, rectangle);
            }
            catch(TopologyException)
            {
                // In rare situations the intersection between the rectangle
                // and the geometric area results in an exception. When this
                // happens fall back to calculating the area with the
                // rectangle's transformation not using the intersection or
                // other rectangles.
                area = GetArea(geo, geo, rectangle.Transformation);
                break;
            }
        }
        return area;
    }

    private static double GetArea(Geometry geo, Rectangle rectangle)
    {
        var area = 0.0;
        // The rectangle might relate to an area that doesn't intersect the 
        // geometric shape. For example; when the shape does not include a 
        // grid rectangle.
        if (geo.Intersects(rectangle.Polygon))
        {
            var intersect = geo.Intersection(rectangle.Polygon);
            if (intersect.NumGeometries == 1)
            {
                if (intersect.Area > 0)
                {
                    area = GetArea(geo, intersect, rectangle.Transformation);
                }
            }
            else
            {
                for (var i = 0; i < intersect.NumGeometries; i++)
                {
                    if (intersect.GetGeometryN(i).Area > 0)
                    {
                        area += GetArea(
                            geo,
                            intersect.GetGeometryN(i),
                            rectangle.Transformation);
                    }
                }
            }
        }
        return area;
    }

    private static double GetArea(
        Geometry geo, 
        Geometry intersect, 
        ICoordinateTransformation transformation)
    {
        try
        {
            // Re-project the intersecting polygon to the UTM
            // coordinate system.
            var transformedPolygon = TransformGeometry(
                intersect,
                transformation.MathTransform);

            // Return the area in square meters.
            return transformedPolygon.Area;
        }
        catch (ArgumentException ex)
        {
            throw new Exception(geo.ToText(), ex);
        }
    }

    private static Geometry TransformGeometry(
        Geometry geometry,
        MathTransform transform)
    {
        // Transform each polygon of a collection on its own.
        if (geometry is not Polygon polygon)
        {
            var parts = new List<Geometry>();
            for (var i = 0; i < geometry.NumGeometries; i++)
            {
                var part = geometry.GetGeometryN(i);
                if (part.Dimension == Dimension.Surface)
                {
                    parts.Add(TransformGeometry(part, transform));
                }
            }
            return geometry.Factory.BuildGeometry(parts);
        }

        // The outer boundary and each hole are transformed as separate rings.
        var holes = new LinearRing[polygon.NumInteriorRings];
        for (var i = 0; i < holes.Length; i++)
        {
            holes[i] = TransformRing(
                (LinearRing)polygon.GetInteriorRingN(i), transform);
        }
        return polygon.Factory.CreatePolygon(
            TransformRing((LinearRing)polygon.ExteriorRing, transform),
            holes);
    }

    private static LinearRing TransformRing(
        LinearRing ring,
        MathTransform transform)
    {
        var source = ring.Coordinates;
        var coordinates = new Coordinate[source.Length];

        for (int i = 0; i < coordinates.Length; i++)
        {
            var transformed =
                transform.Transform([
                    source[i].X,
                    source[i].Y]);
            coordinates[i] = new Coordinate(
                transformed[0],
                transformed[1]);
        }

        return ring.Factory.CreateLinearRing(coordinates);
    }

    /// <summary>
    /// Constructs a grid that covers the world for each latitude and longitude
    /// rectangle.
    /// </summary>
    /// <returns></returns>
    private static Rectangle[][] CreateGrid()
    {
        var grid = new Rectangle[360][];
        var geometryFactory = new GeometryFactory();
        for (int x = -180; x < 180; x++)
        {
            grid[x + 180] = new Rectangle[180];
            for (int y = -90; y < 90; y++)
            {
                grid[x + 180][y + 90] = new Rectangle(
                    geometryFactory.CreatePolygon([
                        new Coordinate(x, y),
                        new Coordinate(x + 1, y),
                        new Coordinate(x + 1, y + 1),
                        new Coordinate(x, y + 1),
                        new Coordinate(x, y)]));
            }
        }
        return grid;
    }

    private static IEnumerable<Rectangle> GetRectangles(Geometry source)
    {
        // Work out the lowest and highest latitude and longitudes for the
        // source.
        double xa = source.Coordinate.X;
        double xb = source.Coordinate.X;
        double ya = source.Coordinate.Y;
        double yb = source.Coordinate.Y;
        for (var i = 1; i < source.Coordinates.Length; i++)
        {
            var c = source.Coordinates[i];
            if (c.X < xa) xa = c.X;
            if (c.X > xb) xb = c.X;
            if (c.Y < ya) ya = c.Y;
            if (c.Y > yb) yb = c.Y;
        }

        // Return all the rectangles from the grid that intersect with the
        // polygon provided.
        for (var x = (int)Math.Floor(xa); x < (int)Math.Ceiling(xb); x++)
        {
            for (var y = (int)Math.Floor(ya); y < (int)Math.Ceiling(yb); y++)
            {
                yield return _grid[x+180][y+90];
            }
        }
    }
}
