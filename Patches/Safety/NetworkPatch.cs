/*
 * Nova Menu  Patches/Safety/NetworkPatch.cs
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
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Nova.Patches.Safety
{
    public class NetworkPatch
    {
        public static bool enabled;

        [HarmonyPatch(typeof(NetworkSystemPUN), "ResetSystem")]
        public class ResetSystem
        {
            private static FieldInfo _tokensField;

            [HarmonyPrefix]
            private static void Prefix(NetworkSystemPUN __instance)
            {
                if (!NetworkPatch.enabled)
                    return;

                _tokensField ??= AccessTools.Field(typeof(NetworkSystemPUN), "_taskCancelTokens");
                List<CancellationTokenSource> tokens = _tokensField?.GetValue(__instance) as List<CancellationTokenSource>;
                tokens?.Clear();
            }
        }

        [HarmonyPatch(typeof(NetworkSystemPUN), "FinishAuthenticating")]
        public class FinishAuthenticating
        {
            private static MethodInfo _stateSetter;
            private static FieldInfo _tokensField;
            private static object _authenticated;

            [HarmonyPrefix]
            private static bool Prefix(NetworkSystemPUN __instance)
            {
                if (!NetworkPatch.enabled)
                    return true;

                if (PhotonNetwork.AuthValues != null)
                    return true;

                _stateSetter ??= AccessTools.PropertySetter(typeof(NetworkSystemPUN), "internalState");
                _tokensField ??= AccessTools.Field(typeof(NetworkSystemPUN), "_taskCancelTokens");

                if (_authenticated == null)
                {
                    Type enumType = AccessTools.Inner(typeof(NetworkSystemPUN), "InternalState");
                    if (enumType != null)
                        _authenticated = Enum.Parse(enumType, "Authenticated");
                }

                if (_authenticated == null)
                    return true;

                _stateSetter?.Invoke(__instance, new object[] { _authenticated });

                List<CancellationTokenSource> tokens = _tokensField?.GetValue(__instance) as List<CancellationTokenSource>;
                if (tokens != null)
                {
                    foreach (CancellationTokenSource token in tokens)
                        try { token.Dispose(); } catch { }
                    tokens.Clear();
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(NetworkSystemPUN), "ReturnToSinglePlayer")]
        public class ReturnToSinglePlayer
        {
            private static FieldInfo _tokensField;

            [HarmonyPrefix]
            private static void Prefix(NetworkSystemPUN __instance)
            {
                if (!NetworkPatch.enabled)
                    return;

                _tokensField ??= AccessTools.Field(typeof(NetworkSystemPUN), "_taskCancelTokens");
                List<CancellationTokenSource> tokens = _tokensField?.GetValue(__instance) as List<CancellationTokenSource>;
                tokens?.Clear();
            }
        }

        [HarmonyPatch(typeof(GorillaComputer), "GeneralFailureMessage")]
        public class GeneralFailureMessage
        {
            [HarmonyPrefix]
            private static bool Prefix() =>
                !NetworkPatch.enabled;
        }
    }
}
