/*
 * Nova Menu  Managers/UnityInput.cs
 * A community driven mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Poison Software
 * Copyright (C) 2026  HZMGTX
 * https://github.com/HZMGTX/Nova
 *
 * Modified from Poison Menu (formerly Seralyth Menu)
 * https://github.com/heycanihavethis/Poison
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using UnityEngine;
using UnityEngine.InputSystem;

namespace Nova.Managers
{
    internal static class UnityInput
    {
        // Keyboard (KeyCode)
        internal static bool GetKey(Key key) => !Menu.UI.IsTyping && Keyboard.current?[key].isPressed == true;
        internal static bool GetKeyDown(Key key) => !Menu.UI.IsTyping && Keyboard.current?[key].wasPressedThisFrame == true;
        internal static bool GetKeyUp(Key key) => !Menu.UI.IsTyping && Keyboard.current?[key].wasReleasedThisFrame == true;

        // Keyboard (string key names)
        internal static bool GetKey(string keyName) => !Menu.UI.IsTyping && Input.GetKey(keyName);
        internal static bool GetKeyDown(string keyName) => !Menu.UI.IsTyping && Input.GetKeyDown(keyName);
        internal static bool GetKeyUp(string keyName) => !Menu.UI.IsTyping && Input.GetKeyUp(keyName);

        // Mouse
        internal static Vector3 mousePosition => Input.mousePosition;

        internal static bool GetMouseButton(int button) => !Menu.UI.HasMouse && Input.GetMouseButton(button);

        internal static bool GetMouseButtonDown(int button) => !Menu.UI.HasMouse && Input.GetMouseButtonDown(button);

        internal static bool GetMouseButtonUp(int button) => !Menu.UI.HasMouse && Input.GetMouseButtonUp(button);

        internal static Vector2 MouseScrollDelta => Menu.UI.HasMouse ? Vector2.zero : Input.mouseScrollDelta;
    }
}
