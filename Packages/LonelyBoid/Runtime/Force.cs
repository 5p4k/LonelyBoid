using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace saccardi.lonelyboid
{
    namespace IO
    {
        [SuppressMessage("ReSharper", "NotAccessedField.Global")]
        internal struct TurbulentForce
        {
            public float spatialScale;
            public float temporalScale;
        };

        [SuppressMessage("ReSharper", "NotAccessedField.Global")]
        internal struct RadialForce
        {
            public float falloffPower;
        };


        [SuppressMessage("ReSharper", "NotAccessedField.Global")]
        internal struct ForceData
        {
            public Vector2 origin;
            public int type;
            public float intensity;

            public TurbulentForce turbulent;
            public RadialForce radial;

            public static ForceData From(Force force)
            {
                return new ForceData
                {
                    origin = force.transform.position,
                    type = (int)force.type,
                    intensity = force.intensity,
                    turbulent = new TurbulentForce
                    {
                        spatialScale = force.spatialScale,
                        temporalScale = force.temporalScale
                    },
                    radial = new RadialForce
                    {
                        falloffPower = force.falloffPower
                    }
                };
            }
        }
    }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum ForceType
    {
        Radial = 1,
        Turbulence = 2
    }

    public class Force : MonoBehaviour
    {
        [Header("Physics")] public ForceType type = ForceType.Radial;
        public float intensity = 1.0f;

        // Radial field only:
        public float falloffPower = 2.0f;

        // Turbulence field only
        public float spatialScale = 0.5f;
        public float temporalScale = 1.0f;
    }
}