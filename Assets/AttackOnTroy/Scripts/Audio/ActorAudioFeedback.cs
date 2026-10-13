using UnityEngine;

namespace AttackOnTroy.Audio
{
    public enum ActorVoiceProfile { Achilles, Odysseus, Cyclops, HeavyCyclops }

    /// <summary>Observes existing actor state. Never writes health, movement, turns or input.</summary>
    [DisallowMultipleComponent]
    public sealed class ActorAudioFeedback : MonoBehaviour
    {
        [SerializeField] private ActorVoiceProfile profile;
        private ActorDamageReceiver2D receiver;
        private GroundedActorMotor2D motor;
        private AchillesProjectileDebugLauncher launcher;
        private CyclopsNormalTurnController cyclops;
        private readonly System.Random vocalRandom = new System.Random();
        private Vector3 previousPosition;
        private float stepDistance;
        private float previousHealth;
        private float nextAttackVoice;
        private bool wasGrounded;
        private bool wasCharging;
        private bool deathPlayed;
        private AchillesWeapon previousWeapon;
        private AudioLoopHandle chargeHandle;

        public ActorVoiceProfile Profile { get => profile; set => profile = value; }
        private bool IsGiant => profile == ActorVoiceProfile.Cyclops || profile == ActorVoiceProfile.HeavyCyclops;

        private void Awake()
        {
            receiver = GetComponent<ActorDamageReceiver2D>();
            motor = GetComponent<GroundedActorMotor2D>();
            launcher = GetComponent<AchillesProjectileDebugLauncher>();
            cyclops = GetComponent<CyclopsNormalTurnController>();
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            stepDistance = 0f;
            deathPlayed = false;
            wasGrounded = motor != null && motor.IsGrounded;
            if (receiver != null)
            {
                previousHealth = receiver.CurrentHealth;
                receiver.DamageApplied += OnDamage;
                receiver.Died += OnDeath;
                receiver.HealthChanged += OnHealth;
            }
            if (launcher != null)
            {
                previousWeapon = launcher.SelectedWeapon;
                launcher.ThrowStarted += OnAttack;
            }
            if (cyclops != null) cyclops.ThrowStarted += OnAttack;
        }

        private void OnDisable()
        {
            if (receiver != null)
            {
                receiver.DamageApplied -= OnDamage;
                receiver.Died -= OnDeath;
                receiver.HealthChanged -= OnHealth;
            }
            if (launcher != null) launcher.ThrowStarted -= OnAttack;
            if (cyclops != null) cyclops.ThrowStarted -= OnAttack;
            if (AudioManager.Instance != null) AudioManager.Instance.StopLoop(chargeHandle);
            chargeHandle = default;
            wasCharging = false;
        }

        private void Update()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null) return;
            Vector3 position = transform.position;
            float travel = Mathf.Abs(position.x - previousPosition.x);
            float verticalSpeed = Time.deltaTime > 0f ? (position.y - previousPosition.y) / Time.deltaTime : 0f;
            previousPosition = position;
            if (receiver != null && receiver.IsDead) return;
            if (!audio.IsGameplayPaused && Time.deltaTime > 0f && motor != null)
            {
                bool grounded = motor.IsGrounded;
                // Ignore teleports, idle jitter, walls and airborne travel.
                if (grounded && travel > Time.deltaTime * 0.15f && travel < 1f)
                {
                    stepDistance += travel;
                    if (stepDistance >= (IsGiant ? 1.1f : 0.7f))
                    {
                        stepDistance = 0f;
                        SoundId footstep = IsGiant ?
                            (profile == ActorVoiceProfile.HeavyCyclops ? SoundId.HeavyCyclopsFootstep : SoundId.CyclopsFootstep) :
                            (gameObject.scene.name.Contains("Level03") ? SoundId.HumanFootstepStone : SoundId.HumanFootstepSand);
                        audio.PlaySFX(footstep, this);
                    }
                }
                else stepDistance = 0f;
                if (grounded && !wasGrounded && verticalSpeed < -1f)
                    audio.PlaySFX(IsGiant ? SoundId.CyclopsLand : SoundId.HumanLand, this);
                wasGrounded = grounded;
            }
            if (launcher != null)
            {
                if (launcher.SelectedWeapon != previousWeapon)
                {
                    audio.PlaySFX(SoundId.WeaponSelect, this);
                    previousWeapon = launcher.SelectedWeapon;
                }
                if (launcher.IsCharging && !wasCharging) chargeHandle = audio.StartLoop(SoundId.ChargeLoop, this);
                if (!launcher.IsCharging && wasCharging) audio.StopLoop(chargeHandle);
                wasCharging = launcher.IsCharging;
            }
        }

        private void OnAttack()
        {
            if (Time.unscaledTime < nextAttackVoice || vocalRandom.Next(3) != 0) return;
            nextAttackVoice = Time.unscaledTime + 3f;
            PlayVoice(SoundId.AchillesAttack, SoundId.OdysseusAttack, SoundId.CyclopsAttack, SoundId.HeavyCyclopsAttack);
        }

        private void OnDamage(float amount, Vector2 point, GameObject source)
        {
            if (amount > 0f && receiver != null && !receiver.IsDead)
                PlayVoice(SoundId.AchillesHurt, SoundId.OdysseusHurt, SoundId.CyclopsHurt, SoundId.HeavyCyclopsHurt);
        }

        private void OnDeath()
        {
            if (deathPlayed) return;
            deathPlayed = true;
            if (AudioManager.Instance != null) AudioManager.Instance.StopLoop(chargeHandle);
            PlayVoice(SoundId.AchillesDeath, SoundId.OdysseusDeath, SoundId.CyclopsDeath, SoundId.HeavyCyclopsDeath);
        }

        private void OnHealth(float current, float maximum)
        {
            if (current > previousHealth + 0.01f && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SoundId.Heal, this);
            previousHealth = current;
        }

        private void PlayVoice(SoundId achilles, SoundId odysseus, SoundId giant, SoundId heavy)
        {
            SoundId id = profile == ActorVoiceProfile.Odysseus ? odysseus :
                profile == ActorVoiceProfile.Cyclops ? giant : profile == ActorVoiceProfile.HeavyCyclops ? heavy : achilles;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(id, this);
        }
    }
}
