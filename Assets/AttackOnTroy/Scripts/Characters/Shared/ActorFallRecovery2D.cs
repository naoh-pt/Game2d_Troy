using UnityEngine;

/// <summary>Development-only fall reset that can later be replaced by death/game-over handling.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class ActorFallRecovery2D : MonoBehaviour
{
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private bool recoveryEnabled = true;
    [SerializeField] private float recoveryWorldY = -12f;

    private Vector2 spawnPosition;
    private float spawnRotation;
    private bool spawnRecorded;

    private void Awake()
    {
        body = body != null ? body : GetComponent<Rigidbody2D>();
        RecordSpawnPose();
    }

    private void FixedUpdate()
    {
#if !UNITY_EDITOR
        if (!Debug.isDebugBuild)
            return;
#endif
        if (!recoveryEnabled || body == null || !spawnRecorded || body.position.y >= recoveryWorldY)
            return;
        RecoverNow();
    }

    public void Configure(Rigidbody2D bodyReference, float worldYThreshold, bool applyThresholdDefault)
    {
        body = bodyReference;
        if (applyThresholdDefault)
            recoveryWorldY = worldYThreshold;
    }

    public void SetRecoveryEnabled(bool enabled)
    {
        recoveryEnabled = enabled;
    }

    public void SetRecoveryThreshold(float worldYThreshold)
    {
        recoveryWorldY = worldYThreshold;
    }

    public void RecordSpawnPose()
    {
        if (body == null)
            return;
        spawnPosition = body.position;
        spawnRotation = body.rotation;
        spawnRecorded = true;
    }

    public void RecoverNow()
    {
        if (body == null || !spawnRecorded)
            return;

        body.position = spawnPosition;
        body.rotation = spawnRotation;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.WakeUp();
    }
}
