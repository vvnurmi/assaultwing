﻿using System;
using AW2.Core;
using AW2.Helpers;
using AW2.Game.GobUtils;

namespace AW2.Game.BonusActions
{
    [GameActionType(5)]
    public class Weapon2UpgradeBonusAction : GameAction
    {
        public override bool DoAction()
        {
            var success = UpgradeWeapon();
            SetActionMessage();
            if (!success) return false;
            return base.DoAction();
        }

        public override void RemoveAction()
        {
            Player.Ship.SetDeviceType(Weapon.OwnerHandleType.SecondaryWeapon, Player.Weapon2Name);
        }

        private bool UpgradeWeapon()
        {
            if (Player.Ship == null) return false;
            var weapon2 = (Weapon)AssaultWingCore.Instance.DataEngine.GetTypeTemplate(Player.Ship.Weapon2Name);
            if (weapon2.UpgradeNames == null || weapon2.UpgradeNames.Length == 0) return false;
            var weaponUpgrade = weapon2.UpgradeNames[0];
            Player.Ship.SetDeviceType(Weapon.OwnerHandleType.SecondaryWeapon, weaponUpgrade);
            return true;
        }

        private void SetActionMessage()
        {
            BonusText = Player.Ship.Weapon2Name;
            BonusIconName = Player.Ship.Weapon2.IconName;
        }
    }
}
