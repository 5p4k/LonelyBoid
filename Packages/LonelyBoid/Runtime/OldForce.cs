using System.Diagnostics.CodeAnalysis;
using UnityEngine;


[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum ForceType
{
    Radial = 1,
    Turbulence = 2
}

[SuppressMessage("ReSharper", "NotAccessedField.Global")]
public struct ForceData
{
    public uint Type;
    public float Intensity;
    public Vector2 Position;
    public float FalloffPower;
    public float SpatialScale;
    public float TemporalScale;
}


public class OldForce : MonoBehaviour
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
        return new ForceData
        {
            Type = enabled ? (uint)type : 0,
            Intensity = intensity,
            Position = transform.position,
            FalloffPower = falloffPower,
            SpatialScale = spatialScale,
            TemporalScale = temporalScale
        };
    }
}