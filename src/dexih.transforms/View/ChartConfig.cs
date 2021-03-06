using System.Runtime.Serialization;


namespace dexih.repository
{

    // [JsonConverter(typeof(StringEnumConverter))]
    public enum EChartType 
    {
        BarVertical = 1,
        BarHorizontal,
        // BarVertical2D,
        // BarHorizontal2D,
        // BarVerticalStacked,
        // BarHorizontalStacked,
        // BarVerticalNormalized,
        // BarHorizontalNormalized,
        Pie,
        // PieAdvanced,
        // PieGrid,
        Line,
        // Area,
        Polar,
        // AreaStacked,
        // AreaNormalized,
        Scatter,
        Error,
        Bubble,
        // ForceDirected,
        // HeatMap,
        // TreeMap,
        // Cards,
        // Gauge,
        // LinearGauge,
        Map,
        BarLineCombo,
        Radar
    }

    public enum ELineCurve
    {
        Straight,
        Bezier,
        Monotone,
    }

    [DataContract]
    public class ChartSeries
    {
        [DataMember(Order = 0)]
        public string Column { get; set; }
        
        [DataMember(Order = 1)]
        public bool LineChart { get; set; } = false;
        
        [DataMember(Order = 2)]
        public string Color { get; set; } = null;
        
        [DataMember(Order = 3)]
        public bool AlternateAxis { get; set; } = false;
    }
    
    [DataContract]
    public class ChartConfig
    {
      
        /// <summary>
        /// Column containing the labels
        /// </summary>
        [DataMember(Order = 0)]
        public string LabelColumn { get; set; }

        [DataMember(Order = 1)]
        public ChartSeries[] SeriesColumns { get; set; }

        [DataMember(Order = 2)]
        public string PivotColumn { get; set; }

        [DataMember(Order = 4)]
        public string XColumn { get; set; }

        [DataMember(Order = 5)]
        public string YColumn { get; set; }

        [DataMember(Order = 6)]
        public string MinColumn { get; set; }

        [DataMember(Order = 7)]
        public string MaxColumn { get; set; }

        [DataMember(Order = 8)]
        public string RadiusColumn { get; set; }

        [DataMember(Order = 9)]
        public string LatitudeColumn { get; set; }

        [DataMember(Order = 10)]
        public string LongitudeColumn { get; set; }

        [DataMember(Order = 11)] 
        public EChartType ChartType { get; set; } = EChartType.BarVertical;

        [DataMember(Order = 12)] 
        public string ColorScheme { get; set; } = "natural";

        // [DataMember(Order = 13)]
        // public bool ShowGradient { get; set; }

        [DataMember(Order = 14)] public bool ShowXAxis { get; set; } = true;

        [DataMember(Order = 15)]
        public bool ShowYAxis { get; set; } = true;

        [DataMember(Order = 16)]
        public bool ShowLegend { get; set; }

        [DataMember(Order = 17)] 
        public string LegendPosition { get; set; } = "bottom";

        [DataMember(Order = 18)]
        public bool ShowXAxisLabel { get; set; } = true;

        [DataMember(Order = 19)]
        public bool ShowYAxisLabel { get; set; } = true;

        [DataMember(Order = 20)]
        public bool ShowGridLines { get; set; }

        [DataMember(Order = 21)]
        public string XAxisLabel { get; set; }

        [DataMember(Order = 22)]
        public string YAxisLabel { get; set; }

        [DataMember(Order = 23)]
        public double? XScaleMax { get; set; }

        [DataMember(Order = 24)]
        public double? XScaleMin { get; set; }

        [DataMember(Order = 25)]
        public double? YScaleMax { get; set; }

        [DataMember(Order = 26)]
        public double? YScaleMin { get; set; }

        [DataMember(Order = 27)]
        public bool AutoScale { get; set; } = true;

        // pie charts only
        [DataMember(Order = 28)]
        public int CutOutPercentage { get; set; }
        
        // line charts only
        [DataMember(Order = 29)]
        public bool Fill { get; set; }
        
        /// <summary>
        /// Include a separate axis for combo charts.
        /// </summary>
        [DataMember(Order = 30)]
        public bool SeparateAxis { get; set; }

        [DataMember(Order = 31)] 
        public ELineCurve LineCurve { get; set; } = ELineCurve.Bezier;
        
        /// <summary>
        /// draw gaps between lines when there are null values.
        /// </summary>
        [DataMember(Order = 32)] 
        public bool SpanGaps { get; set; } = false;
        
        [DataMember(Order = 33)] 
        public bool ShowDataLabel { get; set; } = false;

        [DataMember(Order = 34)] 
        public bool ShowDataValue { get; set; } = false;

        [DataMember(Order = 35)] 
        public string LabelAnchor { get; set; } = "center";

        [DataMember(Order = 36)] 
        public string LabelAlign { get; set; } = "center";

        [DataMember(Order = 37)] 
        public bool SingleBarColor { get; set; } = true;

        [DataMember(Order = 38)]
        public string MultiGridColumn { get; set; } // column when laying out a grid of charts
        
        [DataMember(Order = 39)]
        public string YAxisLabelRight { get; set; }
        

    }
    
}