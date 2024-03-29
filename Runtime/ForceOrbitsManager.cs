using System;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class ForceOrbitsManager : OrbitsManager<Force>
    {
        [NonSerialized] private readonly DualBuffer<IO.ForceData> _forceBuffer = new();

        [NonSerialized] private ComputeShader _orbitsShader;

        protected override ComputeShader OrbitsShader => _orbitsShader;

        // Public API --------------------------------------------------------------------------------------------------

        [SerializeField] public float orbitTimeStep = 0.5f;

        public override void Release()
        {
            _forceBuffer.Release();
            base.Release();
        }

        public override bool RequestNewOrbits(Force force, Rect window)
        {
            if (NeedsFetch) return false;

            _forceBuffer.Fill(new[] { IO.ForceData.From(force) });
            _forceBuffer.Bind(_orbitsShader, 0, ShaderNames.IDForces, true);

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
            _orbitsShader = Resources.Load<ComputeShader>("Shaders/ForceOrbits");
        }
    }
}