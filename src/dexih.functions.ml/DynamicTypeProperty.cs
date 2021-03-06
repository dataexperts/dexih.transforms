using System;
using System.Text.RegularExpressions;
using Dexih.Utils.DataType;

namespace dexih.functions.ml
{
    /// <summary>
    /// A property name, and type used to generate a property in the dynamic class.
    /// </summary>
    public class DynamicTypeProperty
    {
        public DynamicTypeProperty(string name, Type type, EEncoding? encoding = null)
        {
            Name = name;
            Type = type;
            Encoding = encoding;

            TypeCode = DataType.GetTypeCode(type, out _);
        }
        public string Name { get; }

        public string CleanName => Regex.Replace(Name, @"[^0-9a-zA-Z:,]+", "");

        public Type Type { get; }

        public EEncoding? Encoding { get; }

        public ETypeCode TypeCode { get; }

        public object Convert(object value)
        {
            return Operations.Parse(TypeCode, value);
        }
    }
}