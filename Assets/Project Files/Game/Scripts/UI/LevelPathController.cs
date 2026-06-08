using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace BlockShooter
{
    public class LevelPathController : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject pathLinePrefab;
        public GameObject activeLevelPrefab;
        public GameObject hardLevelPrefab;
        public GameObject nextLevelPrefab;

        [Header("Setup")]
        public Transform contentParent;
        public RectTransform playButton;
        public GameConfig gameConfig;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (gameConfig == null)
            {
                var gc = UnityEditor.AssetDatabase.FindAssets("t:GameConfig");
                if (gc.Length > 0)
                {
                    gameConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(gc[0]));
                }
            }
        }
#endif

        [Header("Layout Settings")]
        public float startY = -120f;
        public float spacing = 240f;
        public float playButtonOffset = -140f;

        private void Start()
        {
            UpdateLevelPath();
            BindPlayButton();
        }

        private void BindPlayButton()
        {
            if (playButton != null)
            {
                Button btn = playButton.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveListener(StartGame);
                    btn.onClick.AddListener(StartGame);
                }
            }
        }

        private void StartGame()
        {
            DG.Tweening.DOTween.KillAll();
            SceneManager.LoadScene("Game");
        }

        public void UpdateLevelPath()
        {
            if (contentParent == null) contentParent = transform;

            // 1. Clear existing generated children under contentParent (excluding playButton)
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                Transform child = contentParent.GetChild(i);
                if (playButton != null && child == playButton) continue;
                Destroy(child.gameObject);
            }

            // 2. Instantiate Path Line if prefab is assigned
            if (pathLinePrefab != null)
            {
                GameObject pathLine = Instantiate(pathLinePrefab, contentParent);
                pathLine.name = "Path_Line";
                RectTransform pathRect = pathLine.GetComponent<RectTransform>();
                if (pathRect != null)
                {
                    // Stretch from play button Y position to the top of the container
                    pathRect.anchorMin = new Vector2(0.5f, 0.5f);
                    pathRect.anchorMax = new Vector2(0.5f, 1f);
                    pathRect.pivot = new Vector2(0.5f, 0.5f);
                    
                    float width = pathRect.sizeDelta.x;
                    pathRect.offsetMin = new Vector2(-width / 2f, startY + playButtonOffset);
                    pathRect.offsetMax = new Vector2(width / 2f, 0f);
                }
                pathRect.SetAsFirstSibling(); // Position behind the nodes
            }

            int currentLevel = SaveManager.CurrentLevel;

            // 3. Spawn the three nodes from bottom to top
            SpawnNode(currentLevel, startY, isActive: true);
            SpawnNode(currentLevel + 1, startY + spacing, isActive: false);
            SpawnNode(currentLevel + 2, startY + 2 * spacing, isActive: false);

            // 4. Reposition Play Button below the active node
            if (playButton != null)
            {
                playButton.anchoredPosition = new Vector2(0f, startY + playButtonOffset);
                playButton.SetAsLastSibling(); // Ensure it renders in front
            }
        }

        private void SpawnNode(int levelNumber, float posY, bool isActive)
        {
            bool isHard = false;
            if (gameConfig != null && gameConfig.levelPrefabs != null && gameConfig.levelPrefabs.Count > 0)
            {
                int index = (levelNumber - 1) % gameConfig.levelPrefabs.Count;
                var prefab = gameConfig.levelPrefabs[index];
                if (prefab != null)
                {
                    isHard = prefab.isHardLevel;
                }
            }
            else
            {
                isHard = (levelNumber % 5 == 4);
            }
            GameObject prefabToSpawn;

            if (isActive)
            {
                // Active level can be hard or normal active level
                prefabToSpawn = isHard ? hardLevelPrefab : activeLevelPrefab;
            }
            else
            {
                // Upcoming levels always use the standard next level prefab
                prefabToSpawn = nextLevelPrefab;
            }

            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"[LevelPathController] Prefab missing for node level {levelNumber}!");
                return;
            }

            GameObject nodeGo = Instantiate(prefabToSpawn, contentParent);
            nodeGo.name = $"LevelNode_{levelNumber}";

            RectTransform nodeRect = nodeGo.GetComponent<RectTransform>();
            if (nodeRect != null)
            {
                nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
                nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
                nodeRect.pivot = new Vector2(0.5f, 0.5f);
                nodeRect.anchoredPosition = new Vector2(0f, posY);
            }

            // Find TextMeshProUGUI in the spawned prefab instance and assign the level number
            TextMeshProUGUI textComp = nodeGo.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null)
            {
                textComp.text = levelNumber.ToString();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Build Level Path Preview")]
        public void BuildLevelPathPreview()
        {
            if (contentParent == null) contentParent = transform;

            // Destroy existing children using Undo in Editor mode
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                Transform child = contentParent.GetChild(i);
                if (playButton != null && child == playButton) continue;
                UnityEditor.Undo.DestroyObjectImmediate(child.gameObject);
            }

            // Spawn Path Line in Editor
            if (pathLinePrefab != null)
            {
                GameObject pathLine = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(pathLinePrefab, contentParent);
                pathLine.name = "Path_Line";
                UnityEditor.Undo.RegisterCreatedObjectUndo(pathLine, "Create Path Line");
                RectTransform pathRect = pathLine.GetComponent<RectTransform>();
                if (pathRect != null)
                {
                    // Stretch from play button Y position to the top of the container
                    pathRect.anchorMin = new Vector2(0.5f, 0.5f);
                    pathRect.anchorMax = new Vector2(0.5f, 1f);
                    pathRect.pivot = new Vector2(0.5f, 0.5f);
                    
                    float width = pathRect.sizeDelta.x;
                    pathRect.offsetMin = new Vector2(-width / 2f, startY + playButtonOffset);
                    pathRect.offsetMax = new Vector2(width / 2f, 0f);
                }
                pathRect.SetAsFirstSibling();
            }

            int currentLevel = 1; // Default preview level in editor
            if (Application.isPlaying)
            {
                currentLevel = SaveManager.CurrentLevel;
            }

            // Spawn Nodes in Editor
            SpawnNodeEditor(currentLevel, startY, isActive: true);
            SpawnNodeEditor(currentLevel + 1, startY + spacing, isActive: false);
            SpawnNodeEditor(currentLevel + 2, startY + 2 * spacing, isActive: false);

            if (playButton != null)
            {
                playButton.anchoredPosition = new Vector2(0f, startY + playButtonOffset);
                playButton.SetAsLastSibling();
            }

            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
            
            Debug.Log("[LevelPathController] Editor preview generated successfully!");
        }

        private void SpawnNodeEditor(int levelNumber, float posY, bool isActive)
        {
            bool isHard = false;
            if (gameConfig != null && gameConfig.levelPrefabs != null && gameConfig.levelPrefabs.Count > 0)
            {
                int index = (levelNumber - 1) % gameConfig.levelPrefabs.Count;
                var prefab = gameConfig.levelPrefabs[index];
                if (prefab != null)
                {
                    isHard = prefab.isHardLevel;
                }
            }
            else
            {
                isHard = (levelNumber % 5 == 4);
            }
            GameObject prefabToSpawn;

            if (isActive)
            {
                // Active level can be hard or normal active level
                prefabToSpawn = isHard ? hardLevelPrefab : activeLevelPrefab;
            }
            else
            {
                // Upcoming levels always use the standard next level prefab
                prefabToSpawn = nextLevelPrefab;
            }

            if (prefabToSpawn == null) return;

            GameObject nodeGo = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefabToSpawn, contentParent);
            nodeGo.name = $"LevelNode_{levelNumber}";
            UnityEditor.Undo.RegisterCreatedObjectUndo(nodeGo, "Create Node");

            RectTransform nodeRect = nodeGo.GetComponent<RectTransform>();
            if (nodeRect != null)
            {
                nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
                nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
                nodeRect.pivot = new Vector2(0.5f, 0.5f);
                nodeRect.anchoredPosition = new Vector2(0f, posY);
            }

            TextMeshProUGUI textComp = nodeGo.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null)
            {
                textComp.text = levelNumber.ToString();
            }
        }
#endif
    }
}
