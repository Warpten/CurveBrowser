using System;
using System.Collections.Generic;
using CurveExtractor.Structures;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;

namespace CurveExtractor
{
    public sealed class Curve
    {
        public int ID { get; set; }
        public byte Type { get; set; }

        public List<CurvePointEntry> Points { get; private set; } = new List<CurvePointEntry>(); 

        public void Sort()
        {
            Points = Points.OrderBy(a => a.Index).ToList();
        }

        public float GetValueAt(float x)
        {
            switch (DetermineInterpolationMode(this))
            {
                case CurveInterpolationMode.Linear:
                {
                    var pointIndex = 0;
                    while (pointIndex < Points.Count && Points[pointIndex].X <= x)
                        ++pointIndex;

                    if (pointIndex == 0)
                        return Points[0].Y;

                    if (pointIndex >= Points.Count)
                        return Points.Last().Y;

                    var xDiff = Points[pointIndex].X - Points[pointIndex - 1].X;
                    if (Math.Abs(xDiff) < 1.0E-5f)
                        return Points[pointIndex].Y;

                    return (x - Points[pointIndex - 1].X) / xDiff * (Points[pointIndex].Y - Points[pointIndex - 1].Y) + Points[pointIndex - 1].Y;
                }
                case CurveInterpolationMode.Cosine:
                {
                    var pointIndex = 0;
                    while (pointIndex < Points.Count && Points[pointIndex].X <= x)
                        ++pointIndex;

                    if (pointIndex == 0)
                        return Points[0].Y;

                    if (pointIndex >= Points.Count)
                        return Points.Last().Y;

                    var xDiff = Points[pointIndex].X - Points[pointIndex - 1].X;
                    if (Math.Abs(xDiff) < 1.0E-5f)
                        return Points[pointIndex].Y;

                    return (float)((Points[pointIndex].Y - Points[pointIndex - 1].Y) * (1.0f - Math.Cos((x - Points[pointIndex - 1].X) / xDiff * Math.PI)) * 0.5f) + Points[pointIndex - 1].Y;
                }
                case CurveInterpolationMode.CatmullRom:
                {
                    var pointIndex = 1;
                    while (pointIndex < Points.Count && Points[pointIndex].X <= x)
                        ++pointIndex;

                    if (pointIndex == 1)
                        return Points[1].Y;

                    if (pointIndex >= Points.Count - 1)
                        return Points[Points.Count - 2].Y;

                    var xDiff = Points[pointIndex].X - Points[pointIndex - 1].X;

                    var mu = (x - Points[pointIndex - 1].X) / xDiff;
                    var a0 = -0.5f * Points[pointIndex - 2].Y + 1.5f * Points[pointIndex - 1].Y - 1.5f * Points[pointIndex].Y + 0.5f * Points[pointIndex + 1].Y;
                    var a1 = Points[pointIndex - 2].Y - 2.5f * Points[pointIndex - 1].Y + 2.0f * Points[pointIndex].Y - 0.5f * Points[pointIndex + 1].Y;
                    var a2 = -0.5f * Points[pointIndex - 2].Y + 0.5f * Points[pointIndex].Y;
                    var a3 = Points[pointIndex - 1].Y;

                    return a0 * mu * mu * mu + a1 * mu * mu + a2 * mu + a3;
                }
                case CurveInterpolationMode.Bezier3:
                {
                    var xDiff = Points[2].X - Points[0].X;
                    if (Math.Abs(xDiff) < 1.0E-5f)
                        return Points[1].Y;

                    var mu = (x - Points[0].X) / xDiff;
                    return (1.0f - mu) * (1.0f - mu) * Points[0].Y + (1.0f - mu) * 2.0f * mu * Points[1].Y + mu * mu * Points[2].Y;
                }
                case CurveInterpolationMode.Bezier4:
                {
                    var xDiff = Points[3].X - Points[0].X;
                    if (Math.Abs(xDiff) < 1.0E-5f)
                        return Points[1].Y;

                    var mu = (x - Points[0].X) / xDiff;
                    return (1.0f - mu) * (1.0f - mu) * (1.0f - mu) * Points[0].Y
                        + 3.0f * mu * (1.0f - mu) * (1.0f - mu) * Points[1].Y
                        + 3.0f * mu * mu * (1.0f - mu) * Points[2].Y
                        + mu * mu * mu * Points[3].Y;
                }
                case CurveInterpolationMode.Bezier:
                {
                    var xDiff = Points.Last().X - Points[0].X;
                    if (Math.Abs(xDiff) < 1.0E-5f)
                        return Points.Last().Y;

                    var tmp = new List<float>(Points.Count);
                    tmp.AddRange(Points.Select(pointInfo => pointInfo.Y));

                    var mu = (x - Points[0].X) / xDiff;
                    for (var i = Points.Count - 1; i > 0; --i)
                    {
                        for (var k = 0; k < i; ++k)
                        {
                            var val = tmp[k] + mu * (tmp[k + 1] - tmp[k]);
                            tmp[k] = val;
                        }
                        --i;
                    }
                    return tmp[0];
                }
                case CurveInterpolationMode.Constant:
                    return Points[0].Y;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static CurveInterpolationMode DetermineInterpolationMode(Curve curve)
        {
            switch (curve.Type)
            {
                case 1:
                    return curve.Points.Count < 4 ? CurveInterpolationMode.Cosine : CurveInterpolationMode.CatmullRom;
                case 2:
                    switch (curve.Points.Count)
                    {
                        case 1:
                            return CurveInterpolationMode.Constant;
                        case 2:
                            return CurveInterpolationMode.Linear;
                        case 3:
                            return CurveInterpolationMode.Bezier3;
                        case 4:
                            return CurveInterpolationMode.Bezier4;
                    }
                    return CurveInterpolationMode.Bezier;
                case 3:
                    return CurveInterpolationMode.Cosine;
            }
            return curve.Points.Count != 1 ? CurveInterpolationMode.Linear : CurveInterpolationMode.Constant;
        }
    }

    public enum CurveInterpolationMode : byte
    {
        Linear = 1,
        Cosine = 2,
        CatmullRom = 3,
        Bezier3 = 4,
        Bezier4 = 5,
        Bezier = 6,
        Constant = 7,
    }

    public static class CurveManager
    {
        public static Chart Chart { private get; set; }

        public static void AddCurve(Curve curve)
        {
            if (Chart == null)
                return;

            var serie = Chart.Series.Add($"#{curve.ID}");
            var minBound = curve.Points.First().X;
            var maxBound = curve.Points.Last().X;
            var step = (maxBound - minBound) / (curve.Points.Count * 30);

            for (; minBound < maxBound; minBound += step)
                serie.Points.AddXY(minBound, curve.GetValueAt(minBound));

            serie.BorderWidth = 2;
            serie.ChartType = SeriesChartType.Line;
            serie.IsVisibleInLegend = true;

            // Also load points for display - but don't display them in the legend
            var pointSerie = Chart.Series.Add($"#{curve.ID}P");
            foreach (var point in curve.Points)
                pointSerie.Points.AddXY(point.X, curve.GetValueAt(point.X));

            pointSerie.BorderWidth = 3;
            pointSerie.MarkerStyle = MarkerStyle.Circle;
            pointSerie.ChartType = SeriesChartType.Point;
            pointSerie.IsVisibleInLegend = false;
        }

        public static void RemoveCurve(int curveID)
        {
            if (Chart == null)
                return;

            Chart.Series.Remove(Chart.Series.FindByName($"#{curveID}"));
            Chart.Series.Remove(Chart.Series.FindByName($"#{curveID}P"));
        }

        public static void Clear()
        {
            Chart?.Series.Clear();
        }
    }
}
