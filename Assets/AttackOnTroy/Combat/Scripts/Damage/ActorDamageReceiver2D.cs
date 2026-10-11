using System;
using UnityEngine;

/// <summary>
/// Health receiver and damage responder.
/// Tracks current health, raises health and death events, and triggers
/// non-lethal Hurt or lethal Die animation feedback on attached/child Animator.
/// </summary>
[DisallowMultipleComponent]
public sealed class ActorDamageReceiver2D : MonoBehaviour, IDamageable2D
{
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    [Tooltip("Multiplier applied to incoming damage (e.g. 0.8 for 20% damage reduction, 1.0 for normal).")]
    [SerializeField, Min(0.1f)] private float damageMultiplier = 1.0f;
    [SerializeField] private bool logDamage;

    [Header("Animation Feedback")]
    [SerializeField] private Animator animator;
    [SerializeField] private string hurtTriggerName = "Hurt";
    [SerializeField] private string deathTriggerName = "Die";

    private int hurtTriggerHash;
    private int deathTriggerHash;
    private bool hasHurtTrigger;
    private bool hasDeathTrigger;

    public float CurrentHealth { get; private set; }
    public float MaximumHealth => maximumHealth;
    public float DamageMultiplier
    {
        get => damageMultiplier;
        set => damageMultiplier = Mathf.Max(0.01f, value);
    }
    public bool IsDead => CurrentHealth <= 0f;
    public Animator TargetAnimator => animator;

    public event Action<float, Vector2, GameObject> DamageApplied;
    public event Action<float, float> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        CurrentHealth = maximumHealth;
        InitializeAnimator();
    }

    public void ConfigureAnimator(Animator targetAnimator, string hurtTrigger = "Hurt", string deathTrigger = "Die")
    {
        animator = targetAnimator;
        hurtTriggerName = hurtTrigger;
        deathTriggerName = deathTrigger;
        InitializeAnimator();
    }

    private void InitializeAnimator()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        hasHurtTrigger = false;
        hasDeathTrigger = false;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            hurtTriggerHash = Animator.StringToHash(hurtTriggerName);
            deathTriggerHash = Animator.StringToHash(deathTriggerName);

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hurtTriggerHash && parameters[i].type == AnimatorControllerParameterType.Trigger)
                    hasHurtTrigger = true;
                else if (parameters[i].nameHash == deathTriggerHash && parameters[i].type == AnimatorControllerParameterType.Trigger)
                    hasDeathTrigger = true;
            }
        }
    }

    public void ApplyDamage(float amount, Vector2 hitPosition, GameObject source)
    {
        if (!isActiveAndEnabled || amount <= 0f || CurrentHealth <= 0f) return;
        float scaledAmount = amount * damageMultiplier;
        float applied = Mathf.Min(CurrentHealth, scaledAmount);
        CurrentHealth -= applied;
        if (logDamage) Debug.Log($"[Damage] {name}: -{applied:F1} (raw {amount:F1} * mult {damageMultiplier:F2}), HP {CurrentHealth:F1}/{maximumHealth:F1}", this);

        if (CurrentHealth <= 0f)
        {
            TriggerDeathAnimation();
            HealthChanged?.Invoke(CurrentHealth, maximumHealth);
            Died?.Invoke();
        }
        else
        {
            TriggerHurtAnimation();
            HealthChanged?.Invoke(CurrentHealth, maximumHealth);
        }

        DamageApplied?.Invoke(applied, hitPosition, source);
    }

    public void SetHealth(float health)
    {
        CurrentHealth = Mathf.Clamp(health, 0f, maximumHealth);
        if (CurrentHealth <= 0f)
        {
            TriggerDeathAnimation();
            HealthChanged?.Invoke(CurrentHealth, maximumHealth);
            Died?.Invoke();
        }
        else
        {
            HealthChanged?.Invoke(CurrentHealth, maximumHealth);
        }
    }

    public void TriggerHurtAnimation()
    {
        if (animator != null && hasHurtTrigger && animator.isActiveAndEnabled)
        {
            animator.SetTrigger(hurtTriggerHash);
        }
    }

    public void TriggerDeathAnimation()
    {
        if (animator != null && hasDeathTrigger && animator.isActiveAndEnabled)
        {
            if (hasHurtTrigger)
                animator.ResetTrigger(hurtTriggerHash);
            animator.SetTrigger(deathTriggerHash);
        }
    }
}
