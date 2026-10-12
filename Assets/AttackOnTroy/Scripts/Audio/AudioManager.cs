using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttackOnTroy.Audio
{
    /// <summary>
    /// Central audio manager for Attack on Troy.
    /// Manages persistent background music, smooth crossfading between menu and battle tracks,
    /// master sound mute state (persisted via PlayerPrefs), and opening sting slots.
    /// Operates as a persistent DontDestroyOnLoad singleton.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private const string SoundMutedPrefKey = "AttackOnTroy_SoundMuted";

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

        public event Action<bool> OnSoundToggled;

        public bool IsSoundMuted
        {
            get => isMuted;
            set
            {
                if (isMuted != value)
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

            AutoAssignClipsIfNull();
            EnsureAudioSources();
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
        }

        private void LoadSoundSettings()
        {
            isMuted = PlayerPrefs.GetInt(SoundMutedPrefKey, 0) == 1;
            ApplyMasterVolume(isMuted);
        }

        public void SetSoundMuted(bool muted)
        {
            isMuted = muted;
            PlayerPrefs.SetInt(SoundMutedPrefKey, isMuted ? 1 : 0);
            PlayerPrefs.Save();

            ApplyMasterVolume(isMuted);

            if (!isMuted)
            {
                // When unmuted, guarantee music is actively playing
                if (activeSource == null || !activeSource.isPlaying || activeSource.clip == null)
                {
                    HandleSceneMusic(SceneManager.GetActiveScene().name);
                }
                else if (activeSource.volume < 0.1f)
                {
                    activeSource.volume = defaultMusicVolume;
                }
            }

            OnSoundToggled?.Invoke(isMuted);
            Debug.Log($"[AudioManager] Sound muted state set to: {isMuted}. AudioListener.volume={AudioListener.volume}, ActiveSource playing={activeSource?.isPlaying}, Clip={activeSource?.clip?.name}");
        }

        public void ToggleSound()
        {
            SetSoundMuted(!isMuted);
        }

        private void ApplyMasterVolume(bool muted)
        {
            AudioListener.volume = muted ? 0f : 1f;
            AudioListener.pause = muted;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
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
            if (splashIntroStingClip != null && !isMuted && sfxSource != null)
            {
                sfxSource.PlayOneShot(splashIntroStingClip);
            }
        }

        public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip != null && !isMuted && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip, volumeScale);
            }
        }
    }
}
