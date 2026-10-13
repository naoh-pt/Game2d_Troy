using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AttackOnTroy.Audio
{
    public sealed partial class AudioManager
    {
        public const int OneShotCapacity = 16;
        public const int LoopCapacity = 4;
        private const float MusicVolumeDb = -18f;
        private const float AmbienceVolumeDb = -30f;

        private sealed class Voice
        {
            public AudioSource source;
            public SoundId id;
            public Object owner;
            public bool hasOwner;
            public bool busy;
            public bool paused;
            public uint generation;
            public double endTime;
        }

        private struct ThrottleKey : IEquatable<ThrottleKey>
        {
            public SoundId id;
            public int emitter;
            public bool Equals(ThrottleKey other) => id == other.id && emitter == other.emitter;
            public override bool Equals(object other) => other is ThrottleKey key && Equals(key);
            public override int GetHashCode() => ((int)id * 397) ^ emitter;
        }

        private SoundLibrary soundLibrary;
        private readonly System.Random soundRandom = new System.Random();
        private readonly Dictionary<SoundId, SoundDefinition> definitions = new Dictionary<SoundId, SoundDefinition>();
        private readonly Dictionary<SoundId, int> lastVariants = new Dictionary<SoundId, int>();
        private readonly Dictionary<ThrottleKey, double> lastPlayed = new Dictionary<ThrottleKey, double>();
        private readonly List<ThrottleKey> expiredThrottleKeys = new List<ThrottleKey>();
        private Voice[] oneShots;
        private Voice[] loops;
        private AudioLoopHandle ambienceHandle;
        private bool gameplayPaused;
        private double nextThrottleCleanup;
        private float resultDuckTimer;
        private float currentMusicDb = MusicVolumeDb;

        public SoundLibrary Library => soundLibrary;
        public bool IsGameplayPaused => gameplayPaused;
        public event Action<SoundId> SoundPlayed;
        public int ActiveOneShotCount => CountBusy(oneShots);
        public int ActiveLoopCount => CountBusy(loops);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAudioStatics() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAudio()
        {
            EnsureInstance();
            Instance.HandleSoundScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        public static AudioManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            AudioManager existing = FindAnyObjectByType<AudioManager>();
            if (existing != null)
            {
                // Also works with Enter Play Mode domain/scene reload disabled.
                Instance = existing;
                existing.InitializeSoundLibrary();
                existing.EnsureAudioSources();
                if (existing.oneShots == null) existing.InitializeSoundSources();
                existing.LoadSoundSettings();
                SceneManager.sceneLoaded -= existing.OnSceneLoaded;
                SceneManager.sceneLoaded += existing.OnSceneLoaded;
                return existing;
            }
            return new GameObject("AudioManager").AddComponent<AudioManager>();
        }

        private void InitializeSoundLibrary()
        {
            soundLibrary = Resources.Load<SoundLibrary>(SoundLibrary.ResourcePath);
            definitions.Clear();
            if (soundLibrary == null) return;
            foreach (SoundDefinition definition in soundLibrary.sounds)
                if (definition != null) definitions[definition.id] = definition;
            if (menuMusicClip == null) menuMusicClip = soundLibrary.menuMusic;
            if (battleMusicClip == null) battleMusicClip = soundLibrary.battleMusic;
        }

        private void InitializeSoundSources()
        {
            oneShots = CreateVoices(OneShotCapacity, "SFX");
            loops = CreateVoices(LoopCapacity, "Loop");
            if (soundLibrary == null) return;
            musicSourceA.outputAudioMixerGroup = soundLibrary.musicGroup;
            musicSourceB.outputAudioMixerGroup = soundLibrary.musicGroup;
            sfxSource.outputAudioMixerGroup = soundLibrary.combatGroup;
        }

        private Voice[] CreateVoices(int count, string prefix)
        {
            Voice[] voices = new Voice[count];
            for (int i = 0; i < count; i++)
            {
                Transform existing = transform.Find(prefix + "_" + i);
                GameObject child = existing != null ? existing.gameObject : new GameObject(prefix + "_" + i);
                child.transform.SetParent(transform, false);
                AudioSource source = child.GetComponent<AudioSource>();
                if (source == null) source = child.AddComponent<AudioSource>();
                source.Stop();
                source.clip = null;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                voices[i] = new Voice { source = source };
            }
            return voices;
        }

        public bool PlaySFX(SoundId id, Object emitter = null)
        {
            if (!TryGetSound(id, out SoundDefinition definition) || definition.loop || effectsMuted ||
                (gameplayPaused && definition.bus != SoundBus.UI)) return false;
            ThrottleKey key = new ThrottleKey { id = id, emitter = emitter != null ? emitter.GetHashCode() : 0 };
            double now = Time.realtimeSinceStartupAsDouble;
            if (definition.minimumInterval > 0f && lastPlayed.TryGetValue(key, out double previous) &&
                now - previous < definition.minimumInterval) return false;

            Voice voice = FindFreeVoice(oneShots, definition.priority, true);
            if (voice == null) return false;
            ConfigureVoice(voice, definition, emitter);
            voice.source.Play();
            // Allow the audio thread to consume its first buffer before reclaiming tiny UI clips.
            voice.endTime = AudioSettings.dspTime + voice.source.clip.length / Mathf.Abs(voice.source.pitch) + 0.1;
            if (definition.minimumInterval > 0f) lastPlayed[key] = now;
            if (id == SoundId.Victory || id == SoundId.Defeat) resultDuckTimer = voice.source.clip.length + 0.4f;
            SoundPlayed?.Invoke(id);
            return true;
        }

        public AudioLoopHandle StartLoop(SoundId id, Object owner)
        {
            if (!TryGetSound(id, out SoundDefinition definition) || !definition.loop || owner == null) return default;
            for (int i = 0; i < loops.Length; i++)
                if (loops[i].busy && loops[i].id == id && loops[i].owner == owner)
                    return new AudioLoopHandle(i, loops[i].generation);
            Voice voice = FindFreeVoice(loops, definition.priority, false);
            if (voice == null) return default;
            ConfigureVoice(voice, definition, owner);
            voice.source.Play();
            if (gameplayPaused && definition.bus != SoundBus.Ambience && definition.bus != SoundBus.UI)
            {
                voice.source.Pause();
                voice.paused = true;
            }
            SoundPlayed?.Invoke(id);
            return new AudioLoopHandle(Array.IndexOf(loops, voice), voice.generation);
        }

        public bool IsLoopActive(AudioLoopHandle handle) => handle.IsValid && loops != null &&
            handle.Slot >= 0 && handle.Slot < loops.Length && loops[handle.Slot].busy &&
            loops[handle.Slot].generation == handle.Generation;

        public void StopLoop(AudioLoopHandle handle)
        {
            if (IsLoopActive(handle)) ReleaseVoice(loops[handle.Slot]);
        }

        public void SetGameplayPaused(bool paused)
        {
            if (gameplayPaused == paused) return;
            gameplayPaused = paused;
            PauseVoices(oneShots, paused);
            PauseVoices(loops, paused);
        }

        private void PauseVoices(Voice[] voices, bool paused)
        {
            if (voices == null) return;
            foreach (Voice voice in voices)
            {
                if (!voice.busy || !definitions.TryGetValue(voice.id, out SoundDefinition definition) ||
                    definition.bus == SoundBus.UI || definition.bus == SoundBus.Ambience) continue;
                if (paused)
                {
                    voice.endTime = Math.Max(0, voice.endTime - AudioSettings.dspTime);
                    voice.source.Pause();
                }
                else
                {
                    voice.endTime += AudioSettings.dspTime;
                    voice.source.UnPause();
                }
                voice.paused = paused;
            }
        }

        private bool TryGetSound(SoundId id, out SoundDefinition definition) =>
            oneShots != null && definitions.TryGetValue(id, out definition) ?
                definition.clips != null && definition.clips.Length > 0 : Missing(out definition);

        private static bool Missing(out SoundDefinition definition) { definition = null; return false; }

        private Voice FindFreeVoice(Voice[] voices, int priority, bool allowSteal)
        {
            Voice candidate = null;
            foreach (Voice voice in voices)
            {
                if (voice.busy && voice.source.loop && voice.hasOwner && voice.owner == null) ReleaseVoice(voice);
                if (voice.busy && !voice.source.loop && !voice.paused && !AudioListener.pause &&
                    AudioSettings.dspTime >= voice.endTime) ReleaseVoice(voice);
                if (!voice.busy) return voice;
                if (allowSteal && voice.source.priority > priority &&
                    (candidate == null || voice.source.priority > candidate.source.priority)) candidate = voice;
            }
            if (candidate != null) ReleaseVoice(candidate);
            return candidate;
        }

        private void ConfigureVoice(Voice voice, SoundDefinition definition, Object owner)
        {
            int count = definition.clips.Length;
            int index = soundRandom.Next(count);
            if (count > 1 && lastVariants.TryGetValue(definition.id, out int last) && index == last)
                index = (index + soundRandom.Next(1, count)) % count;
            lastVariants[definition.id] = index;
            voice.source.clip = definition.clips[index];
            voice.source.loop = definition.loop;
            voice.source.volume = definition.volume;
            voice.source.mute = effectsMuted;
            voice.source.pitch = Mathf.Clamp(Mathf.Lerp(definition.pitch.x, definition.pitch.y, (float)soundRandom.NextDouble()), 0.25f, 2f);
            voice.source.priority = definition.priority;
            voice.source.ignoreListenerPause = definition.bus == SoundBus.UI;
            voice.source.outputAudioMixerGroup = soundLibrary.GetGroup(definition.bus);
            voice.id = definition.id;
            voice.owner = owner;
            voice.hasOwner = owner != null;
            voice.busy = true;
            voice.paused = false;
            if (++voice.generation == 0) voice.generation = 1;
        }

        private static void ReleaseVoice(Voice voice)
        {
            voice.source.Stop();
            voice.source.clip = null;
            voice.owner = null;
            voice.busy = false;
            voice.paused = false;
        }

        public void StopGameplaySounds(bool preserveNavigationUI = false)
        {
            if (oneShots != null) foreach (Voice voice in oneShots)
                if (!preserveNavigationUI || !definitions.TryGetValue(voice.id, out SoundDefinition definition) ||
                    definition.bus != SoundBus.UI || voice.id == SoundId.Victory || voice.id == SoundId.Defeat || voice.id == SoundId.SplashSting)
                    ReleaseVoice(voice);
            if (loops != null) foreach (Voice voice in loops) ReleaseVoice(voice);
            lastPlayed.Clear();
            lastVariants.Clear();
            resultDuckTimer = 0f;
        }

        private void HandleSoundScene(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (mode == LoadSceneMode.Single)
            {
                StopGameplaySounds(true);
                SetGameplayPaused(false);
            }
            SceneAudioFeedback.Install(scene);
            SoundId? ambience = scene.name.Contains("Level01") ? SoundId.BeachAmbience :
                scene.name.Contains("Level02") ? SoundId.RiverAmbience :
                scene.name.Contains("Level03") ? SoundId.WallAmbience : (SoundId?)null;
            if (ambience.HasValue && !IsLoopActive(ambienceHandle)) ambienceHandle = StartLoop(ambience.Value, this);
        }

        private void Update()
        {
            if (oneShots == null) return;
            foreach (Voice voice in oneShots)
                if (voice.busy && !voice.paused && (!AudioListener.pause || voice.source.ignoreListenerPause) &&
                    AudioSettings.dspTime >= voice.endTime) ReleaseVoice(voice);
            foreach (Voice voice in loops)
                if (voice.busy && voice.hasOwner && voice.owner == null) ReleaseVoice(voice);
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextThrottleCleanup)
            {
                expiredThrottleKeys.Clear();
                foreach (var item in lastPlayed) if (now - item.Value > 10) expiredThrottleKeys.Add(item.Key);
                foreach (var key in expiredThrottleKeys) lastPlayed.Remove(key);
                nextThrottleCleanup = now + 10;
            }
            resultDuckTimer = Mathf.Max(0f, resultDuckTimer - Time.unscaledDeltaTime);
            if (soundLibrary != null && soundLibrary.mixer != null)
            {
                float target = MusicVolumeDb - (gameplayPaused ? 8f : 0f) - (resultDuckTimer > 0f ? 6f : 0f);
                currentMusicDb = Mathf.MoveTowards(currentMusicDb, target, Time.unscaledDeltaTime * 30f);
                soundLibrary.mixer.SetFloat("MusicVolume", currentMusicDb);
                soundLibrary.mixer.SetFloat("AmbienceVolume", AmbienceVolumeDb - (gameplayPaused ? 6f : 0f));
            }
        }

        private static int CountBusy(Voice[] voices)
        {
            int count = 0;
            if (voices != null) foreach (Voice voice in voices) if (voice.busy) count++;
            return count;
        }
    }
}
