﻿using System;
using System.Threading.Tasks;
using dexih.functions.Query;
using Dexih.Utils.DataType;

namespace dexih.functions.Mappings
{
    public class MapFilter: Mapping
    {
        public MapFilter() {}
        
        public MapFilter(TableColumn column1, TableColumn column2, Filter.ECompare compare = Filter.ECompare.IsEqual)
        {
            Column1 = column1;
            Column2 = column2;
            Compare = compare;
        }

        public MapFilter(TableColumn column1, Object value2, Filter.ECompare compare = Filter.ECompare.IsEqual)
        {
            Column1 = column1;
            Value2 = value2;
            Compare = compare;
        }

        public TableColumn Column1 { get; set; }
        public TableColumn Column2 { get; set; }
        public Object Value1 { get; set; }
        public Object Value2 { get; set; }
        
        public Filter.ECompare Compare { get; set; }

        private int _column1Ordinal = -1;
        private int _column2Ordinal = -1;

        public override void InitializeColumns(Table table, Table joinTable = null)
        {
            if (Column1 != null)
            {
                _column1Ordinal = table.GetOrdinal(Column1);
                if (_column1Ordinal < 0 && Value1 == null)
                {
                    Value1 = Column1.DefaultValue;
                }
            }
            else
            {
                _column1Ordinal = -1;
            }

            if (Column2 != null)
            {
                _column2Ordinal = table.GetOrdinal(Column2);
                if (_column2Ordinal < 0 && Value2 == null)
                {
                    Value2 = Column2.DefaultValue;
                }
            }
            else
            {
                _column2Ordinal = -1;
            }
        }

        public override void AddOutputColumns(Table table)
        {
            return;
        }

        public override bool ProcessInputRow(FunctionVariables functionVariables, object[] row, object[] joinRow = null)
        {
            var value1 = _column1Ordinal == -1 ? Value1 : row[_column1Ordinal];
            var value2 = _column2Ordinal == -1 ? Value2 : row[_column2Ordinal];
            
            switch (Compare)
            {
                case Filter.ECompare.GreaterThan:
                    return Operations.GreaterThan(Column1.DataType, value1, value2);
                case Filter.ECompare.IsEqual:
                    return Operations.Equal(Column1.DataType, value1, value2);
                case Filter.ECompare.GreaterThanEqual:
                    return Operations.GreaterThanOrEqual(Column1.DataType, value1, value2);
                case Filter.ECompare.LessThan:
                    return Operations.LessThan(Column1.DataType, value1, value2);
                case Filter.ECompare.LessThanEqual:
                    return Operations.LessThanOrEqual(Column1.DataType, value1, value2);
                case Filter.ECompare.NotEqual:
                    return !Operations.Equal(Column1.DataType, value1, value2);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void MapOutputRow(object[] row)
        {
            return;
        }

        public override object GetInputValue(object[] row = null)
        {
            throw new NotSupportedException();
        }
        
      
        public MapFilter Copy()
        {
            var filter = new MapFilter()
            {
                Column1 = Column1,
                Column2 = Column2,
                Value1 = Value1,
                Value2 = Value2,
                Compare = Compare
            };

            return filter;
        }
    }
}