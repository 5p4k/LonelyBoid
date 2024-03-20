using System;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(BoidsContainer))]
    public class OldBoidsContainerEditor : UnityEditor.Editor
    {
        public static void VisualizationGUI(BoidsContainer container)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);

            var newOrbitLength = (uint)EditorGUILayout.IntField(ObjectNames.NicifyVariableName("orbitLength"),
                (int)container.orbitLength);

            var newOrbitDensity = (uint)EditorGUILayout.IntField(ObjectNames.NicifyVariableName("orbitDensity"),
                (int)container.orbitDensity);

            var newOrbitTimeStep = EditorGUILayout.FloatField(ObjectNames.NicifyVariableName("orbitTimeStep"),
                container.orbitTimeStep);

            var newLiveUpdate = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName("liveUpdate"),
                container.liveUpdate);

            if (newOrbitDensity == container.orbitDensity && newOrbitLength == container.orbitLength &&
                Math.Abs(newOrbitTimeStep - container.orbitTimeStep) < 0.001f &&
                newLiveUpdate == container.liveUpdate) return;

            container.orbitLength = newOrbitLength;
            container.orbitDensity = newOrbitDensity;
            container.orbitTimeStep = newOrbitTimeStep;
            container.liveUpdate = newLiveUpdate;
            container.RequestFlowFieldUpdate();
        }

        public static void MissingContainerGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("You must parent this to a game object with a BoidsContainer component.",
                MessageType.Warning);
        }

        public override void OnInspectorGUI()
        {
            var container = target as BoidsContainer;

            DrawDefaultInspector();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("New flock"))
            {
                GameObject unused = new("Flock " + (container!.FlockCount + 1), typeof(OldFlock))
                {
                    transform =
                    {
                        parent = container.transform
                    }
                };
            }

            if (GUILayout.Button("New force"))
            {
                GameObject unused = new("Force " + (container!.ForceCount + 1), typeof(Force))
                {
                    transform =
                    {
                        parent = container.transform
                    }
                };
            }

            EditorGUILayout.EndHorizontal();
        }
        
        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy)]
        private static void OnDrawGizmos(BoidsContainer container, GizmoType type)
        {
            if (!Camera.current || !container.shouldDisplayFlowField) return;

            Vector2 pxSize = Camera.current.ScreenToWorldPoint(new Vector3(1, 1, 0))
                             - Camera.current.ScreenToWorldPoint(Vector3.zero);

            var discRadius = 2.0f * Mathf.Max(pxSize.x, pxSize.y);

            for (uint i = 0; i < container.OrbitCount * container.orbitLength; ++i)
            {
                if (i % container.orbitLength == 0)
                {
                    Handles.DrawSolidDisc(container.OrbitBuffer[i], Vector3.back, discRadius);
                }
                else
                {
                    Handles.DrawLine(container.OrbitBuffer[i - 1], container.OrbitBuffer[i]);
                }
            }
        }
    }
}