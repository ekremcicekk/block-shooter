using UnityEngine;

namespace BlockShooter
{
    [CreateAssetMenu(fileName = "BoosterData", menuName = "BlockShooter/Booster Data")]
    public class BoosterData : ScriptableObject
    {
        public BoosterType boosterType;
        public string boosterName;
        [TextArea] public string description;
        public Sprite icon;
        public int unlockLevel;
        public int usesPerLevel = 1;
        public float duration = 5f;
        public Color tintColor = Color.white;
    }

    public enum BoosterType
    {
        Bomb,
        Rainbow,
        Freeze
    }
}
