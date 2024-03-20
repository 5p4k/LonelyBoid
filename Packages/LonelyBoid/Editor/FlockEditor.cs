using UnityEditor;
using UnityEngine;

namespace saccardi.lonelyboid.Editor
{
    [CustomEditor(typeof(Flock))]
    public class FlockEditor : UnityEditor.Editor
    {
        private static void HandleRadius(Component flock, ref float radius, string description)
        {
            EditorGUI.BeginChangeCheck();
            var newRadius = Handles.RadiusHandle(Quaternion.identity, flock.transform.position, radius, true);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(flock, description);
            radius = newRadius;
        }

        public override void OnInspectorGUI()
        {
            var flock = target as Flock;
            if (flock && !flock.boidBlueprint)
            {
                EditorGUILayout.HelpBox("Missing Boid blueprint.",
                    MessageType.Warning);
                EditorGUILayout.Space();
            }

            DrawDefaultInspector();
        }

        public void OnSceneGUI()
        {
            var flock = target as Flock;

            using (new Handles.DrawingScope(Color.green))
            {
                HandleRadius(flock, ref flock!.spawnAtRadius, "Change spawn radius");
            }

            using (new Handles.DrawingScope(Color.red))
            {
                HandleRadius(flock, ref flock!.killAtRadius, "Change kill radius");
            }
        }


        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy | GizmoType.Pickable)]
        public static void OnDrawGizmos(Flock flock, GizmoType gizmoType)
        {
            var active = (gizmoType & GizmoType.Active) != 0;

            using (new Handles.DrawingScope(active ? Color.green : Handles.color))
            {
                Handles.DrawWireDisc(flock.transform.position, Vector3.back, flock.spawnAtRadius);
            }

            using (new Handles.DrawingScope(active ? Color.red : Handles.color))
            {
                Handles.DrawWireDisc(flock.transform.position, Vector3.back, flock.killAtRadius);
            }
        }
    }
}