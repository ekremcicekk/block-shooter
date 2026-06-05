using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    [CreateAssetMenu(fileName = "LevelSequenceConfig", menuName = "BlockShooter/Configs/Level Sequence Config")]
    public class LevelSequenceConfig : ScriptableObject
    {
        public List<LevelRoot> levelPrefabs = new List<LevelRoot>();
    }
}
