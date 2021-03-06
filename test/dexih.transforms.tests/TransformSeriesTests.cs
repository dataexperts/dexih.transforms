using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using dexih.functions;
using dexih.functions.BuiltIn;
using dexih.functions.Parameter;
using dexih.functions.Query;
using dexih.transforms.Mapping;
using Dexih.Utils.DataType;
using Xunit;

namespace dexih.transforms.tests
{
    public class TransformSeriesTests
    {
        private readonly string _seriesFunctions = typeof(SeriesFunctions<double>).FullName;

        [Fact]
        public async Task SeriesTests1()
        {
            var table = new Table("test", 0,
                new TableColumn("StringColumn", ETypeCode.String, EDeltaType.NaturalKey),
                new TableColumn("IntColumn", ETypeCode.Int32, EDeltaType.NaturalKey),
                new TableColumn("DecimalColumn", ETypeCode.Decimal, EDeltaType.NaturalKey),
                new TableColumn("DateColumn", ETypeCode.DateTime, EDeltaType.NaturalKey),
                new TableColumn("SortColumn", ETypeCode.Int32, EDeltaType.TrackingField)
            );

            // data with gaps in the date sequence.
            table.AddRow("value01", 1, 1.1, Convert.ToDateTime("2015/01/01"), 10 );
            table.AddRow("value02", 2, 2.1, Convert.ToDateTime("2015/01/02"), 9 );
            table.AddRow("value05", 5, 5.1, Convert.ToDateTime("2015/01/05"), 6 );
            table.AddRow("value06", 6, 6.1, Convert.ToDateTime("2015/01/06"), 5 );
            table.AddRow("value07", 7, 7.1, Convert.ToDateTime("2015/01/07"), 4 );
            table.AddRow("value09", 9, 9.1, Convert.ToDateTime("2015/01/09"), 2 );
            table.AddRow("value10", 10, 10.1, Convert.ToDateTime("2015/01/10"), 1);

            var source = new ReaderMemory(table, new Sorts() { new Sort("StringColumn") } );
            source.Reset();

            var mappings = new Mappings(false);

            var mavg = Functions.GetFunction(_seriesFunctions, nameof(SeriesFunctions<double>.MovingSeriesAverage), Helpers.BuiltInAssembly).GetTransformFunction(typeof(double));
            
            var parameters = new Parameters
            {
                Inputs = new Parameter[]
                {
                    new ParameterColumn("IntColumn", ETypeCode.Double),
                    new ParameterValue("Aggregate", ETypeCode.Unknown, EAggregate.Sum), 
                },
                ResultInputs = new Parameter[]
                {
                    new ParameterValue("PreCount", ETypeCode.Int32, 3),
                    new ParameterValue("PostCount", ETypeCode.Int32, 3)
                },
                ResultReturnParameters = new List<Parameter> { new ParameterOutputColumn("MAvg", ETypeCode.Double)}
            };
            
            mappings.Add(new MapFunction(mavg, parameters, EFunctionCaching.NoCache));
            mappings.Add(new MapSeries(new TableColumn("DateColumn"), ESeriesGrain.Day, 1, true, null, null, 0));
            
            var transformGroup = new TransformSeries(source, mappings);
            await transformGroup.Open();
            
            Assert.Equal(2, transformGroup.FieldCount);

            var counter = 0;
            double[] mAvgExpectedValues = { 0.75, 1.6, 2.33, 3, 2.86, 3.86, 5.29, 6.17, 6.4, 6.5 };
            string[] expectedDates = { "2015/01/01", "2015/01/02", "2015/01/03", "2015/01/04", "2015/01/05", "2015/01/06", "2015/01/07", "2015/01/08", "2015/01/09", "2015/01/10" };
            while (await transformGroup.ReadAsync())
            {
                Assert.Equal(mAvgExpectedValues[counter], Math.Round((double)transformGroup["MAvg"], 2));
                Assert.Equal(Convert.ToDateTime(expectedDates[counter]), transformGroup["DateColumn"]);
                counter = counter + 1;
            }
            Assert.Equal(10, counter);
        }
        
        [Fact]
        public async Task Group_SeriesTests_DuplicateRows()
        {
            var table = new Table("test", 0,
                new TableColumn("StringColumn", ETypeCode.String, EDeltaType.NaturalKey),
                new TableColumn("IntColumn", ETypeCode.Int32, EDeltaType.NaturalKey),
                new TableColumn("DecimalColumn", ETypeCode.Decimal, EDeltaType.NaturalKey),
                new TableColumn("DateColumn", ETypeCode.DateTime, EDeltaType.NaturalKey),
                new TableColumn("SortColumn", ETypeCode.Int32, EDeltaType.TrackingField)
            );

            // data with duplicates
            table.AddRow("value01", 2, 1.1, Convert.ToDateTime("2015/01/01"), 10 );
            table.AddRow("value02", 2, 2.1, Convert.ToDateTime("2015/01/02"), 9 );
            table.AddRow("value02", 2, 2.1, Convert.ToDateTime("2015/01/02"), 9 );

            var source = new ReaderMemory(table, new Sorts() { new Sort("StringColumn") } );
            source.Reset();

            var mappings = new Mappings(false);

            var mavg = Functions.GetFunction(
                typeof(SeriesFunctions<Double>).FullName, 
                nameof(SeriesFunctions<Double>.MovingSeriesAverage), 
                Helpers.BuiltInAssembly).GetTransformFunction(typeof(Double));
            
            var parameters = new Parameters
            {
                Inputs = new Parameter[]
                {
                    new ParameterColumn("IntColumn", ETypeCode.Double),
                    new ParameterValue("Aggregate", ETypeCode.Unknown, EAggregate.Sum), 
                },
                ResultInputs = new Parameter[]
                {
                    new ParameterValue("PreCount", ETypeCode.Int32, 3),
                    new ParameterValue("PostCount", ETypeCode.Int32, 3)
                },
                ResultReturnParameters = new List<Parameter> { new ParameterOutputColumn("MAvg", ETypeCode.Double)}
            };
            
            mappings.Add(new MapFunction(mavg, parameters, EFunctionCaching.NoCache));
            mappings.Add(new MapSeries(new TableColumn("DateColumn"), ESeriesGrain.Day, 1, false, null, null, 0));
            
            var transformGroup = new TransformSeries(source, mappings);
            await transformGroup.Open();
            
            var counter = 0;
            int[] mAvgExpectedValues = { 3, 3 };
            string[] expectedDates = { "2015/01/01", "2015/01/02",  };
            while (await transformGroup.ReadAsync())
            {
                Assert.Equal(mAvgExpectedValues[counter], Math.Round((double)transformGroup["MAvg"], 2));
                Assert.Equal(Convert.ToDateTime(expectedDates[counter]), transformGroup["DateColumn"]);
                counter = counter + 1;
            }
            Assert.Equal(2, counter);        
        }

        [Fact]
        public async Task Group_SeriesTests2()
        {
            var source = Helpers.CreateSortedTestData();

            //add a row to test highest since.
            source.DataTable.AddRow(new object[] { "value11", 5, 10.1, Convert.ToDateTime("2015/01/11"), 1, new[] { 1, 1 } });
            source.Reset();

            var mappings = new Mappings(true);

            var mavg = Functions.GetFunction(_seriesFunctions, nameof(SeriesFunctions<double>.MovingSeriesAverage), Helpers.BuiltInAssembly).GetTransformFunction(typeof(double));
            
            var parameters = new Parameters
            {
                Inputs = new Parameter[]
                {
                    new ParameterColumn("IntColumn", ETypeCode.Double),
                    new ParameterValue("Aggregate", ETypeCode.Unknown, EAggregate.Sum), 
                },
                ResultInputs = new Parameter[]
                {
                    new ParameterValue("PreCount", ETypeCode.Int32, 3),
                    new ParameterValue("PostCount", ETypeCode.Int32, 3)
                },
                ResultReturnParameters = new List<Parameter> { new ParameterOutputColumn("MAvg", ETypeCode.Double)}
            };
            
            mappings.Add(new MapFunction(mavg, parameters, EFunctionCaching.NoCache));
            
            var highest = Functions.GetFunction(_seriesFunctions, nameof(SeriesFunctions<double>.HighestSince), Helpers.BuiltInAssembly).GetTransformFunction(typeof(double));
            parameters = new Parameters
            {
                Inputs = new Parameter[]
                {
                    new ParameterColumn("IntColumn", ETypeCode.Double),
                    new ParameterValue("Aggregate", ETypeCode.Unknown, EAggregate.Sum), 
                },
                ResultReturnParameters = new List<Parameter>
                {
                    new ParameterOutputColumn("CountBank", ETypeCode.Int32), 
                    new ParameterOutputColumn("Value", ETypeCode.Double), 
                    new ParameterOutputColumn("SeriesItem", ETypeCode.DateTime)
                }
            };
            mappings.Add(new MapFunction(highest, parameters, EFunctionCaching.NoCache));
            mappings.Add(new MapSeries(new TableColumn("DateColumn"), ESeriesGrain.Day, 1, false, null, null, 0));

            var transformGroup = new TransformSeries(source, mappings);
            await transformGroup.Open();
            Assert.Equal(10, transformGroup.FieldCount);

            var counter = 0;
            double[] mAvgExpectedValues = { 2.5, 3, 3.5, 4, 5, 6, 7, 7.14, 7.5, 7.8, 8 };
            string[] highestExpectedValues = { "2015/01/01", "2015/01/02", "2015/01/03", "2015/01/04", "2015/01/05", "2015/01/06", "2015/01/07", "2015/01/08", "2015/01/09", "2015/01/10", "2015/01/10" };
            while (await transformGroup.ReadAsync())
            {
                Assert.Equal(mAvgExpectedValues[counter], Math.Round((double)transformGroup["MAvg"], 2));
                Assert.Equal(highestExpectedValues[counter], ((DateTime)transformGroup["SeriesItem"]).ToString("yyyy/MM/dd"));
                counter = counter + 1;
            }
            Assert.Equal(11, counter);
        }
        
        [Fact]
        public async Task SeriesTests_Fill_Fixed_Start_Finish()
        {
            var table = new Table("test", 0,
                new TableColumn("StringColumn", ETypeCode.String, EDeltaType.NaturalKey),
                new TableColumn("IntColumn", ETypeCode.Int32, EDeltaType.NaturalKey),
                new TableColumn("DecimalColumn", ETypeCode.Decimal, EDeltaType.NaturalKey),
                new TableColumn("DateColumn", ETypeCode.DateTime, EDeltaType.NaturalKey),
                new TableColumn("SortColumn", ETypeCode.Int32, EDeltaType.TrackingField)
            );

            // data with gaps in the date sequence.
            table.AddRow("value01", 1, 1.1, Convert.ToDateTime("2015/01/01"), 10 );
            table.AddRow("value02", 2, 2.1, Convert.ToDateTime("2015/01/05"), 9 );
            table.AddRow("value03", 3, 3.1, Convert.ToDateTime("2015/01/10"), 8 );

            var source = new ReaderMemory(table, new Sorts() { new Sort("StringColumn") } );
            source.Reset();

            var mappings = new Mappings(false);

            var fillValue = Functions.GetFunction(_seriesFunctions, nameof(SeriesFunctions<int>.PreviousSeriesValueNull), Helpers.BuiltInAssembly).GetTransformFunction(typeof(int));
            
            var parameters = new Parameters
            {
                Inputs = new Parameter[]
                {
                    new ParameterColumn("IntColumn", ETypeCode.Int32),
                    new ParameterValue("Aggregate", ETypeCode.Unknown, EAggregate.Sum), 
                },
                ResultReturnParameters = new List<Parameter>
                {
                    new ParameterOutputColumn("SeriesItem", ETypeCode.DateTime),
                    new ParameterOutputColumn("Value", ETypeCode.Int32)
                }
            };
            
            mappings.Add(new MapGroup(new TableColumn("StringColumn")));
            mappings.Add(new MapFunction(fillValue, parameters, EFunctionCaching.NoCache));
            mappings.Add(new MapSeries(new TableColumn("DateColumn", ETypeCode.DateTime), ESeriesGrain.Day, 1, true, Convert.ToDateTime("2015/01/04"), Convert.ToDateTime("2015/01/06"), 0));
            
            var transformGroup = new TransformSeries(source, mappings);
            await transformGroup.Open();
            
            Assert.Equal(4, transformGroup.FieldCount);

            
            var counter = 0;
            int[] values = { 1,1,1,0,2,2, 0,0,0 };
            string[] expectedDates = { "2015/01/04", "2015/01/05", "2015/01/06", "2015/01/04", "2015/01/05", "2015/01/06", "2015/01/04", "2015/01/05", "2015/01/06" };
            while (await transformGroup.ReadAsync())
            {
                Assert.Equal(Convert.ToDateTime(expectedDates[counter]), transformGroup["DateColumn"]);
                Assert.Equal(values[counter], transformGroup["Value"]);
                counter = counter + 1;
            }
            Assert.Equal(9, counter);
        }

        [Fact]
        public async Task SeriesTests_Fill_Fixed_Start()
        {
            var table = new Table("test", 0,
                new TableColumn("StringColumn", ETypeCode.String, EDeltaType.NaturalKey),
                new TableColumn("IntColumn", ETypeCode.Int32, EDeltaType.NaturalKey),
                new TableColumn("DecimalColumn", ETypeCode.Decimal, EDeltaType.NaturalKey),
                new TableColumn("DateColumn", ETypeCode.DateTime, EDeltaType.NaturalKey),
                new TableColumn("SortColumn", ETypeCode.Int32, EDeltaType.TrackingField)
            );

            // data with gaps in the date sequence.
            table.AddRow("value01", 1, 1.1, Convert.ToDateTime("2015/01/01"), 10);
            table.AddRow("value02", 1, 2.1, Convert.ToDateTime("2015/01/03"), 9);
            table.AddRow("value02", 2, 2.1, Convert.ToDateTime("2015/01/05"), 9);
            table.AddRow("value03", 3, 3.1, Convert.ToDateTime("2015/01/10"), 8);

            var source = new ReaderMemory(table, new Sorts() {new Sort("StringColumn")});
            source.Reset();

            var mappings = new Mappings(false);

            var fillValue = Functions
                .GetFunction(_seriesFunctions, nameof(SeriesFunctions<int>.PreviousSeriesValueNull),
                    Helpers.BuiltInAssembly).GetTransformFunction(typeof(int));

            var parameters = new Parameters
            {
                Inputs = new Parameter[]
                {
                    new ParameterColumn("IntColumn", ETypeCode.Int32),
                    new ParameterValue("Aggregate", ETypeCode.Unknown, EAggregate.Sum),
                },
                ResultReturnParameters = new List<Parameter>
                {
                    new ParameterOutputColumn("SeriesItem", ETypeCode.DateTime),
                    new ParameterOutputColumn("Value", ETypeCode.Int32)
                }
            };

            mappings.Add(new MapGroup(new TableColumn("StringColumn")));
            mappings.Add(new MapFunction(fillValue, parameters, EFunctionCaching.NoCache));
            mappings.Add(new MapSeries(new TableColumn("DateColumn", ETypeCode.DateTime), ESeriesGrain.Day, 1, true,
                Convert.ToDateTime("2015/01/04"), null, 0));

            var transformGroup = new TransformSeries(source, mappings);
            await transformGroup.Open();

            Assert.Equal(4, transformGroup.FieldCount);


            var counter = 0;
            int[] values = {1, 2, 0, 0, 0, 0, 0, 0, 3};
            string[] expectedDates =
            {
                "2015/01/04", "2015/01/05", "2015/01/04", "2015/01/05", "2015/01/06", "2015/01/07", "2015/01/08" ,"2015/01/09", "2015/01/10"
            };
            while (await transformGroup.ReadAsync())
            {
                Assert.Equal(values[counter], transformGroup["Value"]);
                Assert.Equal(Convert.ToDateTime(expectedDates[counter]), transformGroup["DateColumn"]);
                counter = counter + 1;
            }

            Assert.Equal(9, counter);
        }
        
           [Fact]
        public async Task SeriesTests_Fill_Fixed_Finish()
        {
            var table = new Table("test", 0,
                new TableColumn("StringColumn", ETypeCode.String, EDeltaType.NaturalKey),
                new TableColumn("IntColumn", ETypeCode.Int32, EDeltaType.NaturalKey),
                new TableColumn("DecimalColumn", ETypeCode.Decimal, EDeltaType.NaturalKey),
                new TableColumn("DateColumn", ETypeCode.DateTime, EDeltaType.NaturalKey),
                new TableColumn("SortColumn", ETypeCode.Int32, EDeltaType.TrackingField)
            );

            // data with gaps in the date sequence.
            table.AddRow("value01", 1, 1.1, Convert.ToDateTime("2015/01/01"), 10 );
            table.AddRow("value02", 2, 2.1, Convert.ToDateTime("2015/01/05"), 9 );
            table.AddRow("value03", 3, 3.1, Convert.ToDateTime("2015/01/10"), 8 );

            var source = new ReaderMemory(table, new Sorts() { new Sort("StringColumn") } );
            source.Reset();

            var mappings = new Mappings(false);

            var fillValue = Functions.GetFunction(_seriesFunctions, nameof(SeriesFunctions<int>.PreviousSeriesValueNull), Helpers.BuiltInAssembly).GetTransformFunction(typeof(int));
            
            var parameters = new Parameters
            {
                Inputs = new Parameter[]
                {
                    new ParameterColumn("IntColumn", ETypeCode.Int32),
                    new ParameterValue("Aggregate", ETypeCode.Unknown, EAggregate.Sum), 
                },
                ResultReturnParameters = new List<Parameter>
                {
                    new ParameterOutputColumn("SeriesItem", ETypeCode.DateTime),
                    new ParameterOutputColumn("Value", ETypeCode.Int32)
                }
            };
            
            mappings.Add(new MapGroup(new TableColumn("StringColumn")));
            mappings.Add(new MapFunction(fillValue, parameters, EFunctionCaching.NoCache));
            mappings.Add(new MapSeries(new TableColumn("DateColumn", ETypeCode.DateTime), ESeriesGrain.Day, 1, true, null, Convert.ToDateTime("2015/01/06"), 0));
            
            var transformGroup = new TransformSeries(source, mappings);
            await transformGroup.Open();
            
            Assert.Equal(4, transformGroup.FieldCount);

            
            var counter = 0;
            int[] values = { 1, 1, 1, 1, 1, 1, 2, 2 };
            string[] expectedDates = { "2015/01/01", "2015/01/02", "2015/01/03","2015/01/04", "2015/01/05", "2015/01/06", 
                "2015/01/05", "2015/01/06" };
            while (await transformGroup.ReadAsync())
            {
                Assert.Equal(Convert.ToDateTime(expectedDates[counter]), transformGroup["DateColumn"]);
                Assert.Equal(values[counter], transformGroup["Value"]);
                counter = counter + 1;
            }
            Assert.Equal(8, counter);
        }
    }
}