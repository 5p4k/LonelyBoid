using UnityEngine;

namespace saccardi.lonelyboid
{
    public sealed class ShaderNames
    {
        public static readonly int IDOrbits = Shader.PropertyToID("orbits");
        public static readonly int IDStride = Shader.PropertyToID("stride");
        public static readonly int IDFlockConfig = Shader.PropertyToID("flockConfig");
        public static readonly int IDBoids = Shader.PropertyToID("boids");
        public static readonly int IDBoidsCount = Shader.PropertyToID("boidsCount");
        public static readonly int IDForces = Shader.PropertyToID("forces");
        public static readonly int IDForcesCount = Shader.PropertyToID("forcesCount");
        public static readonly int IDFlockDrives = Shader.PropertyToID("flockDrives");
        public static readonly int IDTime = Shader.PropertyToID("time");
        public static readonly int IDDeltaTime = Shader.PropertyToID("deltaTime");

    }
}
