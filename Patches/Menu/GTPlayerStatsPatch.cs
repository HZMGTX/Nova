/*
 * Seralyth Menu  Patches/Menu/FPSPatch.cs
 * A community driven mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Seralyth Software
 * https://github.com/Seralyth/Seralyth-Menu
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

using HarmonyLib;

namespace Seralyth.Patches.Menu
{
    [HarmonyPatch(typeof(GTPlayerStats), nameof(GTPlayerStats.DelayedUpdate))]
    public class GTPlayerStatsPatch
    {
        public static bool enabled;
        public static short FPS = -1;
        public static short TargetFPS = -1;
        public static short Ping = -1;

        public static bool Prefix()
        {
            if (enabled)
            {
                if (Ping != -1)
                    GTPlayerStats.Ping = Ping;
                if (TargetFPS != -1)
                    GTPlayerStats.TargetFPS = TargetFPS;
                if (FPS != -1)
                    GTPlayerStats.FPS = FPS;
                return false;
            }
            return true;
        }
    }
}
