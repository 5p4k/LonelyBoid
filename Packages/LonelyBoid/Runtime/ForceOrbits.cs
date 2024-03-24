using System;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class ForceOrbits : Orbits
    {
        [NonSerialized] private readonly DualBuffer<IO.ForceData> _forceBuffer = new();

        [NonSerialized] private ComputeShader _orbitsShader;

        protected override ComputeShader OrbitsShader => _orbitsShader;

        // Public API --------------------------------------------------------------------------------------------------

        public float orbitTimeStep = 0.5f;

        public override void Release()
        {
            _forceBuffer.Release();
            base.Release();
        }

        public bool RequestNewOrbits(Force force, Rect window)
        {
            if (NeedsFetch) return false;

            _forceBuffer.Fill(new IO.ForceData[] { IO.ForceData.From(force) });
            _forceBuffer.Bind(_orbitsShader, 0, Flock.IDForces);

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
            _orbitsShader = Resources.Load<ComputeShader>("Shaders/ForceOrbits");
        }
    }
}
