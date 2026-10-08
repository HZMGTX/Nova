/*
 * Nova Menu  Managers/AchievementManager.cs
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
using Nova.Menu;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Valve.Newtonsoft.Json.Linq;
using static Nova.Menu.Main;
using static Nova.Utilities.AssetUtilities;

namespace Nova.Managers
{
    public static class AchievementManager
    {
        private static List<Achievement> _achievements;
        public static List<Achievement> Achievements
        {
            get
            {
                if (_achievements != null) return _achievements;
                _achievements = new List<Achievement>();

                string[] files = Directory.GetFiles($"{PluginInfo.BaseDirectory}/Achievements");
                foreach (string file in files)
                {
                    if (file.EndsWith(".json"))
                        _achievements.Add(Achievement.FromJObject(JObject.Parse(File.ReadAllText(file))));
                }

                return _achievements;
            }
            set => _achievements = value;
        }

        /// <summary>Every achievement the menu awards.</summary>
        /// <remarks>
        /// Each one is still awarded where it is earned. This list is what the page shows as
        /// locked and what Unlock All grants, so a new achievement belongs here as well.
        /// </remarks>
        public static readonly Achievement[] All =
        {
            new Achievement { name = "Troublemaker", description = "Evade a player report.", icon = "Images/Achievements/troublemaker.png" },
            new Achievement { name = "Sinister", description = "Open the \"Detected Mods\" category.", icon = "Images/Achievements/sinister.png" },
            new Achievement { name = "Veteran", description = "Use the menu for over a year.", icon = "Images/Achievements/veteran.png" },
            new Achievement { name = "Potato", description = "Have 15 FPS for over a minute.", icon = "Images/Achievements/potato.png" },
            new Achievement { name = "EEEEKK!", description = "Be in the same room as a Console administrator.", icon = "Images/Achievements/eeeekk.png" },
            new Achievement { name = "Persistent", description = "Open the menu 100 times.", icon = "Images/Achievements/persistent.png" },
            new Achievement { name = "Dedicated", description = "Enable 50 mods at the same time.", icon = "Images/Achievements/award.png" },
            new Achievement { name = "Too Dedicated", description = "Enable 100 mods at the same time.", icon = "Images/Achievements/red-award.png" },
            new Achievement { name = "Purgatory", description = "Get banned with the menu.", icon = "Images/Achievements/banned.png" },
            new Achievement { name = "Not forever alone...", description = "Make a friend using the friend system.", icon = "Images/Achievements/notforeveralone.png" },
            new Achievement { name = "Popular", description = "Have 25+ friends.", icon = "Images/Achievements/popular.png" }
        };

        public static void EnterAchievementTab()
        {
            // Everything the menu awards, then anything earned that the list does not know.
            Achievement[] shown = All.Concat(Achievements.Where(earned => All.All(known => known.name != earned.name))).ToArray();
            int unlocked = shown.Count(achievement => HasAchievement(achievement.name));

            List<ButtonInfo> achievementButtons = new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Exit Achievements", method = () => Buttons.CurrentCategoryName = "Main", isTogglable = false, toolTip = "Returns you back to the main page." },
                new ButtonInfo { buttonText = "AchievementCount", overlapText = $"{unlocked} of {shown.Length} unlocked", label = true, legal = true }
            };

            if (unlocked < shown.Length)
                achievementButtons.Add(new ButtonInfo { buttonText = "Unlock All Achievements", method = () => UnlockAll(), isTogglable = false, toolTip = "Unlocks every achievement in the menu.", legal = true });

            for (int i = 0; i < shown.Length; i++)
            {
                Achievement achievement = shown[i];
                bool has = HasAchievement(achievement.name);
                achievementButtons.Add(
                    new ButtonInfo
                    {
                        buttonText = $"Achievement{i}",
                        overlapText = has ? achievement.name : $"<color=grey>{achievement.name} (locked)</color>",
                        method = () => PromptSingle($"{achievement.description}{(has ? "" : " (locked)")}<{PluginInfo.ServerResourcePath}/{achievement.icon}>", null, "Done"),
                        isTogglable = false,
                        toolTip = achievement.description,
                        legal = true
                    });
            }

            Buttons.buttons[Buttons.GetCategory("Achievements")] = achievementButtons.ToArray();
            Buttons.CurrentCategoryName = "Achievements";
        }

        /// <summary>Grants every achievement still missing, with one notice for the lot.</summary>
        /// <param name="announceWhenDone">Whether to say so when there was nothing left to unlock.</param>
        public static void UnlockAll(bool announceWhenDone = true)
        {
            Achievement[] missing = All.Where(achievement => !HasAchievement(achievement.name)).ToArray();

            if (missing.Length == 0)
            {
                if (announceWhenDone)
                    NotificationManager.SendNotification("You already have every achievement.", 3000);
                return;
            }

            foreach (Achievement achievement in missing)
                Save(achievement);

            LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/achievement.ogg", "Audio/Menu/achievement.ogg", clip => Play2DAudio(clip, buttonClickVolume / 10f));
            NotificationManager.SendNotification($"<color=grey>[</color><color=purple>ACHIEVEMENT</color><color=grey>]</color> All {All.Length} achievements unlocked!");

            if (Buttons.CurrentCategoryName == "Achievements")
                EnterAchievementTab();
        }

        public static bool HasAchievement(string name) =>
            Achievements.Any(a => a.name == name);

        public static void UnlockAchievement(Achievement achievement)
        {
            if (HasAchievement(achievement.name))
                return;

            LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/achievement.ogg", "Audio/Menu/achievement.ogg", clip => Play2DAudio(clip, buttonClickVolume / 10f));
            NotificationManager.SendNotification($"<color=grey>[</color><color=purple>ACHIEVEMENT</color><color=grey>]</color> Achievement unlocked! \"{achievement.name}\"");

            Save(achievement);
        }

        private static void Save(Achievement achievement)
        {
            Achievements.Add(achievement);
            File.WriteAllText($"{PluginInfo.BaseDirectory}/Achievements/{achievement.name.Hash()}.json", achievement.ToJObject().ToString());
        }

        public struct Achievement
        {
            public string name;

            public string description;
            public string icon;

            public readonly JObject ToJObject() => new JObject
            {
                ["name"] = name,

                ["description"] = description,
                ["icon"] = icon
            };

            public static Achievement FromJObject(JObject obj) => new Achievement
            {
                name = (string)obj["name"],
                description = (string)obj["description"],
                icon = (string)obj["icon"]
            };
        }
    }
}
