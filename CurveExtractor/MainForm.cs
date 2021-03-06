﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using CurveExtractor.Structures;
using DBFilesClient.NET;

namespace CurveExtractor
{
    public partial class MainForm : Form
    {
        private List<Curve> _curves = new List<Curve>();

        public MainForm()
        {
            InitializeComponent();
        }

        private void OnLoad(object sender, EventArgs e)
        {
            CurveManager.Chart = chart1;
            chart1.ChartAreas[0].AxisX.MajorGrid.LineWidth = 0;
            chart1.ChartAreas[0].AxisY.MajorGrid.LineWidth = 0;

            chart1.ChartAreas[0].CursorX.IsUserEnabled = true;
            chart1.ChartAreas[0].CursorX.IsUserSelectionEnabled = true;
            chart1.ChartAreas[0].CursorY.IsUserEnabled = true;
            chart1.ChartAreas[0].CursorY.IsUserSelectionEnabled = true;

            chart1.ChartAreas[0].CursorX.Interval = 0;
            chart1.ChartAreas[0].CursorY.Interval = 0;
            chart1.ChartAreas[0].AxisX.ScaleView.Zoomable = true;
            chart1.ChartAreas[0].AxisX.ScrollBar.ButtonColor = Color.LightSkyBlue;
            chart1.ChartAreas[0].AxisX.ScrollBar.LineColor = Color.SkyBlue;
            chart1.ChartAreas[0].AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.ResetZoom;
            chart1.ChartAreas[0].AxisX.ScrollBar.BackColor = Color.White;
            chart1.ChartAreas[0].AxisY.ScaleView.Zoomable = true;
            chart1.ChartAreas[0].AxisY.ScrollBar.ButtonColor = Color.LightSkyBlue;
            chart1.ChartAreas[0].AxisY.ScrollBar.LineColor = Color.SkyBlue;
            chart1.ChartAreas[0].AxisY.ScrollBar.ButtonStyle = ScrollBarButtonStyles.ResetZoom;
            chart1.ChartAreas[0].AxisY.ScrollBar.BackColor = Color.White;

            chart1.ChartAreas[0].CursorX.LineColor = Color.Black;
            chart1.ChartAreas[0].CursorX.LineWidth = 1;
            chart1.ChartAreas[0].CursorX.LineDashStyle = ChartDashStyle.Dot;
            chart1.ChartAreas[0].CursorX.Interval = 0;
            chart1.ChartAreas[0].CursorY.LineColor = Color.Black;
            chart1.ChartAreas[0].CursorY.LineWidth = 1;
            chart1.ChartAreas[0].CursorY.LineDashStyle = ChartDashStyle.Dot;
            chart1.ChartAreas[0].CursorY.Interval = 0;

            checkedListBox1.Items.Add(CurveInterpolationMode.Constant);
            checkedListBox1.Items.Add(CurveInterpolationMode.Linear);
            checkedListBox1.Items.Add(CurveInterpolationMode.Cosine);
            checkedListBox1.Items.Add(CurveInterpolationMode.Bezier);
            checkedListBox1.Items.Add(CurveInterpolationMode.Bezier3);
            checkedListBox1.Items.Add(CurveInterpolationMode.Bezier4);
            checkedListBox1.Items.Add(CurveInterpolationMode.CatmullRom);

            var curvePointStore = new Storage<CurvePointEntry>("./CurvePoint.db2");
            var curveStore = new Storage<CurveEntry>("./Curve.db2");

            foreach (var curveKvp in curveStore)
            {
                var curveInfo = new Curve
                {
                    ID = curveKvp.Key,
                    Type = curveKvp.Value.Type
                };

                _curves.Add(curveInfo);
            }

            foreach (var curvePointKvp in curvePointStore)
            {
                var curveInfo = _curves.FirstOrDefault(curveEntry => curveEntry.ID == curvePointKvp.Value.CurveID);
                Debug.Assert(curveInfo != null, "Unknown curve found in CurvePoint.db2");

                curveInfo.Points.Add(curvePointKvp.Value);
                curveInfo.Sort();
            }

            foreach (var curveInfo in _curves/*.OrderBy(c => c.Points.Count)*/)
                listBox1.Items.Add(new ListBoxEntry()
                {
                    Entry = curveInfo.ID,
                    Name = $@"Curve #{curveInfo.ID} ({curveInfo.Points.Count} points)"
                });
        }

        private void OnModeSelect(object sender, ItemCheckEventArgs e)
        {
            listBox1.ClearSelected();
            listBox1.Items.Clear();

            var visibilityMask = checkedListBox1.CheckedItems.Cast<object>().Aggregate(0, (current, item) => current | 1 << (int) (CurveInterpolationMode) item);
            if (e.NewValue == CheckState.Checked)
                visibilityMask |= 1 << (int) (CurveInterpolationMode) checkedListBox1.Items[e.Index];
            else
                visibilityMask &= ~(1 << (int)(CurveInterpolationMode)checkedListBox1.Items[e.Index]);

            // e.NewValue
            foreach (var curveInfo in _curves.OrderBy(c => c.Points.Count))
            {
                var mask = 1 << (int)Curve.DetermineInterpolationMode(curveInfo);
                if ((visibilityMask & mask) != 0)
                {
                    listBox1.Items.Add(new ListBoxEntry()
                    {
                        Entry = curveInfo.ID,
                        Name = $@"Curve #{curveInfo.ID} ({curveInfo.Points.Count} points)"
                    });
                }
                else
                {
                    CurveManager.RemoveCurve(curveInfo.ID);
                }
            }
        }

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            chart1.ChartAreas[0].CursorX.SetCursorPixelPosition(e.Location, true);
            chart1.ChartAreas[0].CursorY.SetCursorPixelPosition(e.Location, true);

            if (_previousPosition.HasValue && e.Location == _previousPosition.Value)
                return;

            _tooltip.RemoveAll();
            _previousPosition = e.Location;
            var results = chart1.HitTest(e.X, e.Y, false, ChartElementType.DataPoint);
            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var result in results)
            {
                if (result.ChartElementType != ChartElementType.DataPoint)
                    continue;

                var prop = result.Object as DataPoint;
                if (prop == null)
                    continue;

                var pointXPixel = result.ChartArea.AxisX.ValueToPixelPosition(prop.XValue);
                var pointYPixel = result.ChartArea.AxisY.ValueToPixelPosition(prop.YValues[0]);

                // check if the cursor is really close to the point (3 pixels around)
                if (Math.Abs(e.X - pointXPixel) < 3 && Math.Abs(e.Y - pointYPixel) < 3)
                    _tooltip.Show("X=" + prop.XValue + ", Y=" + prop.YValues[0], chart1, e.X, e.Y - 15);
            }
        }

        private ToolTip _tooltip = new ToolTip();
        private Point? _previousPosition;

        private void OnItemSelected(object sender, ItemCheckEventArgs e)
        {
            if (e.NewValue == CheckState.Checked)
            {
                var listBoxEntry = (ListBoxEntry) listBox1.Items[e.Index];
                var curveInfo = _curves.FirstOrDefault(c => c.ID == listBoxEntry.Entry);
                if (curveInfo != null)
                    CurveManager.AddCurve(curveInfo);
            }
            else if (e.NewValue == CheckState.Unchecked)
            {
                var listBoxEntry = (ListBoxEntry)listBox1.Items[e.Index];
                CurveManager.RemoveCurve(listBoxEntry.Entry);
            }
        }
    }

    public class ListBoxEntry
    {
        public int Entry { get; set; }
        public string Name { get; set; }

        public override string ToString() => Name;
    }
}
