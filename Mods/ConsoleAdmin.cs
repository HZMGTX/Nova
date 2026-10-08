/*
 * Nova Menu  Mods/ConsoleAdmin.cs
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

using Nova.Classes.Menu;
using Nova.Extensions;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static Nova.Menu.Main;
using static Nova.Utilities.RigUtilities;
using Console = Nova.Classes.Menu.Console;

namespace Nova.Mods
{
    /// <summary>
    /// Admin mods for commands Console already understands but nothing in the menu sent.
    /// </summary>
    /// <remarks>
    /// Every command here has a matching case in Console's receive handler, which only
    /// acts on it when the sender is a known administrator, and only players running
    /// Console receive it at all.
    /// </remarks>
    public static class ConsoleAdmin
    {
        private static float delay;

        private static bool Ready(float cooldown)
        {
            if (Time.time < delay)
                return false;

            delay = Time.time + cooldown;
            return true;
        }

        private static void AtGunTarget(float cooldown, System.Action<VRRig> act)
        {
            if (!GetGunInput(false))
                return;

            var gun = RenderGun();
            if (!GetGunInput(true) || gun.Ray.collider == null)
                return;

            VRRig target = gun.Ray.collider.GetComponentInParent<VRRig>();
            if (target && !target.IsLocal() && Ready(cooldown))
                act(target);
        }

        // ── Players ─────────────────────────────────────────────────────────────

        public static void ShakeGun() =>
            AtGunTarget(0.3f, target => Console.ExecuteCommand("shake", GetPlayerFromVRRig(target).ActorNumber, 0.3f, 2f, false));

        public static void ShakeAll() =>
            Console.ExecuteCommand("shake", ReceiverGroup.Others, 0.3f, 2f, false);

        public static void EarthquakeAll() =>
            Console.ExecuteCommand("shake", ReceiverGroup.Others, 0.8f, 5f, true);

        /// <summary>Glides the target to you over a second and a half instead of snapping.</summary>
        public static void SmoothBringGun() =>
            AtGunTarget(0.5f, target => Console.ExecuteCommand("smoothtp", GetPlayerFromVRRig(target).ActorNumber, GorillaTagger.Instance.bodyCollider.transform.position, 1.5f));

        public static void SmoothBringAll() =>
            Console.ExecuteCommand("smoothtp", ReceiverGroup.Others, GorillaTagger.Instance.bodyCollider.transform.position, 1.5f);

        /// <summary>Console users hear you from anywhere in the map while this is on.</summary>
        public static void GlobalVoiceOn() =>
            Console.ExecuteCommand("spatial", ReceiverGroup.Others, true);

        public static void GlobalVoiceOff() =>
            Console.ExecuteCommand("spatial", ReceiverGroup.Others, false);

        // ── World ───────────────────────────────────────────────────────────────

        // Indices match the menu's own local time mods.
        public static void TimeForEveryone(int timeOfDay) =>
            Console.ExecuteCommand("time", ReceiverGroup.All, timeOfDay);

        public static void WeatherForEveryone(bool rain) =>
            Console.ExecuteCommand("weather", ReceiverGroup.All, (int)(rain ? BetterDayNightManager.WeatherType.Raining : BetterDayNightManager.WeatherType.None));

        public static void FogForEveryone(Color color) =>
            Console.ExecuteCommand("setfog", ReceiverGroup.All, color.r, color.g, color.b, color.a, 0f, float.MaxValue, 0f);

        public static void ResetFogForEveryone() =>
            Console.ExecuteCommand("resetfog", ReceiverGroup.All);

        public static void SendEveryoneTo(string map) =>
            Console.ExecuteCommand("map", ReceiverGroup.Others, map);

        public static readonly string[] Maps =
        {
            "Forest", "City", "Canyons", "Caves", "Beach", "Mountains",
            "Clouds", "Basement", "Metropolis", "Arcade", "Rotating", "Critters"
        };

        // ── Admin effects ───────────────────────────────────────────────────────

        private static string RankOf(string adminName) =>
            ServerData.Owners.Contains(adminName) ? "OWNER"
            : ServerData.SuperAdministrators.Contains(adminName) ? "SUPER ADMIN"
            : "ADMIN";

        private static readonly Dictionary<VRRig, TextMeshPro> rankTags = new Dictionary<VRRig, TextMeshPro>();

        /// <summary>Writes each Console administrator's name and rank under their crown, your own included.</summary>
        public static void AdminNameTags()
        {
            HashSet<VRRig> shown = new HashSet<VRRig>();

            if (PhotonNetwork.InRoom)
                foreach (Player player in PhotonNetwork.PlayerList)
                {
                    if (!ServerData.Administrators.TryGetValue(player.UserId, out string adminName))
                        continue;

                    VRRig rig = player.IsLocal ? VRRig.LocalRig : Console.GetVRRigFromPlayer(player);
                    if (rig == null)
                        continue;

                    if (!rankTags.TryGetValue(rig, out TextMeshPro tag) || tag == null)
                    {
                        tag = new GameObject("Nova_AdminRankTag").AddComponent<TextMeshPro>();
                        tag.fontSize = 4.8f;
                        tag.alignment = TextAlignmentOptions.Center;
                        rankTags[rig] = tag;
                    }

                    tag.SafeSetText($"{adminName.Replace("<", "‹")} <color=grey>·</color> {RankOf(adminName)}");
                    tag.color = rig.playerColor;

                    // Just under the crown, which Console floats above the same point.
                    Transform anchor = Visuals.GetNameTagTransform(rig);
                    tag.transform.localScale = Vector3.one * (0.25f * rig.scaleFactor);
                    tag.transform.position = anchor.position + anchor.up * (0.45f * rig.scaleFactor);
                    tag.transform.LookAt(Camera.main.transform.position);
                    tag.transform.Rotate(0f, 180f, 0f);

                    shown.Add(rig);
                }

            foreach (VRRig rig in rankTags.Keys.Where(rig => !shown.Contains(rig)).ToArray())
            {
                if (rankTags[rig] != null)
                    Object.Destroy(rankTags[rig].gameObject);

                rankTags.Remove(rig);
            }
        }

        public static void DisableAdminNameTags()
        {
            foreach (TextMeshPro tag in rankTags.Values.Where(tag => tag != null))
                Object.Destroy(tag.gameObject);

            rankTags.Clear();
        }

        public static void EnableAdminArrival() =>
            NetworkSystem.Instance.OnJoinedRoomEvent += OnArrival;

        public static void DisableAdminArrival() =>
            NetworkSystem.Instance.OnJoinedRoomEvent -= OnArrival;

        private static void OnArrival() =>
            Console.instance.StartCoroutine(ArriveSoon());

        /// <summary>Lightning where you stand and a notice to Console users, once you are in the room.</summary>
        /// <remarks>Waits a moment so the room's clients have your rig before the strike lands on it.</remarks>
        private static IEnumerator ArriveSoon()
        {
            yield return new WaitForSeconds(2f);

            if (!PhotonNetwork.InRoom || !ServerData.Administrators.TryGetValue(PhotonNetwork.LocalPlayer.UserId, out string adminName))
                yield break;

            Console.ExecuteCommand("strike", ReceiverGroup.All, GorillaTagger.Instance.bodyCollider.transform.position);
            Console.ExecuteCommand("notify", ReceiverGroup.Others, $"{RankOf(adminName)} {adminName} has arrived.");
        }
    }
}
