/*
 * Nova Menu  Mods/ConsoleAssets.cs
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

using Photon.Realtime;
using Nova.Classes.Menu;
using Nova.Managers;
using Nova.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using Valve.Newtonsoft.Json.Linq;
using static Nova.Menu.Main;
using Console = Nova.Classes.Menu.Console;

namespace Nova.Mods
{
    /// <summary>
    /// Browses the asset bundles Console hosts and spawns them into the room.
    /// </summary>
    /// <remarks>
    /// Console has always been able to receive an asset: "asset-spawn" arrives over the
    /// network, the bundle downloads, and the object appears. Nothing in the menu ever
    /// sent one. This is the sending half, plus the pages to manage what was sent.
    ///
    /// Every command goes to <see cref="ReceiverGroup.All"/>, which Console applies
    /// locally as well as remotely, so the spawner sees what the room sees. Receivers
    /// drop asset commands from anyone they do not hold as an administrator.
    /// </remarks>
    public static class ConsoleAssets
    {
        public const string BundleCategory = "Console Assets";
        public const string ObjectCategory = "Console Objects";
        public const string SpawnedCategory = "Spawned Assets";
        public const string ControlCategory = "Console Asset Control";

        // Dynamic buttons are prefixed so they can never be mistaken for a mod of the
        // same name elsewhere in the menu when a button is looked up by its text.
        private const string Prefix = "ConsoleAsset:";

        private sealed class Bundle
        {
            public string Name;
            public string Unity;
            public int SizeKb;
        }

        private enum Fit { Unknown, Good, Older, TooNew }

        /// <summary>Bundle names as of this build. A fallback; the server's list replaces it.</summary>
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
            "maps2", "maxwell", "mccsword", "mcsword", "medkit", "menuobject", "minitravis", "minos", "monster2",
            "monsters", "mrbeast_meme", "nameless", "namelesslaser", "netflix", "nextbot", "novadomain", "novaindihcatah",
            "objects", "oldtag", "omega", "overkillcharge", "overkillfire", "overkillgun", "patapim", "pigeon",
            "pizzaman", "portal", "portalgun", "portalsword", "powerglove", "purple", "purpleshoot", "ra",
            "raa", "raaa", "raaaa", "randomstuff", "ravenger", "ravyravy", "rblxcarpet", "rbsword", "realknife",
            "red", "reefbackreaper", "rezero", "rgbendersword", "roblox", "runningfreddy", "saoplayericon",
            "sawgun", "scarylarry", "shibaholdable", "shotgun1", "shrek", "simplehouse", "sis", "skele", "skibidi toilet",
            "smurfcat", "soggy", "sogsog", "sonic", "sp", "spraypaint", "squidgame", "star_win", "starglitcher",
            "starpe", "starwand", "stormblade", "subwaysurfers", "summons", "super-crown", "theboliedone",
            "titan", "towerer", "travis", "travisv2", "tri", "tung-tung-tung-sahur", "veritymonster", "viltrumite",
            "vr", "whiterose", "wii", "wings", "wworld", "zeldasword"
        };

        private static List<Bundle> bundles = FallbackBundles.Select(name => new Bundle { Name = name }).ToList();

        private static bool showIncompatible;
        private static string openBundle;
        private static int spawnCounter;

        private static string gunBundle;
        private static string gunAsset;
        private static int selectedAssetId = -1;
        private static float gunDelay;

        private static readonly List<(string bundle, string asset)> recent = new List<(string, string)>();
        private static readonly HashSet<int> mine = new HashSet<int>();

        // The hands come first: a new asset goes into your right hand unless you choose otherwise.
        private enum Placement { RightHand, LeftHand, InFront, Feet }
        private static Placement placement = Placement.RightHand;
        private static bool faceMe = true;

        /// <summary>Ids are namespaced by actor so two administrators cannot collide.</summary>
        private static int NextAssetId() =>
            NetworkSystem.Instance.LocalPlayer.ActorNumber * 1000 + ++spawnCounter % 1000;

        // ── Manifest ────────────────────────────────────────────────────────────

        /// <summary>Replaces the shipped list with the server's, with versions and sizes when published.</summary>
        public static IEnumerator RefreshManifest()
        {
            Task<string> json = Download("ConsoleAssets.json");
            while (!json.IsCompleted)
                yield return null;

            if (json.Exception == null && !string.IsNullOrWhiteSpace(json.Result))
            {
                List<Bundle> listed = null;
                try
                {
                    listed = JArray.Parse(json.Result)
                        .OfType<JObject>()
                        .Select(entry => new Bundle
                        {
                            Name = (string)entry["name"],
                            Unity = (string)entry["unity"],
                            SizeKb = (int?)entry["sizeKb"] ?? 0
                        })
                        .Where(bundle => !string.IsNullOrEmpty(bundle.Name))
                        .ToList();
                }
                catch (Exception e)
                {
                    Console.Log($"ConsoleAssets.json was not readable: {e.Message}");
                }

                if (listed != null && listed.Count > 0)
                {
                    bundles = listed;
                    yield break;
                }
            }

            // Older servers only publish names.
            Task<string> text = Download("ConsoleAssets.txt");
            while (!text.IsCompleted)
                yield return null;

            if (text.Exception != null || string.IsNullOrWhiteSpace(text.Result))
                yield break;

            List<Bundle> named = text.Result
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#"))
                .Select(name => new Bundle { Name = name })
                .ToList();

            if (named.Count > 0)
                bundles = named;
        }

        private static async Task<string> Download(string file)
        {
            using HttpClient client = new HttpClient();
            return await client.GetStringAsync($"{ServerData.AssetURL}/{file}");
        }

        // ── Compatibility ───────────────────────────────────────────────────────

        private static bool TryReadVersion(string version, out int major, out int minor)
        {
            major = minor = 0;
            if (string.IsNullOrEmpty(version))
                return false;

            string[] parts = version.Split('.');
            return parts.Length >= 2 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor);
        }

        /// <summary>
        /// A bundle built by a newer editor than the game runs cannot be opened at all.
        /// One from an older major version usually opens, but can lose its materials.
        /// </summary>
        private static Fit FitOf(Bundle bundle)
        {
            if (!TryReadVersion(bundle.Unity, out int bundleMajor, out int bundleMinor) ||
                !TryReadVersion(Application.unityVersion, out int gameMajor, out int gameMinor))
                return Fit.Unknown;

            if (bundleMajor > gameMajor || (bundleMajor == gameMajor && bundleMinor > gameMinor))
                return Fit.TooNew;

            return bundleMajor < gameMajor ? Fit.Older : Fit.Good;
        }

        private static string Describe(Bundle bundle)
        {
            string mark = FitOf(bundle) switch
            {
                Fit.Good => "<color=green>●</color>",
                Fit.Older => "<color=yellow>●</color>",
                Fit.TooNew => "<color=red>●</color>",
                _ => "<color=grey>●</color>"
            };

            string size = bundle.SizeKb <= 0 ? "" :
                bundle.SizeKb >= 1024 ? $" <color=grey>{bundle.SizeKb / 1024f:0.0} MB</color>" : $" <color=grey>{bundle.SizeKb} KB</color>";

            return $"{mark} {bundle.Name}{size}";
        }

        private static string DescribeFit(Bundle bundle) => FitOf(bundle) switch
        {
            Fit.Good => $"Built with Unity {bundle.Unity}, which matches the game.",
            Fit.Older => $"Built with Unity {bundle.Unity}, older than the game's {Application.unityVersion}. It usually loads, but materials can come out wrong.",
            Fit.TooNew => $"Built with Unity {bundle.Unity}, newer than the game's {Application.unityVersion}. It cannot load until the game updates.",
            _ => "Unity version unknown."
        };

        // ── Browsing ────────────────────────────────────────────────────────────

        public static void OpenBundles()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { legal = true, buttonText = "Exit Console Assets", method = () => Buttons.CurrentCategoryName = "Admin Mods", isTogglable = false, toolTip = "Returns you back to the admin mods." },
                new ButtonInfo { legal = true, buttonText = Prefix + "Spawned", overlapText = "Spawned Assets", method = OpenSpawned, isTogglable = false, toolTip = "Shows every Console asset currently in the room." },
                new ButtonInfo { legal = true, buttonText = Prefix + "Favourites", overlapText = $"Favourites <color=grey>[{Favourites.Count}]</color>", method = OpenFavourites, isTogglable = false, toolTip = "Objects you have favourited, ready to spawn." },
                new ButtonInfo { legal = true, buttonText = Prefix + "Recent", overlapText = $"Recently Spawned <color=grey>[{recent.Count}]</color>", method = OpenRecent, isTogglable = false, toolTip = "The last objects you spawned." },
                new ButtonInfo { legal = true, buttonText = Prefix + "RespawnLast", overlapText = "Respawn Last", method = RespawnLast, isTogglable = false, toolTip = "Spawns the last object you spawned again." },
                new ButtonInfo { legal = true, buttonText = Prefix + "Placement", overlapText = $"Spawn Position: {PlacementName}", method = CyclePlacement, isTogglable = false, toolTip = "Where new assets appear: held in your right or left hand, in front of you, or at your feet." },
                new ButtonInfo { legal = true, buttonText = Prefix + "FaceMe", overlapText = $"Face Me When Spawned: {(faceMe ? "On" : "Off")}", method = ToggleFaceMe, isTogglable = false, toolTip = "Turns every new asset, held or placed, to face you as it appears." },
                new ButtonInfo { legal = true, buttonText = Prefix + "ShowIncompatible", overlapText = $"Show Bundles That Won't Load: {(showIncompatible ? "On" : "Off")}", method = ToggleIncompatible, isTogglable = false, toolTip = "Lists bundles built with a newer Unity than the game, which cannot load." },
                new ButtonInfo { legal = true, buttonText = Prefix + "Key", overlapText = $"<color=green>●</color> works  <color=yellow>●</color> older  <color=red>●</color> won't load", label = true }
            };

            int hidden = 0;
            foreach (Bundle bundle in bundles)
            {
                if (!showIncompatible && FitOf(bundle) == Fit.TooNew)
                {
                    hidden++;
                    continue;
                }

                Bundle target = bundle;
                buttons.Add(new ButtonInfo
                {
                    buttonText = Prefix + target.Name,
                    overlapText = Describe(target),
                    method = () => OpenBundle(target),
                    isTogglable = false,
                    toolTip = DescribeFit(target)
                });
            }

            if (hidden > 0)
                buttons.Add(new ButtonInfo { legal = true, buttonText = Prefix + "Hidden", overlapText = $"<color=grey>{hidden} hidden: built for a newer Unity than this game</color>", label = true });

            Buttons.buttons[Buttons.GetCategory(BundleCategory)] = buttons.ToArray();
            Buttons.CurrentCategoryName = BundleCategory;
        }

        private static void OpenBundle(Bundle bundle)
        {
            openBundle = bundle.Name;

            Buttons.buttons[Buttons.GetCategory(ObjectCategory)] = new[]
            {
                new ButtonInfo { legal = true, buttonText = "Exit Console Objects", method = OpenBundles, isTogglable = false, toolTip = "Returns you back to the bundle list." },
                new ButtonInfo { legal = true, buttonText = Prefix + "Loading", overlapText = $"Downloading {bundle.Name}...", label = true }
            };
            Buttons.CurrentCategoryName = ObjectCategory;

            Console.instance.StartCoroutine(LoadBundleObjects(bundle));
        }

        private static IEnumerator LoadBundleObjects(Bundle bundle)
        {
            string name = bundle.Name;

            if (!Console.assetBundlePool.ContainsKey(name))
            {
                Task load = Console.LoadAssetBundle(name);
                while (!load.IsCompleted)
                    yield return null;

                if (openBundle != name)
                    yield break;

                if (load.Exception != null)
                {
                    string reason = load.Exception.GetBaseException().Message;
                    Console.Log($"Could not open {name} for browsing: {reason}");

                    Buttons.buttons[Buttons.GetCategory(ObjectCategory)] = new[]
                    {
                        new ButtonInfo { legal = true, buttonText = "Exit Console Objects", method = OpenBundles, isTogglable = false, toolTip = "Returns you back to the bundle list." },
                        new ButtonInfo { legal = true, buttonText = Prefix + "Failed", overlapText = "This bundle would not load.", label = true },
                        new ButtonInfo { legal = true, buttonText = Prefix + "Reason", overlapText = $"<color=grey>{reason}</color>", label = true },
                        new ButtonInfo { legal = true, buttonText = Prefix + "Fit", overlapText = $"<color=grey>{DescribeFit(bundle)}</color>", label = true }
                    };
                    yield break;
                }
            }

            if (!Console.assetBundlePool.TryGetValue(name, out AssetBundle loaded) || openBundle != name)
                yield break;

            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { legal = true, buttonText = "Exit Console Objects", method = OpenBundles, isTogglable = false, toolTip = "Returns you back to the bundle list." }
            };

            // A scene bundle carries no loadable assets and throws if asked for them.
            if (loaded.isStreamedSceneAssetBundle)
            {
                buttons.Add(new ButtonInfo { legal = true, buttonText = Prefix + "Scene", overlapText = "This bundle holds a scene, not objects.", label = true });
                Buttons.buttons[Buttons.GetCategory(ObjectCategory)] = buttons.ToArray();
                yield break;
            }

            // Async so a bundle of several megabytes does not stall the frame, which in a
            // headset reads as the game hanging.
            AssetBundleRequest request = loaded.LoadAllAssetsAsync<GameObject>();
            while (!request.isDone)
                yield return null;

            if (openBundle != name)
                yield break;

            GameObject[] objects = request.allAssets.OfType<GameObject>().ToArray();

            if (objects.Length == 0)
                buttons.Add(new ButtonInfo { legal = true, buttonText = Prefix + "Empty", overlapText = "This bundle holds no objects.", label = true });
            else
                buttons.Add(new ButtonInfo { legal = true, buttonText = Prefix + "Hint", overlapText = "<color=grey>Tap to spawn. The Spawn Gun fires the last one.</color>", label = true });

            foreach (GameObject asset in objects)
            {
                string assetName = asset.name;
                buttons.Add(new ButtonInfo
                {
                    buttonText = Prefix + name + "/" + assetName,
                    overlapText = (IsFavourite(name, assetName) ? "<color=yellow>★</color> " : "") + assetName,
                    method = () => Spawn(name, assetName),
                    isTogglable = false,
                    toolTip = $"Spawns {assetName} for everyone in the room."
                });
            }

            Buttons.buttons[Buttons.GetCategory(ObjectCategory)] = buttons.ToArray();
        }

        private static string PlacementName => placement switch
        {
            Placement.RightHand => "In My Right Hand",
            Placement.LeftHand => "In My Left Hand",
            Placement.Feet => "At My Feet",
            _ => "In Front"
        };

        private static void CyclePlacement()
        {
            placement = (Placement)(((int)placement + 1) % Enum.GetValues(typeof(Placement)).Length);
            OpenBundles();
        }

        private static void ToggleFaceMe()
        {
            faceMe = !faceMe;
            OpenBundles();
        }

        private static void ToggleIncompatible()
        {
            showIncompatible = !showIncompatible;
            OpenBundles();
        }

        // ── Spawning ────────────────────────────────────────────────────────────

        public static void Spawn(string bundle, string assetName) =>
            Console.instance.StartCoroutine(SpawnRoutine(bundle, assetName, null));

        private static Vector3 Placed()
        {
            switch (placement)
            {
                case Placement.Feet:
                    return GorillaTagger.Instance.bodyCollider.transform.position + Vector3.down * 0.4f;
                default:
                    Transform head = GorillaTagger.Instance.headCollider.transform;
                    return head.position + head.forward * 2f;
            }
        }

        /// <summary>A rotation that turns an asset at <paramref name="from"/> to face you, kept upright.</summary>
        private static Quaternion FacingMe(Vector3 from)
        {
            Transform head = GorillaTagger.Instance.headCollider.transform;

            Vector3 toMe = head.position - from;
            toMe.y = 0f;

            // Spawned right at your head, there is no direction to you; face back along your view.
            if (toMe.sqrMagnitude < 0.0001f)
            {
                toMe = -head.forward;
                toMe.y = 0f;
            }

            return toMe.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(toMe);
        }

        /// <summary>Spawns for the room, and makes sure every client ends up with it in the right place.</summary>
        /// <remarks>
        /// The position used to be sent in the same instant as the spawn. A receiver only
        /// waits ten seconds for an asset to exist before dropping a command about it, so on
        /// any client whose download took longer the object stayed wherever the prefab
        /// happened to sit, usually far out of sight, and read as a spawn that failed.
        /// The bundle is now loaded here first, the position follows once the object
        /// exists, and it is sent again twice for clients still downloading. With a hand
        /// placement the asset is attached to that hand instead of being given a position,
        /// so it stays held rather than being left where the hand was. With Face Me on,
        /// the asset is also turned toward you as it appears.
        /// </remarks>
        private static IEnumerator SpawnRoutine(string bundle, string assetName, Vector3? at)
        {
            if (!NetworkSystem.Instance.InRoom)
            {
                NotificationManager.SendNotification("You have to be in a room to spawn an asset.", 5000);
                yield break;
            }

            // Every receiver, this client included, drops an asset command from a sender it
            // does not hold as an administrator.
            if (!ServerData.Administrators.ContainsKey(NetworkSystem.Instance.LocalPlayer.UserId))
            {
                NotificationManager.SendNotification("Only a Console administrator can spawn assets.", 5000);
                yield break;
            }

            // Proved loadable here before the whole room is asked to download it.
            if (!Console.assetBundlePool.ContainsKey(bundle))
            {
                NotificationManager.SendNotification($"Downloading {bundle}...", 3000);
                Task load = Console.LoadAssetBundle(bundle);
                while (!load.IsCompleted)
                    yield return null;

                if (load.Exception != null)
                {
                    NotificationManager.SendNotification($"<color=red>{bundle} would not load:</color> {load.Exception.GetBaseException().Message}", 8000);
                    yield break;
                }
            }

            // Anchor points as Console numbers them: 1 left hand, 2 right hand.
            int hand = at == null && placement == Placement.RightHand ? 2
                : at == null && placement == Placement.LeftHand ? 1
                : -1;
            Vector3 position = at ?? Placed();
            Quaternion rotation = FacingMe(position);
            int me = NetworkSystem.Instance.LocalPlayer.ActorNumber;
            int id = NextAssetId();
            mine.Add(id);
            selectedAssetId = id;
            Remember(bundle, assetName);

            Console.ExecuteCommand("asset-spawn", ReceiverGroup.All, bundle, assetName, id, false);

            float deadline = Time.time + 15f;
            while (Time.time < deadline && !Console.consoleAssets.ContainsKey(id))
                yield return null;

            if (hand != -1)
            {
                // Held, the rotation is relative to the hand bone, so it is worked out from
                // where the hand is now and then turns with the hand.
                Transform palm = hand == 2 ? VRRig.LocalRig.rightHandTransform : VRRig.LocalRig.leftHandTransform;
                Quaternion held = faceMe ? Quaternion.Inverse(palm.parent.rotation) * FacingMe(palm.position) : Quaternion.identity;
                Hold(id, hand, me, HoldOffset(hand), held);
            }
            else
            {
                Console.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, position);
                if (faceMe)
                    Console.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, rotation);
            }

            if (!Console.consoleAssets.ContainsKey(id))
            {
                NotificationManager.SendNotification($"<color=red>{assetName} did not appear.</color> The reason is in the BepInEx log.", 8000);
                yield break;
            }

            NotificationManager.SendNotification(hand == -1
                ? $"Spawned <color=purple>{assetName}</color> from {bundle}."
                : $"Spawned <color=purple>{assetName}</color> in your {(hand == 2 ? "right" : "left")} hand.", 4000);

            // Receivers still downloading drop a command sent before their copy exists.
            foreach (float wait in new[] { 8f, 12f })
            {
                yield return new WaitForSeconds(wait);

                if (!Console.consoleAssets.TryGetValue(id, out Console.ConsoleAsset asset) || asset.assetObject == null)
                    yield break;

                if (hand != -1)
                {
                    // Only while it is still where it was put; attaching it elsewhere since wins.
                    if (asset.bindedToIndex != hand || asset.bindPlayerActor != me)
                        yield break;

                    Hold(id, hand, me, asset.assetObject.transform.localPosition, asset.assetObject.transform.localRotation);
                    continue;
                }

                // An attached asset follows its anchor; a position would pull it off.
                if (asset.bindedToIndex != -1)
                    yield break;

                Console.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, asset.assetObject.transform.position);
                if (faceMe)
                    Console.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, asset.assetObject.transform.rotation);
            }
        }

        private static void Remember(string bundle, string assetName)
        {
            gunBundle = bundle;
            gunAsset = assetName;

            recent.RemoveAll(entry => entry.bundle == bundle && entry.asset == assetName);
            recent.Insert(0, (bundle, assetName));
            if (recent.Count > 12)
                recent.RemoveAt(recent.Count - 1);
        }

        public static void RespawnLast()
        {
            if (gunAsset == null)
            {
                NotificationManager.SendNotification("You have not spawned anything yet.", 4000);
                return;
            }

            Spawn(gunBundle, gunAsset);
        }

        // ── Pointers ────────────────────────────────────────────────────────────

        /// <summary>Hold grip to aim, pull trigger to spawn the last object where you point.</summary>
        public static void SpawnGun()
        {
            if (!GetGunInput(false))
                return;

            var gun = RenderGun();

            if (!GetGunInput(true) || Time.time < gunDelay)
                return;

            gunDelay = Time.time + 0.6f;

            if (gunAsset == null)
            {
                NotificationManager.SendNotification("Spawn something from Console Assets first; the gun fires the last one.", 5000);
                return;
            }

            Console.instance.StartCoroutine(SpawnRoutine(gunBundle, gunAsset, gun.Ray.point));
        }

        /// <summary>Hold grip to aim, pull trigger to move the selected asset where you point.</summary>
        public static void MoveGun()
        {
            if (!GetGunInput(false))
                return;

            var gun = RenderGun();

            if (!GetGunInput(true) || Time.time < gunDelay)
                return;

            gunDelay = Time.time + 0.2f;

            if (!Console.consoleAssets.ContainsKey(selectedAssetId))
            {
                NotificationManager.SendNotification("Spawn an asset, or open one in Spawned Assets, to choose what the gun moves.", 5000);
                return;
            }

            Console.ExecuteCommand("asset-setposition", ReceiverGroup.All, selectedAssetId, gun.Ray.point);
        }

        /// <summary>Hold grip to aim, pull trigger to remove the Console asset you point at.</summary>
        public static void DeleteGun()
        {
            if (!GetGunInput(false))
                return;

            var gun = RenderGun();

            if (!GetGunInput(true) || Time.time < gunDelay || gun.Ray.collider == null)
                return;

            gunDelay = Time.time + 0.3f;

            Transform hit = gun.Ray.collider.transform;
            foreach (Console.ConsoleAsset asset in Console.consoleAssets.Values.ToArray())
            {
                if (asset.assetObject != null && hit.IsChildOf(asset.assetObject.transform))
                {
                    Console.ExecuteCommand("asset-destroy", ReceiverGroup.All, asset.assetId);
                    return;
                }
            }
        }

        // ── Favourites and recent ───────────────────────────────────────────────

        private static string FavouritesPath => $"{PluginInfo.BaseDirectory}/ConsoleFavourites.txt";

        private static List<string> favourites;

        private static List<string> Favourites
        {
            get
            {
                if (favourites != null)
                    return favourites;

                favourites = new List<string>();
                try
                {
                    if (File.Exists(FavouritesPath))
                        favourites.AddRange(File.ReadAllLines(FavouritesPath).Where(line => line.Contains("/")));
                }
                catch (Exception e)
                {
                    Console.Log($"Could not read Console favourites: {e.Message}");
                }

                return favourites;
            }
        }

        private static bool IsFavourite(string bundle, string assetName) =>
            Favourites.Contains($"{bundle}/{assetName}");

        private static void ToggleFavourite(string bundle, string assetName)
        {
            string key = $"{bundle}/{assetName}";
            if (!Favourites.Remove(key))
                Favourites.Add(key);

            try
            {
                Directory.CreateDirectory(PluginInfo.BaseDirectory);
                File.WriteAllLines(FavouritesPath, Favourites);
            }
            catch (Exception e)
            {
                Console.Log($"Could not save Console favourites: {e.Message}");
            }

            NotificationManager.SendNotification(IsFavourite(bundle, assetName)
                ? $"Added <color=yellow>{assetName}</color> to favourites."
                : $"Removed {assetName} from favourites.", 3000);
        }

        private static void OpenFavourites() =>
            OpenShortlist("Favourites", Favourites.Select(key =>
            {
                int split = key.IndexOf('/');
                return (key[..split], key[(split + 1)..]);
            }).ToList(), "You have no favourites yet. Open a spawned asset and favourite it.");

        private static void OpenRecent() =>
            OpenShortlist("Recently Spawned", recent.ToList(), "Nothing spawned yet.");

        private static void OpenShortlist(string title, List<(string bundle, string asset)> entries, string empty)
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { legal = true, buttonText = "Exit Console Objects", method = OpenBundles, isTogglable = false, toolTip = "Returns you back to the bundle list." },
                new ButtonInfo { legal = true, buttonText = Prefix + "Title", overlapText = title, label = true }
            };

            if (entries.Count == 0)
                buttons.Add(new ButtonInfo { legal = true, buttonText = Prefix + "None", overlapText = empty, label = true });

            foreach (var (bundle, asset) in entries)
            {
                string targetBundle = bundle, targetAsset = asset;
                buttons.Add(new ButtonInfo
                {
                    buttonText = Prefix + title + ":" + targetBundle + "/" + targetAsset,
                    overlapText = $"{targetAsset} <color=grey>{targetBundle}</color>",
                    method = () => Spawn(targetBundle, targetAsset),
                    isTogglable = false,
                    toolTip = $"Spawns {targetAsset} from {targetBundle}."
                });
            }

            openBundle = null;
            Buttons.buttons[Buttons.GetCategory(ObjectCategory)] = buttons.ToArray();
            Buttons.CurrentCategoryName = ObjectCategory;
        }

        // ── Spawned assets ──────────────────────────────────────────────────────

        public static void OpenSpawned()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { legal = true, buttonText = "Exit Spawned Assets", method = OpenBundles, isTogglable = false, toolTip = "Returns you back to the bundle list." }
            };

            Console.ConsoleAsset[] assets = Console.consoleAssets.Values.ToArray();

            if (assets.Length == 0)
                buttons.Add(new ButtonInfo { legal = true, buttonText = Prefix + "Nothing", overlapText = "Nothing has been spawned.", label = true });

            foreach (Console.ConsoleAsset asset in assets)
            {
                Console.ConsoleAsset target = asset;
                buttons.Add(new ButtonInfo
                {
                    buttonText = Prefix + "Spawned:" + target.assetId,
                    overlapText = $"{target.assetName} <color=grey>[{target.assetId}]{(mine.Contains(target.assetId) ? " mine" : "")}</color>",
                    method = () => OpenControls(target),
                    isTogglable = false,
                    toolTip = $"Move, scale or remove {target.assetName}."
                });
            }

            buttons.Add(new ButtonInfo { legal = true, buttonText = Prefix + "RemoveMine", overlapText = "Remove My Assets", method = DestroyMine, isTogglable = false, toolTip = "Removes only the assets you spawned." });
            buttons.Add(new ButtonInfo { legal = true, buttonText = Prefix + "RemoveAll", overlapText = "Remove Every Asset", method = DestroyAll, isTogglable = false, toolTip = "Removes every Console asset in the room." });

            Buttons.buttons[Buttons.GetCategory(SpawnedCategory)] = buttons.ToArray();
            Buttons.CurrentCategoryName = SpawnedCategory;
        }

        private static void OpenControls(Console.ConsoleAsset asset)
        {
            int id = asset.assetId;
            selectedAssetId = id;

            string bundle = asset.assetBundle;
            string assetName = asset.assetName;

            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { legal = true, buttonText = "Exit Asset Control", method = OpenSpawned, isTogglable = false, toolTip = "Returns you back to the spawned assets." },
                new ButtonInfo { legal = true, buttonText = Prefix + "Selected", overlapText = $"{assetName} <color=grey>(the Move Gun moves this)</color>", label = true },

                Control("BringToMe", "Bring To Me", () => Move(id, InFront()), "Moves the asset in front of you."),
                Control("ToHand", "Move To My Hand", () => Move(id, GorillaTagger.Instance.rightHandTransform.position), "Moves the asset to your right hand."),
                Control("ToFeet", "Drop At My Feet", () => Move(id, GorillaTagger.Instance.bodyCollider.transform.position), "Moves the asset to where you are standing."),
                Control("Raise", "Raise", () => Nudge(id, Vector3.up * 0.5f), "Lifts the asset half a metre."),
                Control("Lower", "Lower", () => Nudge(id, Vector3.down * 0.5f), "Drops the asset half a metre."),
                Control("Glide", "Glide To Me", () => Glide(id), "Slides the asset smoothly to in front of you."),
                Control("Face", "Face Me", () => FaceSpawner(id), "Turns the asset to face you."),
                Control("Spin", "Turn 90 Degrees", () => Turn(id, 90f), "Rotates the asset a quarter turn."),
                Control("Upright", "Stand Upright", () => Console.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, Quaternion.identity), "Clears any tilt on the asset."),

                Control("Bigger", "Bigger", () => Scale(id, 1.25f), "Grows the asset by a quarter."),
                Control("Smaller", "Smaller", () => Scale(id, 0.8f), "Shrinks the asset by a fifth."),
                Control("Double", "Double Size", () => Scale(id, 2f), "Doubles the asset's size."),
                Control("ResetSize", "Reset Size", () => SetScale(id, Vector3.one), "Returns the asset to its original size."),

                Control("Red", "Paint Red", () => Recolor(id, Color.red), "Recolours the asset red."),
                Control("Blue", "Paint Blue", () => Recolor(id, new Color(0.2f, 0.4f, 1f)), "Recolours the asset blue."),
                Control("Green", "Paint Green", () => Recolor(id, Color.green), "Recolours the asset green."),
                Control("Purple", "Paint Purple", () => Recolor(id, new Color(0.6f, 0.2f, 1f)), "Recolours the asset purple."),
                Control("White", "Paint White", () => Recolor(id, Color.white), "Recolours the asset white."),
                Control("MyColour", "Paint My Colour", () => Recolor(id, VRRig.LocalRig.playerColor), "Recolours the asset to match you."),

                Control("Head", "Attach To My Head", () => Anchor(id, 0), "Sticks the asset to your head."),
                Control("LeftHand", "Attach To My Left Hand", () => Anchor(id, 1), "Sticks the asset to your left hand."),
                Control("RightHand", "Attach To My Right Hand", () => Anchor(id, 2), "Sticks the asset to your right hand."),
                Control("Body", "Attach To My Body", () => Anchor(id, 3), "Sticks the asset to your body."),
                Control("Player", "Attach To A Player", () => OpenAnchorPlayers(id), "Sticks the asset to somebody else."),

                Control("Clone", "Spawn Another", () => Spawn(bundle, assetName), "Spawns another copy of this object."),
                Control("Gun", "Load Into Spawn Gun", () => { gunBundle = bundle; gunAsset = assetName; NotificationManager.SendNotification($"The Spawn Gun now fires {assetName}.", 3000); }, "Makes the Spawn Gun fire this object."),
                Control("Favourite", IsFavourite(bundle, assetName) ? "Remove From Favourites" : "Add To Favourites", () => { ToggleFavourite(bundle, assetName); OpenControls(asset); }, "Keeps this object in your favourites."),
                Control("Colliders", "Remove Colliders", () => Console.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id), "Makes the asset non solid."),
                Control("Remove", "Remove This Asset", () => Destroy(id), "Removes the asset from the room.")
            };

            Buttons.buttons[Buttons.GetCategory(ControlCategory)] = buttons.ToArray();
            Buttons.CurrentCategoryName = ControlCategory;
        }

        private static ButtonInfo Control(string key, string text, Action action, string toolTip) =>
            new ButtonInfo { legal = true, buttonText = Prefix + "Control:" + key, overlapText = text, method = action, isTogglable = false, toolTip = toolTip };

        private static void OpenAnchorPlayers(int id)
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>
            {
                new ButtonInfo { legal = true, buttonText = "Exit Attach To A Player", method = OpenSpawned, isTogglable = false, toolTip = "Returns you back to the spawned assets." }
            };

            if (!NetworkSystem.Instance.InRoom || NetworkSystem.Instance.PlayerListOthers.Length == 0)
                buttons.Add(new ButtonInfo { legal = true, buttonText = Prefix + "Alone", overlapText = "Nobody else is here.", label = true });
            else
                foreach (NetPlayer player in NetworkSystem.Instance.PlayerListOthers)
                {
                    NetPlayer target = player;
                    buttons.Add(new ButtonInfo
                    {
                        buttonText = Prefix + "Anchor:" + target.ActorNumber,
                        overlapText = target.NickName,
                        method = () => Anchor(id, 0, target.ActorNumber),
                        isTogglable = false,
                        toolTip = $"Sticks the asset to {target.NickName}'s head."
                    });
                }

            Buttons.buttons[Buttons.GetCategory(ControlCategory)] = buttons.ToArray();
            Buttons.CurrentCategoryName = ControlCategory;
        }

        // ── Commands ────────────────────────────────────────────────────────────

        private static Vector3 InFront()
        {
            Transform head = GorillaTagger.Instance.headCollider.transform;
            return head.position + head.forward * 2f;
        }

        private static bool TryGet(int id, out Console.ConsoleAsset asset) =>
            Console.consoleAssets.TryGetValue(id, out asset) && asset.assetObject != null;

        private static void Move(int id, Vector3 position) =>
            Console.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, position);

        private static void Nudge(int id, Vector3 offset)
        {
            if (TryGet(id, out Console.ConsoleAsset asset))
                Move(id, asset.assetObject.transform.position + offset);
        }

        private static void Glide(int id)
        {
            if (TryGet(id, out Console.ConsoleAsset asset))
                Console.ExecuteCommand("asset-smoothtp", ReceiverGroup.All, id, 1.5f, InFront(), asset.assetObject.transform.rotation);
        }

        /// <summary>Recolours the first renderer in the asset.</summary>
        /// <remarks>
        /// Console colours only the exact object it is given, and an asset's root is often
        /// an empty holder, so the first child that actually renders is found here and
        /// named by its path from the root, which is the same on every client.
        /// </remarks>
        private static void Recolor(int id, Color color)
        {
            if (!TryGet(id, out Console.ConsoleAsset asset))
                return;

            Renderer renderer = asset.assetObject.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                NotificationManager.SendNotification("This asset has nothing that can be recoloured.", 3000);
                return;
            }

            string path = "";
            for (Transform part = renderer.transform; part != null && part != asset.assetObject.transform; part = part.parent)
                path = path.Length == 0 ? part.name : part.name + "/" + path;

            Console.ExecuteCommand("asset-setcolor", ReceiverGroup.All, id, path, color.r, color.g, color.b, color.a);
        }

        private static void FaceSpawner(int id)
        {
            if (!TryGet(id, out Console.ConsoleAsset asset))
                return;

            Vector3 toSpawner = GorillaTagger.Instance.headCollider.transform.position - asset.assetObject.transform.position;
            toSpawner.y = 0f;

            if (toSpawner == Vector3.zero)
                return;

            Console.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, Quaternion.LookRotation(toSpawner));
        }

        private static void Turn(int id, float degrees)
        {
            if (TryGet(id, out Console.ConsoleAsset asset))
                Console.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, asset.assetObject.transform.rotation * Quaternion.Euler(0f, degrees, 0f));
        }

        private static void Scale(int id, float factor)
        {
            if (TryGet(id, out Console.ConsoleAsset asset))
                SetScale(id, asset.assetObject.transform.localScale * factor);
        }

        private static void SetScale(int id, Vector3 scale) =>
            Console.ExecuteCommand("asset-setscale", ReceiverGroup.All, id, scale);

        private static void Anchor(int id, int anchorPoint, int actorNumber = -1) =>
            Hold(id, anchorPoint, actorNumber == -1 ? NetworkSystem.Instance.LocalPlayer.ActorNumber : actorNumber, HoldOffset(anchorPoint), Quaternion.identity);

        /// <summary>Attaches an asset to a player's head, hand or body, sitting on that spot.</summary>
        /// <remarks>
        /// Console attaches by changing the asset's parent while keeping its local position.
        /// An asset that is not attached to anything has its world position as its local one,
        /// so attaching alone put it that many metres away from the anchor, often out of sight.
        /// The local position and rotation are set alongside it. Because the parent change
        /// keeps local values, the result is the same whichever of these a client applies
        /// first, and a player who joins later is sent all three.
        /// </remarks>
        private static void Hold(int id, int anchorPoint, int actorNumber, Vector3 localPosition, Quaternion localRotation)
        {
            Console.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, id, localPosition);
            Console.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, id, localRotation);
            Console.ExecuteCommand("asset-setanchor", ReceiverGroup.All, id, anchorPoint, actorNumber);
        }

        /// <summary>Where on the anchor the asset sits.</summary>
        /// <remarks>
        /// A hand anchor is the hand bone, whose origin is the wrist. The rig's hand point is
        /// a child of that bone at the palm; its offset comes from the rig itself, so it is
        /// the same on every client and for every player.
        /// </remarks>
        private static Vector3 HoldOffset(int anchorPoint)
        {
            switch (anchorPoint)
            {
                case 1:
                    return VRRig.LocalRig.leftHandTransform.localPosition;
                case 2:
                    return VRRig.LocalRig.rightHandTransform.localPosition;
                default:
                    return Vector3.zero;
            }
        }

        private static void Destroy(int id)
        {
            Console.ExecuteCommand("asset-destroy", ReceiverGroup.All, id);
            mine.Remove(id);
            Console.instance.StartCoroutine(RedrawSpawned());
        }

        private static void DestroyMine()
        {
            foreach (int id in Console.consoleAssets.Keys.Where(mine.Contains).ToArray())
                Console.ExecuteCommand("asset-destroy", ReceiverGroup.All, id);

            mine.Clear();
            Console.instance.StartCoroutine(RedrawSpawned());
        }

        private static void DestroyAll()
        {
            foreach (int id in Console.consoleAssets.Keys.ToArray())
                Console.ExecuteCommand("asset-destroy", ReceiverGroup.All, id);

            mine.Clear();
            Console.instance.StartCoroutine(RedrawSpawned());
        }

        /// <summary>Redraws the list once the removals have actually been applied.</summary>
        /// <remarks>
        /// Destroying runs through a coroutine, so a list rebuilt in the same frame still
        /// shows the asset on its way out and offers buttons that no longer lead anywhere.
        /// </remarks>
        private static IEnumerator RedrawSpawned()
        {
            yield return null;

            if (Buttons.CurrentCategoryName == SpawnedCategory || Buttons.CurrentCategoryName == ControlCategory)
                OpenSpawned();
        }
    }
}
