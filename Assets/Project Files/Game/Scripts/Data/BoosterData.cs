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
        public float duration = 8f;   // used by FreePick (window to pick) and SuperShooter (duration)
        public Color tintColor = Color.white;
    }

    public enum BoosterType
    {
        ExtraSlot,   // Adds one extra firing slot for this level
        FreePick,    // Pick any block from the grid regardless of row order
        SuperShooter, // Select a slotted block → floats, zooms camera, shoots all matching color blocks
        MoveShooter  // Select any block (including locked blocks) on the grid and send it to slot
    }
}
