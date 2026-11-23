using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AcadDevices.DbEditor
{
    internal sealed class ColumnFilterBar : Panel
    {
        private readonly DataGridView grid;
        private readonly ToolTip toolTip;
        private readonly Dictionary<DataGridViewColumn, TextBox> inputs = new Dictionary<DataGridViewColumn, TextBox>();
        private bool clearing;

        internal ColumnFilterBar(DataGridView grid, ToolTip toolTip)
        {
            this.grid = grid;
            this.toolTip = toolTip;
            Dock = DockStyle.Fill;
            Margin = new Padding(0, 0, 0, 4);
            grid.ColumnWidthChanged += (sender, args) => AlignInputs();
            grid.Scroll += (sender, args) => AlignInputs();
            grid.Layout += (sender, args) => AlignInputs();
            grid.SizeChanged += (sender, args) => AlignInputs();
        }

        internal event EventHandler FiltersChanged;

        internal IEnumerable<KeyValuePair<string, string>> Values => inputs.Select(pair =>
            new KeyValuePair<string, string>(pair.Key.DataPropertyName, pair.Value.Text));

        internal void Rebuild(IReadOnlyDictionary<string, string> previousValues)
        {
            foreach (var input in inputs.Values)
            {
                toolTip.SetToolTip(input, null);
                input.Dispose();
            }
            inputs.Clear();

            foreach (DataGridViewColumn column in grid.Columns)
            {
                var name = $"Фильтр · {column.HeaderText}";
                var input = new TextBox { AccessibleName = name, AutoSize = false };
                if (previousValues.TryGetValue(column.DataPropertyName, out var value))
                    input.Text = value;
                input.TextChanged += (sender, args) =>
                {
                    if (!clearing)
                        FiltersChanged?.Invoke(this, EventArgs.Empty);
                };
                inputs.Add(column, input);
                Controls.Add(input);
                toolTip.SetToolTip(input, name);
            }

            AlignInputs();
        }

        internal void Clear()
        {
            clearing = true;
            foreach (var input in inputs.Values)
                input.Clear();
            clearing = false;
            FiltersChanged?.Invoke(this, EventArgs.Empty);
        }

        private void AlignInputs()
        {
            foreach (var pair in inputs)
            {
                if (pair.Key.Index < 0)
                    continue;

                var rectangle = grid.GetColumnDisplayRectangle(pair.Key.Index, cutOverflow: true);
                pair.Value.Visible = rectangle.Width > 12;
                if (rectangle.Width > 12)
                    pair.Value.Bounds = new Rectangle(rectangle.X + 2, 0, rectangle.Width - 4, Height);
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            AlignInputs();
        }
    }
}
