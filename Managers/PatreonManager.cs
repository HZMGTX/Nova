/*
 * Nova Menu  Managers/PatreonManager.cs
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
using Photon.Pun;
using Photon.Realtime;
using Nova.Classes.Menu;
using Nova.Extensions;
using Nova.Menu;
using Nova.Mods;
using Nova.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using static Nova.Utilities.AssetUtilities;
using static Nova.Utilities.RigUtilities;

namespace Nova.Managers
{
    public class PatreonManager : MonoBehaviour
    {
        public static PatreonManager instance = null;

        public void Awake()
        {
            instance = this;
            PhotonNetwork.NetworkingClient.EventReceived += EventReceived;
        }

        public readonly List<PatreonMembership> PatreonMembers = new List<PatreonMembership>();
        public readonly struct PatreonMembership
        {
            public readonly string UserId;
            public readonly string TierName;
            public readonly string IconURL;
            public readonly Color Color;

            public PatreonMembership(string userId, string tierName, string iconURL, Color color)
            {
                UserId = userId;
                TierName = tierName;
                IconURL = iconURL;
                Color = color;
            }
        }

        private Material iconMaterial;
        private readonly Dictionary<VRRig, GameObject> iconPool = new Dictionary<VRRig, GameObject>();
        private static readonly List<Player> excludedIndicators = new List<Player>();

        public static KeyValuePair<NetPlayer, PatreonMembership>[] GetAllMembersInRoom()
        {
            if (!NetworkSystem.Instance.InRoom)
                return Array.Empty<KeyValuePair<NetPlayer, PatreonMembership>>();

            Dictionary<string, PatreonMembership> members = instance.MembersById();
            List<KeyValuePair<NetPlayer, PatreonMembership>> found = new List<KeyValuePair<NetPlayer, PatreonMembership>>();
            foreach (NetPlayer player in NetworkSystem.Instance.PlayerListOthers)
                if (player?.UserId != null && members.TryGetValue(player.UserId, out PatreonMembership membership))
                    found.Add(new KeyValuePair<NetPlayer, PatreonMembership>(player, membership));
            return found.ToArray();
        }

        public static bool IsPlayerPatreonMember(NetPlayer player) =>
            player?.UserId != null && instance.MembersById().ContainsKey(player.UserId);

        // Members looked up by id instead of searched with LINQ for every player every frame.
        private readonly Dictionary<string, PatreonMembership> membersById = new Dictionary<string, PatreonMembership>();
        private int membersVersion = -1, membersByIdVersion = -2;

        /// <summary>Called after the member list is loaded again, so the lookup is rebuilt.</summary>
        public void MembersChanged() => membersVersion++;

        private Dictionary<string, PatreonMembership> MembersById()
        {
            if (membersByIdVersion != membersVersion || membersById.Count == 0 && PatreonMembers.Count > 0)
            {
                membersByIdVersion = membersVersion;
                membersById.Clear();
                foreach (PatreonMembership member in PatreonMembers)
                    if (member.UserId != null)
                        membersById[member.UserId] = member;
            }

            return membersById;
        }

        public static bool IndicatorsEnabled = true;
        private static readonly HashSet<string> failedIcons = new HashSet<string>();
        private static readonly HashSet<string> loadingIcons = new HashSet<string>();
        private readonly Dictionary<VRRig, Material> iconMaterials = new Dictionary<VRRig, Material>();
        private readonly Dictionary<VRRig, Transform> iconNameTags = new Dictionary<VRRig, Transform>();
        private readonly Dictionary<VRRig, string> iconUrls = new Dictionary<VRRig, string>();
        private readonly List<VRRig> staleIcons = new List<VRRig>();

        public void Update()
        {
            staleIcons.Clear();
            foreach (KeyValuePair<VRRig, GameObject> indicator in iconPool)
                if (indicator.Value == null || !IndicatorsEnabled || !indicator.Key.Active() || !IsPlayerPatreonMember(GetPlayerFromVRRig(indicator.Key)) || excludedIndicators.Contains(indicator.Key.GetPhotonPlayer()))
                    staleIcons.Add(indicator.Key);

            foreach (VRRig rig in staleIcons)
                DestroyIndicator(rig);

            if (!IndicatorsEnabled) return;

            if (!NetworkSystem.Instance.InRoom) return;

            Dictionary<string, PatreonMembership> members = MembersById();
            if (members.Count == 0) return;

            Camera viewer = Camera.main;
            Vector3 head = GorillaTagger.Instance.headCollider.transform.position;

            foreach (NetPlayer player in NetworkSystem.Instance.PlayerListOthers)
            {
                if (player?.UserId == null || !members.TryGetValue(player.UserId, out PatreonMembership membership)) continue;

                // Only rigs the cleanup above keeps, so an indicator isn't destroyed and made
                // again every frame.
                VRRig playerRig = GetVRRigFromPlayer(player);
                if (playerRig == null || !playerRig.Active()) continue;
                if (excludedIndicators.Contains(player.GetPlayer())) continue;

                if (!iconPool.TryGetValue(playerRig, out GameObject playerIndicator) || playerIndicator == null)
                    playerIndicator = CreateIndicator(playerRig, player, membership);

                Transform nameTagAnchor = Visuals.GetNameTagTransform(playerRig);
                float distance = Classes.Menu.Console.GetIndicatorDistance(playerRig);
                playerIndicator.transform.localScale = new Vector3(0.4f, 0.4f, 0.01f) * playerRig.scaleFactor;
                playerIndicator.transform.position = nameTagAnchor.position + nameTagAnchor.up * (distance * playerRig.scaleFactor);
                playerIndicator.transform.LookAt(head);

                if (iconNameTags.TryGetValue(playerRig, out Transform nameTag) && nameTag != null)
                {
                    nameTag.position = nameTagAnchor.position + nameTagAnchor.up * ((distance + 0.25f) * playerRig.scaleFactor);
                    if (viewer != null)
                        nameTag.LookAt(viewer.transform.position);
                    nameTag.Rotate(0f, 180f, 0f);
                }
            }
        }

        private GameObject CreateIndicator(VRRig playerRig, NetPlayer player, PatreonMembership membership)
        {
            DestroyIndicator(playerRig);

            GameObject playerIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(playerIndicator.GetComponent<Collider>());

            if (iconMaterial == null)
            {
                iconMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

                iconMaterial.SetFloat("_Surface", 1);
                iconMaterial.SetFloat("_Blend", 0);
                iconMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                iconMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                iconMaterial.SetFloat("_ZWrite", 0);
                iconMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                iconMaterial.renderQueue = (int)RenderQueue.Transparent;
            }

            // Each indicator has its own copy of the material for its own icon; the copy is
            // destroyed with the indicator instead of being left behind.
            Renderer renderer = playerIndicator.GetComponent<Renderer>();
            renderer.material = iconMaterial;
            Material own = renderer.material;
            own.color = Color.white;
            iconMaterials[playerRig] = own;
            iconUrls[playerRig] = membership.IconURL;
            ApplyIcon(own, membership.IconURL, $"Images/Patreon/{player.UserId}.{FileUtilities.GetFileExtension(membership.IconURL)}");

            GameObject go = new GameObject("Nova_Nametag");
            go.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            TextMeshPro textMesh = go.AddComponent<TextMeshPro>();
            textMesh.fontSize = 4.8f;
            textMesh.alignment = TextAlignmentOptions.Center;

            textMesh.SafeSetText(membership.TierName);
            textMesh.SafeSetFontStyle(Main.activeFontStyle);
            textMesh.SafeSetFont(Main.activeFont);
            textMesh.color = membership.Color;
            textMesh.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            textMesh.transform.SetParent(playerIndicator.transform, false);

            iconPool[playerRig] = playerIndicator;
            iconNameTags[playerRig] = go.transform;
            return playerIndicator;
        }

        /// <summary>Puts an icon on an indicator, downloading it in the background the first time.</summary>
        /// <remarks>
        /// The download used to run on the game's main thread inside Update, freezing the game
        /// until it finished.
        /// </remarks>
        private void ApplyIcon(Material material, string url, string fileName)
        {
            if (string.IsNullOrEmpty(url) || failedIcons.Contains(url))
                return;

            string path = $"{PluginInfo.BaseDirectory}/{fileName}";
            if (File.Exists(path))
            {
                try { material.mainTexture = LoadTextureFromURL(url, fileName); }
                catch (Exception e)
                {
                    failedIcons.Add(url);
                    LogManager.LogError($"Could not load a Patreon icon: {e.Message}");
                }
                return;
            }

            if (loadingIcons.Add(url))
                StartCoroutine(DownloadIcon(url, fileName, path));
        }

        private IEnumerator DownloadIcon(string url, string fileName, string path)
        {
            Task download = Task.Run(() =>
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // Written beside the final name and moved into place, so a half-finished file
                // is never taken for a whole one.
                string partial = path + ".part";
                using (WebClient client = new WebClient())
                    client.DownloadFile(url, partial);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(partial, path);
            });

            while (!download.IsCompleted)
                yield return null;

            loadingIcons.Remove(url);
            if (download.Exception != null)
            {
                failedIcons.Add(url);
                LogManager.LogError($"Could not load a Patreon icon: {download.Exception.GetBaseException().Message}");
                yield break;
            }

            // Every indicator still waiting on this icon gets it.
            foreach (KeyValuePair<VRRig, string> waiting in iconUrls)
                if (waiting.Value == url && iconMaterials.TryGetValue(waiting.Key, out Material material) && material != null)
                    ApplyIcon(material, url, fileName);
        }

        private void DestroyIndicator(VRRig rig)
        {
            if (iconMaterials.TryGetValue(rig, out Material material))
            {
                if (material != null && material != iconMaterial)
                    Destroy(material);
                iconMaterials.Remove(rig);
            }

            if (iconPool.TryGetValue(rig, out GameObject indicator) && indicator != null)
                Destroy(indicator);

            iconPool.Remove(rig);
            iconNameTags.Remove(rig);
            iconUrls.Remove(rig);
        }

        public const byte PatreonByte = 74;
        public static void EventReceived(EventData data)
        {
            try
            {
                NetPlayer sender = PhotonNetwork.NetworkingClient.CurrentRoom.GetPlayer(data.Sender);
                if (data.Code != PatreonByte || !IsPlayerPatreonMember(sender)) return;
                VRRig senderRig = GetVRRigFromPlayer(sender);
                object[] args = data.CustomData == null ? new object[] { } : (object[])data.CustomData;
                string command = args.Length > 0 ? (string)args[0] : "";

                switch (command)
                {
                    case "indicator":
                        {
                            if (args.Length > 1 && args[1] is bool enabled)
                            {
                                // Braced: the else used to belong to the inner if, so an
                                // indicator could be hidden but never shown again.
                                if (enabled)
                                {
                                    if (!excludedIndicators.Contains(sender.GetPlayer()))
                                        excludedIndicators.Add(sender.GetPlayer());
                                }
                                else if (excludedIndicators.Contains(sender.GetPlayer()))
                                    excludedIndicators.Remove(sender.GetPlayer());
                            }
                            break;
                        }
                }
            }
            catch { }
        }

        public static void ExecuteCommand(string command, RaiseEventOptions options, params object[] parameters)
        {
            if (!NetworkSystem.Instance.InRoom)
                return;

            PhotonNetwork.RaiseEvent(PatreonByte,
                new object[] { command }
                    .Concat(parameters)
                    .ToArray(),
            options, SendOptions.SendReliable);
        }

        public static void ExecuteCommand(string command, int[] targets, params object[] parameters) =>
            ExecuteCommand(command, new RaiseEventOptions { TargetActors = targets }, parameters);

        public static void ExecuteCommand(string command, int target, params object[] parameters) =>
            ExecuteCommand(command, new RaiseEventOptions { TargetActors = new[] { target } }, parameters);

        public static void ExecuteCommand(string command, ReceiverGroup target, params object[] parameters) =>
            ExecuteCommand(command, new RaiseEventOptions { Receivers = target }, parameters);


        #region Patreon Mods
        public static void SetupPatreonMods(string patreonName)
        {
            NotificationManager.SendNotification($"<color=grey>[</color><color=purple>PATREON</color><color=grey>]</color> Welcome, {patreonName}! Patreon mods have been enabled.", 10000);

            List<ButtonInfo> buttons = Buttons.buttons[Buttons.GetCategory("Main")].ToList();
            buttons.Add(new ButtonInfo { buttonText = "Patreon Mods", method = () => Buttons.CurrentCategoryName = "Patreon Mods", isTogglable = false, toolTip = "Opens the patreon mods." });
            Buttons.buttons[Buttons.GetCategory("Main")] = buttons.ToArray();

            if (Main.dynamicSounds)
            {
                LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/patreon.ogg", "Audio/Menu/patreon.ogg", clip =>
                {
                    clip?.Play(Main.buttonClickVolume / 10f);
                });
            }
        }

        public static void ShowIndicator(bool enabled) =>
            ExecuteCommand("indicator", ReceiverGroup.All, enabled);

        private static int lastPlayerCount;
        public static void ConstantHideIndicator()
        {
            if (!NetworkSystem.Instance.InRoom)
                lastPlayerCount = -1;

            if (NetworkSystem.Instance.InRoom && PhotonNetwork.CurrentRoom.PlayerCount != lastPlayerCount)
            {
                ShowIndicator(false);
                lastPlayerCount = PhotonNetwork.CurrentRoom.PlayerCount;
            }
        }
        #endregion
    }
}