/*
 * Nova Menu  Mods/ConsoleAssets.cs
 * A community driven mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Seralyth Software
 * Copyright (C) 2026  Nova
 *
 * Modified from Seralyth Menu
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

using Nova.Classes.Menu;
using Nova.Managers;
using Nova.Menu;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using Console = Nova.Classes.Menu.Console;

namespace Nova.Mods
{
    /// <summary>
    /// Browses the asset bundles Console hosts and spawns them into the room.
    /// </summary>
    /// <remarks>
    /// Console has always been able to receive an asset: "asset-spawn" arrives over
    /// the network, the bundle is downloaded, and the object appears. Nothing ever
    /// sent one. The only caller of the spawn path was the receive handler itself,
    /// so assets could arrive from another administrator and never be started here.
    /// This is the sending half.
    ///
    /// Every command goes to <see cref="ReceiverGroup.All"/>, which Console handles
    /// locally as well as remotely, so the spawner sees exactly what the room sees.
    /// The receiving side already refuses commands from a sender it does not hold as
    /// an administrator, so these buttons are only useful to one.
    /// </remarks>
    public static class ConsoleAssets
    {
        public const string BundleCategory = "Console Assets";
        public const string ObjectCategory = "Console Objects";
        public const string SpawnedCategory = "Spawned Assets";
        public const string ControlCategory = "Console Asset Control";

        /// <summary>Bundle names as of the last menu release.</summary>
        /// <remarks>
        /// A fallback, not the source of truth. <see cref="RefreshManifest"/> replaces
        /// it with the server's list when one is published, so a bundle added to
        /// ServerData shows up without shipping a new menu.
        /// </remarks>
        private static readonly string[] FallbackBundles =
        {
            "altasmap1", "altasmap4", "amongus_pink", "amongus_red", "angrybird", "aot", "atlas-blue-th", "atlas-light-purple-th",
            "atlas-purple-th", "baldi", "baldi_angry", "banhammer", "basketball", "bckrms", "beam", "blackrsword",
            "blackstar", "block", "bloodstar", "blue", "bluekevinthekube", "boomywoomy", "boryrosesword", "bouncyhammer",
            "brainrotparty", "brite", "broom", "brsword", "btools", "cageyes", "caseohf", "ccc", "cgh", "cghnorb",
            "chair", "chasarena", "cherrybomb", "chickenmapthing", "cinemamovie_theater", "city", "clickbaitmenu",
            "colii", "commandblock", "concert", "console.main1", "consolehamburburassets", "cosmetics", "crosser",
            "cube", "dancer", "dbz", "domain", "domains", "donationnuke", "dragonclaw", "ducity", "effects",
            "enderbasketball", "enderhammer", "enderscythe", "endertravis", "endportalstar", "errorstar", "events",
            "fairyisland", "flamething", "flasheffects", "fnafchase", "football", "friendmirror", "fullysetcoli",
            "glassbridge", "glitchesmaps", "glockgunglockgun", "gm_mcdonalds", "granny", "gth", "gun", "heaven",
            "heavensword", "heavenswordfixed", "hishiba", "hn", "huss", "hut", "icesroseword", "iimenu", "iitomb",
            "industrysravenger", "iphone", "jailcell", "jman", "jobapplication", "jukebox", "karambit", "kars",
            "kittysword", "knife", "l115a33", "lacuca", "leviathan", "lonlyisl", "lowtaper", "map", "maps",
            "maps2", "maxwell", "mccsword", "mcsword", "medkit", "menuobject", "minitravis", "minos", "mistscythe",
            "monster2", "monsters", "mrbeast_meme", "nameless", "namelesslaser", "netflix", "nextbot", "novadomain",
            "novaindihcatah", "objects", "oldtag", "omega", "overkillcharge", "overkillfire", "overkillgun",
            "patapim", "pigeon", "pizzaman", "portal", "portalgun", "portalsword", "powerglove", "purple",
            "purpleshoot", "ra", "raa", "raaa", "raaaa", "randomstuff", "ravenger", "ravyravy", "rblxcarpet",
            "rbsword", "realknife", "red", "reefbackreaper", "rezero", "rgbendersword", "roblox", "runningfreddy",
            "saoplayericon", "sawgun", "scarylarry", "shibaholdable", "shotgun1", "shrek", "simplehouse", "sis",
            "skele", "skibidi toilet", "smurfcat", "soggy", "sogsog", "sonic", "sp", "spraypaint", "squidgame",
            "star_win", "starglitcher", "starpe", "starwand", "stormblade", "subwaysurfers", "summons", "super-crown",
            "theboliedone", "titan", "towerer", "travis", "travisv2", "tri", "tung-tung-tung-sahur", "veritymonster",
            "viltrumite", "vr", "whiterose", "wii", "wings", "wworld", "zeldasword"
        };

        private static string[] bundles = FallbackBundles;

        private static string openBundle;
        private static int spawnCounter;

        /// <summary>Ids are namespaced by actor so two administrators cannot collide.</summary>
        private static int NextAssetId() =>
            NetworkSystem.Instance.LocalPlayer.ActorNumber * 1000 + ++spawnCounter % 1000;

        public static IEnumerator RefreshManifest()
        {
            Task<string> download = DownloadManifest();
            while (!download.IsCompleted)
                yield return null;

            if (download.Exception != null || string.IsNullOrWhiteSpace(download.Result))
                yield break;

            string[] listed = download.Result
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#"))
                .ToArray();

            if (listed.Length > 0)
                bundles = listed;
        }

        private static async Task<string> DownloadManifest()
        {
            using HttpClient client = new HttpClient();
            return await client.GetStringAsync($"{ServerData.AssetURL}/ConsoleAssets.txt");
        }

        // ── Browsing ────────────────────────────────────────────────────────────

        public static void OpenBundles()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Exit Console Assets", method = () => Buttons.CurrentCategoryName = "Admin Mods", isTogglable = false, toolTip = "Returns you back to the admin mods." },
                new ButtonInfo { buttonText = "Spawned Assets", method = OpenSpawned, isTogglable = false, toolTip = "Shows every Console asset currently in the room." }
            };

            foreach (string bundle in bundles)
            {
                string target = bundle;
                buttons.Add(new ButtonInfo
                {
                    buttonText = target,
                    method = () => OpenBundle(target),
                    isTogglable = false,
                    toolTip = $"Loads the {target} bundle and lists what it holds."
                });
            }

            Buttons.buttons[Buttons.GetCategory(BundleCategory)] = buttons.ToArray();
            Buttons.CurrentCategoryName = BundleCategory;
        }

        private static void OpenBundle(string bundle)
        {
            openBundle = bundle;

            Buttons.buttons[Buttons.GetCategory(ObjectCategory)] = new[]
            {
                new ButtonInfo { buttonText = "Exit Console Objects", method = OpenBundles, isTogglable = false, toolTip = "Returns you back to the bundle list." },
                new ButtonInfo { buttonText = $"Loading {bundle}...", label = true }
            };
            Buttons.CurrentCategoryName = ObjectCategory;

            Console.instance.StartCoroutine(LoadBundleObjects(bundle));
        }

        private static IEnumerator LoadBundleObjects(string bundle)
        {
            Task load = Console.LoadAssetBundle(bundle);
            while (!load.IsCompleted)
                yield return null;

            // The player may have walked back out while the download was running.
            if (openBundle != bundle)
                yield break;

            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Exit Console Objects", method = OpenBundles, isTogglable = false, toolTip = "Returns you back to the bundle list." }
            };

            if (load.Exception != null)
            {
                // Carried through verbatim: since the load path started reporting its
                // cause, this is the difference between a missing file and a bundle
                // the game's Unity version cannot open.
                string reason = load.Exception.GetBaseException().Message;
                buttons.Add(new ButtonInfo { buttonText = "This bundle would not load.", label = true });
                buttons.Add(new ButtonInfo { buttonText = reason, label = true });

                Buttons.buttons[Buttons.GetCategory(ObjectCategory)] = buttons.ToArray();
                Console.Log($"Could not open {bundle} for browsing: {reason}");
                yield break;
            }

            AssetBundle loaded = Console.assetBundlePool[bundle];

            // A scene bundle carries no loadable assets and throws if asked for them.
            if (loaded.isStreamedSceneAssetBundle)
            {
                buttons.Add(new ButtonInfo { buttonText = "This bundle holds a scene, not objects.", label = true });
                Buttons.buttons[Buttons.GetCategory(ObjectCategory)] = buttons.ToArray();
                yield break;
            }

            // Async so a bundle of several megabytes does not stall the frame, which
            // in a headset reads as the game hanging.
            AssetBundleRequest request = loaded.LoadAllAssetsAsync<GameObject>();
            while (!request.isDone)
                yield return null;

            if (openBundle != bundle)
                yield break;

            GameObject[] objects = request.allAssets.OfType<GameObject>().ToArray();

            if (objects.Length == 0)
                buttons.Add(new ButtonInfo { buttonText = "This bundle holds no objects.", label = true });

            foreach (GameObject asset in objects)
            {
                string assetName = asset.name;
                buttons.Add(new ButtonInfo
                {
                    buttonText = assetName,
                    method = () => Spawn(bundle, assetName),
                    isTogglable = false,
                    toolTip = $"Spawns {assetName} in front of you for everyone in the room."
                });
            }

            Buttons.buttons[Buttons.GetCategory(ObjectCategory)] = buttons.ToArray();
        }

        // ── Spawning ────────────────────────────────────────────────────────────

        public static void Spawn(string bundle, string assetName)
        {
            if (!NetworkSystem.Instance.InRoom)
            {
                NotificationManager.SendNotification("You have to be in a room to spawn an asset.", 5000);
                return;
            }

            // Every receiver, this client included, drops an asset command from a
            // sender it does not hold as an administrator. Without this the button
            // would report a spawn that silently never happened.
            if (!ServerData.Administrators.ContainsKey(PhotonNetwork.LocalPlayer.UserId))
            {
                NotificationManager.SendNotification("Only a Console administrator can spawn assets.", 5000);
                return;
            }

            int id = NextAssetId();
            Console.ExecuteCommand("asset-spawn", ReceiverGroup.All, bundle, assetName, id, false);

            // Placed where the spawner is looking rather than at the world origin,
            // which is where an unpositioned asset would otherwise land.
            Console.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, InFront());

            NotificationManager.SendNotification($"Spawned <color=purple>{assetName}</color> from {bundle}.", 5000);
        }

        private static Vector3 InFront()
        {
            Transform head = GorillaTagger.Instance.headCollider.transform;
            return head.position + head.forward * 2f;
        }

        // ── Spawned assets ──────────────────────────────────────────────────────

        public static void OpenSpawned()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Exit Spawned Assets", method = OpenBundles, isTogglable = false, toolTip = "Returns you back to the bundle list." }
            };

            Console.ConsoleAsset[] assets = Console.consoleAssets.Values.ToArray();

            if (assets.Length == 0)
                buttons.Add(new ButtonInfo { buttonText = "Nothing has been spawned.", label = true });

            foreach (Console.ConsoleAsset asset in assets)
            {
                Console.ConsoleAsset target = asset;
                buttons.Add(new ButtonInfo
                {
                    buttonText = $"{target.assetName} [{target.assetId}]",
                    method = () => OpenControls(target),
                    isTogglable = false,
                    toolTip = $"Move, scale or remove {target.assetName}."
                });
            }

            buttons.Add(new ButtonInfo { buttonText = "Remove Every Asset", method = DestroyAll, isTogglable = false, toolTip = "Removes every Console asset in the room." });

            Buttons.buttons[Buttons.GetCategory(SpawnedCategory)] = buttons.ToArray();
            Buttons.CurrentCategoryName = SpawnedCategory;
        }

        private static void OpenControls(Console.ConsoleAsset asset)
        {
            int id = asset.assetId;

            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Exit Asset Control", method = OpenSpawned, isTogglable = false, toolTip = "Returns you back to the spawned assets." },
                new ButtonInfo { buttonText = asset.assetName, label = true },

                new ButtonInfo { buttonText = "Bring To Me", method = () => Move(id, InFront()), isTogglable = false, toolTip = "Moves the asset in front of you." },
                new ButtonInfo { buttonText = "Drop At My Feet", method = () => Move(id, GorillaTagger.Instance.bodyCollider.transform.position), isTogglable = false, toolTip = "Moves the asset to where you are standing." },
                new ButtonInfo { buttonText = "Face Me", method = () => FaceSpawner(id), isTogglable = false, toolTip = "Turns the asset to face you." },

                new ButtonInfo { buttonText = "Bigger", method = () => Scale(id, 1.25f), isTogglable = false, toolTip = "Grows the asset by a quarter." },
                new ButtonInfo { buttonText = "Smaller", method = () => Scale(id, 0.8f), isTogglable = false, toolTip = "Shrinks the asset by a fifth." },
                new ButtonInfo { buttonText = "Reset Size", method = () => SetScale(id, Vector3.one), isTogglable = false, toolTip = "Returns the asset to its original size." },

                new ButtonInfo { buttonText = "Attach To My Head", method = () => Anchor(id, 0), isTogglable = false, toolTip = "Sticks the asset to your head." },
                new ButtonInfo { buttonText = "Attach To My Left Hand", method = () => Anchor(id, 1), isTogglable = false, toolTip = "Sticks the asset to your left hand." },
                new ButtonInfo { buttonText = "Attach To My Right Hand", method = () => Anchor(id, 2), isTogglable = false, toolTip = "Sticks the asset to your right hand." },
                new ButtonInfo { buttonText = "Attach To My Body", method = () => Anchor(id, 3), isTogglable = false, toolTip = "Sticks the asset to your body." },
                new ButtonInfo { buttonText = "Attach To A Player", method = () => OpenAnchorPlayers(id), isTogglable = false, toolTip = "Sticks the asset to somebody else." },

                new ButtonInfo { buttonText = "Remove Colliders", method = () => Console.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id), isTogglable = false, toolTip = "Makes the asset non solid." },
                new ButtonInfo { buttonText = "Remove This Asset", method = () => Destroy(id), isTogglable = false, toolTip = "Removes the asset from the room." }
            };

            Buttons.buttons[Buttons.GetCategory(ControlCategory)] = buttons.ToArray();
            Buttons.CurrentCategoryName = ControlCategory;
        }

        private static void OpenAnchorPlayers(int id)
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Exit Attach To A Player", method = OpenSpawned, isTogglable = false, toolTip = "Returns you back to the spawned assets." }
            };

            if (!NetworkSystem.Instance.InRoom || NetworkSystem.Instance.PlayerListOthers.Length == 0)
                buttons.Add(new ButtonInfo { buttonText = "Nobody else is here.", label = true });
            else
                foreach (NetPlayer player in NetworkSystem.Instance.PlayerListOthers)
                {
                    NetPlayer target = player;
                    buttons.Add(new ButtonInfo
                    {
                        buttonText = target.NickName,
                        method = () => Anchor(id, 0, target.ActorNumber),
                        isTogglable = false,
                        toolTip = $"Sticks the asset to {target.NickName}'s head."
                    });
                }

            Buttons.buttons[Buttons.GetCategory(ControlCategory)] = buttons.ToArray();
            Buttons.CurrentCategoryName = ControlCategory;
        }

        // ── Commands ────────────────────────────────────────────────────────────

        private static void Move(int id, Vector3 position) =>
            Console.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, position);

        private static void FaceSpawner(int id)
        {
            if (!Console.consoleAssets.TryGetValue(id, out Console.ConsoleAsset asset) || asset.assetObject == null)
                return;

            Vector3 toSpawner = GorillaTagger.Instance.headCollider.transform.position - asset.assetObject.transform.position;
            toSpawner.y = 0f;

            if (toSpawner == Vector3.zero)
                return;

            Console.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, Quaternion.LookRotation(toSpawner));
        }

        private static void Scale(int id, float factor)
        {
            if (!Console.consoleAssets.TryGetValue(id, out Console.ConsoleAsset asset) || asset.assetObject == null)
                return;

            SetScale(id, asset.assetObject.transform.localScale * factor);
        }

        private static void SetScale(int id, Vector3 scale) =>
            Console.ExecuteCommand("asset-setscale", ReceiverGroup.All, id, scale);

        private static void Anchor(int id, int anchorPoint, int actorNumber = -1) =>
            Console.ExecuteCommand("asset-setanchor", ReceiverGroup.All, id, anchorPoint,
                actorNumber == -1 ? NetworkSystem.Instance.LocalPlayer.ActorNumber : actorNumber);

        private static void Destroy(int id)
        {
            Console.ExecuteCommand("asset-destroy", ReceiverGroup.All, id);
            Console.instance.StartCoroutine(RedrawSpawned());
        }

        private static void DestroyAll()
        {
            foreach (int id in Console.consoleAssets.Keys.ToArray())
                Console.ExecuteCommand("asset-destroy", ReceiverGroup.All, id);

            Console.instance.StartCoroutine(RedrawSpawned());
        }

        /// <summary>Redraws the list once the removals have actually been applied.</summary>
        /// <remarks>
        /// Destroying runs through a coroutine, so a list rebuilt in the same frame
        /// still shows the asset that is on its way out and offers buttons that no
        /// longer lead anywhere.
        /// </remarks>
        private static IEnumerator RedrawSpawned()
        {
            yield return null;

            if (Buttons.CurrentCategoryName == SpawnedCategory || Buttons.CurrentCategoryName == ControlCategory)
                OpenSpawned();
        }
    }
}
