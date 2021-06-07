using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Keysight.OpenTap.ResultsViewer;

namespace OpenTap.Plugins.PluginDevelopment
{
    /// <summary>
    /// Result XY Chart example
    ///
    /// In this example we'll try to create a custom XY chart for use with the Keysight OpenTAP ResultsViewer application.
    /// 
    /// 
    /// The result charting system is designed to be very high performance and very high flexibility.
    /// Hence the API for creating custom plots is a bit cumbersome to use.
    ///
    /// To help visualizing the concept used, imaging the data shown as a big table with each different type of
    /// data being columns. (See the 'Data Table (Full)' plot chart for how this is shown.)
    ///
    /// The data is split into "plot series". Each series represents a slice of the data.
    /// The series contains indexes into each axis, which can be thought of as the columns of the data table.
    /// For any given chart, a set of axis (columns) are selected and some rows (indexes) are taken from each column.
    /// See getPoints for an example. 
    ///
    /// The filters are applied behind the scene and the result of the filtering is a list of indexes for each plot series.
    /// The indexes are used to select the rows of the table, while the axis defined decides which column.
    ///
    /// There is very little copying of the actual data. All filtering is represented by the index list.
    ///
    /// 
    /// </summary>
    [Display("XY Chart Example")]
    public class ResultXYChartExample : Keysight.OpenTap.ResultsViewer.CustomResultsViewerPlugin
    {
        // Settings can be created like when developing other types of plugins,
        // but you have to manually notify when properties has changed using PropertyNotify.
        double offsetX = 0;
        [Display("Offset X")]
        public double OffsetX
        {
            get => offsetX;
            set
            {
                offsetX = value;
                PropertyNotify(nameof(OffsetX));
            }
        }

        double offsetY;
        [Display("Offset Y")]
        
        public double OffsetY
        {
            get => offsetY;
            set
            {
                offsetY = value;
                this.PropertyNotify(nameof(OffsetY));
            } 
        }
        
        /// <summary> Called when the UI wants to clear the plot. </summary>
        public override void Clear()
        {
            canvas.Children.Clear();
        }

        /// <summary> Called when the backing data has been changed. </summary>
        public override void Invalidate()
        {
            
        }

        readonly List<AxisGroup> axisGroups = new List<AxisGroup>
        {
            
            // The axis groups are very important. They define which dimensions are allowed in the plot.
            // The tag is important for identifying the dimension within the date in the RedrawPlot method.
            new AxisGroup
            {
                //Tag = 0: First dimension. This is important later when identifying the plot data.
                Tag = 0,
                // this one we call 'X': the X axis.
                Name = "X",
                // we only accept double number types for this plot
                ValidTypes = AxisType.Double,
                // A description
                Description = "Our X/horizontal axis."
            },
            // Below is the definition of the Y axis.
            new AxisGroup { Tag = 1, Name = "Y",  ValidTypes = AxisType.Double, Description = "Our Y axis." }
        };

        public override List<AxisGroup> GetAxisGroups() => axisGroups;

        /// <summary> False in most cases. if you need all data available, return true.
        /// If this is set to return true, GetAxisGroups should return an empty list.  </summary>
        /// <returns></returns>
        public override bool NeedAllAxes() => false;

        public override bool HasLimits() =>  false;

        List<Point> getPoints(List<AxisData> AxisData, List<PlotSeries> PlotSeries)
        {
            var result = new List<Point>();
            foreach (var yAxis in AxisData.Where(x => x.Tag == 1))
            {
                foreach (var xAxis in AxisData.Where(x => x.Tag == 0))
                {
                    // then we loop across each plot series
                    // the series can be seen  as different lines in a x/y line chart.
                    foreach (var plot in PlotSeries)
                    {
                        foreach (var index in plot.Indices)
                        {
                            result.Add(new Point(xAxis.DoubleData[index], yAxis.DoubleData[index]));
                        }
                    }
                }
            }

            return result;
        }
        
        /// <summary> rebuild the plot </summary>
        /// <param name="Title"> The title of the plot. This can be shown inside the plot area.</param>
        /// <param name="AxisData">The axises selected by the user. The axises used predefined tags to separate dimensions.</param>
        /// <param name="LimitData">not included in example</param>
        /// <param name="PlotSeries">Contains the series we are showing. These are labeled indexes into our plot data</param>
        public override void RedrawPlot(string Title, List<AxisData> AxisData, List<LimitData> LimitData, List<PlotSeries> PlotSeries)
        {
            if (canvas.IsLoaded == false) return;
            
            var points = getPoints(AxisData, PlotSeries);

            // lets normalize the data so its in the center of the screen.

            // center the data vertically.
            var canvasHeight = canvas.ActualHeight;
            var canvasWidth = canvas.ActualWidth;
            
            var minx = points.Select(pt => pt.X).Min();
            var maxx = points.Select(pt => pt.X).Max();
            var miny = points.Select(pt => pt.Y).Min();
            var maxy = points.Select(pt => pt.Y).Max();

            var centerX = (maxx + minx)/2;
            var centerY = (maxy + miny)/2;
            var spanx = minx - maxx;
            if (spanx == 0) spanx = 1;
            var spany = miny - maxy;
            if (spany == 0) spany = 1;
            
            // normalization values
            var scaleX = canvasWidth /spanx;
            var scaleY = canvasHeight /spany;
            var startX = canvasWidth / 2 - centerX * scaleX;
            var startY = canvasHeight / 2 - centerY* scaleY;
            
            // now lets add the points at the right places.
            foreach (var pt in points)
            {
                var point = new Rectangle { };
                point.Width = 2;
                point.Height = 2;
                point.Fill = Brushes.White;

                // extract axis data for this index.
                Canvas.SetLeft(point, (pt.X + OffsetX)*scaleX * 0.95 + startX);
                Canvas.SetTop(point, (pt.Y + OffsetY) *scaleY * 0.95 + startY);
                this.canvas.Children.Add(point);
            }
        }

        readonly Canvas canvas = new Canvas() {Background = Brushes.Black};

        public ResultXYChartExample()
        {
            // when the canvas is loaded we need to do redraw
            canvas.Loaded += (s, e) => PropertyNotify("");
        }

        /// <summary> our control, showing the plot in the user interface</summary>
        public override FrameworkElement UserControl => canvas;

        public override void DeserializeProperties(Dictionary<string, string> Props)
        {
            if (Props.TryGetValue("OffsetX", out var offsetXStr))
                double.TryParse(offsetXStr,  out offsetX);
            if (Props.TryGetValue(nameof(OffsetY), out var offsetYStr))
                double.TryParse(offsetYStr, out offsetY);
        }

        public override Dictionary<string, string> SerializeProperties()
        {
            return new Dictionary<string, string>
            {
                {"OffsetX", OffsetX.ToString(CultureInfo.InvariantCulture)},
                {"OffsetY", OffsetY.ToString(CultureInfo.InvariantCulture)}
            };
        }
    }
}