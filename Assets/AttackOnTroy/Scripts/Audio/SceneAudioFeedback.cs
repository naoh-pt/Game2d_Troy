using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AttackOnTroy.Audio
{
    /// <summary>Installs audio observers, including inactive heroes and menu panels.</summary>
    public sealed class SceneAudioFeedback : MonoBehaviour
    {
        private PlayerEnergySystem energy;
        private Level01TurnManager turns;
        private bool wasEnergyFull;
        private bool wasArmed;
        private bool wasPlayerTurn;
        private bool hasBattleUI;

        public static void Install(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<SceneAudioFeedback>() != null) return;
            GameObject host = new GameObject("SceneAudioFeedback");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<SceneAudioFeedback>();
        }

        private void Start()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (ActorDamageReceiver2D actor in root.GetComponentsInChildren<ActorDamageReceiver2D>(true))
                {
                    ActorAudioFeedback feedback = actor.GetComponent<ActorAudioFeedback>();
                    if (feedback == null) feedback = actor.gameObject.AddComponent<ActorAudioFeedback>();
                    CyclopsNormalTurnController giant = actor.GetComponent<CyclopsNormalTurnController>();
                    AchillesProjectileDebugLauncher hero = actor.GetComponent<AchillesProjectileDebugLauncher>();
                    feedback.Profile = giant != null ?
                        (giant.EnemyId.Contains("Heavy") ? ActorVoiceProfile.HeavyCyclops : ActorVoiceProfile.Cyclops) :
                        (hero != null && hero.ProjectilePrefab != null &&
                         hero.ProjectilePrefab.GetComponent<Projectile2D>()?.ImpactType == ProjectileImpactType.Arrow ?
                            ActorVoiceProfile.Odysseus : ActorVoiceProfile.Achilles);
                }
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                    if (button.GetComponent<ButtonAudioFeedback>() == null) button.gameObject.AddComponent<ButtonAudioFeedback>();
                if (energy == null) energy = root.GetComponentInChildren<PlayerEnergySystem>(true);
                if (turns == null) turns = root.GetComponentInChildren<Level01TurnManager>(true);
                if (root.GetComponentInChildren<Level01BattleUI>(true) != null) hasBattleUI = true;
            }
            if (energy != null)
            {
                wasEnergyFull = energy.IsEnergyFull;
                wasArmed = energy.IsSpecialArmed;
                energy.EnergyChanged += OnEnergy;
                energy.SpecialArmedChanged += OnArmed;
            }
            if (turns != null) turns.BattleEnded += OnBattleEnded;
        }

        private void Update()
        {
            if (hasBattleUI && AudioManager.Instance != null)
                AudioManager.Instance.SetGameplayPaused(Level01BattleUI.IsGamePaused);
            if (turns == null) return;
            bool playerTurn = turns.IsPlayerTurn;
            if (playerTurn && !wasPlayerTurn && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SoundId.PlayerTurn, this);
            wasPlayerTurn = playerTurn;
        }

        private void OnEnergy(float current, float maximum)
        {
            bool full = maximum > 0 && current >= maximum - 0.001f;
            if (full && !wasEnergyFull && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SoundId.EnergyReady, this);
            wasEnergyFull = full;
        }

        private void OnArmed(bool armed)
        {
            // Consumption also raises false; only cancellation retains full energy.
            if (armed != wasArmed && AudioManager.Instance != null && (armed || (energy != null && energy.IsEnergyFull)))
                AudioManager.Instance.PlaySFX(armed ? SoundId.SpecialArm : SoundId.SpecialCancel, this);
            wasArmed = armed;
        }

        private void OnBattleEnded(bool win)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(win ? SoundId.Victory : SoundId.Defeat, this);
        }

        private void OnDestroy()
        {
            if (energy != null)
            {
                energy.EnergyChanged -= OnEnergy;
                energy.SpecialArmedChanged -= OnArmed;
            }
            if (turns != null) turns.BattleEnded -= OnBattleEnded;
        }
    }
}
