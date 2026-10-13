using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AttackOnTroy.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AttackOnTroy.Editor.Audio
{
    /// <summary>Real prefab collision -> audio tests, in a separate local physics world.</summary>
    [InitializeOnLoad]
    public static class ProjectileImpactAudioTests
    {
        private const string SessionKey = "TroyImpactAudioTests";
        private const string StartSceneKey = "TroyImpactPreviousStartScene";
        private const string TestScenePath = "Assets/AttackOnTroy/Editor/Audio/SCN_AudioTests.unity";
        private static readonly List<string> results = new List<string>();
        private static readonly FieldInfo Voices = typeof(AudioManager).GetField("oneShots", BindingFlags.Instance | BindingFlags.NonPublic);
        private static IEnumerator routine;
        private static double waitUntil;
        private static bool failed, enteredPlay;
        private static Scene probeScene;
        private static AudioManager audio;
        private static Action<SoundId> observer;

        static ProjectileImpactAudioTests()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(SessionKey, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode && routine == null) Begin();
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    string previous = SessionState.GetString(StartSceneKey, "");
                    EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                    SessionState.SetBool(SessionKey, false);
                }
            };
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(SessionKey, false) && EditorApplication.isPlaying && routine == null) Begin();
            };
        }

        [MenuItem("AttackOnTroy/Audio/Test Projectile Impact Audio")]
        public static void Run()
        {
            if (routine != null) throw new InvalidOperationException("Impact audio probe is already running.");
            SoundLibrarySetup.RefreshImpactClips();
            if (EditorApplication.isPlaying) { Begin(); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Unity is changing Play Mode.");
            SessionState.SetString(StartSceneKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(SessionKey, true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath);
            AudioEditorControl.WriteStatus("impact-testing", "Entering the existing empty audio test scene.");
            EditorApplication.EnterPlaymode();
        }

        private static void Begin()
        {
            results.Clear(); failed = false; waitUntil = 0;
            enteredPlay = SessionState.GetBool(SessionKey, false);
            results.Add("Unity projectile impact audio verification " + DateTime.Now.ToString("s"));
            results.Add("Real spear/arrow prefabs; isolated PhysicsScene2D; no battle scene restart, APK or emulator.");
            routine = Exercise();
        }

        private static void Tick()
        {
            if (routine == null || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < waitUntil) return;
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode ended during impact tests.");
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

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS: " + message);
        }

        private static AudioSource FindVoice(AudioManager manager, SoundId id, Object owner)
        {
            foreach (object voice in (Array)Voices.GetValue(manager))
            {
                Type type = voice.GetType();
                if ((SoundId)type.GetField("id").GetValue(voice) == id &&
                    ReferenceEquals(type.GetField("owner").GetValue(voice), owner))
                    return (AudioSource)type.GetField("source").GetValue(voice);
            }
            return null;
        }

        private static IEnumerator Exercise()
        {
            yield return .3;
            audio = AudioManager.EnsureInstance();
            Check(!audio.IsSoundMuted && !audio.IsGameplayPaused && !AudioListener.pause && !EditorUtility.audioMasterMute,
                "Audio is enabled; existing mute/pause settings preserved");
            Check(AudioListener.volume > 0, "Listener volume is audible");
            Scene originalScene = SceneManager.GetActiveScene();
            probeScene = SceneManager.CreateScene("ProjectileAudioProbe", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            PhysicsScene2D physics = probeScene.GetPhysicsScene2D();
            Check(physics.IsValid() && physics != Physics2D.defaultPhysicsScene, "Probe physics cannot collide with battle actors");
            Check(SceneManager.GetActiveScene() == originalScene, "Active gameplay scene preserved");
            Check(audio.Library.mixer.GetFloat("CombatVolume", out float combatDb) && Mathf.Abs(combatDb - 4) < .01f,
                "Existing Combat +4 dB mix preserved");

            // Check every imported variant after Unity decoding, including its small-speaker frequency content.
            foreach (SoundId id in new[] { SoundId.FleshImpact, SoundId.GroundImpact, SoundId.StoneImpact })
            {
                SoundDefinition definition = audio.Library.sounds.Single(s => s.id == id);
                foreach (AudioClip clip in definition.clips)
                {
                    if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                    Check(clip.loadState == AudioDataLoadState.Loaded, clip.name + " preloaded");
                    float[] pcm = new float[clip.samples * clip.channels];
                    Check(clip.GetData(pcm, 0), clip.name + " imported PCM available");
                    // One-pole 700 Hz high-pass estimates an audible mid/high contribution.
                    double energy = 0, highEnergy = 0, lastIn = 0, lastOut = 0;
                    double alpha = 1.0 / (1.0 + 2 * Math.PI * 700 / clip.frequency);
                    foreach (float sample in pcm)
                    {
                        double high = alpha * (lastOut + sample - lastIn);
                        energy += sample * sample; highEnergy += high * high;
                        lastIn = sample; lastOut = high;
                    }
                    double ratio = highEnergy / Math.Max(energy, 1e-12);
                    Check(ratio > .15, clip.name + " audible mid/high energy = " + ratio.ToString("P1"));
                }
            }

            string[] prefabs = { "PF_Projectile_Achilles_Spear", "PF_Projectile_Odysseus_Arrow" };
            foreach (string prefabName in prefabs)
            foreach (SoundId expected in new[] { SoundId.GroundImpact, SoundId.StoneImpact, SoundId.FleshImpact })
            {
                string label = prefabName + " -> " + expected;
                AudioEditorControl.WriteStatus("impact-testing", label);
                Vector3 origin = new Vector3(10000, 10000, 0);
                GameObject wall = new GameObject("AudioProbeTarget");
                wall.transform.position = origin + Vector3.right * 5;
                SceneManager.MoveGameObjectToScene(wall, probeScene);
                wall.layer = expected == SoundId.GroundImpact ? LayerMask.NameToLayer("Terrain") : 0;
                BoxCollider2D targetCollider = wall.AddComponent<BoxCollider2D>();
                targetCollider.size = new Vector2(1, 10);
                ProjectileAudioTestTarget target = expected == SoundId.FleshImpact ? wall.AddComponent<ProjectileAudioTestTarget>() : null;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AttackOnTroy/Combat/Prefabs/Projectiles/" + prefabName + ".prefab");
                Check(prefab != null, label + " real prefab exists");
                GameObject clone = Object.Instantiate(prefab, origin, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(clone, probeScene);
                foreach (Renderer renderer in clone.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                Projectile2D projectile = clone.GetComponent<Projectile2D>();
                int launchEvents = 0, impactEvents = 0;
                AudioSource impactSource = null;
                SoundId launchId = projectile.ImpactType == ProjectileImpactType.Spear ? SoundId.SpearLaunch : SoundId.ArrowLaunch;
                observer = id =>
                {
                    AudioSource ownedSource = FindVoice(audio, id, projectile);
                    if (ownedSource == null) return;
                    if (id == launchId) launchEvents++;
                    if (id == expected) { impactEvents++; impactSource = ownedSource; }
                };
                audio.SoundPlayed += observer;
                projectile.Launch(Vector2.right * 12f, null, 0f);
                for (int step = 0; step < 60 && impactEvents == 0; step++)
                {
                    physics.Simulate(.02f);
                    if (impactEvents == 0) yield return .02;
                }
                Check(launchEvents == 1 && impactEvents == 1, label + " actual physics dispatches exactly one launch and impact");
                Check(projectile.IsResolving || projectile.IsCompleted, label + " collision resolution reached");
                ProjectileCompletionReason reason = expected == SoundId.FleshImpact ? ProjectileCompletionReason.ActorImpact :
                    expected == SoundId.GroundImpact ? ProjectileCompletionReason.TerrainImpact : ProjectileCompletionReason.OtherImpact;
                Check(projectile.LastCompletionReason == reason, label + " expected collision reason");
                if (target != null) Check(target.HitCount == 1, label + " dummy receives one hit; no real actor damaged");
                Check(impactSource != null && impactSource.isPlaying, label + " impact AudioSource playing");
                Check(impactSource.outputAudioMixerGroup == audio.Library.combatGroup, label + " routes to Combat mixer");
                float expectedVolume = audio.Library.sounds.Single(s => s.id == expected).volume;
                Check(Mathf.Abs(impactSource.volume - expectedVolume) < .001f, label + " boosted cue volume " + expectedVolume);
                float[] buffer = new float[1024];
                float sourcePeak = 0, mixedPeak = 0;
                for (int frame = 0; frame < 12; frame++)
                {
                    impactSource.GetOutputData(buffer, 0);
                    foreach (float sample in buffer) sourcePeak = Mathf.Max(sourcePeak, Mathf.Abs(sample));
                    AudioListener.GetOutputData(buffer, 0);
                    foreach (float sample in buffer) mixedPeak = Mathf.Max(mixedPeak, Mathf.Abs(sample));
                    yield return .01;
                }
                Check(sourcePeak > .005f, label + " actual impact source output peak " + sourcePeak.ToString("F6"));
                Check(mixedPeak > .005f && mixedPeak < .99f, label + " live mixer output below clipping, peak " + mixedPeak.ToString("F6"));
                if (expected != SoundId.GroundImpact)
                    Check(clone == null && sourcePeak > .005f, label + " audio survives projectile destruction");
                Check(impactEvents == 1, label + " no duplicate impact while resolving");
                audio.SoundPlayed -= observer; observer = null;
                if (clone != null) Object.Destroy(clone);
                Object.Destroy(wall);
                yield return .65;
            }
            Check(SceneManager.GetActiveScene() == originalScene, "Battle scene still active after all six collisions");
            results.Add("Result: PASSED");
        }

        private static void Finish()
        {
            if (audio != null && observer != null) audio.SoundPlayed -= observer;
            observer = null;
            (routine as IDisposable)?.Dispose(); routine = null;
            if (probeScene.IsValid() && probeScene.isLoaded) SceneManager.UnloadSceneAsync(probeScene);
            Directory.CreateDirectory("Logs/AudioTests");
            File.WriteAllLines("Logs/AudioTests/projectile-impact.txt", results);
            AudioEditorControl.WriteStatus(failed ? "impact-failed" : "impact-verified", "Projectile collision audio " + (failed ? "FAILED" : "PASSED"));
            if (enteredPlay) EditorApplication.ExitPlaymode();
            Debug.Log("[Audio] Projectile impact test " + (failed ? "FAILED" : "PASSED") + ". See Logs/AudioTests/projectile-impact.txt");
        }
    }
}
