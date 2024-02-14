using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions;
using Debug = System.Diagnostics.Debug;


public enum ForceType
{
    Radial = 1,
    Turbulence = 2
}

public struct ForceData
{
    public uint type;
    public float intensity;
    public Vector2 position;
    public float falloffPower;
    public float spatialScale;
    public float temporalScale;
}


public class Force : MonoBehaviour
{
    [Header("Physics")] public ForceType type = ForceType.Radial;
    public float intensity = 1.0f;

    // Radial field only:
    [HideInInspector] public float falloffPower = 2.0f;

    // Turbulence field only
    [HideInInspector] public float spatialScale = 0.5f;
    [HideInInspector] public float temporalScale = 1.0f;

    public ForceData ToBufferData()
    {
        var retval = new ForceData();
        retval.type = enabled ? (uint)type : 0;
        retval.intensity = intensity;
        retval.position = transform.position;
        retval.falloffPower = falloffPower;
        retval.spatialScale = spatialScale;
        retval.temporalScale = temporalScale;
        return retval;
    }
}


#if UNITY_EDITOR
[CustomEditor(typeof(Force))]
public class ForceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var force = target as Force;
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
                    EditorGUILayout.FloatField(ObjectNames.NicifyVariableName("temporalScale"), force.temporalScale);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
#endif