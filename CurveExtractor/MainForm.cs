﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                var curveInfo = new Curve()
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

            foreach (var curveInfo in _curves.OrderBy(c => c.Points.Count))
                listBox1.Items.Add(new ListBoxEntry()
                {
                    Entry = curveInfo.ID,
                    Name = $@"Curve #{curveInfo.ID} ({curveInfo.Points.Count} points)"
                });
        }

        private void DoPlot(object sender, EventArgs e)
        {
            var selectedItem = listBox1.SelectedItem as ListBoxEntry;
            if (selectedItem == null)
                return;

            var curveInfo = _curves.FirstOrDefault(c => c.ID == selectedItem.Entry);
            if (curveInfo == null)
                return;

            if (curveInfo.Points.Count == 0)
                return;

            var minBound = curveInfo.Points.First().X;
            var maxBound = curveInfo.Points.Last().X;

            chart1.Series.Clear();
            chart1.Series.Add("Line");
            chart1.Series.Add("Points");

            for (; minBound <= maxBound; minBound += 0.1f)
                chart1.Series["Line"].Points.AddXY(minBound, curveInfo.GetValueAt(minBound));

            chart1.Series["Line"].ChartType = SeriesChartType.Line;

            for (var i = 0; i < curveInfo.Points.Count; ++i)
                chart1.Series["Points"].Points.AddXY(curveInfo.Points[i].X, curveInfo.Points[i].Y);

            chart1.Series["Points"].ChartType = SeriesChartType.Point;
            chart1.Series["Points"].Color = Color.DarkBlue;
            chart1.Series["Points"].BorderWidth = 3;

            chart1.ChartAreas[0].AxisX.MajorGrid.LineWidth = 0;
            chart1.ChartAreas[0].AxisY.MajorGrid.LineWidth = 0;

            label2.Text = Curve.DetermineInterpolationMode(curveInfo).ToString();
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
                    listBox1.Items.Add(new ListBoxEntry()
                    {
                        Entry = curveInfo.ID,
                        Name = $@"Curve #{curveInfo.ID} ({curveInfo.Points.Count} points)"
                    });
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