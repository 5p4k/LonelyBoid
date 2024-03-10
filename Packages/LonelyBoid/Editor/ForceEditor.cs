using System;
using UnityEngine;
using UnityEditor;

namespace Editor
{
    [CustomEditor(typeof(OldForce))]
    public class ForceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var force = target as OldForce;
            Debug.Assert(force);

            DrawDefaultInspector();

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

            var container = BoidsContainer.FindParent(force.gameObject);
            if (!container)
            {
                BoidsContainerEditor.MissingContainerGUI();
            }
            else
            {
                BoidsContainerEditor.VisualizationGUI(container);
            }
        }
    }
}