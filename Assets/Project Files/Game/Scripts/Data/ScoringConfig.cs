using UnityEngine;

namespace BlockShooter
{
    [CreateAssetMenu(fileName = "ScoringConfig", menuName = "BlockShooter/Configs/Scoring Config")]
    public class ScoringConfig : ScriptableObject
    {
        public int scorePerBlock = 10;
        public int scoreComboMultiplier = 2;
    }
}
