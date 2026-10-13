using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AttackOnTroy.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AttackOnTroy.Editor.Audio
{
    /// <summary>Runs only audio in an empty Play Mode scene; never loads game actors or progression.</summary>
    [InitializeOnLoad]
    public static class AudioSmokeTests
    {
        private const string SessionKey = "TroyAudioTestsRunning";
        private const string TestScenePath = "Assets/AttackOnTroy/Editor/Audio/SCN_AudioTests.unity";
        private static IEnumerator routine;
        private static readonly List<string> results = new List<string>();
        private static double waitUntil;
        private static bool failed;
        private static float mixedPeak;

        static AudioSmokeTests()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
            EditorApplication.delayCall += () =>
            {
                // Script refresh can finish just after Play Mode starts; recover the local test session.
                if (SessionState.GetBool(SessionKey,false) && EditorApplication.isPlaying && routine == null) BeginTests();
            };
        }

        [MenuItem("AttackOnTroy/Audio/Run Audio-Only Unity Tests")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit existing Play Mode before running audio tests.");
            SoundLibrarySetup.Validate();
            Scene active = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            GameObject listener = new GameObject("AudioTestListener",typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(listener,scene);
            EditorSceneManager.SaveScene(scene,TestScenePath);
            EditorSceneManager.CloseScene(scene,true);
            if (active.IsValid()) SceneManager.SetActiveScene(active);
            SessionState.SetString("TroyAudioPreviousStartScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetInt("TroyAudioOriginalMuted",PlayerPrefs.GetInt("AttackOnTroy_SoundMuted",0));
            SessionState.SetBool("TroyAudioOriginalMutedExists",PlayerPrefs.HasKey("AttackOnTroy_SoundMuted"));
            SessionState.SetBool("TroyAudioOriginalEditorMute",EditorUtility.audioMasterMute);
            SessionState.SetBool(SessionKey,true);
            EditorUtility.audioMasterMute = false;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath);
            AudioEditorControl.WriteStatus("testing", "Entering isolated audio Play Mode scene.");
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(SessionKey,false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                BeginTests();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                routine = null;
                if (SessionState.GetBool("TroyAudioOriginalMutedExists",false))
                    PlayerPrefs.SetInt("AttackOnTroy_SoundMuted",SessionState.GetInt("TroyAudioOriginalMuted",0));
                else PlayerPrefs.DeleteKey("AttackOnTroy_SoundMuted");
                PlayerPrefs.Save();
                EditorUtility.audioMasterMute = SessionState.GetBool("TroyAudioOriginalEditorMute",false);
                string previous = SessionState.GetString("TroyAudioPreviousStartScene","");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                SessionState.SetBool(SessionKey,false);
            }
        }

        private static void BeginTests()
        {
            results.Clear(); failed = false; mixedPeak = 0; waitUntil = 0;
            routine = ExerciseAudio();
        }

        private static void Tick()
        {
            if (routine == null || EditorApplication.isCompiling || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < waitUntil) return;
            try
            {
                if (!routine.MoveNext()) { Finish(); return; }
                waitUntil = EditorApplication.timeSinceStartup + (routine.Current is double seconds ? seconds : 0);
            }
            catch (Exception exception)
            {
                failed = true;
                results.Add("FAIL: " + exception);
                Debug.LogException(exception);
                Finish();
            }
        }

        private static void Check(bool condition,string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            results.Add("PASS: " + name);
        }

        private static IEnumerator ExerciseAudio()
        {
            yield return 1.0;
            AudioManager audio = AudioManager.EnsureInstance();
            Check(SceneManager.GetActiveScene().name == "SCN_AudioTests", "Isolated scene contains no game logic");
            Check(Object.FindObjectsByType<ActorDamageReceiver2D>(FindObjectsSortMode.None).Length == 0, "No game actors instantiated");
            Check(Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None).Length == 1, "Single audio manager / direct scene bootstrap");
            audio.SetSoundMuted(false);
            audio.StopGameplaySounds();
            audio.SetGameplayPaused(false);
            float[] samples = new float[4096];
            int events = 0;
            Action<SoundId> count = _ => events++;
            audio.SoundPlayed += count;
            UnityEngine.Random.State randomBefore = UnityEngine.Random.state;
            float expectedRandom = UnityEngine.Random.value;
            UnityEngine.Random.state = randomBefore;
            Check(audio.PlaySFX(SoundId.SpearLaunch),"Spear cue starts");
            float actualRandom = UnityEngine.Random.value;
            UnityEngine.Random.state = randomBefore;
            Check(expectedRandom == actualRandom,"Audio does not consume gameplay RNG");
            audio.StopGameplaySounds();

            foreach (SoundDefinition sound in audio.Library.sounds)
            {
                AudioEditorControl.WriteStatus("testing","Checking " + sound.id);
                int before = events;
                AudioLoopHandle handle = default;
                if (sound.loop) handle = audio.StartLoop(sound.id,audio);
                else Check(audio.PlaySFX(sound.id),sound.id + " accepted");
                yield return 0.01;
                float peak = 0f;
                for (int i = 0; i < 10; i++)
                {
                    AudioListener.GetOutputData(samples,0);
                    foreach (float sample in samples) peak = Mathf.Max(peak,Mathf.Abs(sample));
                    yield return 0.025;
                }
                Check(events == before + 1,sound.id + " emits exactly one event");
                Check(peak > 0.000001f,sound.id + " produces nonzero mixer output (duration " + sound.clips[0].length.ToString("F3") + ")");
                mixedPeak = Mathf.Max(mixedPeak,peak);
                if (sound.loop) audio.StopLoop(handle);
                audio.StopGameplaySounds();
            }

            audio.SetSoundMuted(true);
            int mutedBefore = events;
            Check(!audio.PlaySFX(SoundId.UIClick) && events == mutedBefore,"Mute suppresses new one-shots");
            Check(AudioListener.volume == 0f,"Mute sets master output to zero");
            audio.SetSoundMuted(false);
            Check(AudioListener.volume == 1f && !AudioListener.pause,"Unmute restores listener");
            GameObject owner = new GameObject("AudioLoopOwner");
            AudioLoopHandle first = audio.StartLoop(SoundId.FireLoop,owner);
            Check(audio.IsLoopActive(first),"Fire loop starts");
            yield return 0.1;
            AudioSource fireSource = audio.GetComponentsInChildren<AudioSource>().First(s => s.loop && s.clip != null && s.clip.name == "FireLoop");
            Check(audio.StartLoop(SoundId.FireLoop,owner).IsValid && audio.ActiveLoopCount == 1,"Loop owner cannot duplicate loop");
            audio.SetGameplayPaused(true);
            Check(!fireSource.isPlaying,"Pause suspends the actual fire AudioSource");
            Check(!audio.PlaySFX(SoundId.SpearLaunch),"Pause rejects combat cues");
            Check(audio.PlaySFX(SoundId.UIBack),"Pause allows UI cues");
            yield return 0.1;
            Check(audio.IsGameplayPaused && audio.IsLoopActive(first),"Paused loop retains handle");
            audio.SetGameplayPaused(false);
            Check(fireSource.isPlaying,"Resume restarts the actual paused AudioSource");
            audio.StopLoop(first);
            AudioLoopHandle second = audio.StartLoop(SoundId.FireLoop,owner);
            audio.StopLoop(first);
            Check(audio.IsLoopActive(second),"Stale handle cannot stop reused loop");
            Object.Destroy(owner);
            yield return 0.1;
            Check(audio.ActiveLoopCount == 0,"Destroyed owner releases fire loop");

            GameObject a = new GameObject("EmitterA"), b = new GameObject("EmitterB");
            audio.StopGameplaySounds();
            Check(audio.PlaySFX(SoundId.AchillesHurt,a) && !audio.PlaySFX(SoundId.AchillesHurt,a) && audio.PlaySFX(SoundId.AchillesHurt,b),"Vocal throttle is per emitter");
            audio.StopGameplaySounds();
            Check(audio.PlaySFX(SoundId.ArrowLaunch,a) && audio.PlaySFX(SoundId.ArrowLaunch,b),"Double-shot cues are not collapsed");
            audio.StopGameplaySounds();
            audio.PlaySFX(SoundId.ArrowLaunch,a);
            audio.PlaySFX(SoundId.UIConfirm,b);
            audio.StopGameplaySounds(true);
            Check(audio.ActiveOneShotCount == 1,"Scene cleanup preserves navigation UI and stops combat");
            audio.StopGameplaySounds();
            for (int i = 0; i < AudioManager.OneShotCapacity; i++) audio.PlaySFX(SoundId.HumanFootstepSand,new GameObject("PoolEmitter_"+i));
            Check(audio.ActiveOneShotCount == AudioManager.OneShotCapacity,"One-shot pool has fixed capacity");
            Check(audio.PlaySFX(SoundId.Victory) && audio.ActiveOneShotCount == AudioManager.OneShotCapacity,"Result cue replaces lower-priority footstep");
            audio.StopGameplaySounds();
            for (int i=0;i<AudioManager.LoopCapacity;i++) Check(audio.StartLoop(SoundId.FireLoop,new GameObject("LoopEmitter_"+i)).IsValid,"Loop pool accepts owner " + i);
            Check(!audio.StartLoop(SoundId.FireLoop,a).IsValid,"Loop pool does not grow beyond capacity");
            audio.StopGameplaySounds();
            // Measure a representative worst-case layered mix without invoking gameplay.
            audio.StartLoop(SoundId.BeachAmbience,a);
            audio.StartLoop(SoundId.FireLoop,b);
            foreach (SoundId id in new[] { SoundId.BombExplosion,SoundId.JarBreak,SoundId.FleshImpact,SoundId.HeavyCyclopsHurt,SoundId.AchillesHurt,SoundId.EnergyReady,SoundId.ArrowLaunch,SoundId.SpearLaunch })
                audio.PlaySFX(id,new GameObject("MixEmitter_"+id));
            for (int i=0;i<12;i++)
            {
                AudioListener.GetOutputData(samples,0);
                foreach (float sample in samples) mixedPeak=Mathf.Max(mixedPeak,Mathf.Abs(sample));
                yield return 0.04;
            }
            Check(mixedPeak < 0.99f,"Measured mix has clipping headroom (peak " + mixedPeak.ToString("F5") + ")");
            audio.StopGameplaySounds();
            Check(audio.ActiveLoopCount == 0 && audio.ActiveOneShotCount == 0,"Scene cleanup releases all sounds");
            audio.SoundPlayed -= count;
        }

        private static void Finish()
        {
            routine = null;
            string report = "Audio-only Unity Play Mode tests\n" + DateTime.UtcNow.ToString("u") + "\n\n" + string.Join("\n",results) +
                "\n\nResult: " + (failed ? "FAILED" : "PASSED") + "\nMeasured peak: " + mixedPeak.ToString("F6") + "\n";
            Directory.CreateDirectory("Logs/AudioTests");
            File.WriteAllText("Logs/AudioTests/results.txt",report);
            AudioEditorControl.WriteStatus(failed ? "failed" : "passed",report);
            Debug.Log("[AudioTests] " + (failed ? "FAILED" : "PASSED") + ": " + results.Count + " checks. Report: Logs/AudioTests/results.txt");
            EditorApplication.ExitPlaymode();
        }
    }
}
