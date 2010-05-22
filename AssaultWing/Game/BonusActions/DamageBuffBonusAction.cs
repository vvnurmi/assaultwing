﻿using System;
using AW2.Game.GobUtils;
using AW2.Helpers;

namespace AW2.Game.BonusActions
{
    [GameActionType(2)]
    [LimitedSerialization]
    public class DamageBuffBonusAction : GameAction
    {
        [TypeParameter]
        private string _buffName;

        [TypeParameter]
        private string _bonusIconName;

        [TypeParameter]
        private float _damagePerSecond;

        /// <summary>
        /// This constructor is only for serialization.
        /// </summary>
        public DamageBuffBonusAction()
        {
            _buffName = "dummy damage buff";
            _bonusIconName = "dummytexture";
            _damagePerSecond = 500;
        }

        public DamageBuffBonusAction(string buffName, string bonusIconName, float damagePerSecond)
        {
            _buffName = buffName;
            _bonusIconName = bonusIconName;
            _damagePerSecond = damagePerSecond;
        }

        public override void DoAction()
        {
            SetActionMessage();
            base.DoAction();
        }

        public override void Update()
        {
            // HACK: If the ship dies, the player's bonus actions are cleared, which results in a crash
            // because this method is called while iterating over the player's bonus actions.
            // Workaround: delay InflictDamage by using DataEngine.CustomOperations.
            // A more beautiful way around this would be to share deletion logic from Gob to other
            // similar classes such as BonusAction and Weapon. !!!
            float damage = AssaultWing.Instance.PhysicsEngine.ApplyChange(_damagePerSecond, AssaultWing.Instance.GameTime.ElapsedGameTime);
            AssaultWing.Instance.DataEngine.CustomOperations += () => Player.Ship.InflictDamage(damage, new DeathCause(Player.Ship, DeathCauseType.Damage));
        }

        private void SetActionMessage()
        {
            BonusText = _buffName;
            BonusIconName = _bonusIconName;
        }
    }
}