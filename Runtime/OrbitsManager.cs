using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace saccardi.lonelyboid
{
    public abstract class OrbitsManager<T> : ScriptableObject where T : MonoBehaviour
    {
        [NonSerialized] private readonly DualBuffer<Vector2> _orbitsBuffer = new();
        [NonSerialized] private CommandBuffer _commandBuffer;

        // Protected section -------------------------------------------------------------------------------------------

        protected abstract ComputeShader OrbitsShader { get; }
        public abstract bool RequestNewOrbits(T target, Rect window);

        [field: NonSerialized]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        protected int CurrentOrbitLength { get; private set; } = 1;

        protected void BufferPopulateOrbits(Rect window)
        {
            var density = orbitDensity;
            CurrentOrbitLength = orbitLength;
            var orbitsData = _orbitsBuffer.Resize(density * density * CurrentOrbitLength);

            if (CurrentOrbitLength == 0) return;

            var delta = new Vector2(window.width / density, window.height / density);
            var origin = window.min + delta * 0.5f;

            var orbitIndex = 0;
            for (var x = 0; x < density; ++x)
            {
                for (var y = 0; y < density; ++y)
                {
                    var position = origin + new Vector2(x, y) * delta;
                    orbitsData[orbitIndex++ * CurrentOrbitLength] = position;
                }
            }

            _orbitsBuffer.LocalToCompute();
        }

        protected void BufferBindOrbits()
        {
            _orbitsBuffer.Bind(OrbitsShader, 0, ShaderNames.IDOrbits);
            OrbitsShader.SetInt(ShaderNames.IDStride, CurrentOrbitLength);
        }

        protected void DispatchOrbits()
        {
            Debug.Assert(_orbitsBuffer.Count % CurrentOrbitLength == 0);
            var threadGroups = (_orbitsBuffer.Count / CurrentOrbitLength + 31) / 32;

            if (threadGroups == 0) return;

            _commandBuffer = new CommandBuffer();
            _commandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            _commandBuffer.DispatchCompute(OrbitsShader, 0, threadGroups, 1, 1);

            Graphics.ExecuteCommandBufferAsync(_commandBuffer, ComputeQueueType.Default);
            NeedsFetch = true;
        }

        // Public API --------------------------------------------------------------------------------------------------

        [SerializeField] public int orbitDensity = 20;
        [SerializeField] public int orbitLength = 5;

        [field: NonSerialized] public bool NeedsFetch { get; private set; }

        public Vector2[][] FetchOrbits()
        {
            // ReSharper disable once InvertIf
            if (NeedsFetch)
            {
                NeedsFetch = false;
                _orbitsBuffer.ComputeToLocal();
            }

            return _orbitsBuffer.Data
                .Select((entry, index) => new { index, entry })
                .GroupBy(entry => entry.index / CurrentOrbitLength)
                .Select(group => group.Select(grouping => grouping.entry).ToArray()).ToArray();
        }

        // Implementation and event responder --------------------------------------------------------------------------

        public virtual void Release()
        {
            _orbitsBuffer.Release();
        }

        protected virtual void Awake()
        {
            _commandBuffer = new CommandBuffer();
        }

        protected virtual void OnDestroy()
        {
            _orbitsBuffer.Release();
        }
    }
}