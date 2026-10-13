using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AttackOnTroy.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class ButtonAudioFeedback : MonoBehaviour, IPointerUpHandler, ISubmitHandler
    {
        private Button button;
        private void Awake() { button = GetComponent<Button>(); }
        public void OnPointerUp(PointerEventData data)
        {
            // Pointer-up precedes Button.onClick, which may synchronously load a new scene.
            if (data.button == PointerEventData.InputButton.Left && data.eligibleForClick &&
                RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform,data.position,data.pressEventCamera)) Play();
        }
        public void OnSubmit(BaseEventData data) { Play(); }

        private void Play()
        {
            if (button == null || !button.enabled || AudioManager.Instance == null) return;
            string key = name.ToLowerInvariant();
            // These actions already have specific audio feedback, including mute.
            if (key.Contains("sound") || key.Contains("throw") || key.Contains("attack") ||
                key.Contains("special") || key.Contains("spear") || key.Contains("bomb") ||
                key.Contains("arrow") || key.Contains("oiljar")) return;
            SoundId id = !button.interactable ? SoundId.UIUnavailable :
                key.Contains("back") || key.Contains("close") || key.Contains("cancel") ? SoundId.UIBack :
                key.Contains("play") || key.Contains("start") || key.Contains("confirm") ? SoundId.UIConfirm : SoundId.UIClick;
            if (key.Contains("level02") && !BattleSessionData.IsLevelUnlocked(2) ||
                key.Contains("level03") && !BattleSessionData.IsLevelUnlocked(3)) id = SoundId.UIUnavailable;
            AudioManager.Instance.PlaySFX(id, this);
        }
    }
}
