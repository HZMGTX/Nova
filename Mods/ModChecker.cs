/*
 * Nova Menu  Mods/ModChecker.cs
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

using GorillaNetworking;
using Nova.Classes.Menu;
using Nova.Extensions;
using Nova.Managers;
using Nova.Menu;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Console = Nova.Classes.Menu.Console;

namespace Nova.Mods
{
    /// <summary>
    /// Shows what every other player in the room is running.
    /// </summary>
    /// <remarks>
    /// Unmodded Gorilla Tag puts exactly one custom property on a player: the tutorial
    /// flag. The menu's own Bypass Mod Checkers works by stripping everything else, so
    /// any other key a player carries was set by a mod, and mods register under their
    /// own names, which is why the keys are shown as they are. Players running a
    /// Console-based menu can also report which menu and version when an administrator
    /// asks, and the platform and admin rank come from what the game already knows.
    /// </remarks>
    public static class RoomModChecker
    {
        public const string Category = "Room Mod Checker";
        public const string DetailCategory = "Room Mod Checker Player";

        private const string Prefix = "RoomModChecker:";
        private const int ValueLength = 40;

        private sealed class Report
        {
            public Player Player;
            public VRRig Rig;
            public List<(string key, string value)> Properties = new List<(string, string)>();
            public string ConsoleMenu;
            public bool Steam;
            public string Rank;

            public bool Modded => Properties.Count > 0 || ConsoleMenu != null;
        }

        private static Report Check(Player player)
        {
            Report report = new Report { Player = player, Rig = Console.GetVRRigFromPlayer(player) };

            if (player.CustomProperties != null)
                foreach (var entry in player.CustomProperties)
                {
                    string key = entry.Key?.ToString();
                    if (string.IsNullOrEmpty(key) || key == PlayerConfig.Player_HasDoneTutorial)
                        continue;

                    key = Plain(key);

                    report.Properties.Add((NameOf(key), Describe(entry.Value)));
                }

            if (Console.userDictionary.TryGetValue(player, out var menu))
                report.ConsoleMenu = $"{menu.Item2} {menu.Item1}".Trim();

            if (report.Rig != null)
                report.Steam = report.Rig.IsSteam();

            if (ServerData.Administrators.TryGetValue(player.UserId, out string name))
                report.Rank = ServerData.Owners.Contains(name) ? "Owner"
                    : ServerData.SuperAdministrators.Contains(name) ? "Super admin"
                    : "Admin";

            return report;
        }

        // The per-player checker on the Players page only lists mods it has a name for. This
        // uses the same table for names but keeps unknown keys too, since an unlisted mod is
        // still a mod.
        private static readonly Dictionary<string, string> knownMods =
            Visuals.modDictionary.GroupBy(mod => mod.Key.ToLower()).ToDictionary(group => group.Key, group => group.First().Value);

        private static string NameOf(string key) =>
            knownMods.TryGetValue(key.ToLower(), out string name) ? $"{name} <color=grey>({key})</color>" : key;

        /// <summary>Property values are anything a mod chose to put there, so they are shortened and defused.</summary>
        private static string Describe(object value)
        {
            if (value == null)
                return "null";

            string text = value is System.Array array ? $"{value.GetType().GetElementType()?.Name}[{array.Length}]" : value.ToString();
            text = Plain(text);
            return text.Length > ValueLength ? text[..(ValueLength - 1)] + "…" : text;
        }

        // Rich text in a name or value would otherwise restyle the menu, or hide itself.
        private static string Plain(string text) =>
            (text ?? "").Replace("<", "‹").Replace(">", "›").Replace("\n", " ");

        private static string Colour(Report report) =>
            report.Rig != null ? $"#{ColorUtility.ToHtmlStringRGB(report.Rig.playerColor)}" : "#ffffff";

        // ── Room page ───────────────────────────────────────────────────────────

        public static void Open()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Exit Room Mod Checker", method = () => Buttons.CurrentCategoryName = "Safety Mods", isTogglable = false, toolTip = "Returns you back to the safety mods.", legal = true },
                new ButtonInfo { buttonText = Prefix + "Rescan", overlapText = "Rescan Room", method = Scan, isTogglable = false, toolTip = "Checks everyone again. If you are a Console administrator this also asks Console users which menu they run.", legal = true }
            };

            if (!PhotonNetwork.InRoom)
                buttons.Add(new ButtonInfo { buttonText = Prefix + "NoRoom", overlapText = "You are not in a room.", label = true, legal = true });
            else
            {
                Report[] reports = PhotonNetwork.PlayerListOthers.Select(Check).ToArray();
                int modded = reports.Count(report => report.Modded);

                buttons.Add(new ButtonInfo
                {
                    buttonText = Prefix + "Summary",
                    overlapText = reports.Length == 0 ? "Nobody else is here." : $"<color={(modded > 0 ? "red" : "green")}>{modded}</color> of {reports.Length} players show signs of mods",
                    label = true,
                    legal = true
                });

                // Modded players first, then by name.
                foreach (Report report in reports.OrderByDescending(r => r.Modded).ThenBy(r => r.Player.NickName))
                {
                    Report target = report;
                    List<string> tags = new List<string>();

                    if (target.Properties.Count > 0)
                        tags.Add($"<color=red>{target.Properties.Count} mod{(target.Properties.Count == 1 ? "" : "s")}</color>");
                    if (target.ConsoleMenu != null)
                        tags.Add($"<color=orange>{Plain(target.ConsoleMenu)}</color>");
                    if (target.Rank != null)
                        tags.Add($"<color=purple>{target.Rank}</color>");
                    if (target.Rig != null)
                        tags.Add(target.Steam ? "<color=grey>Steam</color>" : "<color=grey>Quest</color>");
                    if (!target.Modded)
                        tags.Add("<color=green>clean</color>");

                    buttons.Add(new ButtonInfo
                    {
                        buttonText = Prefix + target.Player.UserId,
                        overlapText = $"<color={Colour(target)}>{Plain(target.Player.NickName)}</color>  {string.Join(" · ", tags)}",
                        method = () => OpenPlayer(target.Player),
                        isTogglable = false,
                        toolTip = $"Everything the mod checker found on {Plain(target.Player.NickName)}.",
                        legal = true
                    });
                }
            }

            Buttons.buttons[Buttons.GetCategory(Category)] = buttons.ToArray();
            Buttons.CurrentCategoryName = Category;
        }

        /// <summary>Rechecks the room; an administrator also asks Console users to report their menu.</summary>
        public static void Scan()
        {
            if (PhotonNetwork.InRoom && ServerData.Administrators.ContainsKey(PhotonNetwork.LocalPlayer.UserId))
            {
                Console.ExecuteCommand("isusing", ReceiverGroup.All);
                Console.instance.StartCoroutine(ReopenAfterReplies());
            }

            Open();
        }

        private static IEnumerator ReopenAfterReplies()
        {
            yield return new WaitForSeconds(2f);

            if (Buttons.CurrentCategoryName == Category)
                Open();
        }

        // ── Player page ─────────────────────────────────────────────────────────

        private static void OpenPlayer(Player player)
        {
            Report report = Check(player);

            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Exit Room Mod Checker Player", method = Open, isTogglable = false, toolTip = "Returns you back to the mod checker.", legal = true },
                Line("Name", $"<color={Colour(report)}>{Plain(player.NickName)}</color>"),
                Line("Id", $"<color=grey>ID {Plain(player.UserId)}</color>"),
                Line("Platform", report.Rig == null ? "<color=grey>Platform unknown</color>" : report.Steam ? "Steam (PC)" : "Quest"),
                Line("Fps", report.Rig == null ? "<color=grey>FPS unknown</color>" : $"{report.Rig.GetFPS()} FPS"),
                Line("Rank", report.Rank == null ? "<color=grey>Not a Console administrator</color>" : $"<color=purple>Console {report.Rank.ToLower()}</color>"),
                Line("Menu", report.ConsoleMenu == null ? "<color=grey>Console menu: not reported</color>" : $"Console menu: <color=orange>{Plain(report.ConsoleMenu)}</color>"),
                Line("Header", report.Properties.Count == 0 ? "<color=green>No mod properties</color>" : $"<color=red>{report.Properties.Count} mod propert{(report.Properties.Count == 1 ? "y" : "ies")}</color>")
            };

            if (report.Rig != null)
                buttons.Add(new ButtonInfo
                {
                    buttonText = Prefix + "CopyLook",
                    overlapText = "Copy Their Look",
                    method = () =>
                    {
                        VRRig rig = Console.GetVRRigFromPlayer(player);
                        if (rig == null)
                        {
                            NotificationManager.SendNotification($"{Plain(player.NickName)} is no longer here.", 4000);
                            return;
                        }

                        Fun.CopyCosmeticsFrom(rig);
                        NotificationManager.SendNotification($"Now wearing what {Plain(player.NickName)} wears.", 4000);
                    },
                    isTogglable = false,
                    toolTip = "Wears the same cosmetics as this player. Console users see it on you too when you are an administrator."
                });

            int index = 0;
            foreach (var (key, value) in report.Properties)
                buttons.Add(Line("Property" + index++, $"{key} <color=grey>= {value}</color>"));

            Buttons.buttons[Buttons.GetCategory(DetailCategory)] = buttons.ToArray();
            Buttons.CurrentCategoryName = DetailCategory;
        }

        private static ButtonInfo Line(string key, string text) =>
            new ButtonInfo { buttonText = Prefix + "Detail:" + key, overlapText = text, label = true, legal = true };

        // ── Alerts ──────────────────────────────────────────────────────────────

        private static readonly HashSet<string> alerted = new HashSet<string>();

        public static void EnableAlerts()
        {
            alerted.Clear();
            NetworkSystem.Instance.OnPlayerJoined += OnJoined;

            if (PhotonNetwork.InRoom)
                foreach (Player player in PhotonNetwork.PlayerListOthers)
                    Alert(player);
        }

        public static void DisableAlerts() =>
            NetworkSystem.Instance.OnPlayerJoined -= OnJoined;

        private static void OnJoined(NetPlayer joined) =>
            Console.instance.StartCoroutine(AlertSoon(joined.UserId));

        // Mods publish their properties a moment after joining, not with the join itself.
        private static IEnumerator AlertSoon(string userId)
        {
            yield return new WaitForSeconds(3f);

            Player player = PhotonNetwork.PlayerListOthers.FirstOrDefault(p => p.UserId == userId);
            if (player != null)
                Alert(player);
        }

        private static void Alert(Player player)
        {
            Report report = Check(player);
            if (report.Properties.Count == 0 || !alerted.Add(player.UserId))
                return;

            string keys = string.Join(", ", report.Properties.Take(4).Select(p => p.key));
            if (report.Properties.Count > 4)
                keys += $" and {report.Properties.Count - 4} more";

            NotificationManager.SendNotification($"<color=grey>[</color><color=red>MOD CHECKER</color><color=grey>]</color> {Plain(player.NickName)} is running mods: {keys}", 8000);
        }
    }
}
