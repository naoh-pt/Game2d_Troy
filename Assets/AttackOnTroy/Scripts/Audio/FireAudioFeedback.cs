using UnityEngine;

namespace AttackOnTroy.Audio
{
    [DisallowMultipleComponent]
    public sealed class FireAudioFeedback : MonoBehaviour
    {
        private AudioLoopHandle handle;
        private void Start()
        {
            if (AudioManager.Instance == null) return;
            AudioManager.Instance.PlaySFX(SoundId.FireIgnite, this);
            handle = AudioManager.Instance.StartLoop(SoundId.FireLoop, this);
        }
        private void OnDisable()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.StopLoop(handle);
            handle = default;
        }
    }
}
