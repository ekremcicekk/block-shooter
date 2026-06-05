#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlockShooter.Editor
{
    [CustomEditor(typeof(ColorRegistryConfig))]
    public class ColorRegistryConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw everything except 'colors'
            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "colors") continue;
                if (prop.name == "m_Script")
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.PropertyField(prop);
                    EditorGUI.EndDisabledGroup();
                    continue;
                }
                EditorGUILayout.PropertyField(prop, true);
            }

            var config = (ColorRegistryConfig)target;

            GUILayout.Space(15);
            EditorGUILayout.LabelField("Colors & Materials Registry", EditorStyles.boldLabel);
            
            // Draw list
            if (config.colors == null) config.colors = new List<ColorRegistryConfig.ColorDefinition>();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header Row
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Enum Slot", EditorStyles.miniBoldLabel, GUILayout.Width(75));
            GUILayout.Label("Display Name", EditorStyles.miniBoldLabel, GUILayout.Width(110));
            GUILayout.Label("Editor Color", EditorStyles.miniBoldLabel, GUILayout.Width(75));
            GUILayout.Label("Material", EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
            GUILayout.Label("", EditorStyles.miniBoldLabel, GUILayout.Width(24));
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));

            int toRemove = -1;
            for (int i = 0; i < config.colors.Count; i++)
            {
                var def = config.colors[i];
                if (def == null) continue;

                EditorGUILayout.BeginHorizontal();

                // 1. Enum slot (Read-only)
                string slotName = def.colorType.ToString();
                GUILayout.Label(slotName, EditorStyles.miniLabel, GUILayout.Width(75));

                // 2. Display Name
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.TextField(def.displayName, GUILayout.Width(110));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(config, "Change Color Name");
                    def.displayName = newName;
                    EditorUtility.SetDirty(config);
                }

                // 3. Editor Color
                EditorGUI.BeginChangeCheck();
                Color newColor = EditorGUILayout.ColorField(def.editorColor, GUILayout.Width(75));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(config, "Change Color Value");
                    def.editorColor = newColor;
                    EditorUtility.SetDirty(config);
                }

                // 4. Material
                EditorGUI.BeginChangeCheck();
                var newMat = (Material)EditorGUILayout.ObjectField(def.material, typeof(Material), false, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(config, "Change Color Material");
                    def.material = newMat;
                    EditorUtility.SetDirty(config);
                }

                // 5. Delete button
                bool isDefaultColor = (int)def.colorType >= 1 && (int)def.colorType <= 6;
                if (isDefaultColor)
                {
                    // Lock default colors from being deleted
                    EditorGUI.BeginDisabledGroup(true);
                    GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18));
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    GUI.backgroundColor = new Color(.9f, .3f, .3f);
                    if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
                    {
                        toRemove = i;
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (toRemove >= 0)
            {
                Undo.RecordObject(config, "Remove Custom Color");
                config.colors.RemoveAt(toRemove);
                EditorUtility.SetDirty(config);
            }

            GUILayout.Space(8);

            // Add New Color Button
            GUI.backgroundColor = new Color(.3f, .7f, .3f);
            if (GUILayout.Button("Add New Custom Color", GUILayout.Height(24)))
            {
                BlockColorType nextSlot = FindNextUnusedCustomSlot(config);
                if (nextSlot == BlockColorType.None)
                {
                    EditorUtility.DisplayDialog("Registry Full", "All 10 Custom color slots are currently in use! Remove an existing custom color first.", "OK");
                }
                else
                {
                    Undo.RecordObject(config, "Add Custom Color");
                    config.colors.Add(new ColorRegistryConfig.ColorDefinition
                    {
                        colorType = nextSlot,
                        displayName = "New Color",
                        editorColor = Color.white,
                        material = null
                    });
                    EditorUtility.SetDirty(config);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        private BlockColorType FindNextUnusedCustomSlot(ColorRegistryConfig config)
        {
            // Custom slots: Custom1 (7) to Custom10 (16)
            for (int i = 7; i <= 16; i++)
            {
                var type = (BlockColorType)i;
                if (!config.colors.Exists(x => x != null && x.colorType == type))
                {
                    return type;
                }
            }
            return BlockColorType.None;
        }
    }
}
#endif
