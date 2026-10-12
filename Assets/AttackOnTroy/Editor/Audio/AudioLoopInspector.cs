using UnityEditor;
using UnityEngine;

namespace AttackOnTroy.Editor.Audio
{
    public static class AudioLoopInspector
    {
        [MenuItem("AttackOnTroy/Audio/Inspect Music Loops")]
        public static void InspectLoops()
        {
            InspectClip("Assets/AttackOnTroy/Audio/Music/FantasyOrchestralTheme.mp3");
            InspectClip("Assets/AttackOnTroy/Audio/Music/hope_orchestral_battle_music_bpm165.ogg");
        }

        private static void InspectClip(string path)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogError($"Could not load clip at {path}");
                return;
            }

            Debug.Log($"=== Audio Inspection: {clip.name} ===");
            Debug.Log($"Length: {clip.length:F2}s, Samples: {clip.samples}, Channels: {clip.channels}, Frequency: {clip.frequency}Hz");

            // Sample start and end to check for silence / gaps
            int sampleCount = Mathf.Min(clip.samples, 44100); // 1 second sample
            float[] startData = new float[sampleCount * clip.channels];
            clip.GetData(startData, 0);

            float[] endData = new float[sampleCount * clip.channels];
            clip.GetData(endData, clip.samples - sampleCount);

            float startMax = 0f;
            int startSilentSamples = 0;
            for (int i = 0; i < startData.Length; i++)
            {
                float val = Mathf.Abs(startData[i]);
                if (val > startMax) startMax = val;
                if (startMax < 0.01f) startSilentSamples++;
            }

            float endMax = 0f;
            int endSilentSamples = 0;
            for (int i = endData.Length - 1; i >= 0; i--)
            {
                float val = Mathf.Abs(endData[i]);
                if (val > endMax) endMax = val;
                if (endMax < 0.01f) endSilentSamples++;
            }

            float startSilenceSec = (float)(startSilentSamples / clip.channels) / clip.frequency;
            float endSilenceSec = (float)(endSilentSamples / clip.channels) / clip.frequency;

            Debug.Log($"Start silence: ~{startSilenceSec:F3}s, End silence: ~{endSilenceSec:F3}s");
            if (endSilenceSec > 0.1f || startSilenceSec > 0.1f)
            {
                Debug.LogWarning($"[Loop Warning] Clip {clip.name} has noticeable silence at loop boundary (Start: {startSilenceSec:F2}s, End: {endSilenceSec:F2}s).");
            }
            else
            {
                Debug.Log($"[Loop Info] Clip {clip.name} has tight boundaries without large trailing/leading silence.");
            }
        }

        [MenuItem("AttackOnTroy/Audio/Reset Sound Settings (Unmute)")]
        public static void ResetSoundSettings()
        {
            PlayerPrefs.DeleteKey("AttackOnTroy_SoundMuted");
            PlayerPrefs.Save();
            AudioListener.volume = 1f;
            AudioListener.pause = false;
            Debug.Log("[AudioSetup] Reset AttackOnTroy_SoundMuted to Unmuted (AudioListener.volume = 1f).");
        }

        [MenuItem("AttackOnTroy/Audio/Configure Music Importers (Preload and 2D)")]
        public static void ConfigureImporters()
        {
            ConfigureClipImporter("Assets/AttackOnTroy/Audio/Music/FantasyOrchestralTheme.mp3");
            ConfigureClipImporter("Assets/AttackOnTroy/Audio/Music/hope_orchestral_battle_music_bpm165.ogg");
        }

        private static void ConfigureClipImporter(string path)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                var settings = importer.defaultSampleSettings;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                Debug.Log($"[AudioSetup] Successfully configured and reimported {path} (preloadAudioData=true).");
            }
        }
    }
}
