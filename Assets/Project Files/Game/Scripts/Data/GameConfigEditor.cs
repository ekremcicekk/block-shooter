#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using BlockShooter;

namespace BlockShooter.Editor
{
    [CustomEditor(typeof(GameConfig))]
    public class GameConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw standard config fields
            DrawDefaultInspector();

            GUILayout.Space(20);
            
            // Design a premium, neat divider and header for dev tools
            GUI.color = new Color(0.3f, 0.7f, 1f);
            EditorGUILayout.LabelField("DEVELOPER DEBUG TOOLS", EditorStyles.boldLabel);
            GUI.color = Color.white;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);

            // 1. Total Coins (Live editable field)
            int currentCoins = SaveManager.Coins;
            int newCoins = EditorGUILayout.IntField("Live Coins (Total Coin)", currentCoins);
            if (newCoins != currentCoins)
            {
                SaveManager.Coins = newCoins;
                if (Application.isPlaying && UIManager.Instance != null)
                {
                    UIManager.Instance.UpdateCoinUI(true);
                }
            }

            // 2. Current Level (Live editable field)
            int currentLevel = SaveManager.CurrentLevel;
            int newLevel = EditorGUILayout.IntField("Live Current Level", currentLevel);
            if (newLevel != currentLevel)
            {
                SaveManager.CurrentLevel = Mathf.Max(1, newLevel);
                if (Application.isPlaying && LevelManager.Instance != null)
                {
                    LevelManager.Instance.RestartLevel();
                }
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Level Navigation", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Previous Level", GUILayout.Height(25)))
            {
                SaveManager.CurrentLevel = Mathf.Max(1, SaveManager.CurrentLevel - 1);
                if (Application.isPlaying && LevelManager.Instance != null)
                {
                    LevelManager.Instance.RestartLevel();
                }
            }

            if (GUILayout.Button("Next Level", GUILayout.Height(25)))
            {
                SaveManager.CurrentLevel = SaveManager.CurrentLevel + 1;
                if (Application.isPlaying && LevelManager.Instance != null)
                {
                    LevelManager.Instance.RestartLevel();
                }
            }
            
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Resources & Boosters", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Give Money (+1000)", GUILayout.Height(25)))
            {
                SaveManager.Coins += 1000;
                if (Application.isPlaying && UIManager.Instance != null)
                {
                    UIManager.Instance.UpdateCoinUI(true);
                }
            }

            if (GUILayout.Button("Give All Boosters (+5)", GUILayout.Height(25)))
            {
                SaveManager.AddBooster(BoosterType.ExtraSlot, 5);
                SaveManager.AddBooster(BoosterType.SuperShooter, 5);
                SaveManager.AddBooster(BoosterType.MoveShooter, 5);
                Debug.Log("[DevTools] Added +5 of each booster type.");
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(12);

            // 3. Reset Game Data button
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Reset Game Data (Clear Prefs)", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Reset Game Data", "Are you sure you want to delete all PlayerPrefs and reset game data?", "Yes", "No"))
                {
                    SaveManager.ClearAll();
                    Debug.Log("[DevTools] PlayerPrefs cleared successfully.");
                    if (Application.isPlaying && LevelManager.Instance != null)
                    {
                        LevelManager.Instance.RestartLevel();
                    }
                    else if (Application.isPlaying && UIManager.Instance != null)
                    {
                        UIManager.Instance.UpdateCoinUI(false);
                    }
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }
    }
}
#endif
