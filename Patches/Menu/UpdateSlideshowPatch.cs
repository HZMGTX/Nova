/*
 * Nova Menu  Patches/Menu/UpdateSlideshowPatch.cs
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

using HarmonyLib;
using Nova.Classes.Mods;
using TMPro;
using UnityEngine;

namespace Nova.Patches.Menu
{
    [HarmonyPatch(typeof(NewMapsDisplay), nameof(NewMapsDisplay.UpdateSlideshow))]
    public static class UpdateSlideshowPatch
    {
        private static GameObject cachedMapInfoText;
        private static TextMeshPro cachedMapInfoTMP;

        public static bool Prefix(NewMapsDisplay __instance)
        {
            if (VirtualStumpAd.Instance == null || VirtualStumpAd.MapInfoText == null)
                return true;

            if (cachedMapInfoText != VirtualStumpAd.MapInfoText)
            {
                cachedMapInfoText = VirtualStumpAd.MapInfoText;
                cachedMapInfoTMP = cachedMapInfoText.GetComponent<TextMeshPro>();
            }

            __instance.mapImage = VirtualStumpAd.SpriteRenderer;
            if (cachedMapInfoTMP != null)
                __instance.mapInfoTMP = cachedMapInfoTMP;

            if (__instance.mapImage != null && __instance.mapImage.gameObject != null)
                return true;

            // Wait a full interval before trying again instead of retrying every frame
            __instance.lastSlideshowUpdate = Time.time;
            return false;
        }
    }
}
