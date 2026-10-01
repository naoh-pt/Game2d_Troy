using UnityEngine;

/// <summary>One receiver per actor root; child colliders resolve the same receiver.</summary>
public interface IDamageable2D
{
    void ApplyDamage(float amount, Vector2 hitPosition, GameObject source);
}
