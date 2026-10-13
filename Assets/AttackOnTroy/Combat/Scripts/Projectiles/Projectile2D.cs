using System;
using System.Collections.Generic;
using UnityEngine;

public enum ProjectileCompletionReason
{
    TerrainImpact,
    Timeout,
    OutOfBounds,
    ActorImpact,
    OtherImpact,
    Cancelled
}

public enum ProjectileImpactType { Spear, ClubBomb, StoneAxe, Arrow, OilJar }

/// <summary>Shared flight/lifecycle; only ClubBomb's impact path can carve terrain.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class Projectile2D : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform visualTransform;
    [SerializeField] private ProjectileImpactType impactType;
    [SerializeField] private float bombVisualDegreesPerSecond = 360f;
    [SerializeField] private float axeVisualDegreesPerSecond = 720f;
    [SerializeField] private float jarVisualDegreesPerSecond = 360f;

    [Header("Flight & Lifetime")]
    [SerializeField, Min(1f)] private float maxLifetime = 10f;
    [SerializeField] private Collider2D levelBoundsCollider;

    [Header("Direct Hit Impact (Spear & Arrow)")]
    [SerializeField, Min(0f)] private float directHitDamage = 25f;
    [SerializeField, Min(0f)] private float embeddedDuration = 0.8f;
    [SerializeField, Min(0f)] private float embedDepth = 0.08f;

    [Header("Club Bomb Impact")]
    [SerializeField, Min(0f)] private float areaDamage = 30f;
    [SerializeField, Min(0.1f)] private float damageRadius = 2f;
    [SerializeField] private LayerMask damageLayers = ~0;
    [SerializeField, Min(0.1f)] private float craterRadius = 1f;
    [SerializeField] private DestructibleTerrain2D terrainTarget;
    [SerializeField, Min(0.05f)] private float explosionResolveDuration = 0.45f;
    [SerializeField] private ProjectileExplosionEffect explosionEffectPrefab;

    [Header("Oil Jar Impact")]
    [SerializeField] private GameObject firePatchPrefab;
    [Tooltip("Allow oil jar to carve a configurable small crater on wall terrain (e.g. Level 03 Troy Wall) on initial impact.")]
    [SerializeField] private bool oilJarWallCarveEnabled = false;
    [SerializeField, Min(0.1f)] private float oilJarCraterRadius = 0.8f;

    [Header("Wind Sensitivity")]
    [Tooltip("Multiplier applied to wind acceleration. Spear=0.75, Bomb=1.0, Arrow=0.0, OilJar=1.0, StoneAxe=0.8.")]
    [SerializeField, Range(-1f, 2f)] private float windSensitivity = -1f;

    [Header("Terrain Destruction")]
    [SerializeField] private bool canCarveTerrain = false;

    private Rigidbody2D body;
    private Collider2D projectileCollider;
    private SpriteRenderer visualRenderer;
    private float horizontalWind;
    private float lifetimeTimer;
    private float resolutionTimer;
    private float visualSpinAngle;
    private Quaternion initialVisualRotation;
    private Vector2 flightDirection = Vector2.right;
    private bool isLaunched;
    private bool isCompleted;
    private bool isResolving;
    private Bounds cachedBounds;
    private bool hasCachedBounds;
    private Action<Projectile2D> onCompletedCallback;
    private ProjectileExplosionEffect activeExplosionEffect;
    private readonly List<Collider2D> overlaps = new List<Collider2D>(32);
    private readonly HashSet<IDamageable2D> damageRecipients = new HashSet<IDamageable2D>();
    private readonly HashSet<DestructibleTerrain2D> affectedTerrains = new HashSet<DestructibleTerrain2D>();
    private readonly HashSet<Collider2D> ignoredColliders = new HashSet<Collider2D>();
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[8];

    public bool IsLaunched => isLaunched;
    public bool IsCompleted => isCompleted;
    public bool IsResolving => isResolving;
    public ProjectileImpactType ImpactType => impactType;
    public ProjectileCompletionReason LastCompletionReason { get; private set; }
    public Collider2D OwnerCollider { get; private set; }
    public bool OilJarWallCarveEnabled
    {
        get => oilJarWallCarveEnabled;
        set => oilJarWallCarveEnabled = value;
    }
    public float OilJarCraterRadius
    {
        get => oilJarCraterRadius;
        set => oilJarCraterRadius = Mathf.Max(0.1f, value);
    }
    public bool CanCarveTerrain
    {
        get => canCarveTerrain;
        set => canCarveTerrain = value;
    }
    public GameObject Owner => OwnerCollider != null ? OwnerCollider.gameObject : null;
    public float WindSensitivity
    {
        get => GetDefaultWindSensitivity();
        set => windSensitivity = value;
    }

    public float GetDefaultWindSensitivity()
    {
        if (windSensitivity >= 0f) return windSensitivity;
        switch (impactType)
        {
            case ProjectileImpactType.Spear: return 0.75f;
            case ProjectileImpactType.ClubBomb: return 1.0f;
            case ProjectileImpactType.Arrow: return 0.0f;
            case ProjectileImpactType.OilJar: return 1.0f;
            case ProjectileImpactType.StoneAxe: return 0.8f;
            default: return 1.0f;
        }
    }

    public void SetDirectHitDamage(float damage)
    {
        directHitDamage = Mathf.Max(0f, damage);
    }

    private void Awake() => InitializeComponents();

    private void OnValidate()
    {
        maxLifetime = Mathf.Max(1f, maxLifetime);
        craterRadius = Mathf.Max(0.1f, craterRadius);
        damageRadius = Mathf.Max(0.1f, damageRadius);
        embeddedDuration = Mathf.Max(0f, embeddedDuration);
        embedDepth = Mathf.Max(0f, embedDepth);
        explosionResolveDuration = Mathf.Max(0.05f, explosionResolveDuration);
        directHitDamage = Mathf.Max(0f, directHitDamage);
        areaDamage = Mathf.Max(0f, areaDamage);
    }

    private void InitializeComponents()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (projectileCollider == null) projectileCollider = GetComponent<Collider2D>();
        if (visualTransform == null)
        {
            Transform child = transform.Find("Visual");
            visualTransform = child != null ? child : transform;
        }
        if (visualRenderer == null && visualTransform != null)
            visualRenderer = visualTransform.GetComponentInChildren<SpriteRenderer>();
        if (levelBoundsCollider != null && !hasCachedBounds)
        {
            cachedBounds = levelBoundsCollider.bounds;
            hasCachedBounds = true;
        }
    }

    /// <summary>Existing launch API retained. One launch and one completion per instance.</summary>
    public void Launch(Vector2 initialVelocity, Collider2D ownerCollider, float windAcceleration,
        Collider2D boundsCollider = null, DestructibleTerrain2D terrain = null,
        Action<Projectile2D> onCompleted = null)
    {
        if (isLaunched || isCompleted) return;
        InitializeComponents();
        float sensitivity = GetDefaultWindSensitivity();
        horizontalWind = windAcceleration * sensitivity;
        lifetimeTimer = maxLifetime;
        onCompletedCallback = onCompleted;
        if (boundsCollider != null)
        {
            levelBoundsCollider = boundsCollider;
            cachedBounds = boundsCollider.bounds;
            hasCachedBounds = true;
        }
        if (terrain != null) terrainTarget = terrain;
        OwnerCollider = ownerCollider;
        if (ownerCollider != null)
        {
            Collider2D[] ownerColliders = ownerCollider.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < ownerColliders.Length; i++)
            {
                if (ownerColliders[i] != null)
                {
                    ignoredColliders.Add(ownerColliders[i]);
                    if (projectileCollider != null)
                        Physics2D.IgnoreCollision(projectileCollider, ownerColliders[i], true);
                }
            }
        }
        if (impactType == ProjectileImpactType.Arrow)
        {
            body.gravityScale = 0f;
            horizontalWind = 0f;
            flightDirection = initialVelocity.sqrMagnitude > 0.001f ? initialVelocity.normalized : Vector2.right;
        }
        else if (impactType == ProjectileImpactType.OilJar)
        {
            body.gravityScale = 1f;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            if (visualTransform == transform)
                Debug.LogError($"{impactType} requires a separate Visual child; root rotation will not be used.", this);
        }
        else if (impactType == ProjectileImpactType.ClubBomb || impactType == ProjectileImpactType.StoneAxe)
        {
            // Torque/contact forces must not rotate the physical projectile either.
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            if (visualTransform == transform)
                Debug.LogError($"{impactType} requires a separate Visual child; root rotation will not be used.", this);
        }
        initialVisualRotation = visualTransform != null ? visualTransform.localRotation : Quaternion.identity;
        body.linearVelocity = initialVelocity;
        isLaunched = true;
        UpdateFlightOrientation(0f);
        AttackOnTroy.Audio.ProjectileAudio.Launch(this);
    }

    private void FixedUpdate()
    {
        if (!isLaunched || isCompleted || isResolving) return;

        if (impactType == ProjectileImpactType.Arrow)
        {
            // Arrow maintains constant velocity: no wind, no gravity
            Vector2 moveDelta = body.linearVelocity * Time.fixedDeltaTime;
            float castDist = moveDelta.magnitude;
            if (castDist > 0.001f)
            {
                Vector2 castDir = moveDelta / castDist;
                ContactFilter2D filter = new ContactFilter2D();
                filter.SetLayerMask(~0);
                filter.useTriggers = false;
                int hitCount = Physics2D.Raycast(body.position, castDir, filter, sweepHits, castDist + 0.05f);
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit2D hit = sweepHits[i];
                    if (hit.collider == null || hit.collider == projectileCollider) continue;
                    if (ignoredColliders.Contains(hit.collider)) continue;
                    HandleCollisionImpact(hit.collider, hit.point);
                    return;
                }
            }
        }
        else
        {
            if (Mathf.Abs(horizontalWind) > 0.0001f)
                body.linearVelocityX += horizontalWind * Time.fixedDeltaTime;
        }
    }

    private void Update()
    {
        if (!isLaunched || isCompleted) return;
        if (isResolving)
        {
            resolutionTimer -= Time.deltaTime;
            if (resolutionTimer <= 0f && (activeExplosionEffect == null || !activeExplosionEffect.IsPlaying))
                Complete(LastCompletionReason);
            return;
        }
        UpdateFlightOrientation(Time.deltaTime);
        lifetimeTimer -= Time.deltaTime;
        if (lifetimeTimer <= 0f) { Complete(ProjectileCompletionReason.Timeout); return; }
        CheckBounds();
    }

    private void UpdateFlightOrientation(float deltaTime)
    {
        if (impactType == ProjectileImpactType.ClubBomb)
        {
            visualSpinAngle = Mathf.Repeat(visualSpinAngle + bombVisualDegreesPerSecond * deltaTime, 360f);
            if (visualTransform != null && visualTransform != transform)
                visualTransform.localRotation = initialVisualRotation * Quaternion.Euler(0f, 0f, visualSpinAngle);
            return; // No root transform or Rigidbody rotation for bomb visuals.
        }
        if (impactType == ProjectileImpactType.StoneAxe)
        {
            float spinDir = body.linearVelocityX >= 0f ? -1f : 1f;
            visualSpinAngle = Mathf.Repeat(visualSpinAngle + axeVisualDegreesPerSecond * spinDir * deltaTime, 360f);
            if (visualTransform != null && visualTransform != transform)
                visualTransform.localRotation = initialVisualRotation * Quaternion.Euler(0f, 0f, visualSpinAngle);
            return; // Visual spins end-over-end along flight direction.
        }
        if (impactType == ProjectileImpactType.OilJar)
        {
            float spinDir = body.linearVelocityX >= 0f ? -1f : 1f;
            visualSpinAngle = Mathf.Repeat(visualSpinAngle + jarVisualDegreesPerSecond * spinDir * deltaTime, 360f);
            if (visualTransform != null && visualTransform != transform)
                visualTransform.localRotation = initialVisualRotation * Quaternion.Euler(0f, 0f, visualSpinAngle);
            return; // Visual rotates without spinning physics root.
        }
        if (impactType == ProjectileImpactType.Arrow)
        {
            float arrowAngle = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg;
            if (visualTransform != null) visualTransform.rotation = Quaternion.Euler(0f, 0f, arrowAngle);
            transform.rotation = Quaternion.Euler(0f, 0f, arrowAngle);
            body.rotation = arrowAngle;
            return;
        }
        Vector2 velocity = body.linearVelocity;
        if (velocity.sqrMagnitude <= 0.001f) return;
        flightDirection = velocity.normalized;
        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        if (visualTransform != null) visualTransform.rotation = Quaternion.Euler(0f, 0f, angle);
        // Preserve the current spear's trajectory-aligned capsule/root behavior.
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        body.rotation = angle;
    }

    private bool OutsideBounds()
    {
        if (!hasCachedBounds) return false;
        Vector2 position = body.position;
        return position.x < cachedBounds.min.x || position.x > cachedBounds.max.x ||
            position.y < cachedBounds.min.y || position.y > cachedBounds.max.y + 15f;
    }

    private void CheckBounds()
    {
        if (OutsideBounds()) Complete(ProjectileCompletionReason.OutOfBounds);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isLaunched || isCompleted || isResolving) return;
        if (collision.collider != null && ignoredColliders.Contains(collision.collider)) return;
        Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : body.position;
        HandleCollisionImpact(collision.collider, point);
    }

    private void HandleCollisionImpact(Collider2D hitCollider, Vector2 point)
    {
        if (!isLaunched || isCompleted || isResolving) return;
        IDamageable2D recipient = hitCollider.GetComponentInParent<IDamageable2D>();
        DestructibleTerrain2D terrain = hitCollider.GetComponentInParent<DestructibleTerrain2D>();
        int terrainLayer = LayerMask.NameToLayer("Terrain");
        bool terrainHit = terrain != null || (terrainLayer >= 0 && hitCollider.gameObject.layer == terrainLayer);
        ProjectileCompletionReason reason = recipient != null ? ProjectileCompletionReason.ActorImpact :
            terrainHit ? ProjectileCompletionReason.TerrainImpact : ProjectileCompletionReason.OtherImpact;

        // Latch before damage, effect spawning or any callback can reenter.
        isResolving = true;
        LastCompletionReason = reason;
        StopFlight();
        AttackOnTroy.Audio.ProjectileAudio.Impact(this, recipient != null, terrainHit);

        if (impactType == ProjectileImpactType.ClubBomb)
        {
            ResolveBombImpact(point, (damageLayers.value & (1 << hitCollider.gameObject.layer)) != 0 ? recipient : null, terrain);
        }
        else if (impactType == ProjectileImpactType.OilJar)
        {
            if (recipient != null)
                ApplyDamageSafely(recipient, directHitDamage, point);

            bool isLevel03 = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Level03", StringComparison.OrdinalIgnoreCase) >= 0;
            if ((oilJarWallCarveEnabled || isLevel03) && terrainHit)
            {
                DestructibleTerrain2D targetToCarve = terrain != null ? terrain : terrainTarget;
                if (targetToCarve == null)
                    targetToCarve = FindAnyObjectByType<DestructibleTerrain2D>();
                if (targetToCarve != null)
                {
                    try { targetToCarve.DestroyCircle(point, oilJarCraterRadius); }
                    catch (Exception ex) { Debug.LogException(ex, this); }
                }
            }

            SpawnFirePatch(point);
            Complete(reason);
        }
        else if (recipient != null)
        {
            ApplyDamageSafely(recipient, directHitDamage, point);
            Complete(reason);
        }
        else if (terrainHit)
        {
            bool isLevel03 = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Level03", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isHeavyAxe = impactType == ProjectileImpactType.StoneAxe && (gameObject.name.IndexOf("Heavy", StringComparison.OrdinalIgnoreCase) >= 0 || canCarveTerrain);
            if (isLevel03 && isHeavyAxe)
            {
                DestructibleTerrain2D targetToCarve = terrain != null ? terrain : terrainTarget;
                if (targetToCarve == null)
                    targetToCarve = FindAnyObjectByType<DestructibleTerrain2D>();
                if (targetToCarve != null)
                {
                    try { targetToCarve.DestroyCircle(point, craterRadius > 0f ? craterRadius : 1.0f); }
                    catch (Exception ex) { Debug.LogException(ex, this); }
                }
            }

            if (impactType == ProjectileImpactType.Spear || impactType == ProjectileImpactType.Arrow)
                EmbedSpear(point);
            resolutionTimer = embeddedDuration;
            if (resolutionTimer <= 0f) Complete(reason);
        }
        else
        {
            Complete(reason);
        }
    }

    private void SpawnFirePatch(Vector2 point)
    {
        if (firePatchPrefab == null) return;
        try
        {
            GameObject patch = Instantiate(firePatchPrefab, point, Quaternion.identity);
            FirePatch2D fire = patch.GetComponent<FirePatch2D>();
            if (fire != null)
            {
                fire.Initialize(Owner);
                fire.SnapToGround();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void StopFlight()
    {
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        if (projectileCollider != null)
            projectileCollider.enabled = false;
        body.simulated = false;
    }

    private void EmbedSpear(Vector2 point)
    {
        // Align the authored sprite's forward tip without changing its sprite/scale/offset.
        // Only one stopped-body positioning operation; no Transform translation in flight.
        if (visualRenderer == null || visualRenderer.sprite == null) return;
        Bounds spriteBounds = visualRenderer.sprite.bounds;
        float x = visualRenderer.flipX ? spriteBounds.min.x : spriteBounds.max.x;
        Vector2 tip = visualRenderer.transform.TransformPoint(new Vector3(x, spriteBounds.center.y, 0f));
        body.position += point + flightDirection * embedDepth - tip;
    }

    private void ResolveBombImpact(Vector2 point, IDamageable2D directRecipient, DestructibleTerrain2D hitTerrain)
    {
        resolutionTimer = explosionResolveDuration;
        if (visualRenderer != null) visualRenderer.enabled = false;
        damageRecipients.Clear();
        affectedTerrains.Clear();
        if (directRecipient != null) damageRecipients.Add(directRecipient);
        if (hitTerrain != null) affectedTerrains.Add(hitTerrain);
        if (terrainTarget != null) affectedTerrains.Add(terrainTarget);

        // Gather a snapshot before DestroyCircle replaces runtime terrain colliders.
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(~0);
        filter.useTriggers = false;
        overlaps.Clear();
        Physics2D.OverlapCircle(point, Mathf.Max(damageRadius, craterRadius), filter, overlaps);
        for (int i = 0; i < overlaps.Count; i++)
        {
            Collider2D candidate = overlaps[i];
            if (candidate == null || candidate.attachedRigidbody == body) continue;
            DestructibleTerrain2D terrain = candidate.GetComponentInParent<DestructibleTerrain2D>();
            if (terrain != null) affectedTerrains.Add(terrain);
            if ((damageLayers.value & (1 << candidate.gameObject.layer)) == 0 ||
                ((Vector2)candidate.ClosestPoint(point) - point).sqrMagnitude > damageRadius * damageRadius) continue;
            IDamageable2D recipient = candidate.GetComponentInParent<IDamageable2D>();
            if (recipient != null) damageRecipients.Add(recipient);
        }
        foreach (IDamageable2D recipient in damageRecipients) ApplyDamageSafely(recipient, areaDamage, point);
        foreach (DestructibleTerrain2D terrain in affectedTerrains)
        {
            if (terrain == null) continue;
            try { terrain.DestroyCircle(point, craterRadius); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
        try
        {
            if (explosionEffectPrefab != null)
                activeExplosionEffect = Instantiate(explosionEffectPrefab, point, Quaternion.identity);
            else
            {
                GameObject fallback = new GameObject("RuntimeClubBombExplosion");
                fallback.transform.position = point;
                activeExplosionEffect = fallback.AddComponent<ProjectileExplosionEffect>();
            }
            activeExplosionEffect.Play(damageRadius, explosionResolveDuration);
        }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private void ApplyDamageSafely(IDamageable2D recipient, float amount, Vector2 point)
    {
        if (recipient == null || (recipient is UnityEngine.Object target && target == null)) return;
        GameObject source = Owner != null ? Owner : gameObject;
        try { recipient.ApplyDamage(amount, point, source); }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!isLaunched || isCompleted || isResolving || other != levelBoundsCollider) return;
        CheckBounds();
    }

    public void Cancel() => Complete(ProjectileCompletionReason.Cancelled);

    private void Complete(ProjectileCompletionReason reason)
    {
        if (isCompleted) return;
        isCompleted = true;
        LastCompletionReason = reason;
        if (body != null && projectileCollider != null) StopFlight();
        Action<Projectile2D> callback = onCompletedCallback;
        onCompletedCallback = null;
        try { callback?.Invoke(this); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (isLaunched && !isCompleted) Complete(ProjectileCompletionReason.Cancelled);
    }
}
