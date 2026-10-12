using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages player energy (0 to 100) and the Special DOUBLE-SHOT ability in Level 01.
/// Energy is gained exclusively from actual HP lost from enemy attacks (not self-damage,
/// healing, or after death). When full (100) during PlayerTurn, the Special button
/// arms the next attack to fire two simultaneous projectiles.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerEnergySystem : MonoBehaviour
{
    [Header("Energy Configuration")]
    [Header("Energy Configuration")]
    [SerializeField, Range(10f, 200f)] private float maxEnergy = 100f;
    [Tooltip("Hits required from enemy attacks to fully charge energy.")]
    [SerializeField, Range(1, 5)] private int hitsRequiredForFullEnergy = 2;

    [Header("Current State")]
    [SerializeField] private float currentEnergy = 0f;
    [SerializeField] private int hitsReceived = 0;
    [SerializeField] private bool isSpecialArmed = false;

    [Header("Actor & Scene References")]
    [SerializeField] private ActorDamageReceiver2D playerDamageReceiver;
    [SerializeField] private AchillesProjectileDebugLauncher playerLauncher;
    [SerializeField] private Level01TurnManager turnManager;
    [SerializeField] private Transform cyclopsTransform;

    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    public int HitsReceived => hitsReceived;
    public float Energy01 => maxEnergy > 0f ? Mathf.Clamp01(currentEnergy / maxEnergy) : 0f;
    public bool IsSpecialArmed => isSpecialArmed;
    public bool IsEnergyFull => hitsReceived >= hitsRequiredForFullEnergy || currentEnergy >= maxEnergy - 0.001f;

    public bool CanActivateSpecial
    {
        get
        {
            if (isSpecialArmed) return true; // Can cancel while armed
            if (!IsEnergyFull) return false;
            if (turnManager != null && !turnManager.IsPlayerTurn) return false;
            if (playerLauncher != null && !playerLauncher.CanAim) return false;
            return true;
        }
    }

    public event Action<float, float> EnergyChanged;
    public event Action<bool> SpecialArmedChanged;

    private void Awake()
    {
        currentEnergy = 0f;
        hitsReceived = 0;
        isSpecialArmed = false;
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        EnergyChanged?.Invoke(currentEnergy, maxEnergy);
        SpecialArmedChanged?.Invoke(isSpecialArmed);
    }

    private void OnEnable()
    {
        SubscribeDamageEvents();
    }

    private void OnDisable()
    {
        UnsubscribeDamageEvents();
    }

    private void Update()
    {
        // Keyboard shortcuts for PC / testing
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.eKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame)
        {
            ToggleSpecial();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Debug hotkey: Press X to instantly charge energy to 100 (2 hits)
        if (keyboard.xKey.wasPressedThisFrame)
        {
            hitsReceived = hitsRequiredForFullEnergy;
            currentEnergy = maxEnergy;
            EnergyChanged?.Invoke(currentEnergy, maxEnergy);
            Debug.Log("[PlayerEnergySystem] Debug cheat: Energy set to 100 (2 hits)!");
        }
#endif
    }

    public void ResolveReferences()
    {
        if (turnManager == null)
            turnManager = FindAnyObjectByType<Level01TurnManager>();

        if (playerLauncher == null)
            playerLauncher = FindAnyObjectByType<AchillesProjectileDebugLauncher>();

        if (playerDamageReceiver == null && playerLauncher != null)
            playerDamageReceiver = playerLauncher.GetComponent<ActorDamageReceiver2D>();

        if (cyclopsTransform == null)
        {
            var cyclops = GameObject.Find("Enemy_CyclopsNormal");
            if (cyclops != null)
                cyclopsTransform = cyclops.transform;
        }

        if (playerLauncher != null)
            playerLauncher.SetEnergySystem(this);
    }

    public void SetPlayer(ActorDamageReceiver2D damage, AchillesProjectileDebugLauncher launcher)
    {
        UnsubscribeDamageEvents();

        playerDamageReceiver = damage;
        playerLauncher = launcher;

        if (playerLauncher != null)
            playerLauncher.SetEnergySystem(this);

        SubscribeDamageEvents();
    }

    public void SetTurnManager(Level01TurnManager turnMgr)
    {
        turnManager = turnMgr;
    }

    public void SetCyclopsTransform(Transform cyclops)
    {
        cyclopsTransform = cyclops;
    }

    private void SubscribeDamageEvents()
    {
        if (playerDamageReceiver != null)
        {
            playerDamageReceiver.DamageApplied -= OnPlayerDamageApplied;
            playerDamageReceiver.DamageApplied += OnPlayerDamageApplied;
        }
    }

    private void UnsubscribeDamageEvents()
    {
        if (playerDamageReceiver != null)
        {
            playerDamageReceiver.DamageApplied -= OnPlayerDamageApplied;
        }
    }

    private void OnPlayerDamageApplied(float actualHpLost, Vector2 hitPoint, GameObject source)
    {
        if (actualHpLost <= 0f) return;
        if (playerDamageReceiver != null && playerDamageReceiver.IsDead) return;

        if (!IsEnemyAxeDamageSource(source))
        {
            return;
        }

        // Each hit that inflicts actual HP loss increases hit count by 1 up to 2
        if (hitsReceived < hitsRequiredForFullEnergy)
        {
            hitsReceived++;
            currentEnergy = (float)hitsReceived / hitsRequiredForFullEnergy * maxEnergy;
            EnergyChanged?.Invoke(currentEnergy, maxEnergy);
            Debug.Log($"[PlayerEnergySystem] Enemy axe hit {hitsReceived}/{hitsRequiredForFullEnergy} received (Damage: {actualHpLost:F1}). Energy: {currentEnergy:F0}/{maxEnergy:F0}");
        }
    }

    private bool IsEnemyAxeDamageSource(GameObject source)
    {
        if (source == null) return false;

        // FirePatch2D is DoT / ground fire -> not counted
        if (source.GetComponent<FirePatch2D>() != null || source.GetComponentInParent<FirePatch2D>() != null)
            return false;

        // Check Projectile2D: only enemy StoneAxe counts as a hit
        Projectile2D proj = source.GetComponent<Projectile2D>();
        if (proj != null)
        {
            if (proj.ImpactType == ProjectileImpactType.StoneAxe)
                return true;

            // Player projectiles (Spear, Arrow, Bomb, OilJar) do not count
            return false;
        }

        Transform playerTr = playerDamageReceiver != null ? playerDamageReceiver.transform : null;
        if (playerTr != null && (source == playerTr.gameObject || source.transform.IsChildOf(playerTr)))
            return false;

        if (cyclopsTransform != null && (source == cyclopsTransform.gameObject || source.transform.IsChildOf(cyclopsTransform)))
            return true;

        if (source.GetComponentInParent<CyclopsNormalTurnController>() != null)
            return true;

        return false;
    }

    public void AddEnergy(float amount)
    {
        // Retained for API compatibility: translates into energy
        if (amount <= 0f || isSpecialArmed) return;

        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
        hitsReceived = Mathf.RoundToInt(currentEnergy / (maxEnergy / hitsRequiredForFullEnergy));
        EnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    public void SetEnergy(float amount)
    {
        currentEnergy = Mathf.Clamp(amount, 0f, maxEnergy);
        hitsReceived = Mathf.RoundToInt(currentEnergy / (maxEnergy / hitsRequiredForFullEnergy));
        EnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    public bool ArmSpecial()
    {
        if (!CanActivateSpecial || isSpecialArmed) return false;

        isSpecialArmed = true;

        if (playerLauncher != null)
            playerLauncher.SetDoubleShotArmed(true);

        SpecialArmedChanged?.Invoke(true);
        Debug.Log("[PlayerEnergySystem] Special DOUBLE-SHOT armed! Next attack will launch two projectiles.");
        return true;
    }

    public bool CancelSpecial()
    {
        if (!isSpecialArmed) return false;

        isSpecialArmed = false;

        if (playerLauncher != null)
            playerLauncher.SetDoubleShotArmed(false);

        SpecialArmedChanged?.Invoke(false);
        Debug.Log("[PlayerEnergySystem] Special DOUBLE-SHOT cancelled. Energy retained.");
        return true;
    }

    public void ToggleSpecial()
    {
        if (isSpecialArmed)
        {
            CancelSpecial();
        }
        else if (CanActivateSpecial)
        {
            ArmSpecial();
        }
    }

    /// <summary>
    /// Called when the armed double-shot attack is launched.
    /// Resets hit count and energy back to 0.
    /// </summary>
    public void ConsumeSpecialOnLaunch()
    {
        if (!isSpecialArmed) return;

        isSpecialArmed = false;
        hitsReceived = 0;
        currentEnergy = 0f;

        EnergyChanged?.Invoke(currentEnergy, maxEnergy);
        SpecialArmedChanged?.Invoke(false);
        Debug.Log("[PlayerEnergySystem] Special DOUBLE-SHOT consumed on launch. Energy and hits reset to 0.");
    }

    public void ResetEnergy()
    {
        hitsReceived = 0;
        currentEnergy = 0f;
        isSpecialArmed = false;

        if (playerLauncher != null)
            playerLauncher.SetDoubleShotArmed(false);

        EnergyChanged?.Invoke(currentEnergy, maxEnergy);
        SpecialArmedChanged?.Invoke(false);
    }
}
