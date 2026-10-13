using System;
using UnityEngine;
using UnityEngine.Audio;

namespace AttackOnTroy.Audio
{
    public enum SoundId
    {
        SpearLaunch, ArrowLaunch, HeavyThrow, AxeLaunch, HeavyAxeLaunch,
        FleshImpact, GroundImpact, StoneImpact, BombExplosion, JarBreak,
        FireIgnite, FireLoop, ChargeLoop, WeaponSelect, EnergyReady,
        SpecialArm, SpecialCancel, Heal, PlayerTurn,
        HumanFootstepSand, HumanFootstepStone, CyclopsFootstep, HeavyCyclopsFootstep,
        HumanLand, CyclopsLand, AchillesAttack, AchillesHurt, AchillesDeath,
        OdysseusAttack, OdysseusHurt, OdysseusDeath,
        CyclopsAttack, CyclopsHurt, CyclopsDeath, HeavyCyclopsAttack, HeavyCyclopsHurt, HeavyCyclopsDeath,
        UIClick, UIConfirm, UIBack, UIUnavailable, SplashSting, Victory, Defeat,
        BeachAmbience, RiverAmbience, WallAmbience
    }

    public enum SoundBus { Combat, Voices, UI, Ambience }

    [Serializable]
    public sealed class SoundDefinition
    {
        public SoundId id;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        public SoundBus bus;
        [Range(0f, 1f)] public float volume = 0.5f;
        public Vector2 pitch = new Vector2(0.97f, 1.03f);
        [Min(0f)] public float minimumInterval;
        [Range(0, 255)] public int priority = 128;
        public bool loop;
    }

    [CreateAssetMenu(menuName = "Attack on Troy/Audio/Sound Library")]
    public sealed class SoundLibrary : ScriptableObject
    {
        public const string ResourcePath = "Audio/GameSoundLibrary";
        public AudioClip menuMusic;
        public AudioClip battleMusic;
        public AudioMixer mixer;
        public AudioMixerGroup musicGroup;
        public AudioMixerGroup combatGroup;
        public AudioMixerGroup voicesGroup;
        public AudioMixerGroup uiGroup;
        public AudioMixerGroup ambienceGroup;
        public TextAsset credits;
        public SoundDefinition[] sounds = Array.Empty<SoundDefinition>();

        public AudioMixerGroup GetGroup(SoundBus bus)
        {
            switch (bus)
            {
                case SoundBus.Voices: return voicesGroup;
                case SoundBus.UI: return uiGroup;
                case SoundBus.Ambience: return ambienceGroup;
                default: return combatGroup;
            }
        }
    }

    // A generation token prevents an old owner from stopping a reused loop slot.
    public readonly struct AudioLoopHandle
    {
        internal readonly int Slot;
        internal readonly uint Generation;
        internal AudioLoopHandle(int slot, uint generation) { Slot = slot; Generation = generation; }
        public bool IsValid => Generation != 0;
    }
}
