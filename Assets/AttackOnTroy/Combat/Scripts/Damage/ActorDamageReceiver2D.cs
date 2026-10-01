using System;
using UnityEngine;

/// <summary>Prototype health receiver. Does not change physics, animation or turn/death rules.</summary>
[DisallowMultipleComponent]
public sealed class ActorDamageReceiver2D : MonoBehaviour, IDamageable2D
{
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    [SerializeField] private bool logDamage;
    public float CurrentHealth { get; private set; }
    public float MaximumHealth => maximumHealth;
    public event Action<float, Vector2, GameObject> DamageApplied;

    private void Awake() => CurrentHealth = maximumHealth;

    public void ApplyDamage(float amount, Vector2 hitPosition, GameObject source)
    {
        if (!isActiveAndEnabled || amount <= 0f || CurrentHealth <= 0f) return;
        float applied = Mathf.Min(CurrentHealth, amount);
        CurrentHealth -= applied;
        if (logDamage) Debug.Log($"[Damage] {name}: -{applied:F1}, HP {CurrentHealth:F1}/{maximumHealth:F1}", this);
        DamageApplied?.Invoke(applied, hitPosition, source);
    }
}
