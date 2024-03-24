using System;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class FlockOrbits : Orbits
    {
        [NonSerialized] private readonly DualBuffer<IO.BoidData> _boidsBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.FlockConfigData> _flockConfigBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.FlockDrivesData> _flockDrivesBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.ForceData> _forcesBuffer = new();

        [NonSerialized] private ComputeShader _orbitsShader;

        protected override ComputeShader OrbitsShader => _orbitsShader;

        // Public API --------------------------------------------------------------------------------------------------

        public float orbitTimeStep = 0.5f;
        public bool includeForces = true;

        public override void Release()
        {
            _boidsBuffer.Release();
            _flockConfigBuffer.Release();
            _flockDrivesBuffer.Release();
            _forcesBuffer.Release();
            base.Release();
        }

        public bool RequestNewOrbits(Flock flock, Rect window)
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

            _boidsBuffer.Bind(_orbitsShader, 0, Flock.IDBoids, Flock.IDBoidsCount);
            _flockDrivesBuffer.Bind(_orbitsShader, 0, Flock.IDFlockDrives);
            _flockConfigBuffer.Bind(_orbitsShader, 0, Flock.IDFlockConfig);
            _forcesBuffer.Bind(_orbitsShader, 0, Flock.IDForces, Flock.IDForcesCount);

            _orbitsShader.SetFloat(Flock.IDTime, Application.isPlaying ? Time.time : 0.0f);
            _orbitsShader.SetFloat(Flock.IDDeltaTime, orbitTimeStep);

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