﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dexih.functions;
using System.Text;
using System.Threading;
using static Dexih.Utils.DataType.DataType;
using dexih.transforms.Exceptions;

namespace dexih.transforms
{
    public class TransformValidation : Transform
    {
        public TransformValidation() { }

        public TransformValidation(Transform inReader, List<Function> validations, bool validateDataTypes)
        {
            SetInTransform(inReader);
            Validations = validations;
            ValidateDataTypes = validateDataTypes;
        }

        public bool ValidateDataTypes { get; set; }

        private object[] _savedRejectRow; //used as a temporary store for the pass row when a pass and reject occur.

        private bool _lastRecord = false;

        private string _rejectReasonColumnName;
        private int _rejectReasonOrdinal;
        private int _operationOrdinal;
        private int _validationStatusOrdinal;

        private List<int> _mapFieldOrdinals;
        private int _primaryFieldCount;
        private int _columnCount;

        public List<Function> Validations
        {
            get { return Functions;  }
            set { Functions = value;  }
        }

        public override bool InitializeOutputFields()
        {
            CacheTable = PrimaryTransform.CacheTable.Copy();

            //add the operation type, which indicates whether record is rejected 'R' or 'C/U/D' create/update/delete
            if (CacheTable.Columns.SingleOrDefault(c => c.DeltaType == TableColumn.EDeltaType.DatabaseOperation) == null)
            {
                CacheTable.Columns.Insert(0, new TableColumn("Operation", ETypeCode.Byte)
                {
                    DeltaType = TableColumn.EDeltaType.DatabaseOperation
                });
            }

            //add the rejection reason, which details the reason for a rejection.
            if (CacheTable.Columns.SingleOrDefault(c => c.DeltaType == TableColumn.EDeltaType.RejectedReason) == null)
            {
                CacheTable.Columns.Add(new TableColumn("RejectReason", ETypeCode.String)
                {
                    DeltaType = TableColumn.EDeltaType.RejectedReason
                });
            }

            //add the rejection reason, which details the reason for a rejection.
            if (CacheTable.Columns.SingleOrDefault(c => c.DeltaType == TableColumn.EDeltaType.ValidationStatus) == null)
            {
                CacheTable.Columns.Add(new TableColumn("ValidationStatus", ETypeCode.String)
                {
                    DeltaType = TableColumn.EDeltaType.ValidationStatus
                });
            }

            //store reject column details to improve performance.
            _rejectReasonOrdinal = CacheTable.GetDeltaColumnOrdinal(TableColumn.EDeltaType.RejectedReason);
            if (_rejectReasonOrdinal >= 0)
                _rejectReasonColumnName = CacheTable.Columns[_rejectReasonOrdinal].Name;

            _operationOrdinal = CacheTable.GetDeltaColumnOrdinal(TableColumn.EDeltaType.DatabaseOperation);
            _validationStatusOrdinal = CacheTable.GetDeltaColumnOrdinal(TableColumn.EDeltaType.ValidationStatus);

            _primaryFieldCount = PrimaryTransform.FieldCount;
            _columnCount = CacheTable.Columns.Count;
            _mapFieldOrdinals = new List<int>();
            for (int i = 0; i < _primaryFieldCount; i++)
            {
                _mapFieldOrdinals.Add(GetOrdinal(PrimaryTransform.GetName(i)));
            }



            return true;
        }

        public override bool RequiresSort => false;
        public override bool PassThroughColumns => true;


        public override bool ResetTransform()
        {
            _lastRecord = false;
            return true;
        }

        protected override async Task<object[]> ReadRecord(CancellationToken cancellationToken)
        {
            //the saved reject row is when a validation outputs two rows (pass & fail).
            if (_savedRejectRow != null)
            {
                var row = _savedRejectRow;
                _savedRejectRow = null;
                return row;
            }

            if (_lastRecord)
            {
                return null;
            }

            while (await PrimaryTransform.ReadAsync(cancellationToken))
            {
                StringBuilder rejectReason = new StringBuilder();
                Function.EInvalidAction finalInvalidAction = Function.EInvalidAction.Pass;

                //copy row data.
                object[] passRow = new object[_columnCount];
                for (int i = 0; i < _primaryFieldCount; i++)
                {
                    passRow[_mapFieldOrdinals[i]] = PrimaryTransform[i];
                }

                if (passRow[_operationOrdinal] == null)
                    passRow[_operationOrdinal] = 'C';

                object[] rejectRow = null;

                //run the validation functions
                if (Validations != null)
                {
                    foreach (Function validation in Validations)
                    {
                        //set inputs for the validation function
                        foreach (Parameter input in validation.Inputs.Where(c => c.IsColumn))
                        {
                            try
                            {
								input.SetValue(PrimaryTransform[input.Column.TableColumnName()]);
                            }
                            catch(Exception ex)
                            {
								throw new TransformException($"The validation transform failed setting input parameters on the function {validation.FunctionName} parameter {input.Name} for column {input.Column.TableColumnName()}.  {ex.Message}", ex, PrimaryTransform[input.Column.TableColumnName()]);
                            }
                        }

                        bool validationResult;
                        try
                        {
                            var invokeresult = validation.Invoke();
                            validationResult = (bool)invokeresult;
                        }
						catch (FunctionIgnoreRowException)
						{
							validationResult = false;
						}
                        catch(Exception ex)
                        {
                            throw new TransformException($"The validation transform failed on the function {validation.FunctionName}.  {ex.Message}", ex);
                        }

                        //if the validation is negative.  apply any output columns, and set a reject status
                        if (!validationResult)
                        {
							rejectReason.AppendLine("function: " + validation.FunctionName + ", parameters: " + string.Join(",", validation.Inputs.Select(c => c.Name + "=" + (c.IsColumn ? c.Column.TableColumnName() : c.Value.ToString())).ToArray()) + ".");

                            // fail job immediately.
                            if (validation.InvalidAction == Function.EInvalidAction.Abend)
                                throw new Exception(rejectReason.ToString());

                            //if the record is to be discarded, continue the loop and get the next source record.
                            if (validation.InvalidAction == Function.EInvalidAction.Discard)
                                continue;

                            //set the final invalidation action based on priority order of other rejections.
                            finalInvalidAction = finalInvalidAction < validation.InvalidAction ? validation.InvalidAction : finalInvalidAction;

                            if (validation.InvalidAction == Function.EInvalidAction.Reject || validation.InvalidAction == Function.EInvalidAction.RejectClean)
                            {
                                //if the row is rejected, copy unmodified row to a reject row.
                                if (rejectRow == null)
                                {
                                    rejectRow = new object[CacheTable.Columns.Count];
                                    passRow.CopyTo(rejectRow, 0);
                                    rejectRow[_operationOrdinal] = 'R';
                                    TransformRowsRejected++;
                                }

                                //add a reject reason if it exists
                                if (_rejectReasonOrdinal >= 0)
                                {
                                    if (validation.Outputs != null)
                                    {
										Parameter param = validation.Outputs.SingleOrDefault(c => c.Column.TableColumnName() == _rejectReasonColumnName);
                                        if (param != null)
                                        {
                                            rejectReason.Append("  Reason: " + (string)param.Value);
                                        }
                                    }
                                }
                            }

                            if (validation.InvalidAction == Function.EInvalidAction.Clean || validation.InvalidAction == Function.EInvalidAction.RejectClean)
                            {
                                validation.ReturnValue();
                                if (validation.Outputs != null)
                                {
                                    foreach (Parameter output in validation.Outputs)
                                    {
										if (output.Column.TableColumnName() != "")
                                        {
											int ordinal = CacheTable.GetOrdinal(output.Column.TableColumnName());
											TableColumn col = CacheTable[output.Column.TableColumnName()];
                                            if (ordinal >= 0)
                                            {
                                                try
                                                {
                                                    var parseresult = TryParse(col.Datatype, output.Value, col.MaxLength);
                                                    passRow[ordinal] = parseresult;
                                                }
                                                catch(Exception ex)
                                                {
                                                    throw new TransformException($"The validation transform failed parsing output values on the function {validation.FunctionName} parameter {output.Name} column {col.Name}.  {ex.Message}", ex, output.Value);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (ValidateDataTypes && (finalInvalidAction == Function.EInvalidAction.Pass || finalInvalidAction == Function.EInvalidAction.Clean))
                {
                    for (int i = 0; i < _columnCount; i++)
                    {
                        object value = passRow[i];
                        var col = CacheTable.Columns[i];

                        if (col.DeltaType == TableColumn.EDeltaType.TrackingField || col.DeltaType == TableColumn.EDeltaType.NonTrackingField)
                        {

                            if (value == null || value is DBNull)
                            {
                                if (col.AllowDbNull == false)
                                {
                                    if (rejectRow == null)
                                    {
                                        rejectRow = new object[_columnCount];
                                        passRow.CopyTo(rejectRow, 0);
                                        rejectRow[_operationOrdinal] = 'R';
                                        TransformRowsRejected++;
                                    }
                                    rejectReason.AppendLine("Column:" + col.Name + ": Tried to insert null into non-null column.");
                                    finalInvalidAction = Function.EInvalidAction.Reject;
                                }
                                passRow[i] = DBNull.Value;
                            }
                            else
                            {
                                try
                                {
                                    var parseresult = TryParse(col.Datatype, value, col.MaxLength);
                                    passRow[i] = parseresult;
                                }
                                catch (Exception ex)
                                {
                                    // if the parse fails on the column, then write out a reject record.
                                    if (rejectRow == null)
                                    {
                                        rejectRow = new object[_columnCount];
                                        passRow.CopyTo(rejectRow, 0);
                                        rejectRow[_operationOrdinal] = 'R';
                                        TransformRowsRejected++;
                                    }
                                    rejectReason.AppendLine(ex.Message);
                                    finalInvalidAction = Function.EInvalidAction.Reject;
                                }
                            }
                        }
                    }
                }

                switch(finalInvalidAction)
                {
                    case Function.EInvalidAction.Pass:
                        passRow[_validationStatusOrdinal] = "passed";
                        return passRow;
                    case Function.EInvalidAction.Clean:
                        passRow[_validationStatusOrdinal] = "cleaned";
                        return passRow;
                    case Function.EInvalidAction.RejectClean:
                        passRow[_validationStatusOrdinal] = "rejected-cleaned";
                        rejectRow[_validationStatusOrdinal] = "rejected-cleaned";
                        rejectRow[_rejectReasonOrdinal] = rejectReason.ToString();
                        _savedRejectRow = rejectRow;
                        return passRow;
                    case Function.EInvalidAction.Reject:
                        passRow[_validationStatusOrdinal] = "rejected";
                        rejectRow[_validationStatusOrdinal] = "rejected";
                        rejectRow[_rejectReasonOrdinal] = rejectReason.ToString();
                        return rejectRow;
                }

                //should never get here.
                throw new TransformException("Validation failed due to an unknown error.");
            }

            return null;

        }

        public override string Details()
        {
            return "Validation";
        }
    }
}
