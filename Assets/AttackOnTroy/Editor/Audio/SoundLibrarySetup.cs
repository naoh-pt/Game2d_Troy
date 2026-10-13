using System;
using System.IO;
using System.Linq;
using AttackOnTroy.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace AttackOnTroy.Editor.Audio
{
    public static class SoundLibrarySetup
    {
        public const string LibraryPath = "Assets/AttackOnTroy/Resources/Audio/GameSoundLibrary.asset";
        [Serializable] private sealed class Manifest { public Entry[] sounds; public FileEntry[] files; }
        [Serializable] private sealed class Entry
        {
            public string id, bus;
            public string[] clips;
            public float volume, pitchMin, pitchMax, minimumInterval;
            public int priority;
            public bool loop;
        }
        [Serializable] private sealed class FileEntry { public string assetPath; public bool loop; }

        [MenuItem("AttackOnTroy/Audio/Configure Sound Library")]
        public static void Configure()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/AttackOnTroy/Audio/SFX/sound-manifest.json"));
            foreach (FileEntry file in manifest.files)
            {
                AudioImporter importer = AssetImporter.GetAtPath(file.assetPath) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Missing audio importer: " + file.assetPath);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = file.loop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = file.loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                settings.quality = 0.65f;
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                settings.sampleRateOverride = 44100;
                settings.preloadAudioData = !file.loop;
                importer.defaultSampleSettings = settings;
                importer.SetOverrideSampleSettings("Android", settings);
                importer.forceToMono = !file.loop;
                importer.loadInBackground = file.loop;
                importer.ambisonic = false;
                importer.SaveAndReimport();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.menuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AttackOnTroy/Audio/Music/FantasyOrchestralTheme.mp3");
            library.battleMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AttackOnTroy/Audio/Music/hope_orchestral_battle_music_bpm165.ogg");
            library.mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/AttackOnTroy/Audio/Mixers/MX_GameAudio.mixer");
            if (library.mixer == null) throw new InvalidOperationException("Audio mixer failed to import.");
            library.musicGroup = Group(library.mixer,"Music");
            library.combatGroup = Group(library.mixer,"Combat");
            library.voicesGroup = Group(library.mixer,"Voices");
            library.uiGroup = Group(library.mixer,"UI");
            library.ambienceGroup = Group(library.mixer,"Ambience");
            library.credits = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/AttackOnTroy/Audio/SFX/AudioCredits.txt");
            library.sounds = manifest.sounds.Select(entry => new SoundDefinition
            {
                id = (SoundId)Enum.Parse(typeof(SoundId),entry.id),
                bus = (SoundBus)Enum.Parse(typeof(SoundBus),entry.bus),
                clips = entry.clips.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray(),
                volume = entry.volume,
                pitch = new Vector2(entry.pitchMin,entry.pitchMax),
                minimumInterval = entry.minimumInterval,
                priority = entry.priority,
                loop = entry.loop
            }).ToArray();
            EditorUtility.SetDirty(library);
            // Do not save unrelated dirty assets (e.g. dynamically populated TMP atlases).
            AssetDatabase.SaveAssetIfDirty(library);
            Validate();
            Debug.Log($"[Audio] Configured {library.sounds.Length} events / {manifest.files.Length} clips.");
        }

        private static AudioMixerGroup Group(AudioMixer mixer, string name)
        {
            AudioMixerGroup group = mixer.FindMatchingGroups(name).FirstOrDefault(g => g.name == name);
            if (group == null) throw new InvalidOperationException("Missing mixer group " + name);
            return group;
        }

        public static void RefreshImpactClips()
        {
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/AttackOnTroy/Audio/SFX/sound-manifest.json"));
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            foreach (Entry entry in manifest.sounds.Where(e => e.id == "FleshImpact" || e.id == "GroundImpact" || e.id == "StoneImpact"))
            {
                foreach (string path in entry.clips)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                SoundId id = (SoundId)Enum.Parse(typeof(SoundId), entry.id);
                // Update the existing definition, also used by the live manager's dictionary.
                SoundDefinition definition = library.sounds.Single(s => s.id == id);
                definition.clips = entry.clips.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
                definition.volume = entry.volume;
                definition.pitch = new Vector2(entry.pitchMin, entry.pitchMax);
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            Validate();
        }

        [MenuItem("AttackOnTroy/Audio/Apply and Verify Volume Balance")]
        public static void ApplyAndVerifyVolume()
        {
            Validate();
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            AudioManager audio = Application.isPlaying ? AudioManager.EnsureInstance() : null;
            bool paused = audio != null && audio.IsGameplayPaused;
            string[] names = { "MusicVolume", "AmbienceVolume", "CombatVolume", "VoicesVolume", "UIVolume" };
            float[] levels = { paused ? -26f : -18f, paused ? -36f : -30f, 4f, 2f, -2f };
            var report = new System.Text.StringBuilder("Unity audio volume verification\n");
            report.AppendLine("Existing Play Mode preserved: " + Application.isPlaying);
            for (int i = 0; i < names.Length; i++)
            {
                if (!library.mixer.SetFloat(names[i],levels[i]) || !library.mixer.GetFloat(names[i],out float actual) || Mathf.Abs(actual-levels[i]) > 0.01f)
                    throw new InvalidOperationException("Mixer level verification failed: " + names[i]);
                report.AppendLine("PASS: " + names[i] + " = " + actual + " dB");
            }
            if (Application.isPlaying)
            {
                float[] samples = new float[4096];
                AudioListener.GetOutputData(samples,0);
                float peak = samples.Max(value => Mathf.Abs(value));
                if (peak >= 0.99f) throw new InvalidOperationException("Current live audio buffer is clipping: " + peak);
                report.AppendLine("PASS: Current live output buffer below clipping; peak = " + peak.ToString("F6"));
                report.AppendLine("Listener mute/pause preserved; no test cues injected, scene changes, or gameplay restart.");
            }
            report.AppendLine("Result: PASSED");
            Directory.CreateDirectory("Logs/AudioTests");
            File.WriteAllText("Logs/AudioTests/volume-balance.txt",report.ToString());
            Debug.Log("[Audio] Volume balance verified without restarting Play Mode.");
        }

        [MenuItem("AttackOnTroy/Audio/Validate Sound Library")]
        public static void Validate()
        {
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null || library.menuMusic == null || library.battleMusic == null || library.credits == null)
                throw new InvalidOperationException("Library/music/credits missing.");
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                SoundDefinition[] entries = library.sounds.Where(s => s.id == id).ToArray();
                if (entries.Length != 1 || entries[0].clips.Length == 0 || entries[0].clips.Any(c => c == null))
                    throw new InvalidOperationException("Missing, duplicate or empty sound: " + id);
                SoundDefinition entry = entries[0];
                if (library.GetGroup(entry.bus) == null) throw new InvalidOperationException("Unrouted sound: " + id);
                foreach (AudioClip clip in entry.clips)
                    if (clip.frequency != 44100 || clip.length <= 0 || (!entry.loop && clip.channels != 1))
                        throw new InvalidOperationException("Invalid clip format: " + clip.name);
            }
            Debug.Log("[Audio] Library validation passed.");
        }
    }
}
