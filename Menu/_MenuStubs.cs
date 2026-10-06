using UnityEngine;

namespace Nova.Menu
{
    /// <summary>
    /// Stub Main - all functionality disabled due to game API version mismatch.
    /// This DLL can load but will not provide mod menu functionality.
    /// </summary>
    public class Main : MonoBehaviour
    {
        public static void OnLaunch() { }
        public static void UnloadMenu() { }
        public static void Prefix() { }
        public static void ReloadMenu() { }

        private void OnGUI() { }
        private void OnDisable() { }
    }
}
