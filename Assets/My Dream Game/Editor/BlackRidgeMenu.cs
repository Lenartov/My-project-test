using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyDreamGame
{
    [InitializeOnLoad]
    internal static class BlackRidgeMenu
    {
        [Serializable]
        private struct SceneEntry
        {
            public string Path;
            public bool Loaded;
            public bool Active;
        }

        [Serializable]
        private sealed class Workspace
        {
            public SceneEntry[] Scenes;
        }

        private const string PlayPath = "Tools/My Dream Game/Play Black Ridge";
        private const string MusicPath = "Tools/My Dream Game/Choose Music...";
        private const string DefaultPath = "Tools/My Dream Game/Use Original Soundtrack";
        private const string WorkspaceKey = "MyDreamGame.BlackRidge.PreviousScenes";
        private static string MusicKey => "MyDreamGame.BlackRidge.Music." + Application.dataPath;

        static BlackRidgeMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem(PlayPath, false, 10)]
        private static void Play()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            RememberScenes();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NightStage stage = new GameObject("Black Ridge").AddComponent<NightStage>();
            stage.SelectMusic(EditorPrefs.GetString(MusicKey, string.Empty));
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }

        private static void RememberScenes()
        {
            SessionState.EraseString(WorkspaceKey);
            SceneSetup[] open = EditorSceneManager.GetSceneManagerSetup();
            if (open.Length == 0 || Array.Exists(open, scene => string.IsNullOrEmpty(scene.path)))
            {
                return;
            }

            Workspace workspace = new Workspace { Scenes = new SceneEntry[open.Length] };
            for (int i = 0; i < open.Length; i++)
            {
                workspace.Scenes[i] = new SceneEntry { Path = open[i].path, Loaded = open[i].isLoaded, Active = open[i].isActive };
            }

            SessionState.SetString(WorkspaceKey, JsonUtility.ToJson(workspace));
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            string snapshot = SessionState.GetString(WorkspaceKey, string.Empty);
            if (snapshot.Length == 0)
            {
                return;
            }

            SessionState.EraseString(WorkspaceKey);
            Workspace workspace = JsonUtility.FromJson<Workspace>(snapshot);
            SceneSetup[] restore = new SceneSetup[workspace.Scenes.Length];
            for (int i = 0; i < restore.Length; i++)
            {
                SceneEntry entry = workspace.Scenes[i];
                restore[i] = new SceneSetup { path = entry.Path, isLoaded = entry.Loaded, isActive = entry.Active };
            }

            EditorApplication.delayCall += () => EditorSceneManager.RestoreSceneManagerSetup(restore);
        }

        [MenuItem(PlayPath, true)]
        private static bool CanPlay()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        [MenuItem(MusicPath, false, 30)]
        private static void ChooseMusic()
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Choose Black Ridge music", string.Empty,
                new[] { "Audio", "wav,ogg,mp3" });
            if (!string.IsNullOrEmpty(path))
            {
                EditorPrefs.SetString(MusicKey, path);
                NightStage.Active?.SelectMusic(path);
            }
        }

        [MenuItem(DefaultPath, false, 31)]
        private static void ResetMusic()
        {
            EditorPrefs.DeleteKey(MusicKey);
            NightStage.Active?.SelectMusic(string.Empty);
        }
    }
}
