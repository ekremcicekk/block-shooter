using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlockShooter
{
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Levels")]
        public LevelData[] levels;

        [Header("References")]
        public ConveyorBelt conveyorBelt;
        public ShooterGrid shooterGrid;

        private LevelData _currentLevel;

        public LevelData CurrentLevel => _currentLevel;
        public int CurrentLevelIndex => SaveManager.CurrentLevel;

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
            int index = Mathf.Clamp(SaveManager.CurrentLevel - 1, 0, levels.Length - 1);

            // Loop levels after last
            if (SaveManager.CurrentLevel > levels.Length)
                index = (SaveManager.CurrentLevel - 1) % levels.Length;

            _currentLevel = levels[index];
            ApplyLevel(_currentLevel);
        }

        private void ApplyLevel(LevelData data)
        {
            if (conveyorBelt != null) conveyorBelt.Initialize(data);
            if (shooterGrid != null) shooterGrid.Initialize(data);
        }

        public void RestartLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadNextLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadMainMenu()
        {
            SceneManager.LoadScene(0);
        }
    }
}
