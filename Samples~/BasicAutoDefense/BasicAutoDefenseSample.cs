using System.Collections.Generic;
using Deucarian.Attacks;
using Deucarian.Combat;
using Deucarian.DefenseGames;
using Deucarian.Projectiles;
using Deucarian.WeaponSystems;
using Deucarian.WorldNavigation;
using Deucarian.WorldSpawning;
using UnityEngine;

namespace Deucarian.AutoDefense.Samples
{
    public static class BasicAutoDefenseSample
    {
        public static AutoDefenseDefinition CreateDefinition()
        {
            var damageType = new DamageTypeId("damage.basic");
            var attackId = new AttackDefinitionId("attack.basic");
            var projectileId = new ProjectileDefinitionId("projectile.basic");
            var directWeaponId = new WeaponDefinitionId("weapon.direct");
            var projectileWeaponId = new WeaponDefinitionId("weapon.projectile");
            var directMount = new AutoDefenseMountId("mount.direct");
            var projectileMount = new AutoDefenseMountId("mount.projectile");
            return new AutoDefenseDefinition(
                new AutoDefenseObjectiveDefinition(new DefenseObjectiveId("core"), Vector3.zero, 100, damageType),
                AutoDefenseSpawnRingDefinition.FourWay(8f),
                new[] { new AutoDefenseEnemyDefinition(new WorldSpawnableId("enemy.basic"), 10, 2f, 1, damageType) },
                new[]
                {
                    new AutoDefenseMountDefinition(directMount, Vector3.left, new WeaponSlotId("slot.direct"), directWeaponId),
                    new AutoDefenseMountDefinition(projectileMount, Vector3.right, new WeaponSlotId("slot.projectile"), projectileWeaponId)
                },
                new[]
                {
                    new AutoDefenseWeaponModuleDefinition(directMount, new WeaponDefinition(directWeaponId, WeaponFireMode.DirectAttack, attackId, 15), new AttackSourceSnapshot(new AttackSourceId("source.direct"), new CombatantId("core"))),
                    new AutoDefenseWeaponModuleDefinition(projectileMount, new WeaponDefinition(projectileWeaponId, WeaponFireMode.Projectile, attackId, 30, projectileId), new AttackSourceSnapshot(new AttackSourceId("source.projectile"), new CombatantId("core")))
                });
        }
    }
}
