#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BlockShooter
{
    [UnityEditor.CustomEditor(typeof(TutorialManager))]
    public class TutorialManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _canvasControllerProp;
        private SerializedProperty _tutorialsProp;
        private SerializedProperty _autoStartProp;

        private void OnEnable()
        {
            _canvasControllerProp = serializedObject.FindProperty("_canvasController");
            _tutorialsProp = serializedObject.FindProperty("_tutorials");
            _autoStartProp = serializedObject.FindProperty("_autoStartOnLevelLoad");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_canvasControllerProp);
            EditorGUILayout.PropertyField(_autoStartProp);

            EditorGUILayout.Space(8f);
            DrawTutorials();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTutorials()
        {
            EditorGUILayout.LabelField("Tutorials", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Tutorial"))
            {
                _tutorialsProp.arraySize++;
            }

            for (int i = 0; i < _tutorialsProp.arraySize; i++)
            {
                SerializedProperty tutorialProp = _tutorialsProp.GetArrayElementAtIndex(i);
                SerializedProperty levelProp = tutorialProp.FindPropertyRelative("_level");
                SerializedProperty showOnceProp = tutorialProp.FindPropertyRelative("_showOnce");
                SerializedProperty rootProp = tutorialProp.FindPropertyRelative("_tutorialRoot");
                SerializedProperty useStepsProp = tutorialProp.FindPropertyRelative("_useSteps");
                SerializedProperty targetIdProp = tutorialProp.FindPropertyRelative("_targetId");
                SerializedProperty handOffsetProp = tutorialProp.FindPropertyRelative("_handOffset");
                SerializedProperty stepsProp = tutorialProp.FindPropertyRelative("_steps");

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Tutorial {i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    _tutorialsProp.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(levelProp);
                EditorGUILayout.PropertyField(showOnceProp);
                EditorGUILayout.PropertyField(rootProp);
                EditorGUILayout.PropertyField(useStepsProp, new GUIContent("Use Steps"));

                if (!useStepsProp.boolValue)
                {
                    EditorGUILayout.PropertyField(targetIdProp, new GUIContent("Target Id"));
                    EditorGUILayout.PropertyField(handOffsetProp, new GUIContent("Hand Offset"));
                }
                else
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Steps", EditorStyles.boldLabel);

                    if (GUILayout.Button("Add Step"))
                    {
                        stepsProp.arraySize++;
                    }

                    for (int stepIndex = 0; stepIndex < stepsProp.arraySize; stepIndex++)
                    {
                        SerializedProperty stepProp = stepsProp.GetArrayElementAtIndex(stepIndex);
                        SerializedProperty stepRootProp = stepProp.FindPropertyRelative("_stepRoot");
                        SerializedProperty stepTargetIdProp = stepProp.FindPropertyRelative("_targetId");
                        SerializedProperty stepHandOffsetProp = stepProp.FindPropertyRelative("_handOffset");

                        EditorGUILayout.BeginVertical("helpbox");
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"Step {stepIndex + 1}", EditorStyles.boldLabel);
                        if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                        {
                            stepsProp.DeleteArrayElementAtIndex(stepIndex);
                            break;
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.PropertyField(stepRootProp, new GUIContent("Step Root"));
                        EditorGUILayout.PropertyField(stepTargetIdProp, new GUIContent("Target Id"));
                        EditorGUILayout.PropertyField(stepHandOffsetProp, new GUIContent("Hand Offset"));
                        EditorGUILayout.EndVertical();
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }
    }
}
#endif
