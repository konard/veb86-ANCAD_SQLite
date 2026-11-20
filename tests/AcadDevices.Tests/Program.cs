using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AcadDevices.Core;
using AcadDevices.DbEditor;

namespace AcadDevices.Tests
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            var temp = Path.Combine(Path.GetTempPath(), "ANCAD SQLite " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var database = SqliteDatabase.CreateNew(Path.Combine(temp, "Devices.db"));
                Application.EnableVisualStyles();
                Test("Database initialization and existing files", () => CheckDatabase(database, temp));
                Test("Schema creation, literal filtering and CRUD", () => CheckCrud(database));
                Test("Existing tables and generated columns", () => CheckExistingTables(database));
                Test("Transaction rollback and stale edits", () => CheckRollback(database));
                Test("WinForms table creation, binding, filter, delete, save and refresh", () => CheckEditor(database));
                if (args.Length == 1)
                    CaptureScreenshots(args[0]);
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
            finally
            {
                Directory.Delete(temp, recursive: true);
            }
        }

        private static void CheckDatabase(SqliteDatabase database, string temp)
        {
            Check(database.GetTables().SequenceEqual(new[] { "Devices" }), "Initial table list excludes sqlite_sequence.");
            var devices = database.LoadTable("Devices");
            Check(devices.Data.Rows.Count == 0 && devices.TextColumns.Count == 5, "Expected the default Devices schema.");
            Throws<IOException>(() => SqliteDatabase.CreateNew(database.FilePath));
            var missing = Path.Combine(temp, "Missing.db");
            Throws<SQLiteException>(() => new SqliteDatabase(missing).GetTables());
            Check(!File.Exists(missing), "Opening a missing database must not create it.");
            var empty = Path.Combine(temp, "Empty.db");
            SQLiteConnection.CreateFile(empty);
            Check(new SqliteDatabase(empty).GetTables().Count == 0, "An empty SQLite database can be opened.");
        }

        private static void CheckCrud(SqliteDatabase database)
        {
            const string name = "Bench \"devices\"";
            const string note = "Notes ]\\";
            database.CreateTable(name, new Dictionary<string, SqliteColumnType>
            {
                [note] = SqliteColumnType.Text, ["Code"] = SqliteColumnType.Text,
                ["Quantity"] = SqliteColumnType.Integer, ["Price"] = SqliteColumnType.Real
            });
            Throws<ArgumentException>(() => database.CreateTable("Repeated", new[]
            {
                new KeyValuePair<string, SqliteColumnType>("id", SqliteColumnType.Text)
            }));
            Check(database.GetTables().Contains(name), "A table with a quoted name is listed.");

            var table = database.LoadTable(name);
            table.Data.Rows.Add(DBNull.Value, "Meter 100%[*] 'A'", "CAB-1", "2", "2.5");
            table.Data.Rows.Add(DBNull.Value, "Back-up", "METER", "4", "10");
            table.Data.Rows.Add(DBNull.Value, DBNull.Value, "CAB-3", "9", "2");
            table = database.SaveTable(table);
            Check(table.Data.Rows.Count == 3 && (long)table.Data.Rows[0]["Id"] == 1 &&
                (long)table.Data.Rows[0]["Quantity"] == 2 && (double)table.Data.Rows[0]["Price"] == 2.5,
                "SQLite generated an Id and stored numeric values.");

            TableFilter.Apply(table, "100%[*] 'A'");
            Check(table.Data.DefaultView.Count == 1, "Pattern and identifier metacharacters are matched literally.");
            TableFilter.Apply(table, "meter");
            Check(table.Data.DefaultView.Count == 2, "Search is case-insensitive across both text columns.");
            TableFilter.Apply(table, "9");
            Check(table.Data.DefaultView.Count == 0, "Search excludes numeric columns.");
            TableFilter.Apply(table, string.Empty);
            Check(table.Data.DefaultView.Count == 3, "Clearing search restores all rows.");

            table.Data.Rows[0].Delete();
            table.Data.Rows[1][note] = DBNull.Value;
            table.Data.Rows[2]["Price"] = 6.25;
            table.Data.Rows.Add(DBNull.Value, "New", "CAB-4", 1, 7.25);
            database.SaveTable(table);

            var reopened = new SqliteDatabase(database.FilePath).LoadTable(name);
            var rows = reopened.Data.Rows.Cast<DataRow>().ToDictionary(row => (long)row["Id"]);
            Check(rows.Keys.OrderBy(id => id).SequenceEqual(new long[] { 2, 3, 4 }) &&
                rows[2].IsNull(note) && (double)rows[3]["Price"] == 6.25 && (string)rows[4][note] == "New",
                "One commit persisted the insert, updates and deletion.");
        }

        private static void CheckExistingTables(SqliteDatabase database)
        {
            database.ExecuteNonQuery("CREATE TABLE Legacy (name VARCHAR, value INTEGER DEFAULT 42, rowid TEXT, " +
                "summary TEXT GENERATED ALWAYS AS (name || value) STORED)");
            database.ExecuteNonQuery("INSERT INTO Legacy (name, rowid) VALUES ('same', 'shadow'), ('same', 'shadow')");
            var legacy = database.LoadTable("Legacy");
            Check(legacy.Data.Columns["summary"].ReadOnly, "Generated columns are read-only.");
            legacy.Data.Rows[0]["name"] = "edited";
            legacy.Data.Rows.Add("defaulted");
            legacy = database.SaveTable(legacy);
            Check(legacy.Data.Rows.Cast<DataRow>().Count(row => (string)row["name"] == "same") == 1 &&
                legacy.Data.Rows.Cast<DataRow>().Any(row => (string)row["summary"] == "defaulted42"),
                "Hidden rowid distinguishes duplicates and omitted fields get SQL defaults.");

            database.ExecuteNonQuery("CREATE TABLE Composite (a TEXT, b INTEGER, title TEXT, PRIMARY KEY(a, b)) WITHOUT ROWID");
            database.ExecuteNonQuery("INSERT INTO Composite VALUES ('AA', 1, 'one'), ('AA', 2, 'two')");
            var composite = database.LoadTable("Composite");
            composite.Data.Rows[0]["title"] = "updated";
            composite.Data.Rows[1].Delete();
            composite = database.SaveTable(composite);
            Check(composite.Data.Rows.Count == 1 && (string)composite.Data.Rows[0]["title"] == "updated",
                "Existing composite keys work without a SQLite rowid.");
        }

        private static void CheckRollback(SqliteDatabase database)
        {
            database.ExecuteNonQuery("CREATE TABLE Atomic (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL UNIQUE)");
            database.ExecuteNonQuery("INSERT INTO Atomic VALUES (1, 'keep'), (2, 'unchanged')");
            var table = database.LoadTable("Atomic");
            table.Data.Rows[0].Delete();
            table.Data.Rows.Add(DBNull.Value, "valid");
            table.Data.Rows.Add(DBNull.Value, "unchanged");
            Throws<SQLiteException>(() => database.SaveTable(table));
            var persisted = database.LoadTable("Atomic");
            Check(persisted.Data.Rows.Count == 2 && (string)persisted.Data.Rows[0]["Name"] == "keep" &&
                table.Data.Rows[0].RowState == DataRowState.Deleted, "The failed save rolled back and left edits pending.");

            persisted.Data.Rows[1]["Name"] = "stale";
            database.ExecuteNonQuery("UPDATE Atomic SET Name = 'changed elsewhere' WHERE Id = 2");
            Throws<DBConcurrencyException>(() => database.SaveTable(persisted));
            var actual = database.ExecuteQuery("SELECT Name FROM Atomic WHERE Id = @id", new SQLiteParameter("@id", 2L));
            Check((string)actual.Rows[0][0] == "changed elsewhere", "A stale edit does not overwrite persisted data.");
        }

        private static void CheckEditor(SqliteDatabase database)
        {
            using (var create = new CreateTableForm(database))
            {
                create.Show();
                Application.DoEvents();
                Controls<TextBox>(create).Single().Text = "UiTable";
                Controls<DataGridView>(create).Single().Rows[1].Cells[0].Value = "Название";
                Button(create, "Создать").PerformClick();
                Check(create.DialogResult == DialogResult.OK && database.GetTables().Contains("UiTable"),
                    "The table dialog creates the selected schema.");
            }

            using (var editor = new DbEditorForm())
            {
                editor.SetDatabase(new SqliteDatabase(database.FilePath));
                editor.Show();
                Application.DoEvents();
                var combo = Controls<ComboBox>(editor).Single();
                combo.SelectedItem = "UiTable";
                var grid = Controls<DataGridView>(editor).Single();
                Check(Controls<Label>(editor).Any(label => label.Text == database.FilePath) &&
                    grid.Columns.Count == 2 && grid.Columns[0].ReadOnly,
                    "The path, table switch, visible columns and editable new row are wired.");
                var binding = (BindingSource)grid.DataSource;
                Button(editor, "Добавить строку").PerformClick();
                Check(grid.IsCurrentCellInEditMode, "The added row starts editing.");
                ((TextBox)grid.EditingControl).Text = "Клапан А-02";
                ((IDataGridViewEditingControl)grid.EditingControl).EditingControlValueChanged = true;
                grid.NotifyCurrentCellDirty(true);
                Button(editor, "Сохранить изменения").PerformClick();
                var persisted = database.LoadTable("UiTable").Data.Rows;
                Check(persisted.Count == 1, "Save commits the pending bound row.");
                var savedName = Convert.ToString(persisted[0]["Название"]);
                Check(savedName == "Клапан А-02", $"Save commits the active cell value. Stored value was «{savedName}».");

                var filter = Controls<TextBox>(editor).Single();
                filter.Text = "насос";
                Check(binding.Count == 0, "Typing a filter hides nonmatching rows.");
                filter.Text = "а-02";
                Check(binding.Count == 1, "Typing a substring shows matching rows.");
                grid.Rows[0].Selected = true;
                Button(editor, "Удалить строку").PerformClick();
                Button(editor, "Сохранить изменения").PerformClick();
                Check(database.LoadTable("UiTable").Data.Rows.Count == 0, "Delete and save remove the selected record.");

                database.ExecuteNonQuery("INSERT INTO UiTable (Название) VALUES ('Снаружи')");
                filter.Clear();
                Button(editor, "Обновить").PerformClick();
                Check(binding.Count == 1, "Refresh replaces the view with data from the file.");
            }
        }

        private static void CaptureScreenshots(string directory)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "Devices.db");
            var database = SqliteDatabase.CreateNew(path);
            try
            {
                database.ExecuteNonQuery("INSERT INTO Devices (Name, Type, Model, Manufacturer, Description) VALUES " +
                    "('Насос Н-101', 'Насос', 'P-101', 'Гидромаш', 'Контур охлаждения'), " +
                    "('Клапан К-204', 'Клапан', 'DN 50', 'Вентиль', 'Линия подачи'), " +
                    "('Датчик Т-306', 'Датчик', 'PT100', 'Термосенс', 'Температура подачи')");
                using (var editor = new DbEditorForm())
                {
                    editor.SetDatabase(database);
                    Capture(editor, Path.Combine(directory, "editor.png"));
                }
                using (var create = new CreateTableForm(database))
                {
                    Controls<TextBox>(create).Single().Text = "Measurements";
                    var columns = Controls<DataGridView>(create).Single();
                    columns.Rows[1].Cells[0].Value = "Устройство";
                    columns.Rows.Add("Номер канала", "INTEGER");
                    columns.Rows.Add("Давление", "REAL");
                    Capture(create, Path.Combine(directory, "create-table.png"));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static void Capture(Form form, string path)
        {
            form.Show();
            form.Refresh();
            Application.DoEvents();
            using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, bitmap.Size);
                bitmap.Save(path);
            }
        }

        private static IEnumerable<T> Controls<T>(Control parent) where T : Control =>
            parent.Controls.Cast<Control>().SelectMany(child => new[] { child }.OfType<T>().Concat(Controls<T>(child)));

        private static Button Button(Control parent, string text) => Controls<Button>(parent).Single(button => button.Text == text);

        private static void Test(string name, Action test)
        {
            test();
            Console.WriteLine("PASS " + name);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }
    }
}
