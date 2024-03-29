using System;
using UnityEditor;
using UnityEngine;

namespace saccardi.lonelyboid.Editor
{
    [CustomEditor(typeof(Force))]
    public class ForceEditor : EditorWithOrbits<Force, ForceOrbitsManager>
    {
        public override void OnInspectorGUI()
        {
            var force = target as Force;
            Debug.Assert(force);

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Physics", EditorStyles.boldLabel);
            force.type = (ForceType)EditorGUILayout.EnumPopup(ObjectNames.NicifyVariableName("type"), force.type);
            force.intensity = EditorGUILayout.FloatField(ObjectNames.NicifyVariableName("intensity"), force.intensity);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(force.type.ToString(), EditorStyles.boldLabel);

            switch (force.type)
            {
                case ForceType.Radial:
                    force.falloffPower =
                        EditorGUILayout.FloatField(ObjectNames.NicifyVariableName("falloffPower"), force.falloffPower);
                    break;
                case ForceType.Turbulence:
                    force.spatialScale =
                        EditorGUILayout.FloatField(ObjectNames.NicifyVariableName("spatialScale"), force.spatialScale);
                    force.temporalScale =
                        EditorGUILayout.FloatField(ObjectNames.NicifyVariableName("temporalScale"),
                            force.temporalScale);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (EditorGUI.EndChangeCheck())
            {
                OrbitsDirty = true;
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space();
            base.OnInspectorGUI();
        }

        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy | GizmoType.Pickable)]
        public new static void OnDrawGizmos(Force forTarget, GizmoType gizmoType)
        {
            EditorWithOrbits<Force, ForceOrbitsManager>.OnDrawGizmos(forTarget, gizmoType);
        }
    }
}