using AW2.Game.GobUtils;
using AW2.Helpers;
using AW2.Helpers.Serialization;

namespace AW2.Game.Gobs
{
    /// <summary>
    /// A bullet that bounces off walls and hits only damageable gobs.
    /// Bounce bullet dies by timer, if not sooner.
    /// </summary>
    [LimitedSerialization]
    public class BounceBullet : Bullet
    {
        private int _deathBySlownessCounter;

        /// <summary>
        /// Only for serialisation.
        /// </summary>
        public BounceBullet()
        {
        }

        public BounceBullet(CanonicalString typeName)
            : base(typeName)
        {
        }

        public override void Update()
        {
            base.Update();
            if (Move.LengthSquared() < 1 * 1)
                _deathBySlownessCounter++;
            else
                _deathBySlownessCounter = 0;
            if (_deathBySlownessCounter > 3) Die();
        }

        public override Arena.CollisionSideEffectType Collide(CollisionArea myArea, CollisionArea theirArea, bool stuck, Arena.CollisionSideEffectType sideEffectTypes)
        {
            var result = Arena.CollisionSideEffectType.None;
            var collidedWithPhysical = (theirArea.Type & CollisionAreaType.PhysicalDamageable) != 0;
            if ((sideEffectTypes & AW2.Game.Arena.CollisionSideEffectType.Reversible) != 0)
            {
                if (collidedWithPhysical)
                {
                    theirArea.Owner.InflictDamage(_impactDamage, new DamageInfo(this));
                    result |= Arena.CollisionSideEffectType.Reversible;
                }
            }
            if ((sideEffectTypes & AW2.Game.Arena.CollisionSideEffectType.Irreversible) != 0)
            {
                if (collidedWithPhysical || stuck)
                {
                    Die();
                    result |= Arena.CollisionSideEffectType.Irreversible;
                }
            }
            return result;
        }
    }
}
