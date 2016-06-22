﻿using dexih.functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace dexih.transforms
{
    public class TransformWriter
    {
        #region Events
        public delegate void ProgressUpdate(TransformWriterResult transformWriterResult);
        public delegate void StatusUpdate(TransformWriterResult transformWriterResult);

        public event ProgressUpdate OnProgressUpdate;
        public event StatusUpdate OnStatusUpdate;

        #endregion

        /// <summary>
        /// Indicates the rows buffer per commit.  
        /// </summary>
        public virtual int CommitSize { get; protected set; } = 10000;

        private TableCache CreateRows;
        private TableCache UpdateRows;
        private TableCache DeleteRows;
        private TableCache RejectRows;

        private bool WriteOpen = false;
        private int OperationColumnIndex; //the index of the operation in the source data.
        private TableColumns WriteColumns; //the column being written (excludes operation column).

        private Task<ReturnValue<int>> CreateRecordsTask; //task is use to allow writes to run asych with other processing.
        private Task<ReturnValue<int>> UpdateRecordsTask; //task is use to allow writes to run asych with other processing.
        private Task<ReturnValue<int>> DeleteRecordsTask; //task is use to allow writes to run asych with other processing.
        private Task<ReturnValue<int>> RejectRecordsTask; //task is use to allow writes to run asych with other processing.

        private Transform InTransform;
        private Table TargetTable;
        private Table RejectTable;

        private Connection TargetConnection;
        private Connection RejectConnection;

        private CancellationToken CancelToken;

        private InsertQuery TargetInsertQuery;
        private UpdateQuery TargetUpdateQuery;
        private DeleteQuery TargetDeleteQuery;
        private InsertQuery RejectInsertQuery;

        public void doProgressUpdate(TransformWriterResult transformWriterResult)
        {
            OnProgressUpdate?.Invoke(transformWriterResult);
        }

        /// <summary>
        /// Writes all record from the inTransform to the target table and reject table.
        /// </summary>
        /// <param name="inTransform">Transform to read data from</param>
        /// <param name="tableName">Target table name</param>
        /// <param name="connection">Target to write data to</param>
        /// <param name="rejecteTableName">Reject table name</param>
        /// <param name="rejectConnection">Reject connection (if null will use connection)</param>
        /// <returns></returns>
        public async Task<ReturnValue> WriteAllRecords(TransformWriterResult WriterResult, Transform inTransform, Table targetTable, Connection targetConnection, Table rejectTable, Connection rejectConnection, CancellationToken cancelToken)
        {
            try
            {
                CancelToken = cancelToken;
                TargetConnection = targetConnection;
                RejectConnection = rejectConnection;
                //WriterResult = new TransformWriterResult();

                WriterResult.OnProgressUpdate += doProgressUpdate;

                WriterResult.RunStatus = TransformWriterResult.ERunStatus.Started;
                OnStatusUpdate?.Invoke(WriterResult);

                TargetTable = targetTable;
                RejectTable = rejectTable;

                InTransform = inTransform;

                var returnValue = await WriteStart();

                if (returnValue.Success == false)
                {
                    WriterResult.RunStatus = TransformWriterResult.ERunStatus.Abended;
                    WriterResult.Message = returnValue.Message;

                    OnStatusUpdate?.Invoke(WriterResult);
                    return new ReturnValue(false);
                }

                bool firstRead = true;
                while (inTransform.Read())
                {
                    if (firstRead)
                    {
                        WriterResult.RunStatus = TransformWriterResult.ERunStatus.Running;
                        OnStatusUpdate?.Invoke(WriterResult);
                    }

                    returnValue = await WriteRecord(inTransform);
                    if (returnValue.Success == false)
                    {
                        WriterResult.RunStatus = TransformWriterResult.ERunStatus.Abended;
                        WriterResult.Message = returnValue.Message;
                        OnStatusUpdate?.Invoke(WriterResult);
                        return new ReturnValue(false);
                    }

                    if (cancelToken.IsCancellationRequested)
                    {
                        WriterResult.RunStatus = TransformWriterResult.ERunStatus.Cancelled;
                        OnStatusUpdate?.Invoke(WriterResult);
                        return new ReturnValue(false);
                    }
                }

                returnValue = await WriteFinish();
                if (returnValue.Success == false)
                {
                    WriterResult.RunStatus = TransformWriterResult.ERunStatus.Abended;
                    WriterResult.Message = returnValue.Message;
                    OnStatusUpdate?.Invoke(WriterResult);
                    return new ReturnValue(false, returnValue.Message, null);
                }

                WriterResult.RunStatus = TransformWriterResult.ERunStatus.Finished;
                OnStatusUpdate?.Invoke(WriterResult);

                return new ReturnValue(true);
            }
            catch(Exception ex)
            {
                return new ReturnValue(false, "The following error occurred when attempting to run the transform: " + ex.Message, ex);
            }
        }

        public async Task<ReturnValue> WriteStart( )
        {

            if (WriteOpen == true)
                return new ReturnValue(false, "Write cannot start, as a previous operation is still running.  Run the WriteFinish command to reset.", null);

            var returnValue = await InTransform.Open(null);

            OperationColumnIndex = InTransform.CacheTable.GetDeltaColumnOrdinal(TableColumn.EDeltaType.DatabaseOperation);

            WriteColumns = new TableColumns();
            for (int i = 0; i < InTransform.CacheTable.Columns.Count; i++)
            {
                if (i != OperationColumnIndex)
                {
                    WriteColumns.Add(InTransform.CacheTable.Columns[i]);
                }
            }

            CreateRows = new TableCache();
            UpdateRows = new TableCache();
            DeleteRows = new TableCache();
            RejectRows = new TableCache();

            //create template queries, with the values set to paramaters (i.e. @param1, @param2)
            TargetInsertQuery = new InsertQuery(TargetTable.TableName, TargetTable.Columns.Select(c => new QueryColumn(c.ColumnName, c.DataType, "@param" + TargetTable.GetOrdinal(c.ColumnName).ToString())).ToList());

            TargetUpdateQuery = new UpdateQuery(
                TargetTable.TableName,
                TargetTable.Columns.Where(c=> c.DeltaType != TableColumn.EDeltaType.SurrogateKey).Select(c => new QueryColumn(c.ColumnName, c.DataType, "@param" + TargetTable.GetOrdinal(c.ColumnName).ToString())).ToList(),
                TargetTable.Columns.Where(c => c.DeltaType == TableColumn.EDeltaType.SurrogateKey).Select(c=> new Filter(c.ColumnName, Filter.ECompare.IsEqual, "@surrogateKey")).ToList()
                );

            TargetDeleteQuery = new DeleteQuery(TargetTable.TableName, TargetTable.Columns.Where(c => c.DeltaType == TableColumn.EDeltaType.SurrogateKey).Select(c => new Filter(c.ColumnName, Filter.ECompare.IsEqual, "@surrogateKey")).ToList());

            if(RejectTable != null)
                RejectInsertQuery = new InsertQuery(RejectTable.TableName, RejectTable.Columns.Select(c => new QueryColumn(c.ColumnName, c.DataType, "@param" + RejectTable.GetOrdinal(c.ColumnName).ToString())).ToList());

            //if the table doesn't exist, create it.  
            returnValue = await TargetConnection.CreateTable(TargetTable, false);

            returnValue = await TargetConnection.DataWriterStart(TargetTable);

            //await InTransform.Open();

            WriteOpen = true;

            return new ReturnValue(true);
        }


        public async Task<ReturnValue> WriteRecord(Transform reader)
        {
            if (WriteOpen == false)
                return new ReturnValue(false, "Cannot write records as the WriteStart has not been called.", null);

            //split the operation field (if it exists) and create copy of the row.
            object[] row = new object[WriteColumns.Count];
            char operation;


            //determine the type of operation (create, update, delete, reject)
            if (OperationColumnIndex == -1)
            {
                operation = 'C';
                reader.GetValues(row);
            }
            else
            {
                operation = (char)reader[OperationColumnIndex];
                int count = 0;
                for (int i = 0; i < InTransform.FieldCount; i++)
                {
                    if (i != OperationColumnIndex)
                    {
                        row[count] = reader[i];
                        count++;
                    }
                }
            }

            switch (operation)
            {
                case 'C':
                    CreateRows.Add(row);
                    if (CreateRows.Count >= CommitSize)
                        return await doCreates();
                    break;
                case 'U':
                    UpdateRows.Add(row);
                    if (UpdateRows.Count >= CommitSize)
                        return await doUpdate();
                    break;
                case 'D':
                    DeleteRows.Add(row);
                    if (DeleteRows.Count >= CommitSize)
                        return await doDelete();
                    break;
                case 'R':
                    RejectRows.Add(row);
                    if (RejectRows.Count >= CommitSize)
                        return await doReject();
                    break;
            }

            return new ReturnValue(true);
        }

        public async Task<ReturnValue> WriteFinish()
        {
            WriteOpen = false;

            if (CreateRows.Count > 0)
            {
                var returnValue = await doCreates();
                if (returnValue.Success == false)
                    return returnValue;
            }

            if (UpdateRows.Count > 0)
            {
                var returnValue = await doUpdate();
                if (returnValue.Success == false)
                    return returnValue;
            }

            if (DeleteRows.Count > 0)
            {
                var returnValue = await doDelete();
                if (returnValue.Success == false)
                    return returnValue;
            }

            if (RejectRows.Count > 0)
            {
                var returnValue = await doReject();
                if (returnValue.Success == false)
                    return returnValue;
            }

            if (CreateRecordsTask != null && !CreateRecordsTask.Result.Success)
                return CreateRecordsTask.Result;

            if (UpdateRecordsTask != null && !UpdateRecordsTask.Result.Success)
                return UpdateRecordsTask.Result;

            if (DeleteRecordsTask != null && !DeleteRecordsTask.Result.Success)
                return DeleteRecordsTask.Result;

            if (RejectRecordsTask != null && !RejectRecordsTask.Result.Success)
                return RejectRecordsTask.Result;

            var returnValue2 = await TargetConnection.DataWriterFinish(TargetTable);

            return new ReturnValue(true);
        }
        private async Task<ReturnValue> doCreates()
        {
            //wait for the previous create task to finish before writing next buffer.
            if (CreateRecordsTask != null)
            {
                var result = await CreateRecordsTask;
                if (!result.Success)
                    return result;
            }

            Table createTable = new Table(TargetTable.TableName, WriteColumns, CreateRows);
            var createReader = new ReaderMemory(createTable);
            CreateRecordsTask = TargetConnection.ExecuteInsertBulk(TargetTable, createReader, CancelToken);  //this has no await to ensure processing continues.

            CreateRows = new TableCache();
            return new ReturnValue(true);
        }

        private async Task<ReturnValue> doUpdate()
        {
            //update must wait for any inserts to complete (to avoid updates on records that haven't been inserted yet)
            if (CreateRecordsTask != null)
            {
                var result = await CreateRecordsTask;
                if (!result.Success)
                    return result;
            }

            if (UpdateRecordsTask != null)
            {
                var result = await UpdateRecordsTask;
                if (!result.Success)
                    return result;
            }

            List<UpdateQuery> updateQueries = new List<UpdateQuery>();
            foreach(object[] row in UpdateRows)
            {
                UpdateQuery updateQuery = new UpdateQuery(
                TargetTable.TableName,
                TargetTable.Columns.Where(c => c.DeltaType != TableColumn.EDeltaType.SurrogateKey).Select(c => new QueryColumn(c.ColumnName, c.DataType, row[TargetTable.GetOrdinal(c.ColumnName)])).ToList(),
                TargetTable.Columns.Where(c => c.DeltaType == TableColumn.EDeltaType.SurrogateKey).Select(c => new Filter(c.ColumnName, Filter.ECompare.IsEqual, row[TargetTable.GetOrdinal(c.ColumnName)])).ToList()
                );

                updateQueries.Add(updateQuery);
            }

            UpdateRecordsTask = TargetConnection.ExecuteUpdate(TargetTable, updateQueries, CancelToken);  //this has no await to ensure processing continues.

            UpdateRows = new TableCache();

            return new ReturnValue(true);
        }

        private async Task<ReturnValue> doDelete()
        {
            //delete must wait for any inserts to complete (to avoid updates on records that haven't been inserted yet)
            if (CreateRecordsTask != null)
            {
                var result = await CreateRecordsTask;
                if (!result.Success)
                    return result;
            }

            if (UpdateRecordsTask != null)
            {
                var result = await UpdateRecordsTask;
                if (!result.Success)
                    return result;
            }

            if (DeleteRecordsTask != null)
            {
                var result = await DeleteRecordsTask;
                if (!result.Success)
                    return result;
            }

            TargetDeleteQuery = new DeleteQuery(TargetTable.TableName, TargetTable.Columns.Where(c => c.DeltaType == TableColumn.EDeltaType.SurrogateKey).Select(c => new Filter(c.ColumnName, Filter.ECompare.IsEqual, "@surrogateKey")).ToList());

            List<DeleteQuery> deleteQueries = new List<DeleteQuery>();
            foreach (object[] row in DeleteRows)
            {
                DeleteQuery deleteQuery = new DeleteQuery(
                TargetTable.TableName,
                TargetTable.Columns.Where(c => c.DeltaType == TableColumn.EDeltaType.SurrogateKey).Select(c => new Filter(c.ColumnName, Filter.ECompare.IsEqual, row[TargetTable.GetOrdinal(c.ColumnName)])).ToList()
                );

                deleteQueries.Add(deleteQuery);
            }

            DeleteRecordsTask = TargetConnection.ExecuteDelete(TargetTable, deleteQueries, CancelToken);  //this has no await to ensure processing continues.

            DeleteRows = new TableCache();

            return new ReturnValue(true);
        }

        private async Task<ReturnValue> doReject()
        {
            //wait for the previous create task to finish before writing next buffer.
            if (RejectRecordsTask != null)
            {
                var result = await RejectRecordsTask;
                if (!result.Success)
                    return result;
            }

            Table createTable = new Table(RejectTable.TableName, WriteColumns, RejectRows);

            var createReader = new ReaderMemory(createTable);
            RejectRecordsTask = TargetConnection.ExecuteInsertBulk(createTable, createReader, CancelToken);  //this has no await to ensure processing continues.

            return new ReturnValue(true);
        }

    }
}
