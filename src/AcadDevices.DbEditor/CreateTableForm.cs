using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AcadDevices.Core;

namespace AcadDevices.DbEditor
{
    internal sealed class CreateTableForm : Form
    {
        private readonly TextBox tableName = new TextBox { Dock = DockStyle.Fill, AccessibleName = "Имя таблицы" };
        private readonly DataGridView columns = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            BackgroundColor = SystemColors.Window,
            AccessibleName = "Столбцы"
        };

        internal CreateTableForm(SqliteDatabase database)
        {
            Text = "Создать таблицу";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9);
            ClientSize = new Size(500, 320);
            MinimumSize = new Size(460, 280);
            ShowIcon = false;
            ShowInTaskbar = false;

            columns.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Имя столбца", FillWeight = 70 });
            var types = new DataGridViewComboBoxColumn { HeaderText = "Тип данных", FillWeight = 30, FlatStyle = FlatStyle.Flat };
            types.Items.AddRange("TEXT", "INTEGER", "REAL");
            columns.Columns.Add(types);
            columns.Rows.Add("Id", "INTEGER");
            columns.Rows[0].ReadOnly = true;
            columns.Rows.Add(string.Empty, "TEXT");

            var add = DbEditorForm.MakeButton("Добавить столбец", () =>
            {
                int index = columns.Rows.Add(string.Empty, "TEXT");
                columns.CurrentCell = columns.Rows[index].Cells[0];
                columns.BeginEdit(true);
            });
            var remove = DbEditorForm.MakeButton("Удалить столбец", () =>
            {
                if (columns.CurrentRow?.Index > 0)
                    columns.Rows.Remove(columns.CurrentRow);
            });
            var create = DbEditorForm.MakeButton("Создать", () => CreateTable(database));
            var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
            AcceptButton = create;
            CancelButton = cancel;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(DbEditorForm.MakeLabel("Имя таблицы"), 0, 0);
            layout.Controls.Add(tableName, 1, 0);
            layout.Controls.Add(columns, 0, 1);
            layout.SetColumnSpan(columns, 2);
            var buttons = DbEditorForm.MakeBar(add, remove, create, cancel);
            layout.Controls.Add(buttons, 0, 2);
            layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);
        }

        internal string TableName => tableName.Text.Trim();

        private void CreateTable(SqliteDatabase database)
        {
            if (!columns.EndEdit())
                return;

            try
            {
                var definitions = columns.Rows.Cast<DataGridViewRow>().Skip(1)
                    .Select(row => new KeyValuePair<string, SqliteColumnType>(
                        Convert.ToString(row.Cells[0].Value).Trim(),
                        (SqliteColumnType)Enum.Parse(typeof(SqliteColumnType), (string)row.Cells[1].Value, true)))
                    .ToArray();
                if (definitions.Length == 0)
                    throw new InvalidOperationException("Добавьте хотя бы один столбец.");

                database.CreateTable(TableName, definitions);
                DialogResult = DialogResult.OK;
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
