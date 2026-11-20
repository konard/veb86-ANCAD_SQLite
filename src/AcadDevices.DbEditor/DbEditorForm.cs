using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using AcadDevices.Core;

namespace AcadDevices.DbEditor
{
    internal sealed class DbEditorForm : Form
    {
        private const string DatabaseFilter = "База данных SQLite (*.db)|*.db";
        private readonly Label path = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            Text = "База данных не открыта",
            TextAlign = ContentAlignment.MiddleLeft
        };
        private readonly ToolTip pathTip = new ToolTip();
        private readonly ComboBox tables = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220,
            AccessibleName = "Таблица"
        };
        private readonly TextBox filter = new TextBox { Width = 220, AccessibleName = "Поиск / фильтр" };
        private readonly Label status = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        private readonly BindingSource rows = new BindingSource();
        private readonly DataGridView grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
            BackgroundColor = SystemColors.Window,
            AccessibleName = "Записи"
        };
        private readonly Button createTable;
        private readonly Button addRow;
        private readonly Button deleteRow;
        private readonly Button save;
        private readonly Button refresh;
        private SqliteDatabase database;
        private SqliteTable table;
        private bool changingSelection;

        internal DbEditorForm()
        {
            Text = "Редактор базы данных SQLite";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9);
            ClientSize = new Size(1000, 560);
            MinimumSize = new Size(855, 420);
            ShowIcon = false;
            ShowInTaskbar = false;

            createTable = MakeButton("Создать таблицу", CreateTable);
            addRow = MakeButton("Добавить строку", () => Run(AddRow));
            deleteRow = MakeButton("Удалить строку", () => Run(DeleteRows));
            save = MakeButton("Сохранить изменения", () => Run(Save));
            refresh = MakeButton("Обновить", () => Run(RefreshTables));
            var fileButtons = MakeBar(MakeButton("Создать новую БД", CreateDatabase),
                MakeButton("Открыть БД", OpenDatabase));
            var fileBar = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
            fileBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fileBar.Controls.Add(fileButtons, 0, 0);
            fileBar.Controls.Add(path, 1, 0);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(fileBar, 0, 0);
            layout.Controls.Add(MakeBar(MakeLabel("Таблица"), tables, createTable, MakeLabel("Поиск / фильтр"), filter), 0, 1);
            layout.Controls.Add(grid, 0, 2);
            layout.Controls.Add(MakeBar(addRow, deleteRow, save, refresh, status), 0, 3);
            Controls.Add(layout);

            tables.SelectedIndexChanged += (sender, args) => ChangeTable();
            filter.TextChanged += (sender, args) =>
            {
                if (table != null)
                    Run(() => TableFilter.Apply(table, filter.Text));
            };
            grid.CellDoubleClick += (sender, args) =>
            {
                if (args.RowIndex >= 0 && args.ColumnIndex >= 0)
                    grid.BeginEdit(true);
            };
            grid.CellParsing += ParseCell;
            grid.KeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Delete && !grid.IsCurrentCellInEditMode)
                {
                    args.SuppressKeyPress = true;
                    Run(DeleteRows);
                }
            };
            grid.DataError += (sender, args) => ShowError(args.Exception);

            UpdateControls();
        }

        internal void SetDatabase(SqliteDatabase selected)
        {
            ShowTables(selected);
            database = selected;
            path.Text = selected.FilePath;
            pathTip.SetToolTip(path, selected.FilePath);
            UpdateControls();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!CanDiscardEdits())
                e.Cancel = true;
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pathTip.Dispose();
                rows.Dispose();
            }
            base.Dispose(disposing);
        }

        private void CreateDatabase()
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = DatabaseFilter,
                DefaultExt = "db",
                AddExtension = true,
                OverwritePrompt = false
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && CanDiscardEdits())
                    Run(() => SetDatabase(SqliteDatabase.CreateNew(dialog.FileName)));
            }
        }

        private void OpenDatabase()
        {
            using (var dialog = new OpenFileDialog { Filter = DatabaseFilter, CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && CanDiscardEdits())
                    Run(() => SetDatabase(new SqliteDatabase(dialog.FileName)));
            }
        }

        private void CreateTable()
        {
            if (!CanDiscardEdits())
                return;

            using (var dialog = new CreateTableForm(database))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    Run(() => ShowTables(database, dialog.TableName));
            }
        }

        private void ShowTables(SqliteDatabase selected, string preferredTable = null, bool keepFilter = false)
        {
            var names = selected.GetTables().ToArray();
            var name = names.Contains(preferredTable) ? preferredTable : names.FirstOrDefault();
            var snapshot = name == null ? null : selected.LoadTable(name);

            changingSelection = true;
            try
            {
                tables.Items.Clear();
                tables.Items.AddRange(names);
                tables.SelectedItem = name;
                ShowTable(snapshot, keepFilter);
            }
            finally
            {
                changingSelection = false;
            }
        }

        private void ChangeTable()
        {
            if (changingSelection)
                return;

            var previousName = table?.Data.TableName;
            if (tables.SelectedItem is string name && name != previousName &&
                CanDiscardEdits() && Run(() => ShowTable(database.LoadTable(name))))
                return;

            changingSelection = true;
            try
            {
                tables.SelectedItem = previousName;
            }
            finally
            {
                changingSelection = false;
            }
        }

        private void ShowTable(SqliteTable snapshot, bool keepFilter = false)
        {
            grid.DataSource = null;
            rows.DataSource = null;
            grid.Columns.Clear();
            table = snapshot;
            status.Text = string.Empty;
            if (!keepFilter)
                filter.Clear();

            if (table != null)
            {
                foreach (DataColumn column in table.Data.Columns)
                {
                    if (column.ColumnMapping == MappingType.Hidden)
                        continue;

                    grid.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        HeaderText = column.Caption,
                        DataPropertyName = column.ColumnName,
                        MinimumWidth = 75,
                        ReadOnly = column.ReadOnly || string.Equals(column.Caption, "Id", StringComparison.OrdinalIgnoreCase)
                    });
                }

                TableFilter.Apply(table, filter.Text);
                rows.DataSource = table.Data.DefaultView;
                grid.DataSource = rows;
            }

            UpdateControls();
        }

        private void UpdateControls()
        {
            tables.Enabled = tables.Items.Count > 0;
            createTable.Enabled = refresh.Enabled = database != null;
            filter.Enabled = addRow.Enabled = deleteRow.Enabled = save.Enabled = table != null;
        }

        private void AddRow()
        {
            if (!FinishEdit())
                return;

            filter.Clear();
            var row = rows.AddNew();
            var editableColumn = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(column => !column.ReadOnly);
            if (editableColumn != null)
            {
                grid.CurrentCell = grid.Rows[rows.IndexOf(row)].Cells[editableColumn.Index];
                grid.Focus();
                grid.BeginEdit(true);
            }
        }

        private void DeleteRows()
        {
            if (table == null || !FinishEdit())
                return;

            var selected = grid.SelectedRows.Cast<DataGridViewRow>()
                .Select(row => row.DataBoundItem).OfType<DataRowView>().ToArray();
            foreach (var row in selected)
                row.Delete();
        }

        private void ParseCell(object sender, DataGridViewCellParsingEventArgs args)
        {
            if (!(args.Value is string text))
                return;

            var column = table.Data.Columns[grid.Columns[args.ColumnIndex].DataPropertyName];
            args.Value = text.Length == 0 ? (object)DBNull.Value : text;
            if (table.ColumnTypes.TryGetValue(column, out var type))
            {
                var culture = CultureInfo.CurrentCulture;
                if (type == SqliteColumnType.Integer && long.TryParse(text, NumberStyles.Integer, culture, out var integer))
                    args.Value = integer;
                if (type == SqliteColumnType.Real && double.TryParse(text, NumberStyles.Float, culture, out var real) &&
                    !double.IsInfinity(real) && !double.IsNaN(real))
                    args.Value = real;
            }
            args.ParsingApplied = true;
        }

        private void Save()
        {
            if (!FinishEdit())
                return;

            ShowTable(database.SaveTable(table), keepFilter: true);
            status.Text = "Изменения сохранены";
        }

        private void RefreshTables()
        {
            if (CanDiscardEdits())
                ShowTables(database, table?.Data.TableName, keepFilter: true);
        }

        private bool FinishEdit()
        {
            if (!grid.EndEdit())
                return false;
            rows.EndEdit();
            return true;
        }

        private bool CanDiscardEdits()
        {
            if (table == null)
                return true;

            var pending = false;
            if (!Run(() => pending = !FinishEdit() ||
                table.Data.Rows.Cast<DataRow>().Any(row => row.RowState != DataRowState.Unchanged)))
                return false;

            return !pending || MessageBox.Show(this, "Отменить несохранённые изменения?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private bool Run(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception error)
            {
                ShowError(error);
                return false;
            }
        }

        private void ShowError(Exception error) =>
            MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);

        internal static Button MakeButton(string title, Action action)
        {
            var button = new Button { Text = title, AutoSize = true, Padding = new Padding(6, 2, 6, 2) };
            button.Click += (sender, args) => action();
            return button;
        }

        internal static FlowLayoutPanel MakeBar(params Control[] controls)
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            foreach (var control in controls)
                control.Anchor = AnchorStyles.Left;
            bar.Controls.AddRange(controls);
            return bar;
        }

        internal static Label MakeLabel(string title) => new Label
        {
            Text = title,
            AutoSize = true,
            Margin = new Padding(8, 3, 4, 3)
        };
    }
}
