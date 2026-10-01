using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact screen HUD displaying current projectile angle, charging power bar,
/// and prepared wind velocity with directional indicator.
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

    [Header("Display Settings")]
    [SerializeField] private bool dimWhenInactive = true;
    [SerializeField, Range(0.1f, 1f)] private float activeAlpha = 1.0f;
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.35f;

    private int lastAngle = -1;
    private float lastWind = float.NaN;
    private bool lastCanAim = true;
    private AchillesWeapon? lastWeapon;

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
        if (powerBarFill != null)
            powerBarFill.fillAmount = 0f;
    }

    private void OnDisable()
    {
        if (powerBarFill != null)
            powerBarFill.fillAmount = 0f;
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

    private void InitializeComponents()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        EnsureFillSprite();
    }

    private void EnsureFillSprite()
    {
        if (powerBarFill != null && powerBarFill.sprite == null)
        {
            powerBarFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }
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
            if (windLabel != null)
            {
                string arrow;
                if (wind > 0.05f)
                    arrow = "▶";
                else if (wind < -0.05f)
                    arrow = "◀";
                else
                    arrow = "•";

                windLabel.text = $"WIND: {Mathf.Abs(wind):F1} {arrow}";
            }
        }
    }
}
