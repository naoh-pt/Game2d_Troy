using UnityEngine;

/// <summary>
/// Cross-scene session state storing the selected player hero and menu navigation flags.
/// Persists across scene reloads and restarts via PlayerPrefs and static state.
/// </summary>
public static class BattleSessionData
{
    private const string HeroPrefKey = "AttackOnTroy_SelectedHero";
    public const string HeroAchilles = "Achilles";
    public const string HeroOdysseus = "Odysseus";

    private static string selectedHero;
    private static bool returnToCharacterSelect;

    public static string SelectedHero
    {
        get
        {
            if (string.IsNullOrEmpty(selectedHero))
            {
                selectedHero = PlayerPrefs.GetString(HeroPrefKey, HeroAchilles);
            }
            return selectedHero;
        }
        set
        {
            selectedHero = string.IsNullOrEmpty(value) ? HeroAchilles : value;
            PlayerPrefs.SetString(HeroPrefKey, selectedHero);
            PlayerPrefs.Save();
        }
    }

    public static bool ReturnToCharacterSelect
    {
        get => returnToCharacterSelect;
        set => returnToCharacterSelect = value;
    }

    public static bool IsOdysseus => SelectedHero == HeroOdysseus;
    public static bool IsAchilles => !IsOdysseus;
}
