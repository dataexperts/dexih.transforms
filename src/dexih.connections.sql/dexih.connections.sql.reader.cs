﻿using dexih.transforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dexih.functions;
using System.Data.Common;
using System.Threading;
using dexih.transforms.Exceptions;
using dexih.functions.Query;
using Dexih.Utils.DataType;

namespace dexih.connections.sql
{
    public class ReaderSql : Transform
    {
        private bool _isOpen = false;
        private DbDataReader _sqlReader;
        private DbConnection _sqlConnection;

        private List<int> _fieldOrdinals;
        private int _fieldCount;

        private List<Sort> _sortFields;

        public ReaderSql(ConnectionSql connection, Table table)
        {
            ReferenceConnection = connection;
            CacheTable = table;
        }

        protected override void Dispose(bool disposing)
        {
            if (_sqlReader != null)
                _sqlReader.Dispose();

            if (_sqlConnection != null)
                _sqlConnection.Dispose();

            _isOpen = false;

            base.Dispose(disposing);
        }

        public override async Task<bool> Open(Int64 auditKey, SelectQuery query, CancellationToken cancelToken)
        {
            try
            {
                AuditKey = auditKey;

                if (_isOpen)
                {
                    throw new ConnectionException("The reader is already open.");
                }

                _sqlConnection = await ((ConnectionSql)ReferenceConnection).NewConnection();
                _sqlReader = await ReferenceConnection.GetDatabaseReader(CacheTable, _sqlConnection, query, cancelToken);


                _fieldCount = _sqlReader.FieldCount;
                _fieldOrdinals = new List<int>();
                for (int i = 0; i < _sqlReader.FieldCount; i++)
                {
                    var fieldName = _sqlReader.GetName(i);
                    var ordinal = CacheTable.GetOrdinal(fieldName);
                    if (ordinal < 0)
                    {
                        throw new ConnectionException($"The reader could not be opened as column {fieldName} could not be found in the table {CacheTable.Name}.");
                    }
                    _fieldOrdinals.Add(ordinal);
                }

                _sortFields = query?.Sorts;

                _isOpen = true;
                return true;
            }
            catch(Exception ex)
            {
                throw new ConnectionException($"Open reader failed. {ex.Message}", ex);
            }
        }

        public override string Details()
        {
            return "SqlConnection";
        }

        public override List<Sort> SortFields
        {
            get
            {
                return _sortFields;
            }
        }

        public override bool InitializeOutputFields()
        {
            return true;
        }

        public override bool ResetTransform()
        {
            if (_isOpen)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        protected override async Task<object[]> ReadRecord(CancellationToken cancellationToken)
        {
            try
            {
                if (!await _sqlReader.ReadAsync(cancellationToken))
                {
                    return null;
                }

                //load the new row up, converting datatypes where neccessary.
                object[] row = new object[CacheTable.Columns.Count];

                for (int i = 0; i < _fieldCount; i++)
                {
                    try
                    {
                        row[_fieldOrdinals[i]] = DataType.TryParse(CacheTable.Columns[_fieldOrdinals[i]].Datatype, _sqlReader[i]);
                    }
                    catch(Exception ex)
                    {
                        throw new ConnectionException($"The value on column {CacheTable.Columns[_fieldOrdinals[i]].Name} could not be converted to {CacheTable.Columns[_fieldOrdinals[i]].Datatype}.  {ex.Message}", ex, _sqlReader[i]);
                    }

                }
                return row;
            }
            catch(Exception ex)
            {
                throw new ConnectionException($"Read record failed. {ex.Message}", ex);
            }
        }

        public override bool CanLookupRowDirect { get; } = true;

        /// <summary>
        /// This performns a lookup directly against the underlying data source, returns the result, and adds the result to cache.
        /// </summary>
        /// <param name="filters"></param>
        /// <returns></returns>
        public override async Task<object[]> LookupRowDirect(List<Filter> filters, CancellationToken cancelToken)
        {
            try
            {
                SelectQuery query = new SelectQuery()
                {
                    Columns = CacheTable.Columns.Where(c => c.DeltaType != TableColumn.EDeltaType.IgnoreField).Select(c => new SelectColumn(c)).ToList(),
                    Filters = filters,
                };

                using (var connection = await ((ConnectionSql)ReferenceConnection).NewConnection())
                {
                    using (var reader = await ReferenceConnection.GetDatabaseReader(CacheTable, connection, query, cancelToken))
                    {
                        if (await reader.ReadAsync())
                        {
                            object[] values = new object[CacheTable.Columns.Count];
                            reader.GetValues(values);
                            return values;
                        }
                        else
                            return null;
                    }
                }
            }
            catch(Exception ex)
            {
                throw new ConnectionException($"Lookup direct row failed. {ex.Message}", ex);
            }
        }
    }
}
