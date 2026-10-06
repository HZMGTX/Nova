using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Nova.Managers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using static Nova.Menu.Main;
using static Nova.Utilities.AssetUtilities;

namespace Nova.Menu
{
    public partial class UI
    {
        private readonly PointerQueue<Event> padEvents = new PointerQueue<Event>();
        private readonly Scroll inputScroll = new Scroll();
        private Vector2 padPoint;
        private bool padCursor;
        private bool padLeft;
        private bool padRight;
        private bool clearPointer;
        private bool padReady;
        private int padId = -1;
        private int bind = -1;
        private float bindUntil;
        private bool bindCancelled;
        private string padName = "No controller";
        private string padDebug = "";
        private float clickTestUntil;
        private Wii wii;
        private Wii.State wiiState;
        private string wiiStatus = "Wiimote: off";
        private int wiiSession = -1;
        private bool wiiCursor;
        private bool wiiBlocked;
        private bool guiPad;
        private bool guiLeft;
        private Vector2 guiPoint;
        private Vector2 guiOrigin;
        private Event guiEvent;
        private int padControl;
        private Texture2D wiiPoint;
        private Texture2D wiiGrab;

        private Event InputEvent => guiPad ? guiEvent : Event.current;
        private int PointerControl
        {
            get => guiPad ? padControl : GUIUtility.hotControl;
            set
            {
                if (guiPad) padControl = value;
                else GUIUtility.hotControl = value;
            }
        }

        private EventType InputType(int id)
        {
            if (!guiPad) return Event.current.GetTypeForControl(id);
            return padControl != 0 && padControl != id &&
                (guiEvent.isMouse || guiEvent.type == EventType.ScrollWheel) ? EventType.Used : guiEvent.type;
        }

        private Vector2 GuiPoint => guiPad ? GUIUtility.ScreenToGUIPoint(guiOrigin + guiPoint) : Event.current.mousePosition;

        private Vector2 PointerPosition => padCursor ? padPoint : Mouse.current != null
            ? new Vector2(Mouse.current.position.ReadValue().x, ViewHeight - Mouse.current.position.ReadValue().y)
            : new Vector2(ViewWidth / 2, ViewHeight / 2);
        private bool PointerHeld => padCursor ? guiLeft : Mouse.current?.leftButton.isPressed == true;
        private bool DrawPointer => isOpen && Application.isFocused && !Hud.HasMouse &&
            (padCursor || options.remoteMouse && Mouse.current != null);

        private void ResetPointer()
        {
            if (padCursor || padLeft || padRight) clearPointer = true;
            padEvents.Clear();
            padCursor = padLeft = padRight = false;
            guiLeft = false;
            padControl = 0;
            wiiCursor = false;
            padId = -1;
        }

        private Joystick GetJoystick()
        {
            if (Joystick.all.Count == 0) return null;
            return Joystick.all[Mathf.Clamp(options.joystick, 0, Joystick.all.Count - 1)];
        }

        private ButtonControl JoyButton(Joystick joy, int index)
        {
            if (joy == null) return null;
            string path = options.joyButtons[index];
            return string.IsNullOrEmpty(path) ? null : joy.TryGetChildControl<ButtonControl>(path);
        }

        private void ReadPad()
        {
            bindCancelled = false;
            inputScroll.Step(Time.unscaledDeltaTime, options.smoothScroll, options.scrollSmoothing);
            if (options.wiiInput && options.controllerInput)
            {
                if (wii != null && wii.Closing && wii.Finished) wii = null;
                if (wii == null) wii = new Wii();
                if (wii.Closing)
                {
                    wiiState = default;
                    wiiStatus = "Wiimote: reconnecting";
                }
                else wiiState = wii.Read(out wiiStatus);
            }
            else StopWii();
            if (!Application.isFocused || Hud.HasMouse || !options.controllerInput)
            {
                wiiBlocked = true;
                ResetPointer();
                bind = -1;
                return;
            }

            Mouse mouseNow = Mouse.current;
            bool reclaim = mouseNow != null &&
                ((wiiState.points < 2 && mouseNow.delta.ReadValue().sqrMagnitude > 1) ||
                mouseNow.leftButton.wasPressedThisFrame || mouseNow.rightButton.wasPressedThisFrame ||
                mouseNow.scroll.ReadValue().sqrMagnitude > 0);
            if (reclaim)
            {
                ResetPointer();
                bind = -1;
                return;
            }
            if (ReadWii()) return;

            Gamepad gamepad = options.inputMode == 2 ? null : Gamepad.current;
            Joystick joy = options.inputMode == 1 ? null : GetJoystick();
            InputDevice device = gamepad != null ? (InputDevice)gamepad : joy;
            if (device == null || !device.added || !device.enabled)
            {
                padName = "No controller";
                ResetPointer();
                bind = -1;
                return;
            }
            padName = device.displayName;
            if (padId != device.deviceId)
            {
                ResetPointer();
                padId = device.deviceId;
            }

            if (bind >= 0)
            {
                bindCancelled = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
                if (!isOpen || Time.unscaledTime > bindUntil || bindCancelled)
                    bind = -1;
                else if (joy != null)
                {
                    foreach (InputControl control in joy.allControls)
                    {
                        if (!(control is ButtonControl button) || control.parent is Vector2Control || !button.wasPressedThisFrame) continue;
                        options.joyButtons[bind] = control.path.Substring(joy.path.Length + 1);
                        bind = -1;
                        Changed();
                        break;
                    }
                }
                return;
            }

            Vector2 move;
            float scroll;
            bool left, right, menu, back;
            float precision = 1;
            if (gamepad != null)
            {
                move = gamepad.leftStick.ReadValue();
                Vector2 dpad = gamepad.dpad.ReadValue();
                if (dpad.sqrMagnitude > 0) move = dpad;
                scroll = gamepad.rightStick.ReadValue().y;
                if (gamepad.leftShoulder.isPressed) scroll = 1;
                if (gamepad.rightShoulder.isPressed) scroll = -1;
                left = gamepad.buttonSouth.isPressed;
                right = gamepad.buttonWest.isPressed;
                menu = gamepad.startButton.wasPressedThisFrame;
                back = gamepad.buttonEast.wasPressedThisFrame;
                if (gamepad.leftTrigger.isPressed) precision = 0.3f;
            }
            else
            {
                move = joy.stick?.ReadValue() ?? Vector2.zero;
                foreach (InputControl control in joy.allControls)
                    if (control is DpadControl dpad && dpad.ReadValue().sqrMagnitude > 0) { move = dpad.ReadValue(); break; }
                left = JoyButton(joy, 0)?.isPressed == true;
                right = JoyButton(joy, 1)?.isPressed == true;
                back = JoyButton(joy, 2)?.wasPressedThisFrame == true;
                menu = JoyButton(joy, 3)?.wasPressedThisFrame == true;
                scroll = (JoyButton(joy, 4)?.isPressed == true ? 1 : 0) - (JoyButton(joy, 5)?.isPressed == true ? 1 : 0);
            }

            if (menu)
            {
                ToggleGUI();
                if (isOpen) TakePointer();
                return;
            }
            if (!isOpen) { ResetPointer(); return; }
            if (back)
            {
                if (CurrentPrompt != null)
                {
                    PromptData prompt = CurrentPrompt;
                    if (prompt.DeclineText != null)
                        actions.Enqueue(() => { if (CurrentPrompt == prompt) Main.Toggle("Decline Prompt", true, true); });
                }
                else if (!string.IsNullOrEmpty(focusedInput)) ClearInput();
                else ToggleGUI();
                ResetPointer();
                return;
            }

            if (move.magnitude < 0.2f) move = Vector2.zero;
            else move = move.normalized * Mathf.InverseLerp(0.2f, 1, Mathf.Min(1, move.magnitude));
            if (Mathf.Abs(scroll) < 0.2f) scroll = 0;
            Mouse mouse = Mouse.current;
            bool mouseUsed = mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 1 ||
                mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.scroll.ReadValue().sqrMagnitude > 0);
            if (mouseUsed)
            {
                ResetPointer();
                return;
            }
            bool activity = move.sqrMagnitude > 0 || scroll != 0 || left || right;
            if (!padCursor && !activity) return;
            if (!padCursor) TakePointer();
            Vector2 old = padPoint;
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            padPoint += new Vector2(move.x, -move.y) * options.cursorSpeed * ScreenScale() * precision * delta;
            padPoint.x = Mathf.Clamp(padPoint.x, 0, Mathf.Max(0, ViewWidth - 1));
            padPoint.y = Mathf.Clamp(padPoint.y, 0, Mathf.Max(0, ViewHeight - 1));
            QueueClick(left, ref padLeft, 0);
            QueueClick(right, ref padRight, 1);
            if (padEvents.Count == 0)
            {
                if (padLeft && padPoint != old) QueuePointer(EventType.MouseDrag, 0, padPoint - old);
                else if (scroll != 0) QueuePointer(EventType.ScrollWheel, 0, new Vector2(0, -scroll * 24 * delta));
            }
        }

        private void TakePointer()
        {
            if (!padReady || Mouse.current != null)
            {
                padPoint = PointerPosition;
                padReady = true;
            }
            clearPointer = true;
            padCursor = true;
        }

        private void StopWii()
        {
            wii?.Dispose();
            wiiState = default;
            wiiStatus = "Wiimote: off";
            wiiSession = -1;
            if (wiiCursor) ResetPointer();
        }

        private bool ReadWii()
        {
            if (!wiiState.connected)
            {
                if (wiiCursor) ResetPointer();
                return false;
            }
            padName = "Wiimote (menu cursor only)";
            bind = -1;
            if (wiiBlocked)
            {
                if (wiiState.buttons != 0) return true;
                wiiBlocked = false;
                wiiState.pressed = 0;
            }
            if (wiiSession != wiiState.session)
            {
                ResetPointer();
                wiiSession = wiiState.session;
            }
            int pressed = wiiState.pressed;
            if ((pressed & 0x80) != 0) ToggleGUI();
            if (!isOpen) return true;
            if (!wiiCursor)
            {
                ResetPointer();
                padPoint = new Vector2(ViewWidth / 2, ViewHeight / 2);
                padReady = padCursor = wiiCursor = true;
                clearPointer = true;
            }
            if ((pressed & 2) != 0 && wiiState.points >= 2)
            {
                options.wiiX = 0.5f - wiiState.x;
                options.wiiY = 0.5f - wiiState.y;
                Changed();
            }
            Vector2 old = padPoint;
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            int buttons = wiiState.buttons | pressed;
            Vector2 move = new Vector2(((buttons & 0x200) != 0 ? 1 : 0) - ((buttons & 0x100) != 0 ? 1 : 0),
                ((buttons & 0x400) != 0 ? 1 : 0) - ((buttons & 0x800) != 0 ? 1 : 0));
            if (move.sqrMagnitude > 0)
                padPoint += move.normalized * options.cursorSpeed * ScreenScale() * delta;
            else if (wiiState.points >= 2)
            {
                Vector2 target = new Vector2(
                    Mathf.Clamp01(0.5f + (wiiState.x + options.wiiX - 0.5f) * 1.6f) * (ViewWidth - 1),
                    Mathf.Clamp01(0.5f + (wiiState.y + options.wiiY - 0.5f) * 1.6f) * (ViewHeight - 1));
                padPoint = Vector2.Lerp(padPoint, target, 1 - Mathf.Exp(-25 * delta));
            }
            padPoint.x = Mathf.Clamp(padPoint.x, 0, Mathf.Max(0, ViewWidth - 1));
            padPoint.y = Mathf.Clamp(padPoint.y, 0, Mathf.Max(0, ViewHeight - 1));
            QueueClick((buttons & 8) != 0, ref padLeft, 0);
            QueueClick((buttons & 4) != 0, ref padRight, 1);
            float scroll = ((buttons & 0x10) != 0 ? 1 : 0) - ((buttons & 0x1000) != 0 ? 1 : 0);
            if (padEvents.Count == 0)
            {
                if (padLeft && padPoint != old) QueuePointer(EventType.MouseDrag, 0, padPoint - old);
                else if (scroll != 0) QueuePointer(EventType.ScrollWheel, 0, new Vector2(0, -scroll * 24 * delta));
            }
            return true;
        }

        private void QueueClick(bool held, ref bool previous, int button)
        {
            if (held == previous) return;
            previous = held;
            QueuePointer(held ? EventType.MouseDown : EventType.MouseUp, button, Vector2.zero);
        }

        private void QueuePointer(EventType type, int button, Vector2 delta)
        {
            padEvents.Enqueue(new Event { type = type, button = button, mousePosition = padPoint, delta = delta, clickCount = 1 });
        }

        private Event DrawInputUIPrepare()
        {
            Event source = Event.current;
            if (clearPointer)
            {
                GUIUtility.hotControl = 0;
                padControl = 0;
                draggingWindow = false;
                draggingPanel = resizingPanel = null;
                clearPointer = false;
            }
            bool usePad = padCursor && isOpen && Application.isFocused && !Hud.HasMouse;
            guiPad = usePad;
            if (!usePad) return null;
            Matrix4x4 matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            guiOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
            GUI.matrix = matrix;
            guiPoint = padPoint;
            if (padEvents.Take(Time.frameCount, source.type == EventType.Repaint, out Event next))
            {
                guiEvent = next;
                if (guiEvent.type == EventType.MouseDown) padControl = 0;
                guiPoint = guiEvent.mousePosition;
                padDebug = guiEvent.type + " btn=" + guiEvent.button + " ctl=" + padControl +
                    " @(" + Mathf.RoundToInt(guiPoint.x) + "," + Mathf.RoundToInt(guiPoint.y) + ")";
                if (guiEvent.button == 0)
                {
                    if (guiEvent.type == EventType.MouseDown)
                    {
                        guiLeft = true;
                        padPressFrame = Time.frameCount;
                        padPressButton = 0;
                        padPressPoint = guiPoint;
                    }
                    else if (guiEvent.type == EventType.MouseUp) guiLeft = false;
                }
                if (guiEvent.button == 1 && guiEvent.type == EventType.MouseDown)
                {
                    padPressFrame = Time.frameCount;
                    padPressButton = 1;
                    padPressPoint = guiPoint;
                }
                return new Event(next);
            }
            guiEvent = source.isKey ? source : new Event { type = EventType.Used };
            return null;
        }

        private void DrawInputUIDraw()
        {
            try { DrawUI(); }
            finally
            {
                guiPad = false;
                guiEvent = null;
            }
        }

        private bool MenuButton(Rect rect, GUIContent content, GUIStyle style)
        {
            return guiPad ? Clicked(rect) : GUI.Button(rect, content, style);
        }

        private void DrawCursor()
        {
            if (!DrawPointer || Event.current.type != EventType.Repaint) return;
            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
            Vector2 point = PointerPosition;
            if (wiiCursor)
            {
                if (wiiPoint == null) wiiPoint = LoadTextureFromResource(PluginInfo.ClientResourcePath + ".wii-point.png");
                if (wiiGrab == null) wiiGrab = LoadTextureFromResource(PluginInfo.ClientResourcePath + ".wii-grab.png");
                float size = 72 * Mathf.Clamp(ScreenScale(), 0.75f, 1.3f);
                GUI.DrawTexture(new Rect(point.x - size / 2, point.y - size / 2, size, size),
                    PointerHeld ? wiiGrab : wiiPoint, ScaleMode.StretchToFill, true);
            }
            else
            {
                Box(new Rect(point.x - 5, point.y - 5, 10, 10), Color.black, 5);
                Box(new Rect(point.x - 3, point.y - 3, 6, 6), PointerHeld ? new Color(0.6f, 0.85f, 1) : Color.white, 3);
            }
            GUI.matrix = matrix;
            GUI.color = color;
        }

        private void DrawInputSettings(Rect area, bool classic)
        {
            BeginScroll(area, inputScroll, 830);
            float width = area.width - 18;
            float y = 0;
            InputLabel(new Rect(4, y, width - 8, 24), padName, classic); y += 30;
            InputLabel(new Rect(4, y, width - 8, 24), wiiStatus, classic); y += 30;
            if (InputButton(new Rect(0, y, width, 28), "Wiimote pointing (mode 4): " + (options.wiiInput ? "On" : "Off"), classic))
            { options.wiiInput = !options.wiiInput; StopWii(); Changed(); } y += 34;
            if (InputButton(new Rect(0, y, width, 28), "Reconnect Wiimote", classic))
            { options.wiiInput = options.controllerInput = true; StopWii(); Changed(); } y += 34;
            if (InputButton(new Rect(0, y, width, 28),
                Time.unscaledTime < clickTestUntil ? "CLICKED!" : "Click test (point here and press A)", classic))
            { clickTestUntil = Time.unscaledTime + 1.5f; } y += 34;
            InputLabel(new Rect(4, y, width - 8, 24),
                "cursor: " + (padCursor ? "on" : "off") + "  A held: " + (guiLeft ? "yes" : "no") +
                "  ctl: " + padControl + "  last: " + padDebug, classic); y += 30;
            InputLabel(new Rect(4, y, width - 8, 24), "Aim at screen center and press 1 to center the pointer", classic); y += 30;
            if (InputButton(new Rect(0, y, width, 28), "Controller cursor: " + (options.controllerInput ? "On" : "Off"), classic))
            { options.controllerInput = !options.controllerInput; Changed(); } y += 34;
            if (InputButton(new Rect(0, y, width, 28), "Draw mouse cursor: " + (options.remoteMouse ? "On" : "Off"), classic))
            { options.remoteMouse = !options.remoteMouse; Changed(); } y += 34;
            string[] modes = { "Auto", "Gamepad", "Joystick" };
            if (InputButton(new Rect(0, y, width, 28), "Input: " + modes[options.inputMode], classic))
            { options.inputMode = (options.inputMode + 1) % modes.Length; ResetPointer(); Changed(); } y += 34;
            Joystick joy = GetJoystick();
            if (InputButton(new Rect(0, y, width, 28), "Joystick: " + (joy?.displayName ?? "Not connected"), classic) && Joystick.all.Count > 0)
            { options.joystick = (options.joystick + 1) % Joystick.all.Count; ResetPointer(); Changed(); } y += 34;
            InputLabel(new Rect(4, y, width - 8, 24), "Cursor speed: " + options.cursorSpeed.ToString("0"), classic); y += 26;
            if (InputButton(new Rect(0, y, 90, 26), "Slower", classic)) { options.cursorSpeed = Mathf.Max(100, options.cursorSpeed - 100); Changed(); }
            if (InputButton(new Rect(98, y, 90, 26), "Faster", classic)) { options.cursorSpeed = Mathf.Min(1600, options.cursorSpeed + 100); Changed(); } y += 36;
            InputLabel(new Rect(4, y, width - 8, 24), "Joystick buttons (click a row, then press a button)", classic); y += 28;
            string[] names = { "Click / drag", "Right click", "Back", "Open / close", "Scroll up", "Scroll down" };
            for (int i = 0; i < names.Length; i++)
            {
                string label = bind == i ? "Press a joystick button... (Esc cancels)" : names[i] + ": " + options.joyButtons[i];
                if (InputButton(new Rect(0, y, width, 28), label, classic) && joy != null)
                {
                    bind = i;
                    bindUntil = Time.unscaledTime + 10;
                    options.inputMode = 2;
                    options.controllerInput = true;
                    ResetPointer();
                    Changed();
                }
                y += 34;
            }
            InputLabel(new Rect(4, y + 6, width - 8, 22), "Xbox: stick / D-pad move, A click, X right click, B back", classic);
            InputLabel(new Rect(4, y + 30, width - 8, 22), "Menu opens/closes; right stick or LB/RB scroll; LT slows down", classic);
            InputLabel(new Rect(4, y + 60, width - 8, 22), "Wiimote: mode 4, A click/drag, B right click, Home open/close", classic);
            InputLabel(new Rect(4, y + 84, width - 8, 22), "Point or use D-pad; +/- scroll; 1 centers the pointer", classic);
            InputLabel(new Rect(4, y + 108, width - 8, 22), "Close Dolphin / remappers while using direct Wiimote input", classic);
            EndScroll();
        }

        private bool InputButton(Rect rect, string text, bool classic) => classic ? Button(rect, text) : TextButton(rect, text);
        private void InputLabel(Rect rect, string text, bool classic)
        {
            if (classic) FitLabel(rect, text, labelStyle, dimColor);
            else Label(rect, text, smallStyle);
        }
    }
}
