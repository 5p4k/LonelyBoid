using System;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class FlockOrbitsManager : OrbitsManager<Flock>
    {
        [NonSerialized] private readonly DualBuffer<IO.BoidData> _boidsBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.FlockConfigData> _flockConfigBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.FlockDrivesData> _flockDrivesBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.ForceData> _forcesBuffer = new();

        [NonSerialized] private ComputeShader _orbitsShader;

        protected override ComputeShader OrbitsShader => _orbitsShader;

        // Public API --------------------------------------------------------------------------------------------------

        [SerializeField] public float orbitTimeStep = 0.5f;
        [SerializeField] public bool includeForces = true;

        public override void Release()
        {
            _boidsBuffer.Release();
            _flockConfigBuffer.Release();
            _flockDrivesBuffer.Release();
            _forcesBuffer.Release();
            base.Release();
        }

        public override bool RequestNewOrbits(Flock flock, Rect window)
        {
            if (NeedsFetch) return false;

            flock.BufferPopulateConfig(_flockConfigBuffer);
            flock.BufferPopulateFlockBoids(_boidsBuffer, _flockDrivesBuffer);
            if (includeForces)
            {
                flock.BufferPopulateForces(_forcesBuffer);
            }
            else
            {
                _forcesBuffer.Fill(new IO.ForceData[] { });
            }

            _boidsBuffer.Bind(_orbitsShader, 0, ShaderNames.IDBoids, ShaderNames.IDBoidsCount);
            _flockDrivesBuffer.Bind(_orbitsShader, 0, ShaderNames.IDFlockDrives);
            _flockConfigBuffer.Bind(_orbitsShader, 0, ShaderNames.IDFlockConfig);
            _forcesBuffer.Bind(_orbitsShader, 0, ShaderNames.IDForces, ShaderNames.IDForcesCount);

            _orbitsShader.SetFloat(ShaderNames.IDTime, Application.isPlaying ? Time.time : 0.0f);
            _orbitsShader.SetFloat(ShaderNames.IDDeltaTime, orbitTimeStep);

            BufferPopulateOrbits(window);
            BufferBindOrbits();
            DispatchOrbits();

            return true;
        }

        // Implementation and event responder --------------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();
            _orbitsShader = Resources.Load<ComputeShader>("Shaders/FlockOrbits");
        }
    }
}