using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace AcadDevices.Core
{
    public enum SqliteColumnType
    {
        Text,
        Integer,
        Real
    }

    public sealed class SqliteTable
    {
        internal SqliteTable(string databasePath, DataTable data, IReadOnlyDictionary<DataColumn, SqliteColumnType> columnTypes,
            string rowIdAlias, DataColumn rowIdColumn)
        {
            DatabasePath = databasePath;
            Data = data;
            ColumnTypes = columnTypes;
            TextColumns = columnTypes.Where(pair => pair.Value == SqliteColumnType.Text)
                .Select(pair => pair.Key).ToList().AsReadOnly();
            RowIdAlias = rowIdAlias;
            RowIdColumn = rowIdColumn;
        }

        public DataTable Data { get; }
        public IReadOnlyDictionary<DataColumn, SqliteColumnType> ColumnTypes { get; }
        public IReadOnlyList<DataColumn> TextColumns { get; }
        internal string DatabasePath { get; }
        internal string RowIdAlias { get; }
        internal DataColumn RowIdColumn { get; }
    }
}
