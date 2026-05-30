using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlockShooter
{
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Level Prefabs")]
        [Tooltip("Array of self-contained level prefabs (LevelRoot). Loops when exhausted.")]
        public LevelRoot[] levelPrefabs;

        [Header("Spawn Point")]
        [Tooltip("Parent transform under which the active level prefab is instantiated.")]
        public Transform levelSpawnParent;

        private LevelRoot _activeLevelRoot;

        public LevelRoot CurrentLevelRoot => _activeLevelRoot;
        public int CurrentLevelIndex     => SaveManager.CurrentLevel;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            LoadCurrentLevel();
        }

        public void LoadCurrentLevel()
        {
            if (levelPrefabs == null || levelPrefabs.Length == 0)
            {
                Debug.LogWarning("[LevelManager] No level prefabs assigned.");
                return;
            }

            int raw   = SaveManager.CurrentLevel - 1;
            int index = raw % levelPrefabs.Length;
            SpawnLevel(levelPrefabs[Mathf.Clamp(index, 0, levelPrefabs.Length - 1)]);
        }

        private void SpawnLevel(LevelRoot prefab)
        {
            if (_activeLevelRoot != null)
                Destroy(_activeLevelRoot.gameObject);

            Transform parent = levelSpawnParent != null ? levelSpawnParent : transform;
            _activeLevelRoot = Instantiate(prefab, parent);
            _activeLevelRoot.transform.localPosition = Vector3.zero;
            _activeLevelRoot.Initialize();
        }

        public void RestartLevel()  => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        public void LoadNextLevel() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        public void LoadMainMenu()  => SceneManager.LoadScene(0);
    }
}
