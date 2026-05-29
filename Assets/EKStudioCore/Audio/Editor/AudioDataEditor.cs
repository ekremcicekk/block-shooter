using UnityEngine;
using UnityEditor;

namespace EKStudio.Audio.Editor
{
    /// <summary>
    /// Custom inspector for AudioData
    /// </summary>
    [CustomEditor(typeof(AudioData))]
    public class AudioDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            AudioData audioData = (AudioData)target;

            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox($"Total Sounds: {audioData.audioClips.Count}", MessageType.Info);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Open Audio Settings", GUILayout.Height(35)))
            {
                AudioSettingsWindow.ShowWindow();
            }

            EditorGUILayout.Space(10);

            DrawDefaultInspector();
        }
    }
}

