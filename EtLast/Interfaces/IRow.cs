﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Generic;

    public interface IRow
    {
        IEtlContext Context { get; }
        int UID { get; }

        IProcess CreatorProcess { get; }
        IProcess CurrentProcess { get; set; }

        void Init(IEtlContext context, IProcess creatorProcess, int uid, IEnumerable<KeyValuePair<string, object>> initialValues); // called right after creation

        void SetValue(IProcess process, string column, object newValue);

        public void SetStagedValue(string column, object newValue);
        void ApplyStaging(IProcess process);
        bool HasStaging { get; }

        object this[string column] { get; }
        IEnumerable<KeyValuePair<string, object>> Values { get; }

        bool HasValue(string column);

        int ColumnCount { get; }

        bool HasError();

        T GetAs<T>(string column);
        T GetAs<T>(string column, T defaultValueIfNull);

        bool Equals<T>(string column, T value);

        bool IsNull(string column);
        bool IsNullOrEmpty(string column);

        bool IsNullOrEmpty();

        bool Is<T>(string column);
        string FormatToString(string column, IFormatProvider formatProvider = null);

        string ToDebugString();
    }
}