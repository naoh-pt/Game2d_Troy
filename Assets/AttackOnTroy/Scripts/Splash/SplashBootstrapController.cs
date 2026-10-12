using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace AttackOnTroy.Splash
{
    /// <summary>
    /// Controls the initial bootstrap Splash screen.
    /// Displays for at least 3 seconds (real-time, independent of Time.timeScale),
    /// asynchronously preloads SCN_Home in the background, smoothly fades out,
    /// and activates Home only once both the 3s timer and async load are fully complete.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SplashBootstrapController : MonoBehaviour
    {
        [Header("Timing & Destination")]
        [Tooltip("Minimum real-time display duration in seconds (independent of Time.timeScale).")]
        [SerializeField] private float minimumDisplayDuration = 3f;

        [Tooltip("Name of the scene to load asynchronously.")]
        [SerializeField] private string targetSceneName = "SCN_Home";

        [Header("Visual Elements")]
        [SerializeField] private CanvasGroup splashCanvasGroup;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("Opening Sound (Optional)")]
        [Tooltip("Slot for future opening intro sting/sound effect.")]
        [SerializeField] private AudioClip openingIntroSound;

        private void Start()
        {
            Time.timeScale = 1f;

            // Optional opening sound slot
            if (openingIntroSound != null && Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlaySFX(openingIntroSound);
            }
            else if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlaySplashSting();
            }

            StartCoroutine(SplashSequenceRoutine());
        }

        private IEnumerator SplashSequenceRoutine()
        {
            // Start async load of target scene
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetSceneName);
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = false;
            }

            float elapsed = 0f;

            // Wait for both 3.0s unscaled time AND async load completion (progress reaches 0.9f)
            while (elapsed < minimumDisplayDuration || (asyncLoad != null && asyncLoad.progress < 0.9f))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Short fade out
            if (splashCanvasGroup != null && fadeDuration > 0.05f)
            {
                float fadeElapsed = 0f;
                while (fadeElapsed < fadeDuration)
                {
                    fadeElapsed += Time.unscaledDeltaTime;
                    splashCanvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeElapsed / fadeDuration);
                    yield return null;
                }
                splashCanvasGroup.alpha = 0f;
            }

            // Transition to target scene
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = true;
            }
            else
            {
                SceneManager.LoadScene(targetSceneName);
            }
        }
    }
}
