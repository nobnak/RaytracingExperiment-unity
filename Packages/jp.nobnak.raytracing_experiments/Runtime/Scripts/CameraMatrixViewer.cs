using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CameraMatrixViewer : MonoBehaviour {


    
#if UNITY_EDITOR
    [CustomEditor(typeof(CameraMatrixViewer))]
    public class CameraMatrixViewerEditor : Editor {

        public override void OnInspectorGUI() {
            EditorGUILayout.LabelField("Camera Matrix Viewer");

            var viewer = (CameraMatrixViewer)target;
            var cam = viewer.GetComponent<Camera>();
            if (cam == null) {
                EditorGUILayout.LabelField("No Camera component found on this GameObject.");
                return;
            }

            var view = cam.worldToCameraMatrix;
            var proj = cam.projectionMatrix;
            var gpuProj = GL.GetGPUProjectionMatrix(proj, false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Camera View Matrix:");
            EditorGUILayout.TextArea(view.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Camera Projection Matrix:");
            EditorGUILayout.TextArea(proj.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("GPU Projection Matrix:");
            EditorGUILayout.TextArea(gpuProj.ToString());
        }
    }
#endif
}