using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AttackOnTroy.Audio
{
    /// <summary>Two independent audio buttons, styled from the existing Home/Pause UI.</summary>
    [DisallowMultipleComponent]
    public sealed class AudioSettingsControls : MonoBehaviour
    {
        [SerializeField] private Button musicButton;
        [SerializeField] private Button effectsButton;
        private AudioManager audio;

        public Button MusicButton => musicButton;
        public Button EffectsButton => effectsButton;

        public static AudioSettingsControls InstallHome(Button template)
        {
            Transform parent = template.transform.parent;
            AudioSettingsControls controls = GetOrAdd(parent);
            controls.musicButton = template;
            controls.effectsButton = FindOrClone(template, parent, "EffectsSoundButton");
            template.name = "MusicSoundButton";
            Layout(template, Vector2.one, new Vector2(-155, -60), new Vector2(250, 56), 22);
            Layout(controls.effectsButton, Vector2.one, new Vector2(-155, -126), new Vector2(250, 56), 22);
            controls.Bind();
            return controls;
        }

        public static AudioSettingsControls InstallPause(GameObject panel)
        {
            if (panel == null) return null;
            Transform parent = panel.transform.Find("DialogFrame");
            if (parent == null) parent = panel.transform;
            Transform resume = parent.Find("ResumeButton");
            if (resume == null || !resume.TryGetComponent(out Button template)) return null;
            AudioSettingsControls controls = GetOrAdd(parent);
            controls.musicButton = FindOrClone(template, parent, "MusicSoundButton");
            controls.effectsButton = FindOrClone(template, parent, "EffectsSoundButton");
            // Leave a separate audio row inside the frame, above its decorative lower border.
            RaisePauseNavigation(parent, "RestartButton", 97);
            RaisePauseNavigation(parent, "HomeButton", 101);
            Layout(controls.musicButton, new Vector2(.5f, 0), new Vector2(-92, 76), new Vector2(170, 36), 16);
            Layout(controls.effectsButton, new Vector2(.5f, 0), new Vector2(92, 76), new Vector2(170, 36), 16);
            StylePauseButton(controls.musicButton);
            StylePauseButton(controls.effectsButton);
            controls.Bind();
            return controls;
        }

        private static AudioSettingsControls GetOrAdd(Transform parent)
        {
            AudioSettingsControls controls = parent.GetComponent<AudioSettingsControls>();
            return controls != null ? controls : parent.gameObject.AddComponent<AudioSettingsControls>();
        }

        private static void RaisePauseNavigation(Transform parent, string name, float minimumY)
        {
            RectTransform rect = parent.Find(name) as RectTransform;
            if (rect != null)
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, Mathf.Max(rect.anchoredPosition.y, minimumY));
        }

        private static void StylePauseButton(Button button)
        {
            Image image = button.GetComponent<Image>();
            if (image == null) return;
            image.sprite = null;
            image.color = new Color(.08f,.065f,.05f,.94f);
            Outline border = button.GetComponent<Outline>();
            if (border == null) border = button.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(.65f,.45f,.18f,.9f);
            border.effectDistance = new Vector2(1,-1);
        }

        private static Button FindOrClone(Button template, Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<Button>();
            Button button = Instantiate(template, parent, false);
            button.name = name;
            // A cloned Resume button must never resume/restart/navigate when used for audio.
            button.onClick = new Button.ButtonClickedEvent();
            return button;
        }

        private static void Layout(Button button, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
        {
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null) return;
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }

        private void OnEnable() { Bind(); }
        private void OnDisable() { Unbind(); }

        private void Bind()
        {
            if (musicButton == null || effectsButton == null) return;
            musicButton.onClick.RemoveListener(ToggleMusic);
            musicButton.onClick.AddListener(ToggleMusic);
            effectsButton.onClick.RemoveListener(ToggleEffects);
            effectsButton.onClick.AddListener(ToggleEffects);
            Unbind();
            if (!Application.isPlaying) { Refresh(); return; }
            audio = AudioManager.EnsureInstance();
            if (isActiveAndEnabled) audio.OnAudioSettingsChanged += Refresh;
            Refresh();
        }

        private void Unbind()
        {
            if (audio != null) audio.OnAudioSettingsChanged -= Refresh;
        }

        private void ToggleMusic()
        {
            audio = AudioManager.EnsureInstance();
            audio.ToggleMusic();
            audio.PlaySFX(SoundId.UIClick, this);
        }

        private void ToggleEffects()
        {
            audio = AudioManager.EnsureInstance();
            audio.ToggleEffects();
            if (!audio.IsEffectsMuted) audio.PlaySFX(SoundId.UIClick, this);
        }

        public void Refresh()
        {
            int legacy = PlayerPrefs.GetInt("AttackOnTroy_SoundMuted", 0);
            SetLabel(musicButton, "MUSIC", audio != null ? audio.IsMusicMuted : PlayerPrefs.GetInt("AttackOnTroy_MusicMuted", legacy) == 1);
            SetLabel(effectsButton, "SFX", audio != null ? audio.IsEffectsMuted : PlayerPrefs.GetInt("AttackOnTroy_EffectsMuted", legacy) == 1);
        }

        private static void SetLabel(Button button, string channel, bool muted)
        {
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = channel + ": <color=" + (muted ? "#D98E79>OFF" : "#9DD487>ON") + "</color>";
        }
    }
}
