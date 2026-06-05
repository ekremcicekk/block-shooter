using UnityEngine;

namespace BlockShooter
{
    public static class SaveManager
    {
        private const string KEY_LEVEL          = "CurrentLevel";
        private const string KEY_COINS          = "Coins";
        private const string KEY_BOOSTER_SLOT   = "Booster_ExtraSlot";
        private const string KEY_BOOSTER_PICK   = "Booster_FreePick";
        private const string KEY_BOOSTER_BLAST  = "Booster_ColorBlast";
        private const string KEY_BOOSTER_MOVE   = "Booster_MoveShooter";

        public static int CurrentLevel
        {
            get => PlayerPrefs.GetInt(KEY_LEVEL, 1);
            set { PlayerPrefs.SetInt(KEY_LEVEL, value); PlayerPrefs.Save(); }
        }

        public static int Coins
        {
            get => PlayerPrefs.GetInt(KEY_COINS, 0);
            set { PlayerPrefs.SetInt(KEY_COINS, value); PlayerPrefs.Save(); }
        }

        public static int GetBoosterCount(BoosterType type)
        {
            string key = KeyFor(type);
            return string.IsNullOrEmpty(key) ? 0 : PlayerPrefs.GetInt(key, 0);
        }

        public static void SetBoosterCount(BoosterType type, int count)
        {
            string key = KeyFor(type);
            if (!string.IsNullOrEmpty(key))
            {
                PlayerPrefs.SetInt(key, Mathf.Max(0, count));
                PlayerPrefs.Save();
            }
        }

        public static void AddBooster(BoosterType type, int amount = 1) =>
            SetBoosterCount(type, GetBoosterCount(type) + amount);

        public static bool UseBooster(BoosterType type)
        {
            int count = GetBoosterCount(type);
            if (count <= 0) return false;
            SetBoosterCount(type, count - 1);
            return true;
        }

        public static void ClearAll() => PlayerPrefs.DeleteAll();

        private static string KeyFor(BoosterType type) => type switch
        {
            BoosterType.ExtraSlot  => KEY_BOOSTER_SLOT,
            BoosterType.FreePick   => KEY_BOOSTER_PICK,
            BoosterType.SuperShooter => KEY_BOOSTER_BLAST,
            BoosterType.MoveShooter => KEY_BOOSTER_MOVE,
            _ => ""
        };
    }
}
