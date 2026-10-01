using System;
using UnityEngine;

public enum ProjectileCompletionReason
{
    TerrainImpact,
    Timeout,
    OutOfBounds
}

/// <summary>
/// Controls 2D ballistic projectile flight, horizontal wind influence, visual orientation,
/// bounds and lifetime tracking, and circular destructible terrain impact.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public sealed class Projectile2D : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform visualTransform;

    [Header("Flight & Lifetime")]
    [SerializeField, Min(1f)] private float maxLifetime = 10f;
    [SerializeField] private Collider2D levelBoundsCollider;

    [Header("Terrain Destruction")]
    [SerializeField, Min(0.1f)] private float craterRadius = 1.0f;
    [SerializeField] private DestructibleTerrain2D terrainTarget;

    private Rigidbody2D body;
    private CapsuleCollider2D projectileCollider;
    private float horizontalWind;
    private float lifetimeTimer;
    private bool isLaunched;
    private bool isCompleted;
    private Bounds cachedBounds;
    private bool hasCachedBounds;
    private Action<Projectile2D> onCompletedCallback;

    public bool IsLaunched => isLaunched;
    public bool IsCompleted => isCompleted;
    public ProjectileCompletionReason LastCompletionReason { get; private set; }

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnValidate()
    {
        maxLifetime = Mathf.Max(1f, maxLifetime);
        craterRadius = Mathf.Max(0.1f, craterRadius);
    }

    private void InitializeComponents()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (projectileCollider == null)
            projectileCollider = GetComponent<CapsuleCollider2D>();
        if (visualTransform == null)
        {
            Transform child = transform.Find("Visual");
            visualTransform = child != null ? child : transform;
        }

        if (levelBoundsCollider != null && !hasCachedBounds)
        {
            cachedBounds = levelBoundsCollider.bounds;
            hasCachedBounds = true;
        }
    }

    /// <summary>
    /// Launches the projectile with the specified velocity, ignores collision with the owner,
    /// and configures wind and completion callbacks.
    /// </summary>
    public void Launch(
        Vector2 initialVelocity,
        Collider2D ownerCollider,
        float windAcceleration,
        Collider2D boundsCollider = null,
        DestructibleTerrain2D terrain = null,
        Action<Projectile2D> onCompleted = null)
    {
        InitializeComponents();

        horizontalWind = windAcceleration;
        lifetimeTimer = maxLifetime;
        onCompletedCallback = onCompleted;

        if (boundsCollider != null)
        {
            levelBoundsCollider = boundsCollider;
            cachedBounds = boundsCollider.bounds;
            hasCachedBounds = true;
        }

        if (terrain != null)
            terrainTarget = terrain;

        // Ignore collision with owner and any of owner's child colliders
        if (ownerCollider != null && projectileCollider != null)
        {
            Collider2D[] ownerColliders = ownerCollider.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < ownerColliders.Length; i++)
            {
                if (ownerColliders[i] != null)
                    Physics2D.IgnoreCollision(projectileCollider, ownerColliders[i], true);
            }
        }

        body.linearVelocity = initialVelocity;
        UpdateFlightOrientation();
        isLaunched = true;
    }

    private void FixedUpdate()
    {
        if (!isLaunched || isCompleted)
            return;

        if (Mathf.Abs(horizontalWind) > 0.0001f)
        {
            body.linearVelocityX += horizontalWind * Time.fixedDeltaTime;
        }
    }

    private void Update()
    {
        if (!isLaunched || isCompleted)
            return;

        UpdateFlightOrientation();
        CheckLifetime();
        CheckBounds();
    }

    private void UpdateFlightOrientation()
    {
        Vector2 vel = body.linearVelocity;
        if (vel.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
            if (visualTransform != null)
                visualTransform.rotation = Quaternion.Euler(0f, 0f, angle);

            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            body.rotation = angle;
        }
    }

    private void CheckLifetime()
    {
        lifetimeTimer -= Time.deltaTime;
        if (lifetimeTimer <= 0f)
        {
            Complete(ProjectileCompletionReason.Timeout);
        }
    }

    private void CheckBounds()
    {
        if (!hasCachedBounds && levelBoundsCollider != null)
        {
            cachedBounds = levelBoundsCollider.bounds;
            hasCachedBounds = true;
        }

        if (hasCachedBounds)
        {
            Vector3 pos = transform.position;
            bool outside = pos.x < cachedBounds.min.x ||
                           pos.x > cachedBounds.max.x ||
                           pos.y < cachedBounds.min.y ||
                           pos.y > (cachedBounds.max.y + 15f);
            if (outside)
            {
                Complete(ProjectileCompletionReason.OutOfBounds);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isLaunched || isCompleted)
            return;

        DestructibleTerrain2D terrain = collision.collider.GetComponentInParent<DestructibleTerrain2D>();
        if (terrain == null && terrainTarget != null)
        {
            if (collision.gameObject.layer == terrainTarget.gameObject.layer ||
                collision.collider.transform.IsChildOf(terrainTarget.transform) ||
                string.Equals(collision.collider.name, "RuntimeTerrainColliders", StringComparison.Ordinal))
            {
                terrain = terrainTarget;
            }
        }

        if (terrain != null)
        {
            Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            terrain.DestroyCircle(impactPoint, craterRadius);
            Complete(ProjectileCompletionReason.TerrainImpact);
            return;
        }

        int terrainLayer = LayerMask.NameToLayer("Terrain");
        if (terrainLayer >= 0 && collision.gameObject.layer == terrainLayer)
        {
            if (terrainTarget != null)
            {
                Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
                terrainTarget.DestroyCircle(impactPoint, craterRadius);
            }
            Complete(ProjectileCompletionReason.TerrainImpact);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!isLaunched || isCompleted)
            return;

        if (levelBoundsCollider != null && other == levelBoundsCollider)
        {
            Vector3 pos = transform.position;
            if (pos.x < cachedBounds.min.x || pos.x > cachedBounds.max.x || pos.y < cachedBounds.min.y)
            {
                Complete(ProjectileCompletionReason.OutOfBounds);
            }
        }
    }

    private void Complete(ProjectileCompletionReason reason)
    {
        if (isCompleted)
            return;

        isCompleted = true;
        LastCompletionReason = reason;

        try
        {
            onCompletedCallback?.Invoke(this);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        Destroy(gameObject);
    }
}
