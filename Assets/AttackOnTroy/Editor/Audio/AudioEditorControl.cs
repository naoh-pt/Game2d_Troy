using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AttackOnTroy.Editor.Audio
{
    /// <summary>Only accepts local, audio-specific commands. No network or arbitrary code execution.</summary>
    [InitializeOnLoad]
    public static class AudioEditorControl
    {
        private const string RequestPath = "Temp/AudioImport/editor-request.txt";
        private static double nextPoll;
        static AudioEditorControl() { EditorApplication.update += Poll; }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            if (!File.Exists(RequestPath)) return;
            string command = File.ReadAllText(RequestPath).Trim();
            File.Delete(RequestPath);
            try
            {
                if (command == "configure")
                {
                    WriteStatus("configuring", "Importing sound clips and mixer routes.");
                    SoundLibrarySetup.Configure();
                    WriteStatus("configured", "Sound library imported and validated.");
                }
                else if (command == "test-audio") AudioSmokeTests.Run();
                else if (command == "check-impacts") ProjectileImpactAudioTests.Run();
                else if (command == "remove-credits-ui")
                {
                    // Remove the old runtime canvas from an already-open Play Mode session.
                    if (Application.isPlaying)
                        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (canvas.gameObject.name == "AudioCreditsCanvas")
                                UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    WriteStatus("credits-ui-removed", "Audio credits UI removed; source/license files retained.");
                }
                else if (command == "apply-volume")
                {
                    SoundLibrarySetup.ApplyAndVerifyVolume();
                    WriteStatus("volume-verified", "Current audio volume balance applied and verified.");
                }
                else WriteStatus("error", "Unknown audio command.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                WriteStatus("error", exception.ToString());
            }
        }

        [Serializable] private sealed class Status { public string state, message; }
        public static void WriteStatus(string state, string message)
        {
            Directory.CreateDirectory("Temp/AudioImport");
            File.WriteAllText("Temp/AudioImport/editor-status.json", JsonUtility.ToJson(new Status { state = state, message = message },true));
        }
    }
}
