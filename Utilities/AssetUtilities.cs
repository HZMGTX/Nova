/*
 * Nova Menu  Utilities/AssetUtilities.cs
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

using Nova.Managers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using static Nova.Utilities.FileUtilities;

namespace Nova.Utilities
{
    public class AssetUtilities
    {
        private static AssetBundle assetBundle;
        private static void LoadAssetBundle()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{PluginInfo.ClientResourcePath}.novamenu");
            if (stream != null)
                assetBundle = AssetBundle.LoadFromStream(stream);
            else
                LogManager.LogError("Failed to load assetbundle");
        }

        public static T LoadObject<T>(string assetName) where T : Object
        {
            if (assetBundle == null)
                LoadAssetBundle();

            T gameObject = Object.Instantiate(assetBundle.LoadAsset<T>(assetName));
            return gameObject;
        }

        public static T LoadAsset<T>(string assetName) where T : Object
        {
            if (assetBundle == null)
                LoadAssetBundle();

            T gameObject = assetBundle.LoadAsset(assetName) as T;
            return gameObject;
        }

        public static readonly Dictionary<string, AudioClip> audioFilePool = new Dictionary<string, AudioClip>();

        public static void LoadSoundFromFile(string fileName, System.Action<AudioClip> onLoaded)
        {
            CoroutineManager.instance.StartCoroutine(Load());

            IEnumerator Load()
            {
                if (audioFilePool.TryGetValue(fileName, out var cached) && cached != null)
                {
                    onLoaded?.Invoke(cached);
                    yield break;
                }

                string filePath = $"{GetGamePath()}/{PluginInfo.BaseDirectory}/{fileName}";
                string url = $"file://{filePath}";
                var handler = new DownloadHandlerAudioClip(url, AudioType.UNKNOWN);

                using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET, handler, null);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogManager.LogError($"Failed to load audio file '{fileName}': {request.error}\nPath: {url}");
                    onLoaded?.Invoke(null);
                    yield break;
                }

                AudioClip clip = handler.audioClip;

                if (clip != null)
                    audioFilePool[fileName] = clip;

                onLoaded?.Invoke(clip);
            }
        }

        // Sounds already fetched from the server this session, and the ones being fetched right now
        private static readonly HashSet<string> downloadedSounds = new HashSet<string>();
        private static readonly HashSet<string> downloadingSounds = new HashSet<string>();

        public static void LoadSoundFromURL(string resourcePath, string fileName, System.Action<AudioClip> action = null)
        {
            CoroutineManager.instance.StartCoroutine(Load());

            IEnumerator Load()
            {
                string filePath = $"{PluginInfo.BaseDirectory}/{fileName}";
                string directory = Path.GetDirectoryName(filePath);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // The file was deleted since it was fetched (e.g. the soundboard re-downloads it), so fetch it again
                if (!File.Exists(filePath) && !downloadingSounds.Contains(fileName))
                    downloadedSounds.Remove(fileName);

                if (downloadedSounds.Add(fileName))
                {
                    downloadingSounds.Add(fileName);
                    try
                    {
                        using UnityWebRequest request = UnityWebRequest.Get(resourcePath);
                        yield return request.SendWebRequest();

                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            LogManager.LogError($"Failed to download {fileName}: {request.error}");

                            // Nothing to fall back to, so allow another attempt later
                            if (!File.Exists(filePath))
                                downloadedSounds.Remove(fileName);
                        }
                        else
                        {
                            byte[] remoteData = request.downloadHandler.data;
                            bool shouldWrite = true;

                            if (File.Exists(filePath))
                            {
                                byte[] localData = File.ReadAllBytes(filePath);

                                using var sha = System.Security.Cryptography.SHA256.Create();
                                byte[] remoteHash = sha.ComputeHash(remoteData);
                                byte[] localHash = sha.ComputeHash(localData);

                                shouldWrite = !remoteHash.SequenceEqual(localHash);
                            }

                            if (shouldWrite)
                            {
                                LogManager.Log("Downloaded " + fileName);
                                File.WriteAllBytes(filePath, remoteData);
                                audioFilePool.Remove(fileName);
                            }
                        }
                    }
                    finally
                    {
                        downloadingSounds.Remove(fileName);
                    }
                }
                else
                {
                    while (downloadingSounds.Contains(fileName))
                        yield return null;
                }

                if (action == null)
                    yield break;

                bool pooled = audioFilePool.TryGetValue(fileName, out AudioClip cached) && cached != null;
                if (!pooled && !File.Exists(filePath))
                {
                    action.Invoke(null);
                    yield break;
                }

                LoadSoundFromFile(fileName, action);
            }
        }

        public static readonly Dictionary<string, Texture2D> textureResourceDictionary = new Dictionary<string, Texture2D>();
        public static Texture2D LoadTextureFromResource(string resourcePath)
        {
            if (textureResourceDictionary.TryGetValue(resourcePath, out Texture2D existingTexture))
                return existingTexture;

            Texture2D texture = new Texture2D(2, 2);

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);
            if (stream != null)
            {
                byte[] fileData = new byte[stream.Length];
                // ReSharper disable once MustUseReturnValue
                stream.Read(fileData, 0, (int)stream.Length);
                texture.LoadImage(fileData);
            }
            else
                LogManager.LogError("Failed to load texture from resource: " + resourcePath);

            textureResourceDictionary[resourcePath] = texture;

            return texture;
        }

        public static readonly Dictionary<string, Texture2D> textureUrlDictionary = new Dictionary<string, Texture2D>();
        public static Texture2D LoadTextureFromURL(string resourcePath, string fileName)
        {
            if (textureUrlDictionary.TryGetValue(resourcePath, out Texture2D existingTexture))
                return existingTexture;

            string filePath = $"{PluginInfo.BaseDirectory}/{fileName}";
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
                // ReSharper disable once AssignNullToNotNullAttribute
                Directory.CreateDirectory(directory);

            if (!File.Exists(filePath))
            {
                LogManager.Log("Downloading " + fileName);
                WebClient stream = new WebClient();
                stream.DownloadFile(resourcePath, filePath);
            }

            Texture2D texture = LoadTextureFromFile(fileName);

            textureUrlDictionary[resourcePath] = texture;

            return texture;
        }

        public static readonly Dictionary<string, Texture2D> textureFileDirectory = new Dictionary<string, Texture2D>();
        public static Texture2D LoadTextureFromFile(string fileName)
        {
            if (textureFileDirectory.TryGetValue(fileName, out Texture2D existingTexture))
                return existingTexture;

            string filePath = $"{PluginInfo.BaseDirectory}/{fileName}";
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            Texture2D texture = new Texture2D(2, 2);

            byte[] bytes = File.ReadAllBytes(filePath);
            texture.LoadImage(bytes);

            textureFileDirectory[fileName] = texture;

            return texture;
        }
    }
}
