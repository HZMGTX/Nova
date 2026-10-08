using System.Collections.Generic;
using UnityEngine;


using UnityEngine.InputSystem;
using static Poison.Menu.Main;

namespace Poison.Menu
{
    public partial class UI
    {
        private sealed class Motion
        {
            public float value;
            public float seen;
            public int frame = -1;
        }

        private readonly Dictionary<string, Motion> motions = new Dictionary<string, Motion>();
        private float cleanAt;
        private bool pointerInside = true;
        private bool snapAnimations;
        private readonly Stack<bool> scrollClips = new Stack<bool>();

        private float Ease(float speed) => 1 - Mathf.Exp(-Time.unscaledDeltaTime * speed * options.animationSpeed);

        private float Animate(string key, float target, float speed = 14)
        {
            if (!motions.TryGetValue(key, out Motion motion))
            {
                motion = new Motion { value = target };
                motions.Add(key, motion);
            }
            motion.seen = Time.unscaledTime;
            if (!options.animations || snapAnimations) motion.value = target;
            else if (Event.current.type == EventType.Repaint && motion.frame != Time.frameCount)
            {
                motion.value = Mathf.Lerp(motion.value, target, Ease(speed));
                motion.frame = Time.frameCount;
            }
            return motion.value;
        }

        private void SnapAnimations() => snapAnimations = true;

        private float Hover(string key, Rect rect) => Animate(key, options.hoverEffects && GUI.enabled && pointerInside && rect.Contains(GuiPoint) ? 1 : 0);

        private static Color Alpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void WhiteText(GUIStyle style)
        {
            foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused, style.onNormal, style.onHover, style.onActive, style.onFocused })
                state.textColor = Color.white;
        }

        private void UpdateTheme()
        {
            Color baseColor = new Color(0.055f, 0.066f, 0.09f, backgroundBaseAlpha);
            Color button = new Color(0.086f, 0.1f, 0.14f, panelBaseAlpha);
            Color highlight = new Color32(158, 139, 255, 255);
            Color text = new Color32(235, 237, 245, 255);
            if (options.syncTheme)
            {
                baseColor = Darken(ThemeColor(menuBackgroundColor), 0.1f);
                button = Darken(ThemeColor(buttonColors[0]), 0.14f);
                highlight = ThemeColor(buttonColors[1]);
                Color.RGBToHSV(highlight, out float h, out float s, out float v);
                highlight = Color.HSVToRGB(h, Mathf.Min(s, 0.7f), Mathf.Max(v, 0.8f));
                text = ThemeColor(textColors[1]);
                if (text.grayscale < 0.72f) text = Color.Lerp(text, Color.white, 0.85f);
                text.a = 1;
            }
            baseColor.a = backgroundBaseAlpha;
            button.a = panelBaseAlpha;
            float speed = options.animations ? Ease(7) : 1;
            background = Color.Lerp(background, baseColor, speed);
            panel = Color.Lerp(panel, Color.Lerp(baseColor, button, 0.65f), speed);
            accent = Color.Lerp(accent, highlight, speed);
            bright = Color.Lerp(bright, text, speed);
            Color.RGBToHSV(accent, out float accentHue, out float accentSat, out float accentVal);
            accent = Color.HSVToRGB(accentHue, Mathf.Clamp01(accentSat * options.accentAmount), Mathf.Max(accentVal, 0.6f));
            border = Color.Lerp(panel, accent, 0.2f * options.accentAmount);
            muted = Color.Lerp(bright, panel, 0.3f);
        }

        private static Color Darken(Color color, float value)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Min(s, 0.35f), Mathf.Clamp(v, value * 0.7f, value));
        }

        private static Color ThemeColor(Poison.Classes.Menu.ExtGradient gradient)
        {
            if (gradient == null) return Color.magenta;
            if (gradient.rainbow) return Color.HSVToRGB(0.75f, 1f, 1f);
            if (gradient.pastelRainbow) return Color.HSVToRGB(0.75f, 0.3f, 1f);
            if (gradient.customColor != null) return gradient.customColor();
            if (gradient.colors != null && gradient.colors.Length > 0) return gradient.colors[0].color;
            return Color.magenta;
        }

        private void BeginScroll(Rect area, Scroll state, float height, Color? scrollColor = null)
        {
            bool showScrollbar = !options.classicUI;
            state.SetBounds(height - area.height);
            Event current = InputEvent;
            int id = GUIUtility.GetControlID(state.GetHashCode(), FocusType.Passive);
            Rect track = new Rect(area.xMax - 10, area.y + 2, 8, area.height - 4);
            float thumbHeight = Mathf.Min(track.height, Mathf.Max(28, track.height * area.height / Mathf.Max(height, area.height)));
            float travel = track.height - thumbHeight;
            Rect thumb = new Rect(track.x, track.y + (state.max > 0 ? state.value / state.max * travel : 0), 8, thumbHeight);
            bool hover = pointerInside && area.Contains(GuiPoint);
            EventType type = InputType(id);
            if (GUI.enabled && type == EventType.ScrollWheel && hover)
            {
                state.target = Mathf.Clamp(state.target + current.delta.y * options.scrollSpeed, 0, state.max);
                if (!options.smoothScroll) state.value = state.target;
                current.Use();
            }
            if (showScrollbar && GUI.enabled && hover && state.max > 0 && type == EventType.MouseDown && current.button == 0 && track.Contains(GuiPoint))
            {
                PointerControl = id;
                ClearInput();
                state.grab = thumb.Contains(GuiPoint) ? GuiPoint.y - thumb.y : thumbHeight / 2;
                state.target = state.value = Mathf.Clamp01((GuiPoint.y - track.y - state.grab) / travel) * state.max;
                current.Use();
            }
            if (PointerControl == id)
            {
                if (type == EventType.MouseDrag)
                {
                    state.target = state.value = Mathf.Clamp01((GuiPoint.y - track.y - state.grab) / Mathf.Max(1, travel)) * state.max;
                    current.Use();
                }
                if (type == EventType.MouseUp || type == EventType.Ignore)
                {
                    PointerControl = 0;
                    if (type == EventType.MouseUp) current.Use();
                }
            }
            if (showScrollbar && state.max > 0)
            {
                float amount = Animate("scroll-" + state.GetHashCode(), track.Contains(GuiPoint) || PointerControl == id ? 1 : 0);
                float width = Mathf.Lerp(4, 8, amount);
                Color ink = scrollColor ?? accent;
                Box(new Rect(track.center.x - 2, track.y, 4, track.height), Alpha(scrollColor ?? border, 0.45f), 2);
                thumb.y = track.y + state.value / state.max * travel;
                Box(new Rect(track.center.x - width / 2, thumb.y, width, thumb.height), Color.Lerp(Alpha(ink, 0.45f), ink, amount), width / 2);
            }
            scrollClips.Push(pointerInside);
            pointerInside = hover;
            GUI.BeginGroup(new Rect(area.x, area.y, area.width - 14, area.height));
            GUI.BeginGroup(new Rect(0, -state.value, area.width - 14, Mathf.Max(area.height, height)));
        }

        private void EndScroll()
        {
            GUI.EndGroup();
            GUI.EndGroup();
            pointerInside = scrollClips.Pop();
        }

        private void CycleTheme(bool forward)
        {
            var button = Buttons.GetIndex("Change Menu Theme");
            if (button != null) Activate(button, forward);
        }
    }
}
