using UnityEditor;

namespace World.EditorTools
{
    /// <summary>
    /// Holds script reloads back while the editor is in Play Mode. The game's views keep their built state in plain C#
    /// objects that Unity does not serialize, and so does the test runner: a script edit saved during a Play Mode test
    /// run used to reload the domain mid-run, leaving the dance circle without its drums (a NullReferenceException every
    /// frame) and the test runner without its test tree, so the run broke and the editor had to be restarted.
    /// With this lock the edit is still compiled, but the reload waits until Play Mode ends (the same behaviour as the
    /// "Recompile After Finished Playing" preference, for everyone who opens the project).
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeReloadGuard
    {
        private static bool _locked;

        static PlayModeReloadGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Entering Play Mode reloads the domain; if this class is loaded while already playing, take the lock now
            // (EnteredPlayMode follows and finds it taken).
            if (EditorApplication.isPlaying) Lock();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode) Lock();
            else if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode) Unlock();
        }

        private static void Lock()
        {
            if (_locked) return;
            EditorApplication.LockReloadAssemblies();
            _locked = true;
        }

        private static void Unlock()
        {
            if (!_locked) return;
            _locked = false;
            EditorApplication.UnlockReloadAssemblies();
        }
    }
}
