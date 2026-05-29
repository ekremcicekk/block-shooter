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
        public float duration = 8f;   // used by FreePick (window to pick) and ColorBlast (rapid-fire duration)
        public Color tintColor = Color.white;
    }

    public enum BoosterType
    {
        ExtraSlot,   // Adds one extra firing slot for this level
        FreePick,    // Pick any block from the grid regardless of row order
        ColorBlast   // Select a slotted block → it fires at ALL matching-color blocks at once
    }
}
