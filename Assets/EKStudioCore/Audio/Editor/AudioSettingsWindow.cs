using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace EKStudio.Audio.Editor
{
    /// <summary>
    /// Modern and user-friendly audio settings window
    /// </summary>
    public class AudioSettingsWindow : EditorWindow
    {
        private AudioData audioData;
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private SoundType filterType = SoundType.UI;
        private bool showAllTypes = true;
        private string newSoundName = "";
        private AudioClip newAudioClip;
        private SoundType newSoundType = SoundType.Gameplay;
        private string newDescription = "";
        
        private bool showAddSection = false;
        private bool showImportSection = false;
        
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;
        private Color accentColor = new Color(0.3f, 0.7f, 1f);

        [MenuItem("Tools/Audio Settings", false, 0)]
        public static void ShowWindow()
        {
            AudioSettingsWindow window = GetWindow<AudioSettingsWindow>("Audio Settings");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            LoadAudioData();
        }

        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = accentColor }
                };
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 10, 10)
                };
            }

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private void LoadAudioData()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioData");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                audioData = AssetDatabase.LoadAssetAtPath<AudioData>(path);
            }
        }

        private void OnGUI()
        {
            InitializeStyles();
            
            EditorGUILayout.BeginVertical();
            
            DrawHeader();
            DrawToolbar();
            
            EditorGUILayout.Space(5);
            
            if (audioData == null)
            {
                DrawNoDataMessage();
            }
            else
            {
                DrawAudioDataSection();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(boxStyle);
            
            GUILayout.Label("🎵 Audio Settings", headerStyle, GUILayout.Height(30));
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Refresh", GUILayout.Width(80), GUILayout.Height(25)))
            {
                LoadAudioData();
                if (audioData != null)
                    EditorUtility.SetDirty(audioData);
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            GUILayout.Label("Search:", GUILayout.Width(50));
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
            
            GUILayout.Space(10);
            
            showAllTypes = GUILayout.Toggle(showAllTypes, "All Types", EditorStyles.toolbarButton);
            if (!showAllTypes)
            {
                filterType = (SoundType)EditorGUILayout.EnumPopup(filterType, EditorStyles.toolbarDropDown, GUILayout.Width(100));
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoDataMessage()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox("No AudioData asset found. Create one to get started!", MessageType.Info);
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Create AudioData", GUILayout.Width(150), GUILayout.Height(30)))
            {
                CreateAudioData();
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(20);
            EditorGUILayout.EndVertical();
        }

        private void DrawAudioDataSection()
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField("Audio Data Asset:", EditorStyles.boldLabel, GUILayout.Width(120));
            audioData = (AudioData)EditorGUILayout.ObjectField(audioData, typeof(AudioData), false);
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Statistics
            DrawStatistics();
            
            EditorGUILayout.Space(5);
            
            // Add new sound section
            DrawAddSoundSection();
            
            EditorGUILayout.Space(5);
            
            // Import from folder section
            DrawImportSection();
            
            EditorGUILayout.Space(5);
            
            // Sound list
            DrawSoundList();
        }

        private void DrawStatistics()
        {
            EditorGUILayout.BeginHorizontal(boxStyle);
            
            int totalSounds = audioData.audioClips.Count;
            int uiSounds = audioData.audioClips.Count(x => x.soundType == SoundType.UI);
            int gameplaySounds = audioData.audioClips.Count(x => x.soundType == SoundType.Gameplay);
            int musicSounds = audioData.audioClips.Count(x => x.soundType == SoundType.Music);
            
            GUILayout.Label($"📊 Total Sounds: {totalSounds}", EditorStyles.miniLabel);
            GUILayout.Label($"🖱️ UI: {uiSounds}", EditorStyles.miniLabel);
            GUILayout.Label($"🎮 Gameplay: {gameplaySounds}", EditorStyles.miniLabel);
            GUILayout.Label($"🎵 Music: {musicSounds}", EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAddSoundSection()
        {
            showAddSection = EditorGUILayout.Foldout(showAddSection, "➕ Add New Sound", true, EditorStyles.foldoutHeader);
            
            if (showAddSection)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                newSoundName = EditorGUILayout.TextField("Sound Name", newSoundName);
                newAudioClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", newAudioClip, typeof(AudioClip), false);
                newSoundType = (SoundType)EditorGUILayout.EnumPopup("Sound Type", newSoundType);
                
                EditorGUILayout.LabelField("Description");
                newDescription = EditorGUILayout.TextArea(newDescription, GUILayout.Height(50));
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                
                GUI.enabled = !string.IsNullOrEmpty(newSoundName) && newAudioClip != null;
                
                if (GUILayout.Button("Add Sound", buttonStyle, GUILayout.Height(30)))
                {
                    AddNewSound();
                }
                
                GUI.enabled = true;
                
                if (GUILayout.Button("Clear", GUILayout.Width(80), GUILayout.Height(30)))
                {
                    ClearAddForm();
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawImportSection()
        {
            showImportSection = EditorGUILayout.Foldout(showImportSection, "📁 Import from Folder", true, EditorStyles.foldoutHeader);
            
            if (showImportSection)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.HelpBox("Import all audio files from a folder. Default path: Assets/{GameTemplateName}_Files/Game/Audio/Sounds", MessageType.Info);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Import from Default Path", GUILayout.Height(30)))
                {
                    ImportFromDefaultPath();
                }
                
                if (GUILayout.Button("Import from Custom Folder", GUILayout.Height(30)))
                {
                    ImportFromCustomFolder();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawSoundList()
        {
            EditorGUILayout.LabelField("Sound List", EditorStyles.boldLabel);
            
            List<AudioClipData> filteredClips = GetFilteredClips();
            
            if (filteredClips.Count == 0)
            {
                EditorGUILayout.HelpBox("No sounds found matching the filter.", MessageType.Info);
                return;
            }
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            for (int i = 0; i < filteredClips.Count; i++)
            {
                DrawSoundItem(filteredClips[i], i);
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawSoundItem(AudioClipData clipData, int index)
        {
            if (clipData == null) return;
            
            EditorGUILayout.BeginHorizontal();
            
            // Type icon
            string icon = GetSoundTypeIcon(clipData.soundType);
            GUILayout.Label(icon, GUILayout.Width(20));
            
            // Sound name
            GUILayout.Label(clipData.soundName, EditorStyles.boldLabel, GUILayout.Width(150));
            
            // Audio clip field
            AudioClip newClip = (AudioClip)EditorGUILayout.ObjectField(clipData.clip, typeof(AudioClip), false);
            if (newClip != clipData.clip)
            {
                clipData.clip = newClip;
                EditorUtility.SetDirty(audioData);
            }
            
            GUILayout.FlexibleSpace();
            
            // Type label
            GUILayout.Label(clipData.soundType.ToString(), EditorStyles.miniLabel, GUILayout.Width(80));
            
            // Remove button
            if (GUILayout.Button("✕", GUILayout.Width(20), GUILayout.Height(20)))
            {
                if (EditorUtility.DisplayDialog("Remove Sound", 
                    $"Are you sure you want to remove '{clipData.soundName}'?", 
                    "Yes", "No"))
                {
                    RemoveSound(clipData);
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private List<AudioClipData> GetFilteredClips()
        {
            if (audioData == null || audioData.audioClips == null)
                return new List<AudioClipData>();
            
            var clips = audioData.audioClips;
            
            // Filter by search
            if (!string.IsNullOrEmpty(searchFilter))
            {
                clips = clips.Where(x => x.soundName.ToLower().Contains(searchFilter.ToLower())).ToList();
            }
            
            // Filter by type
            if (!showAllTypes)
            {
                clips = clips.Where(x => x.soundType == filterType).ToList();
            }
            
            return clips;
        }

        private string GetSoundTypeIcon(SoundType type)
        {
            switch (type)
            {
                case SoundType.UI: return "🖱️";
                case SoundType.Gameplay: return "🎮";
                case SoundType.Ambient: return "🌊";
                case SoundType.Music: return "🎵";
                case SoundType.Special: return "✨";
                default: return "🔊";
            }
        }

        private void AddNewSound()
        {
            AudioClipData newClipData = new AudioClipData(newSoundName, newAudioClip, newSoundType)
            {
                description = newDescription
            };
            
            audioData.AddClip(newClipData);
            EditorUtility.SetDirty(audioData);
            AssetDatabase.SaveAssets();
            
            ClearAddForm();
            
            EditorUtility.DisplayDialog("Success", $"Sound '{newSoundName}' added successfully!", "OK");
        }

        private void ClearAddForm()
        {
            newSoundName = "";
            newAudioClip = null;
            newSoundType = SoundType.Gameplay;
            newDescription = "";
        }

        private void RemoveSound(AudioClipData clipData)
        {
            audioData.RemoveClip(clipData.soundName);
            EditorUtility.SetDirty(audioData);
            AssetDatabase.SaveAssets();
        }

        private void CreateAudioData()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create AudioData",
                "AudioData",
                "asset",
                "Create a new AudioData asset"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                AudioData newData = CreateInstance<AudioData>();
                AssetDatabase.CreateAsset(newData, path);
                AssetDatabase.SaveAssets();
                
                audioData = newData;
                
                EditorUtility.DisplayDialog("Success", "AudioData created successfully!", "OK");
            }
        }

        private void ImportFromDefaultPath()
        {
            // Find game template folders
            string[] directories = Directory.GetDirectories("Assets", "*_Files", SearchOption.TopDirectoryOnly);
            
            if (directories.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "No game template folder found (looking for *_Files pattern)", "OK");
                return;
            }
            
            string soundsPath = Path.Combine(directories[0], "Game", "Audio", "Sounds");
            
            if (!Directory.Exists(soundsPath))
            {
                if (EditorUtility.DisplayDialog("Folder Not Found", 
                    $"Sounds folder not found at {soundsPath}. Create it?", 
                    "Yes", "No"))
                {
                    Directory.CreateDirectory(soundsPath);
                    AssetDatabase.Refresh();
                }
                return;
            }
            
            ImportSoundsFromFolder(soundsPath);
        }

        private void ImportFromCustomFolder()
        {
            string path = EditorUtility.OpenFolderPanel("Select Sounds Folder", "Assets", "");
            
            if (!string.IsNullOrEmpty(path))
            {
                if (!path.StartsWith(Application.dataPath))
                {
                    EditorUtility.DisplayDialog("Error", "Please select a folder inside the Assets folder", "OK");
                    return;
                }
                
                string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                ImportSoundsFromFolder(relativePath);
            }
        }

        private void ImportSoundsFromFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                EditorUtility.DisplayDialog("Error", $"Folder not found: {folderPath}", "OK");
                return;
            }

            string[] audioFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".wav") || f.EndsWith(".mp3") || f.EndsWith(".ogg") || f.EndsWith(".aif") || f.EndsWith(".aiff"))
                .ToArray();
            
            if (audioFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("No Audio Files", "No audio files found in the selected folder", "OK");
                return;
            }
            
            int importedCount = 0;
            
            foreach (string filePath in audioFiles)
            {
                try
                {
                    // Fix the path conversion
                    string relativePath;
                    if (filePath.StartsWith(Application.dataPath))
                    {
                        relativePath = "Assets" + filePath.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        relativePath = filePath;
                    }

                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);
                    
                    if (clip != null)
                    {
                        string soundName = Path.GetFileNameWithoutExtension(filePath);
                        
                        if (!audioData.HasSound(soundName))
                        {
                            AudioClipData clipData = new AudioClipData(soundName, clip, SoundType.Gameplay);
                            audioData.AddClip(clipData);
                            importedCount++;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to import audio file: {filePath}. Error: {e.Message}");
                }
            }
            
            if (importedCount > 0)
            {
                EditorUtility.SetDirty(audioData);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Import Complete", $"Imported {importedCount} audio files", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Import Complete", "No new audio files to import (all already exist)", "OK");
            }
        }

        private void ShowClipLocation(AudioClip clip)
        {
            if (clip == null) return;
            
            // Ping the asset in the project window
            EditorGUIUtility.PingObject(clip);
            Selection.activeObject = clip;
        }
    }
}

