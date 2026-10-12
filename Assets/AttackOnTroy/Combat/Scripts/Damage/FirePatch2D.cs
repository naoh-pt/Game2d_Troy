using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground fire patch created on Oil Jar impact.
/// Burns for a configurable duration at its world position, damages actors
/// in its area at configurable intervals, and persists across turns without destroying terrain.
/// </summary>
[DisallowMultipleComponent]
public sealed class FirePatch2D : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField, Min(0.5f)] private float duration = 3.0f;

    [Header("Damage")]
    [SerializeField, Min(0.1f)] private float damageInterval = 1.0f;
    [SerializeField, Min(0f)] private float damagePerTick = 8f;
    [SerializeField, Min(0.1f)] private float damageRadius = 1.6f;
    [SerializeField] private LayerMask damageLayers = ~0;

    [Header("Visual Animation")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] animationFrames;
    [SerializeField, Min(1f)] private float animationFps = 12f;

    private static readonly Dictionary<IDamageable2D, float> targetLastFireDamageTime = new Dictionary<IDamageable2D, float>();

    private float lifetimeTimer;
    private float tickTimer;
    private float animationTimer;
    private int currentFrameIndex;
    private GameObject owner;

    private readonly Collider2D[] overlapBuffer = new Collider2D[32];
    private readonly HashSet<IDamageable2D> uniqueRecipients = new HashSet<IDamageable2D>();

    public float Duration => duration;
    public float DamageRadius => damageRadius;
    public float DamagePerTick => damagePerTick;
    public GameObject Owner => owner;

    public static void ResetFireThrottle()
    {
        targetLastFireDamageTime.Clear();
    }

    public void Initialize(GameObject patchOwner)
    {
        owner = patchOwner;
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        lifetimeTimer = duration;
        tickTimer = 0f; // Immediate first damage tick on spawn (t=0)
        SnapToGround();
    }

    /// <summary>
    /// Snaps the fire patch down onto the nearest terrain surface.
    /// </summary>
    public void SnapToGround()
    {
        int terrainLayer = LayerMask.NameToLayer("Terrain");
        int mask = terrainLayer >= 0 ? (1 << terrainLayer) : ~0;
        RaycastHit2D hit = Physics2D.Raycast(transform.position + Vector3.up * 0.5f, Vector2.down, 3.0f, mask);
        if (hit.collider != null)
        {
            transform.position = new Vector3(hit.point.x, hit.point.y + 0.15f, transform.position.z);
        }
    }

    private void Update()
    {
        // Lifetime countdown
        lifetimeTimer -= Time.deltaTime;
        if (lifetimeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // Flipbook sprite animation
        if (animationFrames != null && animationFrames.Length > 0 && spriteRenderer != null)
        {
            animationTimer += Time.deltaTime;
            currentFrameIndex = (int)(animationTimer * animationFps) % animationFrames.Length;
            spriteRenderer.sprite = animationFrames[currentFrameIndex];
        }

        // Damage ticking
        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = damageInterval;
            ApplyDamageTick();
        }
    }

    private void ApplyDamageTick()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(damageLayers);
        filter.useTriggers = true;

        int count = Physics2D.OverlapCircle(transform.position, damageRadius, filter, overlapBuffer);
        uniqueRecipients.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider2D col = overlapBuffer[i];
            if (col == null) continue;

            IDamageable2D recipient = col.GetComponentInParent<IDamageable2D>();
            if (recipient != null)
            {
                uniqueRecipients.Add(recipient);
            }
        }

        float now = Time.time;
        foreach (IDamageable2D target in uniqueRecipients)
        {
            if (target == null || (target is UnityEngine.Object obj && obj == null))
                continue;

            // Enforce minimum 1.0s window between fire ticks for any target to prevent overlapping patch damage stacking
            if (targetLastFireDamageTime.TryGetValue(target, out float lastTime))
            {
                if (now < lastTime + 0.95f)
                    continue;
            }

            targetLastFireDamageTime[target] = now;
            try
            {
                target.ApplyDamage(damagePerTick, transform.position, owner != null ? owner : gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }
    }

    private void OnDestroy()
    {
        // Cleanup destroyed targets
        var keysToRemove = new List<IDamageable2D>();
        foreach (var kvp in targetLastFireDamageTime)
        {
            if (kvp.Key == null || (kvp.Key is UnityEngine.Object obj && obj == null))
                keysToRemove.Add(kvp.Key);
        }
        for (int i = 0; i < keysToRemove.Count; i++)
            targetLastFireDamageTime.Remove(keysToRemove[i]);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}
