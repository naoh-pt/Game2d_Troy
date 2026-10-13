using System.Reflection;
using AttackOnTroy.Audio;
using UnityEditor;
using UnityEngine;

namespace AttackOnTroy.Editor.Audio
{
    public sealed class SoundPreviewWindow : EditorWindow
    {
        private Vector2 scroll;
        [MenuItem("AttackOnTroy/Audio/Preview Sound Effects")]
        public static void Open() { GetWindow<SoundPreviewWindow>("Troy SFX"); }

        private void OnGUI()
        {
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(SoundLibrarySetup.LibraryPath);
            if (library == null) { EditorGUILayout.HelpBox("Configure Sound Library first.",MessageType.Info); return; }
            EditorGUILayout.LabelField("47 events / selected variants",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Edit Mode: preview a clip. Play Mode: use the actual mixer, variations and volume.",MessageType.None);
            if (GUILayout.Button("Stop preview / loops"))
            {
                typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil")?.GetMethod("StopAllPreviewClips",BindingFlags.Static|BindingFlags.Public)?.Invoke(null,null);
                if (Application.isPlaying && AudioManager.Instance != null) AudioManager.Instance.StopGameplaySounds();
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (SoundDefinition sound in library.sounds)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(sound.id + " (" + sound.clips.Length + ")");
                if (GUILayout.Button("Play",GUILayout.Width(70)))
                {
                    if (Application.isPlaying)
                    {
                        AudioManager audio = AudioManager.EnsureInstance();
                        if (sound.loop) audio.StartLoop(sound.id,audio); else audio.PlaySFX(sound.id);
                    }
                    else
                    {
                        var utility = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
                        utility?.GetMethod("PlayPreviewClip",BindingFlags.Static|BindingFlags.Public,null,
                            new[] { typeof(AudioClip), typeof(int), typeof(bool) },null)?.Invoke(null,new object[] { sound.clips[0],0,false });
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
