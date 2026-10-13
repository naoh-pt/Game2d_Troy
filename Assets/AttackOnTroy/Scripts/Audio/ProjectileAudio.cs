using UnityEngine;

namespace AttackOnTroy.Audio
{
    public static class ProjectileAudio
    {
        public static void Launch(Projectile2D projectile)
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null) return;
            bool heavy = projectile.Owner != null &&
                projectile.Owner.GetComponent<CyclopsNormalTurnController>()?.EnemyId.Contains("Heavy") == true;
            SoundId id = projectile.ImpactType == ProjectileImpactType.Spear ? SoundId.SpearLaunch :
                projectile.ImpactType == ProjectileImpactType.Arrow ? SoundId.ArrowLaunch :
                projectile.ImpactType == ProjectileImpactType.StoneAxe ?
                    (heavy ? SoundId.HeavyAxeLaunch : SoundId.AxeLaunch) : SoundId.HeavyThrow;
            audio.PlaySFX(id, projectile);
        }

        public static void Impact(Projectile2D projectile, bool actor, bool terrain)
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null) return;
            SoundId id = projectile.ImpactType == ProjectileImpactType.ClubBomb ? SoundId.BombExplosion :
                projectile.ImpactType == ProjectileImpactType.OilJar ? SoundId.JarBreak :
                actor ? SoundId.FleshImpact :
                (projectile.gameObject.scene.name.Contains("Level03") || !terrain ? SoundId.StoneImpact : SoundId.GroundImpact);
            audio.PlaySFX(id, projectile);
        }
    }
}
