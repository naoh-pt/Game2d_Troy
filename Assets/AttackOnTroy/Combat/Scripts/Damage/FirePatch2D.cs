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
    [SerializeField, Min(0.5f)] private float duration = 4.0f;

    [Header("Damage")]
    [SerializeField, Min(0.1f)] private float damageInterval = 0.5f;
    [SerializeField, Min(0f)] private float damagePerTick = 8f;
    [SerializeField, Min(0.1f)] private float damageRadius = 1.6f;
    [SerializeField] private LayerMask damageLayers = ~0;

    [Header("Visual Animation")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] animationFrames;
    [SerializeField, Min(1f)] private float animationFps = 12f;

    private float lifetimeTimer;
    private float tickTimer;
    private float animationTimer;
    private int currentFrameIndex;

    private readonly Collider2D[] overlapBuffer = new Collider2D[32];
    private readonly HashSet<IDamageable2D> uniqueRecipients = new HashSet<IDamageable2D>();

    public float Duration => duration;
    public float DamageRadius => damageRadius;
    public float DamagePerTick => damagePerTick;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        lifetimeTimer = duration;
        tickTimer = 0f; // Immediate first damage tick on spawn
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

        foreach (IDamageable2D target in uniqueRecipients)
        {
            try
            {
                target.ApplyDamage(damagePerTick, transform.position, gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}
