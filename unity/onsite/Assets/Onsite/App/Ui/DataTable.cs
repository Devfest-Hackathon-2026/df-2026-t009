using System;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public sealed class DataTable : VisualElement
    {
        readonly int columns;
        public DataTable(params string[] headings)
        {
            AddToClassList("data-table"); columns = headings.Length;
            var header = new VisualElement(); header.AddToClassList("table-header"); header.AddToClassList("row");
            header.Add(UiKit.Label("", "row-triangle"));
            for (var i = 0; i < headings.Length; i++) header.Add(Cell(headings[i], i, true));
            Add(header);
        }

        static Label Cell(string text, int index, bool header)
        {
            var label = UiKit.Label(text, "table-cell cell-" + index + (header ? " section-label" : index == 1 ? "" : " mono"));
            label.tooltip = text; return label;
        }

        public VisualElement AddRow(string[] cells, Action clicked = null, bool selected = false)
        {
            var row = new VisualElement(); row.AddToClassList("table-row"); row.AddToClassList("row");
            row.EnableInClassList("selected", selected); row.focusable = clicked != null;
            row.Add(UiKit.Label("▽", "row-triangle mono"));
            for (var i = 0; i < columns; i++) row.Add(Cell(i < cells.Length ? cells[i] : "", i, false));
            if (clicked != null)
            {
                row.RegisterCallback<ClickEvent>(_ => clicked());
                row.RegisterCallback<NavigationSubmitEvent>(evt => { clicked(); evt.StopPropagation(); });
            }
            Add(row); return row;
        }
    }
}

