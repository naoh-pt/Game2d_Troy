using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact screen HUD displaying current projectile angle, charging power bar,
/// weapon selection, and prepared wind velocity with directional arrows and strength indicator.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProjectileAimHUD : MonoBehaviour
{
    [Header("Launcher Reference")]
    [SerializeField] private AchillesProjectileDebugLauncher launcher;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI angleLabel;
    [SerializeField] private Image powerBarFill;
    [SerializeField] private TextMeshProUGUI windLabel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI weaponLabel;

    [Header("Wind UI References")]
    [SerializeField] private Image windArrowLeft;
    [SerializeField] private Image windArrowRight;
    [SerializeField] private Image windNeutralIndicator;
    [SerializeField] private Image windStrengthBarBg;
    [SerializeField] private Image windStrengthBarFill;
    [SerializeField, Min(0.1f)] private float maxWindSpeed = 2.0f;
    [SerializeField] private float neutralThreshold = 0.05f;

    [Header("Wind Colors")]
    [SerializeField] private Color activeWindColor = new Color(0.4f, 0.85f, 1f, 1f);        // Bright Cyan
    [SerializeField] private Color inactiveWindColor = new Color(0.35f, 0.4f, 0.45f, 0.25f); // Dimmed Slate
    [SerializeField] private Color neutralWindColor = new Color(0.9f, 0.9f, 0.95f, 0.9f);    // Bright Neutral Silver

    [Header("Display Settings")]
    [SerializeField] private bool dimWhenInactive = true;
    [SerializeField, Range(0.1f, 1f)] private float activeAlpha = 1.0f;
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.35f;

    private int lastAngle = -1;
    private float lastWind = float.NaN;
    private bool lastCanAim = true;
    private AchillesWeapon? lastWeapon;

    private static Sprite arrowSpriteCache;
    private static Sprite circleSpriteCache;

    #region Public Test & Inspection API
    public bool IsNeutralWind => Mathf.Abs(launcher != null ? launcher.CurrentWind : (float.IsNaN(lastWind) ? 0f : lastWind)) <= neutralThreshold;
    public bool IsWindBlowingLeft => (launcher != null ? launcher.CurrentWind : (float.IsNaN(lastWind) ? 0f : lastWind)) < -neutralThreshold;
    public bool IsWindBlowingRight => (launcher != null ? launcher.CurrentWind : (float.IsNaN(lastWind) ? 0f : lastWind)) > neutralThreshold;
    public float CurrentWindStrength01 => Mathf.Clamp01(Mathf.Abs(launcher != null ? launcher.CurrentWind : (float.IsNaN(lastWind) ? 0f : lastWind)) / Mathf.Max(0.1f, maxWindSpeed));
    public float LastWind => lastWind;
    #endregion

    public void ConfigureWeaponLabel(TextMeshProUGUI label)
    {
        weaponLabel = label;
        lastWeapon = null;
    }

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        EnsureFillSprite();
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene.IndexOf("Level01", StringComparison.OrdinalIgnoreCase) >= 0)
            maxWindSpeed = 0.8f;
        else if (scene.IndexOf("Level02", StringComparison.OrdinalIgnoreCase) >= 0)
            maxWindSpeed = 1.5f;
        else if (scene.IndexOf("Level03", StringComparison.OrdinalIgnoreCase) >= 0)
            maxWindSpeed = 2.0f;

        if (powerBarFill != null)
            powerBarFill.fillAmount = 0f;
    }

    private void OnDisable()
    {
        if (powerBarFill != null)
            powerBarFill.fillAmount = 0f;
    }

    public void SetLauncher(AchillesProjectileDebugLauncher newLauncher)
    {
        launcher = newLauncher;
        lastAngle = -1;
        lastWind = float.NaN;
        lastCanAim = true;
        lastWeapon = null;
    }

    public void Configure(
        AchillesProjectileDebugLauncher launcherRef,
        TextMeshProUGUI angleText,
        Image powerFill,
        TextMeshProUGUI windText,
        CanvasGroup group)
    {
        launcher = launcherRef;
        angleLabel = angleText;
        powerBarFill = powerFill;
        windLabel = windText;
        canvasGroup = group;
        InitializeComponents();
    }

    public void ConfigureWindReferences(
        Image arrowLeft,
        Image arrowRight,
        Image neutralDot,
        Image barFill,
        Image barBg = null,
        float maxSpeed = 2.0f)
    {
        windArrowLeft = arrowLeft;
        windArrowRight = arrowRight;
        windNeutralIndicator = neutralDot;
        windStrengthBarFill = barFill;
        windStrengthBarBg = barBg;
        maxWindSpeed = maxSpeed;
        InitializeComponents();
        if (!float.IsNaN(lastWind))
            UpdateWindDisplay(lastWind);
    }

    private void InitializeComponents()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        AutoFindWindReferences();
        EnsureFillSprite();
    }

    private void AutoFindWindReferences()
    {
        if (windLabel == null)
        {
            Transform t = transform.Find("WindHUD/WindText") ?? transform.Find("WindText") ?? transform.Find("WindLabel");
            if (t != null) windLabel = t.GetComponent<TextMeshProUGUI>();
        }

        if (windArrowLeft == null)
        {
            Transform t = transform.Find("WindHUD/WindMeterRow/ArrowLeft")
                       ?? transform.Find("WindMeterRow/ArrowLeft")
                       ?? transform.Find("ArrowLeft");
            if (t != null) windArrowLeft = t.GetComponent<Image>();
        }

        if (windArrowRight == null)
        {
            Transform t = transform.Find("WindHUD/WindMeterRow/ArrowRight")
                       ?? transform.Find("WindMeterRow/ArrowRight")
                       ?? transform.Find("ArrowRight");
            if (t != null) windArrowRight = t.GetComponent<Image>();
        }

        if (windStrengthBarBg == null)
        {
            Transform t = transform.Find("WindHUD/WindMeterRow/StrengthBarBg")
                       ?? transform.Find("WindMeterRow/StrengthBarBg")
                       ?? transform.Find("StrengthBarBg");
            if (t != null) windStrengthBarBg = t.GetComponent<Image>();
        }

        if (windStrengthBarFill == null)
        {
            Transform t = transform.Find("WindHUD/WindMeterRow/StrengthBarBg/StrengthBarFill")
                       ?? transform.Find("WindMeterRow/StrengthBarBg/StrengthBarFill")
                       ?? transform.Find("StrengthBarFill");
            if (t != null) windStrengthBarFill = t.GetComponent<Image>();
        }

        if (windNeutralIndicator == null)
        {
            Transform t = transform.Find("WindHUD/WindMeterRow/StrengthBarBg/NeutralDot")
                       ?? transform.Find("WindMeterRow/StrengthBarBg/NeutralDot")
                       ?? transform.Find("NeutralDot");
            if (t != null) windNeutralIndicator = t.GetComponent<Image>();
        }
    }

    private void EnsureFillSprite()
    {
        if (powerBarFill != null && powerBarFill.sprite == null)
        {
            powerBarFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        if (windStrengthBarBg != null && windStrengthBarBg.sprite == null)
        {
            windStrengthBarBg.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        if (windStrengthBarFill != null && windStrengthBarFill.sprite == null)
        {
            windStrengthBarFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        if (arrowSpriteCache == null)
            arrowSpriteCache = CreateArrowSprite();

        if (circleSpriteCache == null)
            circleSpriteCache = CreateCircleSprite();

        if (windArrowLeft != null && windArrowLeft.sprite == null)
            windArrowLeft.sprite = arrowSpriteCache;

        if (windArrowRight != null && windArrowRight.sprite == null)
            windArrowRight.sprite = arrowSpriteCache;

        if (windNeutralIndicator != null && windNeutralIndicator.sprite == null)
            windNeutralIndicator.sprite = circleSpriteCache;
    }

    private static Sprite CreateArrowSprite()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color transparent = new Color(0f, 0f, 0f, 0f);
        Color white = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isStem = (x >= 4 && x <= 18) && (y >= 11 && y <= 20);
                float dy = Mathf.Abs(y - 15.5f);
                bool isHead = (x >= 16 && x <= 28) && (dy <= (28 - x) * 1.2f);
                tex.SetPixel(x, y, (isStem || isHead) ? white : transparent);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color transparent = new Color(0f, 0f, 0f, 0f);
        Color white = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                tex.SetPixel(x, y, dist <= 7f ? white : transparent);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void Update()
    {
        if (launcher == null)
            return;

        UpdateCanvasAlpha();
        UpdateAngle();
        UpdatePowerBar();
        UpdateWind();
        if (weaponLabel != null && lastWeapon != launcher.SelectedWeapon)
        {
            lastWeapon = launcher.SelectedWeapon;
            weaponLabel.text = launcher.SelectedWeaponName;
        }
    }

    private void UpdateCanvasAlpha()
    {
        bool canAim = launcher.CanAim;
        if (canAim != lastCanAim)
        {
            lastCanAim = canAim;
            if (canvasGroup != null)
                canvasGroup.alpha = canAim ? activeAlpha : (dimWhenInactive ? inactiveAlpha : 0f);
        }
    }

    private void UpdateAngle()
    {
        int roundedAngle = Mathf.RoundToInt(launcher.CurrentAngle);
        if (roundedAngle != lastAngle)
        {
            lastAngle = roundedAngle;
            if (angleLabel != null)
                angleLabel.text = $"ANGLE: {roundedAngle}°";
        }
    }

    private void UpdatePowerBar()
    {
        if (powerBarFill != null)
        {
            if (powerBarFill.sprite == null)
                EnsureFillSprite();

            powerBarFill.fillAmount = launcher != null ? launcher.CurrentCharge01 : 0f;
        }
    }

    private void UpdateWind()
    {
        float wind = launcher.CurrentWind;
        if (Mathf.Abs(wind - lastWind) > 0.01f || float.IsNaN(lastWind))
        {
            lastWind = wind;
            UpdateWindDisplay(wind);
        }
    }

    public void UpdateWindDisplay(float wind)
    {
        float absWind = Mathf.Abs(wind);
        bool isNeutral = absWind <= neutralThreshold;
        bool isLeft = wind < -neutralThreshold;
        bool isRight = wind > neutralThreshold;
        float strength01 = Mathf.Clamp01(absWind / Mathf.Max(0.1f, maxWindSpeed));

        if (windLabel != null)
        {
            windLabel.text = isNeutral ? "WIND: 0.0" : $"WIND: {absWind:F1}";
        }

        if (windStrengthBarFill != null)
        {
            if (windStrengthBarFill.sprite == null)
                EnsureFillSprite();

            windStrengthBarFill.fillOrigin = isLeft ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            windStrengthBarFill.fillAmount = isNeutral ? 0f : strength01;
            windStrengthBarFill.color = isNeutral ? inactiveWindColor : activeWindColor;
        }

        if (windArrowLeft != null)
        {
            windArrowLeft.color = isLeft ? activeWindColor : inactiveWindColor;
        }

        if (windArrowRight != null)
        {
            windArrowRight.color = isRight ? activeWindColor : inactiveWindColor;
        }

        if (windNeutralIndicator != null)
        {
            windNeutralIndicator.color = isNeutral ? neutralWindColor : inactiveWindColor;
        }
    }

    public void SetWindForTesting(float wind)
    {
        lastWind = wind;
        UpdateWindDisplay(wind);
    }
}
