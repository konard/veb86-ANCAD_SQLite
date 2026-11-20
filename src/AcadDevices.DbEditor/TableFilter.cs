using System.Linq;
using System.Text;
using AcadDevices.Core;

namespace AcadDevices.DbEditor
{
    internal static class TableFilter
    {
        internal static void Apply(SqliteTable table, string text)
        {
            if (text.Length == 0 || table.TextColumns.Count == 0)
            {
                table.Data.DefaultView.RowFilter = string.Empty;
                return;
            }

            var pattern = new StringBuilder();
            foreach (var character in text)
            {
                switch (character)
                {
                    case '\'': pattern.Append("''"); break;
                    case '%':
                    case '*':
                    case '[':
                    case ']':
                        pattern.Append('[').Append(character).Append(']');
                        break;
                    default: pattern.Append(character); break;
                }
            }

            var clauses = table.TextColumns.Select(column =>
            {
                var name = column.ColumnName.Replace("\\", "\\\\").Replace("]", "\\]");
                return $"CONVERT([{name}], 'System.String') LIKE '%{pattern}%'";
            });
            table.Data.DefaultView.RowFilter = string.Join(" OR ", clauses);
        }
    }
}
