using System.Collections.Generic;
using dexih.functions.Exceptions;
using Dexih.Utils.DataType;

namespace dexih.functions.BuiltIn
{
    public class CacheFunctions<T>
    {
        private T _cacheValue;
        private Queue<T> _cacheQueue;
        private int _cacheCount;

        public bool Reset()
        {
            _cacheQueue = null;
            _cacheValue = default;
            _cacheCount = 0;
            return true;
        }

        private (T value, bool found) AddToQueue(T value, int count)
        {
            if (_cacheQueue == null)
            {
                _cacheQueue = new Queue<T>();
            }

            if (count < 1)
            {
                throw new FunctionException("The cache row count must be >= 1");
            }
            
            _cacheQueue.Enqueue(value);
            if (_cacheQueue.Count > count)
            {
                return (_cacheQueue.Dequeue(), true);
            }

            return (default, false);
        }

        [TransformFunction(FunctionType = EFunctionType.Aggregate, Category = "Row Caching", Name = "Previous Group Row",
            Description = "Returns the value from the previous row in the group.", GenericType = EGenericType.All, GenericTypeDefault = ETypeCode.String,
            ResetMethod = nameof(Reset))]
        public T PreviousGroupRow(T value, [TransformFunctionParameter(Name = "Number of rows back")] int preCount = 1) => PreviousRow(value, preCount);
        
        [TransformFunction(FunctionType = EFunctionType.Map, Category = "Row Caching", Name = "Previous Row",
            Description = "Returns the value from the previous row.", GenericType = EGenericType.All, GenericTypeDefault = ETypeCode.String, ResetMethod = nameof(Reset))]
        public T PreviousRow(T value, 
            [TransformFunctionParameter(Name = "Number of rows back")] int preCount = 1)
        {
            return AddToQueue(value, preCount).value;
        }

        [TransformFunction(FunctionType = EFunctionType.Aggregate, Category = "Row Caching",
            Name = "Previous Row in Group If Null",
            Description = "Returns the value from the previous row from the current group if the current value is null.",
            GenericType = EGenericType.All, GenericTypeDefault = ETypeCode.Decimal, ResetMethod = nameof(Reset))]
        public T PreviousGroupRowIfNull(T value) => PreviousRowIfNull(value);
        
        [TransformFunction(FunctionType = EFunctionType.Map, Category = "Row Caching", Name = "Previous Row If Null",
            Description = "Returns the value from the previous row if the current value is null.", GenericType = EGenericType.All, GenericTypeDefault = ETypeCode.Decimal, ResetMethod = nameof(Reset))]
        public T PreviousRowIfNull(T value)
        {
            if(EqualityComparer<T>.Default.Equals(value, default(T))) {
                return _cacheValue;
            }
            else
            {
                _cacheValue = value;
                return value;
            }
        }

        [TransformFunction(FunctionType = EFunctionType.Aggregate, Category = "Row Caching", Name = "Group Row Number",
            Description = "Returns the current row number in the group", ResetMethod = nameof(Reset))]
        public int GroupRowNumber() => RowNumber();
        
        [TransformFunction(FunctionType = EFunctionType.Map, Category = "Row Caching", Name = "Row Number",
            Description = "Returns the current row number", ResetMethod = nameof(Reset))]
        public int RowNumber()
        {
            return ++_cacheCount;
        }

        [TransformFunction(FunctionType = EFunctionType.Aggregate, Category = "Row Caching", Name = "Running Group Sum",
            Description = "The running sum of rows in the current group.", ResetMethod = nameof(Reset),
            GenericTypeDefault = ETypeCode.Decimal, GenericType = EGenericType.Numeric)]
        public T RunningGroupSum(T value, [TransformFunctionParameter(Name = "Number of rows (0=all)")]
            int preCount = 0) => RunningSum(value, preCount);

        [TransformFunction(FunctionType = EFunctionType.Map, Category = "Row Caching", Name = "Running Sum", Description = "The running sum of rows.", ResetMethod = nameof(Reset), GenericTypeDefault = ETypeCode.Decimal, GenericType = EGenericType.Numeric)]
        public T RunningSum(T value, [TransformFunctionParameter(Name = "Number of rows (0=all)")] int preCount = 0)
        {
            // if precount is zero we run keep a running value
            if (preCount == 0)
            {
                _cacheValue = Operations.Add(_cacheValue, value);
                return _cacheValue;
            }
            
            AddToQueue(value, preCount);
            T sum = default;
            foreach (var item in _cacheQueue)
            {
                sum = Operations.Add(sum, item);
            }

            return sum;
        }

        [TransformFunction(FunctionType = EFunctionType.Aggregate, Category = "Row Caching",
            Name = "Running Group Average", Description = "The running average of rows in the current group.",
            ResetMethod = nameof(Reset), GenericTypeDefault = ETypeCode.Decimal, GenericType = EGenericType.Numeric)]
        public T RunningGroupAverage(T value, [TransformFunctionParameter(Name = "Number of rows (0=all)")]
            int preCount = 0) => RunningAverage(value, preCount);
        
        [TransformFunction(FunctionType = EFunctionType.Map, Category = "Row Caching", Name = "Running Average", Description = "The running average.", ResetMethod = nameof(Reset), GenericTypeDefault = ETypeCode.Decimal, GenericType = EGenericType.Numeric)]
        public T RunningAverage(T value, [TransformFunctionParameter(Name = "Number of rows (0=all)")] int preCount = 0)
        {
            // if precount is zero we run keep a running value
            if (preCount == 0)
            {
                _cacheValue = Operations.Add(_cacheValue, value);
                _cacheCount++;
                return Operations.DivideInt(_cacheValue, _cacheCount);
            }
            
            AddToQueue(value, preCount);
            T sum = default;
            foreach (var item in _cacheQueue)
            {
                sum = Operations.Add(sum, item);
            }

            return Operations.DivideInt(sum, _cacheQueue.Count);
        }

        [TransformFunction(FunctionType = EFunctionType.Map, Category = "Row Caching", Name = "Previous Row Change",
            Description = "The change from the previous row value to the current.", ResetMethod = nameof(Reset),
            GenericTypeDefault = ETypeCode.Decimal, GenericType = EGenericType.Numeric)]
        public T PreviousRowChange(T value, [TransformFunctionParameter(Name = "Number of rows back")]
            int preCount = 1, [TransformFunctionParameter(Name = "Zero when no previous row")] bool zeroNoPrevious = true) => PreviousGroupRowChange(value, preCount, zeroNoPrevious);

        [TransformFunction(FunctionType = EFunctionType.Aggregate, Category = "Row Caching", Name = "Previous Group Row Change", Description = "The change from the previous row value to the current in the group.", ResetMethod = nameof(Reset), GenericTypeDefault = ETypeCode.Decimal, GenericType = EGenericType.Numeric)]
        public T PreviousGroupRowChange(T value, [TransformFunctionParameter(Name = "Number of rows back")] int preCount = 1, [TransformFunctionParameter(Name = "Zero when no previous row")] bool zeroNoPrevious = true)
        {
            var previousValue = AddToQueue(value, preCount);
            if (previousValue.found)
            {
                var result = Operations.Subtract(value, previousValue.value);
                return result;
            }

            return default;
        }

        [TransformFunction(FunctionType = EFunctionType.Aggregate, Category = "Row Caching",
            Name = "Previous Group Row Ratio",
            Description = "The ratio of the current value / previous row value in the current group.",
            ResetMethod = nameof(Reset), GenericTypeDefault = ETypeCode.Decimal, GenericType = EGenericType.Numeric)]
        public T PreviousGroupRowRatio(
            T value, 
            [TransformFunctionParameter(Name = "Number of rows back")]
            int preCount = 1,
            [TransformFunctionParameter(Name = "Value when previous is 0/null")]T defaultRatio = default) => PreviousRowRatio(value, preCount, defaultRatio );

        
        [TransformFunction(FunctionType = EFunctionType.Map, Category = "Row Caching", Name = "Previous Row Ratio", Description = "The ratio of the current value / previous row value.", ResetMethod = nameof(Reset), GenericTypeDefault = ETypeCode.Decimal, GenericType = EGenericType.Numeric)]
        public T PreviousRowRatio(T value, [TransformFunctionParameter(Name = "Number of rows back")] int preCount = 1,
            [TransformFunctionParameter(Name = "Value when previous is 0/null")]T defaultRatio = default)
        {
            var previousValue = AddToQueue(value, preCount);

            if (!previousValue.found || Operations.Equal(previousValue.value, default))
            {
                return defaultRatio;
            }
            
            var result = Operations.Divide(value, previousValue.value);
            return result;
        }
    }
}