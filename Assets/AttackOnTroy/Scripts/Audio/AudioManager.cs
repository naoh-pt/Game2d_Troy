using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttackOnTroy.Audio
{
    /// <summary>
    /// Central audio manager for Attack on Troy.
    /// Manages persistent background music, smooth crossfading between menu and battle tracks,
    /// separate music/effects preferences (persisted via PlayerPrefs), and opening sting slots.
    /// Operates as a persistent DontDestroyOnLoad singleton.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class AudioManager : MonoBehaviour
    {
        private const string SoundMutedPrefKey = "AttackOnTroy_SoundMuted";
        private const string MusicMutedPrefKey = "AttackOnTroy_MusicMuted";
        private const string EffectsMutedPrefKey = "AttackOnTroy_EffectsMuted";

        public static AudioManager Instance { get; private set; }

        [Header("Audio Clips")]
        [Tooltip("Menu background music (FantasyOrchestralTheme.mp3) played across Splash, Home, LevelSelect, CharacterSelect.")]
        [SerializeField] private AudioClip menuMusicClip;

        [Tooltip("Battle background music (hope_orchestral_battle_music_bpm165.ogg) played across Level 01, 02, and 03.")]
        [SerializeField] private AudioClip battleMusicClip;

        [Tooltip("Optional opening sound effect or sting played during Splash.")]
        [SerializeField] private AudioClip splashIntroStingClip;

        [Header("Settings")]
        [Range(0.2f, 1.0f)]
        [SerializeField] private float crossfadeDuration = 0.4f;

        [Range(0f, 1f)]
        [SerializeField] private float defaultMusicVolume = 1.0f;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSourceA;
        [SerializeField] private AudioSource musicSourceB;
        [SerializeField] private AudioSource sfxSource;

        private AudioSource activeSource;
        private AudioSource inactiveSource;
        private Coroutine crossfadeCoroutine;
        private bool isMuted;
        private bool musicMuted;
        private bool effectsMuted;

        public event Action<bool> OnSoundToggled;
        public event Action OnAudioSettingsChanged;
        public bool IsMusicMuted => musicMuted;
        public bool IsEffectsMuted => effectsMuted;

        public bool IsSoundMuted
        {
            get => isMuted;
            set
            {
                if (musicMuted != value || effectsMuted != value)
                {
                    SetSoundMuted(value);
                }
            }
        }

        public AudioClip MenuMusicClip => menuMusicClip;
        public AudioClip BattleMusicClip => battleMusicClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeSoundLibrary();
            AutoAssignClipsIfNull();
            EnsureAudioSources();
            InitializeSoundSources();
            LoadSoundSettings();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        private void Start()
        {
            AutoAssignClipsIfNull();
            HandleSceneMusic(SceneManager.GetActiveScene().name);
        }

        private void AutoAssignClipsIfNull()
        {
#if UNITY_EDITOR
            if (menuMusicClip == null)
            {
                menuMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AttackOnTroy/Audio/Music/FantasyOrchestralTheme.mp3");
                if (menuMusicClip != null)
                {
                    Debug.Log("[AudioManager] Auto-loaded menuMusicClip from AssetDatabase.");
                }
            }
            if (battleMusicClip == null)
            {
                battleMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AttackOnTroy/Audio/Music/hope_orchestral_battle_music_bpm165.ogg");
                if (battleMusicClip != null)
                {
                    Debug.Log("[AudioManager] Auto-loaded battleMusicClip from AssetDatabase.");
                }
            }
#endif
            if (menuMusicClip != null && menuMusicClip.loadState != AudioDataLoadState.Loaded)
            {
                menuMusicClip.LoadAudioData();
            }
            if (battleMusicClip != null && battleMusicClip.loadState != AudioDataLoadState.Loaded)
            {
                battleMusicClip.LoadAudioData();
            }
        }

        private void EnsureAudioSources()
        {
            if (musicSourceA == null)
            {
                musicSourceA = gameObject.AddComponent<AudioSource>();
                ConfigureMusicSource(musicSourceA, "MusicSource_A");
            }
            if (musicSourceB == null)
            {
                musicSourceB = gameObject.AddComponent<AudioSource>();
                ConfigureMusicSource(musicSourceB, "MusicSource_B");
            }
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                sfxSource.spatialBlend = 0f;
            }

            if (activeSource == null) activeSource = musicSourceA;
            if (inactiveSource == null) inactiveSource = musicSourceB;
        }

        private void ConfigureMusicSource(AudioSource source, string sourceName)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 2D stereo
            source.volume = defaultMusicVolume;
            source.mute = musicMuted;
        }

        private void LoadSoundSettings()
        {
            // Preserve the old all-sound choice for players without separate preferences yet.
            int legacyMute = PlayerPrefs.GetInt(SoundMutedPrefKey, 0);
            musicMuted = PlayerPrefs.GetInt(MusicMutedPrefKey, legacyMute) == 1;
            effectsMuted = PlayerPrefs.GetInt(EffectsMutedPrefKey, legacyMute) == 1;
            ApplySoundSettings();
            OnAudioSettingsChanged?.Invoke();
        }

        public void SetSoundMuted(bool muted)
        {
            SetChannelSettings(muted, muted);
        }

        public void SetMusicMuted(bool muted) => SetChannelSettings(muted, effectsMuted);
        public void SetEffectsMuted(bool muted) => SetChannelSettings(musicMuted, muted);
        public void ToggleMusic() => SetMusicMuted(!musicMuted);
        public void ToggleEffects() => SetEffectsMuted(!effectsMuted);
        public void ToggleSound() => SetSoundMuted(!isMuted);

        private void SetChannelSettings(bool muteMusic, bool muteEffects)
        {
            if (muteEffects && !effectsMuted)
            {
                // Old one-shots must not burst back in when effects are enabled again.
                if (sfxSource != null) sfxSource.Stop();
                if (oneShots != null) foreach (Voice voice in oneShots) ReleaseVoice(voice);
            }
            musicMuted = muteMusic;
            effectsMuted = muteEffects;
            ApplySoundSettings();
            PlayerPrefs.SetInt(MusicMutedPrefKey, musicMuted ? 1 : 0);
            PlayerPrefs.SetInt(EffectsMutedPrefKey, effectsMuted ? 1 : 0);
            PlayerPrefs.SetInt(SoundMutedPrefKey, isMuted ? 1 : 0);
            PlayerPrefs.Save();
            if (!musicMuted && (activeSource == null || !activeSource.isPlaying || activeSource.clip == null))
                HandleSceneMusic(SceneManager.GetActiveScene().name);
            OnSoundToggled?.Invoke(isMuted);
            OnAudioSettingsChanged?.Invoke();
        }

        private void ApplySoundSettings()
        {
            isMuted = musicMuted && effectsMuted;
            AudioListener.volume = isMuted ? 0f : 1f;
            AudioListener.pause = isMuted;
            if (musicSourceA != null) musicSourceA.mute = musicMuted;
            if (musicSourceB != null) musicSourceB.mute = musicMuted;
            if (sfxSource != null) sfxSource.mute = effectsMuted;
            if (oneShots != null) foreach (Voice voice in oneShots) voice.source.mute = effectsMuted;
            // Keep owned loops alive while muted, so fire/ambience return when enabled.
            if (loops != null) foreach (Voice voice in loops) voice.source.mute = effectsMuted;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HandleSoundScene(scene, mode);
            HandleSceneMusic(scene.name);
        }

        private void HandleSceneMusic(string sceneName)
        {
            if (sceneName == "SCN_Splash" || sceneName == "SCN_Home")
            {
                PlayMenuMusic();
            }
            else if (sceneName.Contains("Level01") || sceneName.Contains("Level02") || sceneName.Contains("Level03"))
            {
                PlayBattleMusic();
            }
        }

        public void PlayMenuMusic()
        {
            AutoAssignClipsIfNull();
            PlayMusic(menuMusicClip);
        }

        public void PlayBattleMusic()
        {
            AutoAssignClipsIfNull();
            PlayMusic(battleMusicClip);
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] PlayMusic failed: clip is null!");
                return;
            }

            EnsureAudioSources();

            // If already playing the requested clip on the active source, ensure volume and keep playing
            if (activeSource != null && activeSource.isPlaying && activeSource.clip == clip)
            {
                if (activeSource.volume < 0.1f)
                {
                    activeSource.volume = defaultMusicVolume;
                }
                return;
            }

            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
            }

            crossfadeCoroutine = StartCoroutine(CrossfadeMusicRoutine(clip));
        }

        private IEnumerator CrossfadeMusicRoutine(AudioClip newClip)
        {
            EnsureAudioSources();
            float targetVolume = defaultMusicVolume;

            if (newClip.loadState != AudioDataLoadState.Loaded)
            {
                newClip.LoadAudioData();
            }

            // If nothing is playing yet (first start), play directly on active source
            bool isFirstPlay = activeSource == null || !activeSource.isPlaying || activeSource.clip == null;
            if (isFirstPlay)
            {
                activeSource.clip = newClip;
                activeSource.time = 0f;
                activeSource.volume = targetVolume;
                activeSource.Play();
                Debug.Log($"[AudioManager] Started playback on {activeSource.name} for {newClip.name} at volume {targetVolume}");
                crossfadeCoroutine = null;
                yield break;
            }

            // Crossfade between active and inactive source
            inactiveSource.clip = newClip;
            inactiveSource.time = 0f;
            inactiveSource.volume = 0f;
            inactiveSource.Play();

            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, crossfadeDuration);
            float startVol = activeSource.volume;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (activeSource != null)
                {
                    activeSource.volume = Mathf.Lerp(startVol, 0f, t);
                }
                if (inactiveSource != null)
                {
                    inactiveSource.volume = Mathf.Lerp(0f, targetVolume, t);
                }

                yield return null;
            }

            if (activeSource != null)
            {
                activeSource.volume = 0f;
                activeSource.Stop();
                activeSource.clip = null;
            }
            if (inactiveSource != null)
            {
                inactiveSource.volume = targetVolume;
            }

            // Swap active and inactive references
            AudioSource temp = activeSource;
            activeSource = inactiveSource;
            inactiveSource = temp;

            Debug.Log($"[AudioManager] Crossfade complete. Now playing: {activeSource.clip?.name} on {activeSource.name} at volume {activeSource.volume}");
            crossfadeCoroutine = null;
        }

        public void PlaySplashSting()
        {
            if (splashIntroStingClip != null && !effectsMuted && sfxSource != null)
            {
                sfxSource.PlayOneShot(splashIntroStingClip);
            }
            else if (splashIntroStingClip == null)
            {
                PlaySFX(SoundId.SplashSting);
            }
        }

        public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip != null && !effectsMuted && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip, volumeScale);
            }
        }
    }
}
