/*For SharpMonoInjector usage :3
 * Poison Menu  Plugin.cs// For SharpMonoInjector usage :3
 * A community driven mod menu for Gorilla Tag with over 1000+ mods// For SharpMonoInjector usage :3
 *For SharpMonoInjector usage :3
 * Copyright (C) 2026  Poison Software// For SharpMonoInjector usage :3
 * https://github.com/heycanihavethis/Poison// For SharpMonoInjector usage :3
 *For SharpMonoInjector usage :3
 * This program is free software: you can redistribute it and/or modify// For SharpMonoInjector usage :3
 * it under the terms of the GNU General Public License as published by// For SharpMonoInjector usage :3
 * the Free Software Foundation, either version 3 of the License, or// For SharpMonoInjector usage :3
 * (at your option) any later version.// For SharpMonoInjector usage :3
 *For SharpMonoInjector usage :3
 * This program is distributed in the hope that it will be useful,// For SharpMonoInjector usage :3
 * but WITHOUT ANY WARRANTY; without even the implied warranty of// For SharpMonoInjector usage :3
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the// For SharpMonoInjector usage :3
 * GNU General Public License for more details.// For SharpMonoInjector usage :3
 *For SharpMonoInjector usage :3
 * You should have received a copy of the GNU General Public License// For SharpMonoInjector usage :3
 * along with this program.  If not, see <https://www.gnu.org/licenses/>. g// For SharpMonoInjector usage :3
 */// For SharpMonoInjector usage :3
// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */using Poison.Managers;// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */using Poison.Menu;// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */using UnityEngine;// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */
namespace Poison// For SharpMonoInjector usage :3
{// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */    public static class Plugin// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */    {// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */        // For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */        // Don't merge these methods, it just doesn't work
/* For SharpMonoInjector usage :3 */        public static void Inject()// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */        {// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */            var go = new GameObject("Poison");// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */            go.AddComponent<Injector>();// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */        }// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */
/* For SharpMonoInjector usage :3 */        public static void InjectDontDestroy()// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */        {// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */            var go = new GameObject("Poison");// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */            Object.DontDestroyOnLoad(go);// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */            go.AddComponent<Injector>();// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */        }// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */
/* For SharpMonoInjector usage :3 */        private sealed class Injector : MonoBehaviour// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */        {// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */            private void Awake()// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */            {// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                LogManager.SetLogger((Level level, string msg) =>// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                {// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                    switch (level)// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                    {// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                        case Level.Error:// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                            Debug.LogError(msg);// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                            break;// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                        case Level.Warning:// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                            Debug.LogWarning(msg);// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                            break;// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                        default:// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                            Debug.Log(msg);// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                            break;// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                    }// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                });// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */
/* For SharpMonoInjector usage :3 */                Bootstrapper.Initialize();// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */            }// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */
/* For SharpMonoInjector usage :3 */            private void OnDestroy() =>// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */                Main.UnloadMenu();// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */        }// For SharpMonoInjector usage :3
/* For SharpMonoInjector usage :3 */    }// For SharpMonoInjector usage :3
}// For SharpMonoInjector usage :3
