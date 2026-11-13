namespace World.Game
{
    /// <summary>
    /// The Tab map: a read-only top-down view of the island; tapping sets an auto-walk target (REFACTOR_SPEC 2.D,
    /// appendix B). Owner: package P. F-0 stub: only the open flag (Tab still switches to the interim God View).
    /// </summary>
    public static class MapScreen
    {
        public static bool IsOpen { get; private set; }
        public static void Open() => IsOpen = true;
        public static void Close() => IsOpen = false;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => IsOpen = false;
    }
}
