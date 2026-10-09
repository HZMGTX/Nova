/*
 * Nova Menu  Patches/Menu/AntiCrashPatches.cs
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

using ExitGames.Client.Photon;
using GorillaExtensions;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Patches.Menu
{
    public class AntiCrashPatches
    {
        public static bool enabled;

        [HarmonyPatch(typeof(VRRig), nameof(VRRig.DroppedByPlayer))]
        public class DroppedByPlayer
        {
            public static bool enabled;

            public static bool Prefix(VRRig __instance, VRRig grabbedByRig, Vector3 throwVelocity)
            {
                return !enabled || !__instance.isLocal || throwVelocity.IsValid();
            }
        }

        [HarmonyPatch(typeof(VRRig), nameof(VRRig.RequestCosmetics))]
        public class RequestCosmetics
        {
            private static readonly List<float> callTimestamps = new List<float>();
            public static bool Prefix(VRRig __instance)
            {
                if (enabled && __instance.isLocal)
                {
                    callTimestamps.Add(Time.time);
                    callTimestamps.RemoveAll(t => Time.time - t > 1);

                    return callTimestamps.Count < 15;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(VRRig), nameof(VRRig.RequestMaterialColor))]
        public class RequestMaterialColor
        {
            private static readonly List<float> callTimestamps = new List<float>();
            public static bool Prefix(VRRig __instance)
            {
                if (enabled && __instance.isLocal)
                {
                    callTimestamps.Add(Time.time);
                    callTimestamps.RemoveAll(t => Time.time - t > 1);

                    return callTimestamps.Count < 15;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(DeployedChild), nameof(DeployedChild.Deploy))]
        public class Deploy
        {
            // Clamp instead of skipping: by now the parent has already hidden itself for the deploy
            public static void Prefix(DeployedChild __instance, DeployableObject parent, Vector3 launchPos, Quaternion launchRot, ref Vector3 releaseVel, bool isRemote = false)
            {
                if (enabled && parent != null && parent.m_VRRig != VRRig.LocalRig)
                {
                    if (!releaseVel.IsValid())
                        releaseVel = Vector3.zero;
                    else if (releaseVel.magnitude > 10f)
                        releaseVel = Vector3.ClampMagnitude(releaseVel, 10f);
                }
            }
        }

        [HarmonyPatch(typeof(LuauVm), nameof(LuauVm.OnEvent))]
        public class LuauVmOnEvent
        {
            public static bool Prefix(EventData eventData)
            {
                if (enabled)
                {
                    if (eventData.Code != 180) return false;

                    // Malformed payloads are left to the original, which validates and drops them
                    if (!(eventData.CustomData is object[] args) || args.Length < 2 || !(args[0] is string command) || !(args[1] is double v))
                        return true;

                    if (eventData.Sender != PhotonNetwork.LocalPlayer.ActorNumber && v == PhotonNetwork.LocalPlayer.ActorNumber && command == "leaveGame")
                        return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(RoomInfo), nameof(RoomInfo.InternalCacheProperties))]
        public class InternalCacheProperties
        {
            public static bool Prefix(RoomInfo __instance, Hashtable propertiesToCache)
            {
                return __instance.masterClientId != PhotonNetwork.LocalPlayer.ActorNumber || propertiesToCache.Count != 1 || !propertiesToCache.ContainsKey(248) || !enabled;
            }
        }

        [HarmonyPatch(typeof(GameEntityManager), nameof(GameEntityManager.JoinWithItemsRPC))]
        public class JoinWithItemsRPC
        {
            public static bool Prefix(GameEntityManager __instance, byte[] stateData, int[] netIds, int joiningActorNum, PhotonMessageInfo info)
            {
                return stateData != null && stateData.Length < 255;
            }
        }

        [HarmonyPatch(typeof(GorillaWrappedSerializer), nameof(GorillaWrappedSerializer.FailedToSpawn))]
        public class FailedToSpawn
        {
            public static bool Prefix(GorillaWrappedSerializer __instance)
            {
                if (enabled)
                {
                    __instance.gameObject.SetActive(false);
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(GTSignalRelay), "Photon.Realtime.IOnEventCallback.OnEvent", new System.Type[] { typeof(EventData) })]
        public class GTSignalRelayOnEvent
        {
            public static bool Prefix(EventData eventData)
            {
                if (enabled && eventData.Code == 186 && eventData.CustomData is object[] array)
                {
                    if (array.Length == 0)
                        return false;
                    if (!(array[0] is int))
                        return false;
                }
                return true;
            }
        }
    }
}
