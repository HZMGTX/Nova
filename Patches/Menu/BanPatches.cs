/*
 * Nova Menu  Patches/Menu/BanPatches.cs
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

using GorillaNetworking;
using HarmonyLib;
using PlayFab;
using PlayFab.CloudScriptModels;
using PlayFab.Internal;
using Nova.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nova.Patches.Menu
{
    public class BanPatches
    {
        public static bool enabled;

        [HarmonyPatch(typeof(GorillaServer), nameof(GorillaServer.CheckForBadName))]
        public class AutoBanPlayfabFunction
        {
            public static bool Prefix(CheckForBadNameRequest request, Action<ExecuteFunctionResult> successCallback, Action<PlayFabError> errorCallback)
            {
                if (enabled)
                {
                    successCallback?.Invoke(new ExecuteFunctionResult { FunctionResult = new PlayFab.Json.JsonObject { { "result", 0 } } });
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(GorillaComputer), nameof(GorillaComputer.CheckAutoBanListForName))]
        public class CheckAutoBanListForName
        {
            public static bool Prefix(string nameToCheck, ref bool __result)
            {
                if (enabled)
                {
                    __result = true;
                    return false;
                }

                return true;
            }

            public static bool CheckBanList(string nameToCheck)
            {
                nameToCheck = nameToCheck.ToLower();
                nameToCheck = new string(Array.FindAll<char>(nameToCheck.ToCharArray(), (char c) => char.IsLetterOrDigit(c)));

                foreach (string twoWeekNames in GorillaComputer.instance.anywhereTwoWeek)
                {
                    if (nameToCheck.IndexOf(twoWeekNames) >= 0)
                        return false;
                }

                foreach (string oneWeekNames in GorillaComputer.instance.anywhereOneWeek)
                {
                    if (nameToCheck.IndexOf(oneWeekNames) >= 0 && !nameToCheck.Contains("fagol"))
                        return false;
                }

                string[] exactNames = GorillaComputer.instance.exactOneWeek;
                for (int i = 0; i < exactNames.Length; i++)
                {
                    if (exactNames[i] == nameToCheck)
                        return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PlayFabUnityHttp), nameof(PlayFabUnityHttp.MakeApiCall))]
        public class AntiBanCrash1
        {
            public static bool enabled;

            public static bool Prefix(object reqContainerObj)
            {
                if (!enabled || reqContainerObj == null)
                    return true;

                CallRequestContainer callRequestContainer = (CallRequestContainer)reqContainerObj;

                if (callRequestContainer.ErrorCallback != null)
                {
                    Action<PlayFabError> errorCallback = callRequestContainer.ErrorCallback;
                    void overrideError(PlayFabError error)
                    {
                        string lowerMessage = error?.ErrorMessage?.ToLower() ?? "";
                        if (lowerMessage.Contains("ban") || lowerMessage.Contains("banned") || lowerMessage.Contains("suspended") || lowerMessage.Contains("suspension"))
                        {
                            bool ipBan = lowerMessage.Contains("this ip");
                            if (ipBan)
                                NotificationManager.SendNotification("<color=grey>[</color><color=red>ANTI-BAN</color><color=grey>]</color> Your IP address is currently banned.");
                            else
                                NotificationManager.SendNotification("<color=grey>[</color><color=red>ANTI-BAN</color><color=grey>]</color> Your account is currently banned.");

                            string target = ipBan ? "IP address" : "account";
                            string expiry = error.ErrorDetails?.Values.FirstOrDefault(details => details != null && details.Count > 0)?[0];

                            string fakeMessage;
                            if (expiry == null || expiry == "Indefinite")
                                fakeMessage = $"Your {target} has been banned indefinitely.";
                            else if (DateTime.TryParse(expiry, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime expiryTime))
                                fakeMessage = $"Your {target} has been banned. Hours left: {(int)((expiryTime - DateTime.UtcNow).TotalHours + 1.0)}";
                            else
                                fakeMessage = $"Your {target} has been banned.";

                            PlayFabError fakeError = new PlayFabError
                            {
                                Error = PlayFabErrorCode.UnknownError,
                                ErrorMessage = fakeMessage,
                                ErrorDetails = new Dictionary<string, List<string>>()
                            };

                            // PlayFab invokes error callbacks on the main thread
                            if (NetworkSystem.Instance == null || !NetworkSystem.Instance.InRoom)
                                ShowFailureMessage(fakeMessage);

                            errorCallback?.Invoke(fakeError);
                            return;
                        }
                        errorCallback?.Invoke(error);
                    }

                    callRequestContainer.ErrorCallback = overrideError;
                }

                return true;
            }

            private static void ShowFailureMessage(string message)
            {
                if (GorillaComputer.instance != null)
                    GorillaComputer.instance.GeneralFailureMessage(message);
                else if (CoroutineManager.instance != null)
                    CoroutineManager.instance.StartCoroutine(ShowFailureMessageDelayed(message));
            }

            private static IEnumerator ShowFailureMessageDelayed(string message)
            {
                while (GorillaComputer.instance == null)
                    yield return null;

                GorillaComputer.instance.GeneralFailureMessage(message);
            }
        }

        [HarmonyPatch(typeof(PlayFabWebRequest), nameof(PlayFabWebRequest.MakeApiCall))]
        public class AntiBanCrash2
        {
            public static bool Prefix(object reqContainerObj)
                        => AntiBanCrash1.Prefix(reqContainerObj);
        }
    }
}
