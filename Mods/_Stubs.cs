using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;

namespace Nova.Mods
{
    /// <summary>
    /// Stub namespace - all mod implementations excluded from build
    /// due to dependencies on patches which are incompatible with current Gorilla Tag version
    /// </summary>

    public static class Settings
    {
        public static Dictionary<ControllerBinding, UnityEngine.KeyCode> pcBindings = new();

        public enum ControllerBinding
        {
            RightPrimaryButton,
            RightSecondaryButton,
            LeftPrimaryButton,
            LeftSecondaryButton,
            LeftGrip,
            RightGrip,
            LeftTrigger
        }

        public static void LoadPreferences() { }
        public static void LoadPCControls() { }
        public static void Panic() { }
    }

    public static class Sound
    {
        public static void LoadSoundboard(bool arg) { }
    }

    public static class Movement
    {
        public static void LoadMacros() { }
    }

    public static class Visuals
    {
        public static void InstantiateWatch() { }
    }

    public static class ConsoleAssets
    {
        public static IEnumerator RefreshManifest()
        {
            yield break;
        }
    }
}

namespace Nova.Patches.Menu
{
    public static class SerializePatch
    {
        public static event Action<int> OnSerialize;
    }

    public static class PlayerSerializePatch
    {
        public static event Action<int, int> OnPlayerSerialize;
    }
}

