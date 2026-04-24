using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

public class RotatedRectIntersection
{
    private static List<PointF> GetRotatedRectVertices(RotatedRect rect)
    {
        return rect.GetVertices().ToList();
    }


    private static float DotProduct(PointF p1, PointF p2)
    {
        return p1.X * p2.X + p1.Y * p2.Y;
    }


    private static (float min, float max) ProjectPolygon(List<PointF> polygon, PointF axis)
    {
        float min = DotProduct(polygon[0], axis);
        float max = min;

        foreach (var point in polygon.Skip(1))
        {
            float projection = DotProduct(point, axis);
            min = Math.Min(min, projection);
            max = Math.Max(max, projection);
        }

        return (min, max);
    }

    public static bool IsIntersect(RotatedRect rect1, RotatedRect rect2)
    {
        List<PointF> vertices1 = GetRotatedRectVertices(rect1);
        List<PointF> vertices2 = GetRotatedRectVertices(rect2);
        List<PointF> edges1 = GetEdges(vertices1);
        List<PointF> edges2 = GetEdges(vertices2);

        foreach (var edge in edges1.Concat(edges2))
        {
            PointF axis = new PointF(-edge.Y, edge.X); 

            var projection1 = ProjectPolygon(vertices1, axis);
            var projection2 = ProjectPolygon(vertices2, axis);

            if (projection1.max < projection2.min || projection2.max < projection1.min)
            {
                return false;
            }
        }
        return true;
    }

    private static List<PointF> GetEdges(List<PointF> vertices)
    {
        List<PointF> edges = new List<PointF>();
        for (int i = 0; i < vertices.Count; i++)
        {
            int next = (i + 1) % vertices.Count;
            PointF edge = new PointF(vertices[next].X - vertices[i].X, vertices[next].Y - vertices[i].Y);
            edges.Add(edge);
        }
        return edges;
    }

   
}