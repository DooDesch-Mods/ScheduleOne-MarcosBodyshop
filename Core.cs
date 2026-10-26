using MelonLoader;

[assembly: MelonInfo(typeof(MarcosBodyshop.Core), "Marco's Bodyshop", "0.0.0-dev", "DooDesch")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace MarcosBodyshop
{
    /// <summary>MelonLoader entry point. Phase 1 holds only the menu mockup and its console commands.</summary>
    public sealed class Core : MelonMod
    {
        public static MelonLogger.Instance Log { get; private set; }

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            int patched = 0;
            foreach (var _ in HarmonyInstance.GetPatchedMethods()) patched++;
            Log.Msg("[Core] loaded; Harmony patches on " + patched + " method(s).");
        }

        public override void OnUpdate()
        {
#if DEBUG
            Mock.GarageMock.Tick();
#endif
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
#if DEBUG
            Mock.GarageMock.SceneChanged(sceneName);
#endif
        }
    }
}
