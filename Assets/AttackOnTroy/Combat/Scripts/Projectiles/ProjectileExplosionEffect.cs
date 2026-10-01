using UnityEngine;

/// <summary>Separate, non-physical sprite effect; visible expanding-ring fallback when art is absent.</summary>
[DisallowMultipleComponent]
public sealed class ProjectileExplosionEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Color fallbackColor = new Color(1f, 0.55f, 0.08f, 1f);
    private float duration;
    private float elapsed;
    private float radius;
    private float spriteScale;
    private bool playing;
    private LineRenderer ring;
    private Material ringMaterial;
    public bool IsPlaying => playing && isActiveAndEnabled;

    public void Play(float worldRadius, float seconds)
    {
        radius = Mathf.Max(0.1f, worldRadius);
        duration = Mathf.Max(0.05f, seconds);
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        if (visual != null && frames != null && frames.Length > 0)
        {
            float largest = 0.01f;
            foreach (Sprite sprite in frames)
                if (sprite != null) largest = Mathf.Max(largest, sprite.bounds.size.x, sprite.bounds.size.y);
            spriteScale = 2f * radius / largest;
        }
        else
        {
            if (visual != null) visual.enabled = false;
            ring = gameObject.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            ringMaterial = new Material(shader);
            ring.sharedMaterial = ringMaterial;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 32;
            ring.widthMultiplier = 0.14f;
            ring.sortingLayerName = "Projectile";
            ring.sortingOrder = 10;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2f / 32f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }
        }
        elapsed = 0f;
        playing = true;
        Refresh();
    }

    private void Update()
    {
        if (!playing) return;
        elapsed += Time.deltaTime;
        if (elapsed >= duration) { playing = false; Destroy(gameObject); return; }
        Refresh();
    }

    private void Refresh()
    {
        float progress = Mathf.Clamp01(elapsed / duration);
        if (ring != null)
        {
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, radius, progress);
            Color color = fallbackColor;
            color.a *= 1f - progress;
            ring.startColor = ring.endColor = color;
        }
        else if (visual != null && frames != null && frames.Length > 0)
        {
            Sprite sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(progress * frames.Length))];
            visual.sprite = sprite;
            if (visual.transform != transform && sprite != null)
            {
                visual.transform.localScale = new Vector3(spriteScale, spriteScale, 1f);
                visual.transform.localPosition = -sprite.bounds.center * spriteScale;
            }
        }
    }

    private void OnDestroy()
    {
        if (ringMaterial != null) Destroy(ringMaterial);
    }
}
