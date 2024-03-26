using System;
using UnityEditor;
using UnityEngine;

namespace saccardi.lonelyboid.Editor
{
    [CustomEditor(typeof(Flock))]
    public class FlockEditor : EditorWithOrbits<Flock, FlockOrbitsManager>
    {
        private static void HandleRadius(Component flock, ref float radius, string description)
        {
            EditorGUI.BeginChangeCheck();
            var newRadius = Handles.RadiusHandle(Quaternion.identity, flock.transform.position, radius, true);
            if (!EditorGUI.EndChangeCheck()) return;
            OrbitsDirty = true;
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

            if (DrawDefaultInspector())
            {
                OrbitsDirty = true;
            }

            EditorGUILayout.Space();
            base.OnInspectorGUI();
        }

        protected override void OnSceneGUI()
        {
            base.OnSceneGUI();
            var flock = target as Flock;

            // Draw handles
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
        public new static void OnDrawGizmos(Flock forTarget, GizmoType gizmoType)
        {
            EditorWithOrbits<Flock, FlockOrbitsManager>.OnDrawGizmos(forTarget, gizmoType);
        }

        protected override void DrawGizmos(Flock forTarget, GizmoType gizmoType)
        {
            var active = (gizmoType & GizmoType.Active) != 0;

            using (new Handles.DrawingScope(active ? Color.green : Handles.color))
            {
                Handles.DrawWireDisc(forTarget.transform.position, Vector3.back, forTarget.spawnAtRadius);
            }

            using (new Handles.DrawingScope(active ? Color.red : Handles.color))
            {
                Handles.DrawWireDisc(forTarget.transform.position, Vector3.back, forTarget.killAtRadius);
            }
        }
    }
}