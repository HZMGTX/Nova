using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;



namespace Poison.Menu
{
    public partial class UI
    {
        [Serializable]
        private sealed class Options
        {
            public bool classicUI;
            public float classicWindowX = 0.5f;
            public float classicWindowY = 0.5f;
            public bool syncTheme = true;
            public float scale = 1;
            public float rounding = 1;
            public bool effects = true;
            public bool particles = true;
            public float effectStrength = 1;
            public bool animations = true;
            public float animationSpeed = 1;
            public bool hoverEffects = true;
            public bool smoothScroll = true;
            public float scrollSpeed = 22;
            public float scrollSmoothing = 12;
            public bool compact;
            public bool descriptions = true;
            public bool categoryLabels = true;
            public bool sortModules;
            public bool tooltips = true;
            public bool arraylist = true;
            public bool listOverMenu;
            public bool listBackground = true;
            public bool listAccent = true;
            public bool listByWidth = true;
            public float listScale = 1;
            public float listOpacity = 0.8f;
            public float listSpacing = 4;
            public float listMargin = 18;
            public bool snapGrid;
            public float snapSize = 8;
            public float gridStrength = 1;
            public bool showGrips = true;
            public bool autoArrange;
            public float rowHeight = 22;
            public float uiOpacity = 1;
            public bool panelShadows = true;
            public bool alternatingRows;
            public float accentAmount = 1;
            public List<PanelLayoutEntry> panelLayout;
        }

        [Serializable]
        public sealed class PanelLayoutEntry
        {
            public string key;
            public float x;
            public float y;
            public bool open;
        }

        private Options options = new Options();
        private float saveAt;
        private readonly string optionsPath = Path.Combine(PluginInfo.BaseDirectory, "UI.json");

        private void LoadOptions()
        {
            try
            {
                if (File.Exists(optionsPath))
                {
                    var loaded = new Options();
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(optionsPath), loaded);
                    options = loaded;
                }
                options.scale = Limit(options.scale, 0.75f, 1.3f, 1);
                options.classicWindowX = Limit(options.classicWindowX, 0, 1, 0.5f);
                options.classicWindowY = Limit(options.classicWindowY, 0, 1, 0.5f);
                options.rounding = Limit(options.rounding, 0, 1.5f, 1);
                options.effectStrength = Limit(options.effectStrength, 0, 2, 1);
                options.animationSpeed = Limit(options.animationSpeed, 0.35f, 2.5f, 1);
                options.scrollSpeed = Limit(options.scrollSpeed, 8, 60, 22);
                options.scrollSmoothing = Limit(options.scrollSmoothing, 4, 30, 12);
                options.listScale = Limit(options.listScale, 0.5f, 2, 1);
                options.listOpacity = Limit(options.listOpacity, 0.1f, 1, 0.8f);
                options.listSpacing = Limit(options.listSpacing, 0, 12, 4);
                options.listMargin = Limit(options.listMargin, 4, 100, 18);
                refresh = true;
                saveAt = 0;
            }
            catch (Exception error) { DebugPrint("UI settings: " + error.Message); }
        }

        private static float Limit(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

        private void SaveOptions()
        {
            saveAt = 0;
            try
            {
                Directory.CreateDirectory(PluginInfo.BaseDirectory);
                string temporary = optionsPath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(options, true));
                if (File.Exists(optionsPath)) File.Replace(temporary, optionsPath, null);
                else File.Move(temporary, optionsPath);
            }
            catch (Exception error) { DebugPrint("UI settings: " + error.Message); }
        }

        private void Changed()
        {
            saveAt = Time.unscaledTime + 0.7f;
            refresh = true;
        }

        private float rowY;
        private float settingsHeight;

        private void DrawSettingsBody(Panel deck, float w, float h)
        {
            if (TextButton(new Rect(w - 150, 32, 68, 22), "Reset")) { options = new Options(); Changed(); }
            if (TextButton(new Rect(w - 78, 32, 70, 22), "Save")) SaveOptions();

            float pad = 14f;
            float col = w - pad * 2 - 18f;
            rowY = 0f;
            float content = settingsHeight <= 0f ? 2400f : settingsHeight + 30f;
            BeginScroll(new Rect(pad, 62, w - pad * 2, h - 70), deck.scroll, content);
            rowY = 0f;
            float x = 0f;

            Header(x, col, "Appearance");
            Setting(Next(x, col, SettingHeight), "Sync menu theme", ref options.syncTheme);
            Slider(Next(x, col, SliderHeight), "UI scale", ref options.scale, 0.75f, 1.3f, "P0");
            Slider(Next(x, col, SliderHeight), "Corner cut", ref options.rounding, 0, 1.5f);
            Slider(Next(x, col, SliderHeight), "UI opacity", ref options.uiOpacity, 0.5f, 1, "P0");
            Setting(Next(x, col, SettingHeight), "Panel shadows", ref options.panelShadows);
            Setting(Next(x, col, SettingHeight), "Backdrop grid", ref options.effects);
            Setting(Next(x, col, SettingHeight), "Backdrop sparks", ref options.particles);
            Slider(Next(x, col, SliderHeight), "Effect strength", ref options.effectStrength, 0, 2);

            Header(x, col, "Motion");
            Setting(Next(x, col, SettingHeight), "Animations", ref options.animations);
            Slider(Next(x, col, SliderHeight), "Animation speed", ref options.animationSpeed, 0.35f, 2.5f);
            Setting(Next(x, col, SettingHeight), "Hover animations", ref options.hoverEffects);
            Setting(Next(x, col, SettingHeight), "Smooth scrolling", ref options.smoothScroll);
            Slider(Next(x, col, SliderHeight), "Scroll distance", ref options.scrollSpeed, 8, 60, "0");
            Slider(Next(x, col, SliderHeight), "Scroll response", ref options.scrollSmoothing, 4, 30, "0");

            Header(x, col, "Grid");
            Setting(Next(x, col, SettingHeight), "Snap to grid", ref options.snapGrid);
            Setting(Next(x, col, SettingHeight), "Resize grips", ref options.showGrips);
            Slider(Next(x, col, SliderHeight), "Grid size", ref options.snapSize, 4, 64, "0");
            Slider(Next(x, col, SliderHeight), "Grid brightness", ref options.gridStrength, 0, 2, "P0");

            Header(x, col, "Modules");
            Setting(Next(x, col, SettingHeight), "Compact rows", ref options.compact);
            Setting(Next(x, col, SettingHeight), "Descriptions", ref options.descriptions);
            Setting(Next(x, col, SettingHeight), "Category labels", ref options.categoryLabels);
            Setting(Next(x, col, SettingHeight), "Sort alphabetically", ref options.sortModules);
            Setting(Next(x, col, SettingHeight), "Tooltips", ref options.tooltips);
            Slider(Next(x, col, SliderHeight), "Row height", ref options.rowHeight, 18, 34, "0");
            Slider(Next(x, col, SliderHeight), "Accent amount", ref options.accentAmount, 0, 2);

            Header(x, col, "Arraylist");
            Setting(Next(x, col, SettingHeight), "Show arraylist", ref options.arraylist);
            Setting(Next(x, col, SettingHeight), "Draw over the menu", ref options.listOverMenu);
            Setting(Next(x, col, SettingHeight), "Row backgrounds", ref options.listBackground);
            Setting(Next(x, col, SettingHeight), "Accent bars", ref options.listAccent);
            Setting(Next(x, col, SettingHeight), "Sort by width", ref options.listByWidth);
            MenuSetting(Next(x, col, SettingHeight), "Right side", "Flip Arraylist");
            Slider(Next(x, col, SliderHeight), "Text scale", ref options.listScale, 0.5f, 2, "P0");
            Slider(Next(x, col, SliderHeight), "Background opacity", ref options.listOpacity, 0.1f, 1, "P0");
            Slider(Next(x, col, SliderHeight), "Row spacing", ref options.listSpacing, 0, 12, "0");
            Slider(Next(x, col, SliderHeight), "Screen margin", ref options.listMargin, 4, 100, "0");

            Header(x, col, "Theme");
            var theme = Buttons.GetIndex("Change Menu Theme");
            Label(new Rect(x + 4, rowY, col - 8, 20), Plain(theme?.overlapText ?? "Theme"), smallStyle, muted);
            rowY += 24f;
            float half = (col - 6f) / 2f;
            if (TextButton(new Rect(x, rowY, half, 30), "Previous")) CycleTheme(false);
            if (TextButton(new Rect(x + half + 6f, rowY, half, 30), "Next")) CycleTheme(true);
            rowY += 36f;
            if (TextButton(new Rect(x, rowY, half, 30), "Layout editor")) ToggleLayout();
            if (TextButton(new Rect(x + half + 6f, rowY, half, 30), "Save layout")) SaveLayout();
            rowY += 36f;
            if (TextButton(new Rect(x, rowY, half, 30), "Auto sort")) AutoSortPanels();
            if (TextButton(new Rect(x + half + 6f, rowY, half, 30), "Auto arrange: " + (options.autoArrange ? "on" : "off"))) { options.autoArrange = !options.autoArrange; Changed(); }
            rowY += 36f;

            settingsHeight = rowY;
            EndScroll();
        }

        private static float SettingHeight => 32f;
        private static float SliderHeight => 44f;

        private void Header(float x, float width, string title)
        {
            rowY += 14f;
            Label(new Rect(x + 4, rowY, width - 8, 20), title, textStyle, bright);
            rowY += 20f;
            Box(new Rect(x + 4, rowY, width - 8, 1), Alpha(border, 0.7f), 0);
            rowY += 8f;
        }

        private Rect Next(float x, float width, float height)
        {
            Rect rect = new Rect(x, rowY, width, height);
            rowY += height + 6f;
            return rect;
        }

        private float MeasureSettings()
        {
            float saved = rowY;
            rowY = 0f;
            Header(0, 0, "");
            rowY += SettingHeight + 6f + (SliderHeight + 6f) * 3f + (SettingHeight + 6f) * 3f + SliderHeight + 6f;
            Header(0, 0, "");
            rowY += (SettingHeight + 6f) * 2f + (SliderHeight + 6f) * 2f + (SettingHeight + 6f) * 2f;
            Header(0, 0, "");
            rowY += (SettingHeight + 6f) * 2f + (SliderHeight + 6f) * 2f;
            Header(0, 0, "");
            rowY += (SettingHeight + 6f) * 5f + (SliderHeight + 6f) * 2f;
            Header(0, 0, "");
            rowY += (SettingHeight + 6f) * 6f + (SliderHeight + 6f) * 4f;
            Header(0, 0, "");
            rowY += 24f + 36f + 36f;
            float result = rowY + 30f;
            rowY = saved;
            return result;
        }

        private void MenuSetting(Rect rect, string label, string name)
        {
            var button = Buttons.GetIndex(name);
            if (button == null) return;
            bool value = button.enabled;
            if (ToggleSetting(rect, label, value)) Activate(button, true);
        }

        private void Setting(Rect rect, string label, ref bool value)
        {
            if (!ToggleSetting(rect, label, value)) return;
            value = !value;
            Changed();
        }

        private bool ToggleSetting(Rect rect, string label, bool value)
        {
            float hover = Hover("setting-" + label, rect);
            Box(rect, Alpha(accent, hover * 0.07f), 5);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            Label(new Rect(rect.x + 8, rect.y, rect.width - 76, rect.height), label, textStyle);
            float amount = Animate("setting-value-" + label, value ? 1 : 0);
            Rect track = new Rect(rect.xMax - 52, rect.center.y - 11, 44, 22);
            Box(track, Color.Lerp(border, accent, amount), 8);
            Box(new Rect(track.x + 3 + 22 * amount, track.y + 3, 16, 16), bright, 6);
            return clicked;
        }

        private void Slider(Rect rect, string label, ref float value, float min, float max, string format = "0.00")
        {
            Label(new Rect(rect.x + 8, rect.y, rect.width - 95, 22), label, textStyle);
            float typed = NumberField(new Rect(rect.xMax - 68, rect.y, 62, 22), label, value, min, max, format);
            if (!Mathf.Approximately(typed, value)) { value = typed; Changed(); }

            Rect track = new Rect(rect.x + 12, rect.y + 35, rect.width - 24, 4);
            int id = GUIUtility.GetControlID(label.GetHashCode(), FocusType.Keyboard);
            Event current = Event.current;
            EventType type = current.GetTypeForControl(id);
            float next = value;
            if (GUI.enabled && pointerInside && type == EventType.MouseDown && current.button == 0 && new Rect(rect.x, rect.y + 22, rect.width, 26).Contains(current.mousePosition))
            {
                GUIUtility.hotControl = id;
                GUIUtility.keyboardControl = id;
                next = Mathf.Lerp(min, max, Mathf.InverseLerp(track.x, track.xMax, current.mousePosition.x));
                current.Use();
            }
            if (GUIUtility.hotControl == id)
            {
                if (type == EventType.MouseDrag)
                {
                    next = Mathf.Lerp(min, max, Mathf.InverseLerp(track.x, track.xMax, current.mousePosition.x));
                    current.Use();
                }
                if (type == EventType.MouseUp || type == EventType.Ignore)
                {
                    GUIUtility.hotControl = 0;
                    if (type == EventType.MouseUp) current.Use();
                }
            }
            if (GUI.enabled && GUIUtility.keyboardControl == id && type == EventType.KeyDown && (current.keyCode == KeyCode.LeftArrow || current.keyCode == KeyCode.RightArrow))
            {
                next = Mathf.Clamp(value + (current.keyCode == KeyCode.RightArrow ? 1 : -1) * (max - min) / 50, min, max);
                current.Use();
            }
            if (!Mathf.Approximately(value, next)) { value = next; Changed(); }

            float fill = Animate("slider-" + label, Mathf.InverseLerp(min, max, value), 22);
            float knobX = track.x + track.width * fill;
            Box(track, border, 2);
            Box(new Rect(track.x, track.y, track.width * fill, 4), accent, 2);
            float radius = 4 + Hover("slider-hover-" + label, rect) * 1.5f;
            Box(new Rect(knobX - radius, track.center.y - radius, radius * 2, radius * 2), bright, radius);
        }

        private float NumberField(Rect rect, string label, float value, float min, float max, string format)
        {
            string id = "ui-num-" + label;
            bool focused = focusedInput == id;

            if (GUI.enabled && !focused && Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                focusedInput = id;
                inputText = format.IndexOf('P') >= 0
                    ? (value * 100f).ToString("0", CultureInfo.InvariantCulture)
                    : value.ToString(format, CultureInfo.InvariantCulture);
                inputClicked = true;
                focused = true;
                Event.current.Use();
            }

            float hover = Hover("num-hover-" + label, rect);
            Box(rect, Color.Lerp(border, accent, focused ? 0.5f : hover * 0.4f), 6);
            Box(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Color.Lerp(panel, accent, focused ? 0.14f : hover * 0.1f), 5);

            if (focused)
            {
                if (inputText.Length > 0 && float.TryParse(inputText, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                {
                    if (format.IndexOf('P') >= 0) parsed /= 100f;
                    value = Mathf.Clamp(parsed, min, max);
                }
                Label(rect, inputText, numberStyle);
                if (Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f)
                {
                    float textWidth = Mathf.Min(numberStyle.CalcSize(new GUIContent(inputText)).x, rect.width - 4);
                    Box(new Rect(rect.center.x + textWidth * 0.5f, rect.y + 4, 1, rect.height - 8), bright, 0);
                }
                return value;
            }

            Label(rect, value.ToString(format, CultureInfo.InvariantCulture), numberStyle);
            return value;
        }
    }
}

