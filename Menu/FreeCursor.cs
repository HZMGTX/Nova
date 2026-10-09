/*
 * Nova Menu  Menu/FreeCursor.cs
 * A community driven mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  HZMGTX
 * https://github.com/HZMGTX/Nova
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

using System.Collections.Generic;
using UnityEngine;

namespace Nova.Menu
{
    /// <summary>Frees the mouse for whichever menus need it, and puts it back as it was once none do.</summary>
    /// <remarks>
    /// The GUI and the HUD each used to save and restore the cursor on their own, so each
    /// saved the state the other had just set and the game's own lock could be lost.
    /// </remarks>
    internal static class FreeCursor
    {
        private static readonly HashSet<object> holders = new HashSet<object>();
        private static bool savedVisible;
        private static CursorLockMode savedLock;

        /// <summary>Keeps the mouse free while this holder needs it; safe to call every frame.</summary>
        public static void Hold(object holder)
        {
            if (holders.Count == 0)
            {
                savedVisible = Cursor.visible;
                savedLock = Cursor.lockState;
            }

            holders.Add(holder);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Lets go for this holder; the cursor goes back as it was once nobody holds it.</summary>
        public static void Release(object holder)
        {
            if (!holders.Remove(holder) || holders.Count > 0)
                return;

            Cursor.lockState = savedLock;
            Cursor.visible = savedVisible;
        }
    }
}
