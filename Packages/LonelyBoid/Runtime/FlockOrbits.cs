using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace saccardi.lonelyboid
{
    public class FlockOrbits : ScriptableObject
    {
        [NonSerialized] private int _currentOrbitLength = 1;

        [NonSerialized] private readonly DualBuffer<Vector2> _orbitsBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.BoidData> _boidsBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.FlockConfigData> _flockConfigBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.FlockDrivesData> _flockDrivesBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.ForceData> _forcesBuffer = new();

        [NonSerialized] private ComputeShader _orbitsShader;
        [NonSerialized] private CommandBuffer _commandBuffer;
        
        // Shader ID names ---------------------------------------------------------------------------------------------

        private static readonly int IDStride = Shader.PropertyToID("stride");
        private static readonly int IDOrbits = Shader.PropertyToID("orbits");
        
        // Public API --------------------------------------------------------------------------------------------------

        public int orbitDensity = 20;
        public float orbitTimeStep = 0.5f;
        public bool includeForces = true;

        [SerializeField] private int orbitLength = 5;

        public int OrbitLength
        {
            get => Math.Max(2, orbitLength);
            set => orbitLength = Math.Max(2, value);
        }

        [field: NonSerialized]
        public bool NeedsFetch { get; private set; }

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
                .GroupBy(entry => entry.index / _currentOrbitLength)
                .Select(group => group.Select(grouping => grouping.entry).ToArray()).ToArray();
        }

        public bool RequestNewOrbits(Flock flock, Rect window)
        {
            BufferPopulateOrbits(window);
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
            _orbitsShader.SetInt(IDStride, _currentOrbitLength);

            _orbitsBuffer.Bind(_orbitsShader, 0, IDOrbits);

            Debug.Assert(_orbitsBuffer.Count % _currentOrbitLength == 0);
            var threadGroups = (_orbitsBuffer.Count / _currentOrbitLength + 31) / 32;

            _commandBuffer = new CommandBuffer();
            _commandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            _commandBuffer.DispatchCompute(_orbitsShader, 0, threadGroups, 1, 1);

            Graphics.ExecuteCommandBufferAsync(_commandBuffer, ComputeQueueType.Default);
            NeedsFetch = true;
            return true;
        }

        // Implementation and event responder --------------------------------------------------------------------------

        private void Awake()
        {
            _orbitsShader = Resources.Load<ComputeShader>("Shaders/FlockOrbits");
            _commandBuffer = new CommandBuffer();
        }

        private void OnDestroy()
        {
            _orbitsBuffer.Release();
        }

        private void BufferPopulateOrbits(Rect window)
        {
            var density = orbitDensity;
            _currentOrbitLength = OrbitLength;
            var orbitsData = _orbitsBuffer.Resize(density * density * _currentOrbitLength);


            var delta = new Vector2(window.width / density, window.height / density);
            var origin = window.min + delta * 0.5f;

            var orbitIndex = 0;
            for (var x = 0; x < density; ++x)
            {
                for (var y = 0; y < density; ++y)
                {
                    var position = origin + new Vector2(x, y) * delta;
                    orbitsData[orbitIndex++ * _currentOrbitLength] = position;
                }
            }
            _orbitsBuffer.LocalToCompute();
        }
    }
}