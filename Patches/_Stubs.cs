namespace Nova.Patches
{
    /// <summary>
    /// Stub namespace - all patch implementations excluded from build
    /// due to game API version mismatch with current Gorilla Tag version
    /// </summary>
}

namespace Nova.Patches.Menu
{
    public static class TOSPatches
    {
        public static bool enabled;
    }

    public static class PatchHandler
    {
        public static void PatchAll(bool initial = false)
        {
            // Patches disabled - game API mismatch
        }

        public static void UnpatchAll()
        {
            // Patches disabled - game API mismatch
        }
    }
}

namespace Nova.Patches.Safety
{
    /// <summary>
    /// Stub namespace - all safety patch implementations excluded
    /// </summary>
}
