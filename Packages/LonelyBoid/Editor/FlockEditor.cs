using System;
using UnityEditor;
using UnityEngine;

namespace saccardi.lonelyboid.Editor
{
    [CustomEditor(typeof(Flock))]
    public class FlockEditor : UnityEditor.Editor
    {
        [NonSerialized] private static FlockOrbits _flockOrbits;
        [NonSerialized] private static bool _orbitsDirty = true;
        [NonSerialized] private static Vector2[][] _orbits;
        [NonSerialized] private static Flock _lastTarget;
        [NonSerialized] private static Rect _lastWindow = Rect.zero;

        private static void HandleRadius(Component flock, ref float radius, string description)
        {
            EditorGUI.BeginChangeCheck();
            var newRadius = Handles.RadiusHandle(Quaternion.identity, flock.transform.position, radius, true);
            if (!EditorGUI.EndChangeCheck()) return;
            _orbitsDirty = true;
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

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            if (!EditorGUI.EndChangeCheck()) return;
            _orbitsDirty = true;
            serializedObject.ApplyModifiedProperties();
        }


        private void OnEnable()
        {
            if (!_flockOrbits) _flockOrbits = CreateInstance<FlockOrbits>();
            SceneView.duringSceneGui += OwnOnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OwnOnSceneGUI;
        }

        private void OwnOnSceneGUI(SceneView sv)
        {
            var flock = target as Flock;

            // Check if we are targeting a different flock
            if (_lastTarget != flock)
            {
                _lastTarget = flock;
                _orbitsDirty = true;
            }

            // Check if we have changed the camera location
            if (sv.camera)
            {
                var llc = sv.camera.ViewportToWorldPoint(new Vector3(0f, 0f, 0));
                var urc = sv.camera.ViewportToWorldPoint(new Vector3(1f, 1f, 0));
                var window = new Rect(llc, urc - llc);
                if (window != _lastWindow)
                {
                    _orbitsDirty = true;
                    _lastWindow = window;
                }
            }

            // Check if any change requires us to request new orbits
            if (flock && _orbitsDirty && _flockOrbits)
            {
                _orbitsDirty = !_flockOrbits.RequestNewOrbits(flock, _lastWindow);
            }

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

            // Check if we have any orbits pending to fetch, and if so, update
            if (_flockOrbits && _flockOrbits.NeedsFetch)
            {
                _orbits = _flockOrbits.FetchOrbits();
            }

            // Draw orbits
            if (_orbits is not { Length: > 0 }) return;

            Vector2 pxSize = Camera.current.ScreenToWorldPoint(new Vector3(1, 1, 0))
                             - Camera.current.ScreenToWorldPoint(Vector3.zero);

            var discRadius = 2.0f * Mathf.Max(pxSize.x, pxSize.y);
            foreach (var orbit in _orbits)
            {
                Handles.DrawSolidDisc(orbit[0], Vector3.back, discRadius);
                for (var i = 1; i < orbit.Length; ++i)
                {
                    Handles.DrawLine(orbit[i - 1], orbit[i]);
                }
            }
        }
    }
}