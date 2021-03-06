﻿namespace FizzCode.EtLast.EPPlus
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using FizzCode.EtLast;
    using OfficeOpenXml;

    public class EpPlusSingleExcelFileWriterMutator<TState> : AbstractMutator, IRowWriter
        where TState : BaseExcelWriterState, new()
    {
        public string FileName { get; set; }
        public Action<ExcelPackage, TState> Initialize { get; set; }
        public Action<IRow, ExcelPackage, TState> Action { get; set; }
        public Action<ExcelPackage, TState> Finalize { get; set; }
        public ExcelPackage ExistingPackage { get; set; }
        private TState _state;
        private ExcelPackage _package;
        private int? _storeUid;

        public EpPlusSingleExcelFileWriterMutator(ITopic topic, string name)
            : base(topic, name)
        {
        }

        protected override void StartMutator()
        {
            _state = new TState();
        }

        protected override void CloseMutator()
        {
            if (_state.LastWorksheet != null)
            {
                Finalize?.Invoke(_package, _state);
            }

            if (ExistingPackage == null && _package != null)
            {
                var iocUid = Context.RegisterIoCommandStart(this, IoCommandKind.fileWrite, PathHelpers.GetFriendlyPathName(FileName), null, null, null, null,
                    "saving excel package to {FileName}",
                    PathHelpers.GetFriendlyPathName(FileName));

                try
                {
                    _package.Save();
                    Context.RegisterIoCommandSuccess(this, IoCommandKind.fileWrite, iocUid, null);
                }
                catch (Exception ex)
                {
                    Context.RegisterIoCommandFailed(this, IoCommandKind.fileWrite, iocUid, null, ex);
                    throw;
                }

                _package.Dispose();
                _package = null;
            }

            _state = null;
        }

        protected override IEnumerable<IRow> MutateRow(IRow row)
        {
            if (_package == null) // lazy load here instead of prepare
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                _package = ExistingPackage ?? new ExcelPackage(new FileInfo(FileName));
#pragma warning restore CA2000 // Dispose objects before losing scope
                Initialize?.Invoke(_package, _state);
            }

            try
            {
                Action.Invoke(row, _package, _state);

                if (_storeUid != null)
                    Context.OnRowStored?.Invoke(this, row, _storeUid.Value);
            }
            catch (Exception ex)
            {
                var exception = new ProcessExecutionException(this, row, "error raised during writing an excel file", ex);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "error raised during writing an excel file, file name: {0}, message: {1}, row: {2}",
                    FileName, ex.Message, row.ToDebugString()));

                exception.Data.Add("FileName", FileName);
                throw exception;
            }

            yield return row;
        }

        protected override void ValidateMutator()
        {
            base.ValidateMutator();

            if (string.IsNullOrEmpty(FileName))
                throw new ProcessParameterNullException(this, nameof(FileName));

            if (Action == null)
                throw new ProcessParameterNullException(this, nameof(Action));
        }

        public void AddWorkSheet(string name)
        {
            _state.LastWorksheet = _package.Workbook.Worksheets.Add(name);
            _storeUid = Context.GetStoreUid(PathHelpers.GetFriendlyPathName(FileName), name);
        }
    }
}