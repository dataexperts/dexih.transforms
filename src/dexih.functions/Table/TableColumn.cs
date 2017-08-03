﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using static dexih.functions.DataType;

namespace dexih.functions
{
   
    public class TableColumn
    {
        public TableColumn()
        {
//            ExtendedProperties = new Dictionary<string, string>();
        }

        public TableColumn(string columName)
        {
            Name = columName;
            Datatype = ETypeCode.String;
            DeltaType = EDeltaType.TrackingField;
        }

        public TableColumn(string columName, ETypeCode dataType, string schema = null)
        {
            Name = columName;
            Datatype = dataType;
            DeltaType = EDeltaType.TrackingField;
            Schema = schema;
        }

        public TableColumn(string columName, ETypeCode dataType, EDeltaType deltaType, string schema = null)
        {
            Name = columName;
            Datatype = dataType;
            DeltaType = deltaType;
            Schema = schema;
        }

        public enum EDeltaType
        {
            SurrogateKey,
            SourceSurrogateKey,
            ValidFromDate,
            ValidToDate,
            CreateDate,
            UpdateDate,
            CreateAuditKey,
            UpdateAuditKey,
            IsCurrentField,
            NaturalKey,
            TrackingField,
            NonTrackingField,
            IgnoreField,
            ValidationStatus,
            RejectedReason,
            FileName,
            AzureRowKey, //special column type for Azure Storage Tables.  
            AzurePartitionKey,//special column type for Azure Storage Tables.  
            TimeStamp, //column that is generated by the database.
            DatabaseOperation, // C/U/D/T/R (Create/Update/Delete/Truncate/Reject)
            AutoIncrement // column is auto incremeneted by the ExecuteInsert function.
        }

        public enum ESecurityFlag
        {
            None,
            FastEncrypt,
            FastDecrypt,
            StrongEncrypt,
            StrongDecrypt,
            OneWayHash,
            Hide
        }

        public virtual string Schema { get; set; }
        public string Name { get; set; }

        public string LogicalName { get; set; }

        public string Description { get; set; }

        public ETypeCode Datatype
        {
            get
            {
                if (SecurityFlag == ESecurityFlag.None || SecurityFlag == ESecurityFlag.FastDecrypt || SecurityFlag == ESecurityFlag.StrongDecrypt)
                    return BaseDataType;
                return ETypeCode.String;
            }
            set
            {
                BaseDataType = value;
            }
        }

        public int? MaxLength
        {
            get
            {
                if (SecurityFlag == ESecurityFlag.None || SecurityFlag == ESecurityFlag.FastDecrypt || SecurityFlag == ESecurityFlag.StrongDecrypt)
                    return BaseMaxLength;
                return 250;
            }
            set
            {
                BaseMaxLength = value;
            }
        }

        //this is the underlying datatype of a non encrypted data type.  
        public ETypeCode BaseDataType { get; set; }
        //this is the max length of the non-encrypted data type.
        public int? BaseMaxLength { get; set; }

        public int? Precision { get; set; }

        public int? Scale { get; set; }

        public bool AllowDbNull { get; set; }

        public EDeltaType DeltaType { get; set; }

        public string DefaultValue { get; set; }

        public bool IsUnique { get; set; }

        public bool IsMandatory { get; set; }

        public ESecurityFlag SecurityFlag { get; set; } = ESecurityFlag.None;

        public bool IsInput { get; set; }

        public bool IsIncrementalUpdate { get; set; }
        // public Dictionary<string, string> ExtendedProperties { get; set; }

        [JsonIgnore]
        public Type ColumnGetType
        {
            get
            {
                return DataType.GetType(Datatype);
            }
            set 
            {
                Datatype = GetTypeCode(value);
            }
        }

        /// <summary>
        /// Returns a string with the schema.columnname
        /// </summary>
        /// <returns></returns>
        public string SchemaColumnName()
        {
            var columnName = string.IsNullOrEmpty(Schema) ? Name : Schema + "." + Name;
            return columnName;
        }

        /// <summary>
        /// Is the column one form the source (vs. a value added column).
        /// </summary>
        /// <returns></returns>
        public bool IsSourceColumn()
        {
            switch (DeltaType)
            {
                case EDeltaType.NaturalKey:
                case EDeltaType.TrackingField:
                case EDeltaType.NonTrackingField:
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Columns which require no mapping and are generated automatically for auditing.
        /// </summary>
        /// <returns></returns>
        public bool IsGeneratedColumn()
        {
            switch (DeltaType)
            {
                case EDeltaType.CreateAuditKey:
                case EDeltaType.UpdateAuditKey:
                case EDeltaType.CreateDate:
                case EDeltaType.UpdateDate:
                case EDeltaType.SurrogateKey:
                case EDeltaType.ValidationStatus:
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Columns which indicate if the record is current.  These are the createdate, updatedate, iscurrentfield
        /// </summary>
        /// <returns></returns>
        public bool IsCurrentColumn()
        {
            switch (DeltaType)
            {
                case EDeltaType.ValidFromDate:
                case EDeltaType.ValidToDate:
                case EDeltaType.IsCurrentField:
                    return true;
            }
            return false;
        }

        //public string GetExtendedProperty(string name)
        //{
        //    if (ExtendedProperties == null)
        //        return null;
        //    if (ExtendedProperties.ContainsKey(name))
        //        return ExtendedProperties[name];
        //    return null;
        //}

        //public void SetExtendedProperty(string name, string value)
        //{
        //    if (ExtendedProperties == null)
        //        ExtendedProperties = new Dictionary<string, string>();

        //    if (ExtendedProperties.ContainsKey(name))
        //        ExtendedProperties[name] = value;
        //    else
        //        ExtendedProperties.Add(name, value);
        //}


        /// <summary>
        /// Creates a copy of the column which can be used when generating other tables.
        /// </summary>
        /// <returns></returns>
        public TableColumn Copy()
        {
            var newColumn = new TableColumn
            {
                Schema = Schema,
                Name = Name,
                LogicalName = LogicalName,
                Description = Description,
                Datatype = Datatype,
                MaxLength = MaxLength,
                Precision = Precision,
                Scale = Scale,
                AllowDbNull = AllowDbNull,
                DefaultValue = DefaultValue,
                DeltaType = DeltaType,
                IsUnique = IsUnique,
                SecurityFlag = ESecurityFlag.None, //don't copy securityFlag as only the first table requires encryption.
                IsInput = IsInput,
                IsMandatory = IsMandatory,
                IsIncrementalUpdate = IsIncrementalUpdate
            };

            return newColumn;
        }
    }
}