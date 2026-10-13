#if UNITY_EDITOR
using UnityEngine;

namespace AttackOnTroy.Audio
{
    /// <summary>Editor audio probe only. Never included in a player build or attached to game actors.</summary>
    public sealed class ProjectileAudioTestTarget : MonoBehaviour, IDamageable2D
    {
        public int HitCount { get; private set; }
        public void ApplyDamage(float amount, Vector2 hitPosition, GameObject source) { HitCount++; }
    }
}
#endif
