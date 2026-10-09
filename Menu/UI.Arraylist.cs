using Nova.Classes.Menu;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


using static Nova.Menu.Main;

namespace Nova.Menu
{
    public partial class UI
    {
        private sealed class ListRow
        {
            public ButtonInfo button;
            public string text;
            public bool active;
            public float alpha;
            public float measured;
            public int frame = -1;
            public string measuredText;
            public int measuredSize = -1;
            public float measuredLimit = -1;
        }

        // Rows are measured and sorted again only when the list, the text size or the order
        // setting changes, not on every GUI event.
        private readonly List<ListRow> sortedRows = new List<ListRow>();
        private readonly List<ButtonInfo> removedRows = new List<ButtonInfo>();
        private int listVersion, sortedVersion = -1, sortedSize = -1;
        private bool sortedByWidth;
        private int overflowShown = -1;
        private string overflowText;

        private static readonly System.Comparison<ListRow> ByWidth = (a, b) =>
        {
            int order = b.measured.CompareTo(a.measured);
            return order != 0 ? order : string.CompareOrdinal(a.text, b.text);
        };

        private static readonly System.Comparison<ListRow> ByName = (a, b) => string.CompareOrdinal(a.text, b.text);

        private readonly Dictionary<ButtonInfo, ListRow> listRows = new Dictionary<ButtonInfo, ListRow>();
        private GUIStyle listStyle;

        private void UpdateArraylist()
        {
            foreach (ListRow row in listRows.Values) row.active = false;
            foreach (Entry entry in entries)
            {
                ButtonInfo button = entry.button;
                if (!button.enabled || button.label || button.hideFromArraylist || entry.category == "Temporary Category") continue;
                if ((hideSettings && entry.category.Contains("Settings")) || (hideMacros && entry.category.Contains("Macro"))) continue;
                if (!listRows.TryGetValue(button, out ListRow row))
                {
                    row = new ListRow { button = button };
                    listRows.Add(button, row);
                }
                string text = entry.title ?? button.buttonText;
                if (!ReferenceEquals(row.text, text)) listVersion++;
                row.text = text;
                row.active = true;
            }
            removedRows.Clear();
            foreach (KeyValuePair<ButtonInfo, ListRow> pair in listRows)
                if (!pair.Value.active) removedRows.Add(pair.Key);
            foreach (ButtonInfo removed in removedRows)
                listRows.Remove(removed);
            if (removedRows.Count > 0 || sortedRows.Count != listRows.Count) listVersion++;
        }

        private void DrawArraylist()
        {
            if (!options.arraylist || listRows.Count == 0) return;

            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            bool enabled = GUI.enabled;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
            GUI.enabled = true;

            listStyle ??= new GUIStyle(textStyle);
            float fit = Mathf.Min(1, ViewHeight / 720f);
            listStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(arraylistScale * options.listScale * fit), 10, 64);
            listStyle.alignment = flipArraylist ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;

            float margin = options.listMargin * fit;
            float rowHeight = listStyle.fontSize + 12;
            float step = rowHeight + options.listSpacing * fit;

            float limit = ViewWidth * 0.65f;
            bool remeasured = false;
            foreach (ListRow row in listRows.Values)
            {
                if (ReferenceEquals(row.measuredText, row.text) && row.measuredSize == listStyle.fontSize && Mathf.Approximately(row.measuredLimit, limit)) continue;
                row.measuredText = row.text;
                row.measuredSize = listStyle.fontSize;
                row.measuredLimit = limit;
                row.measured = Mathf.Min(limit, listStyle.CalcSize(Measure(row.text)).x + 28);
                remeasured = true;
            }

            if (remeasured || sortedVersion != listVersion || sortedSize != listStyle.fontSize || sortedByWidth != options.listByWidth)
            {
                sortedVersion = listVersion;
                sortedSize = listStyle.fontSize;
                sortedByWidth = options.listByWidth;
                sortedRows.Clear();
                sortedRows.AddRange(listRows.Values);
                sortedRows.Sort(options.listByWidth ? ByWidth : ByName);
            }
            List<ListRow> rows = sortedRows;

            float speed = options.animations ? Ease(12) : 1;
            int overflow = 0;
            float y = margin;
            foreach (ListRow row in rows)
            {
                if (Event.current.type == EventType.Repaint && row.frame != Time.frameCount)
                {
                    row.alpha = options.animations ? Mathf.Lerp(row.alpha, 1, speed) : 1;
                    row.frame = Time.frameCount;
                }
                if (y + rowHeight > ViewHeight - margin)
                {
                    overflow++;
                    continue;
                }
                float width = row.measured;
                float x = flipArraylist ? ViewWidth - margin - width : margin;
                GUI.color = new Color(1, 1, 1, row.alpha);
                Rect rect = new Rect(x, y, width, rowHeight);
                if (options.listBackground)
                    Box(rect, Alpha(background, options.listOpacity), 3);
                if (options.listAccent)
                    Box(new Rect(flipArraylist ? rect.xMax - 4 : rect.x + 2, rect.y + 4, 2, rect.height - 8), Alpha(accent, 0.9f), 0);
                float inset = options.listAccent ? 14 : 12;
                Rect text = new Rect(rect.x + inset, rect.y, rect.width - inset - 12, rect.height);
                Label(text, row.text, listStyle, advancedArraylist ? accent : bright);
                y += step;
            }
            if (overflow > 0)
            {
                GUI.color = Color.white;
                if (overflow != overflowShown)
                {
                    overflowShown = overflow;
                    overflowText = "+" + overflow;
                }
                float width = listStyle.CalcSize(Measure(overflowText)).x + 28;
                float x = flipArraylist ? ViewWidth - margin - width : margin;
                Label(new Rect(x, y, width, rowHeight), overflowText, listStyle, muted);
            }

            GUI.matrix = matrix;
            GUI.color = color;
            GUI.enabled = enabled;
        }
    }
}
