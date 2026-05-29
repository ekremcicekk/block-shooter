#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BlockShooter
{
    [CustomEditor(typeof(ConveyorTrackMesh))]
    public class ConveyorTrackMeshEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Rebuild Mesh Now", GUILayout.Height(32)))
            {
                var t = (ConveyorTrackMesh)target;
                t.Rebuild();
                EditorUtility.SetDirty(t);
            }
        }
    }
}
#endif
