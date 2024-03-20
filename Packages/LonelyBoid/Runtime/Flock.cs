using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;


namespace saccardi.lonelyboid
{
    [Serializable]
    public struct AlienFlockDrives
    {
        public Flock flock;
        public float alignment;
        public float cohesion;
        public float separation;
    }

    [Serializable]
    public struct ForceWeightPair
    {
        public Force force;
        public float weight;
    }

    namespace IO
    {
        [SuppressMessage("ReSharper", "NotAccessedField.Global")]
        internal struct FlockDrivesData
        {
            public float alignment;
            public float cohesion;
            public float separation;

            public static FlockDrivesData From(AlienFlockDrives drives)
            {
                return new FlockDrivesData
                {
                    alignment = drives.alignment,
                    cohesion = drives.cohesion,
                    separation = drives.separation
                };
            }

            public static FlockDrivesData From(Flock flock)
            {
                return new FlockDrivesData
                {
                    alignment = flock.alignmentDrive,
                    cohesion = flock.cohesionDrive,
                    separation = flock.separationDrive
                };
            }
        }

        [SuppressMessage("ReSharper", "NotAccessedField.Global")]
        internal struct FlockConfigData
        {
            public Vector2 origin;

            public float spawnAtRadius;
            public float killAtRadius;

            public float survivalDrive;

            public float viewRadius;
            public float viewAngleTau;

            public float avoidRadius;
            public float avoidAngleTau;

            public float minSpeed;
            public float maxSpeed;
            public float maxAcceleration;
            public float maxAngularSpeedTau;

            public static FlockConfigData From(Flock flock)
            {
                return new FlockConfigData
                {
                    origin = flock.transform.position,
                    spawnAtRadius = flock.spawnAtRadius,
                    killAtRadius = flock.killAtRadius,
                    survivalDrive = flock.survivalDrive,
                    viewRadius = flock.viewRadius,
                    viewAngleTau = flock.viewAngleTau,
                    avoidRadius = flock.avoidRadius,
                    avoidAngleTau = flock.avoidAngleTau,
                    minSpeed = flock.minSpeed,
                    maxSpeed = flock.maxSpeed,
                    maxAcceleration = flock.maxAcceleration,
                    maxAngularSpeedTau = flock.maxAngularSpeedTau
                };
            }
        }
    }

    internal class ByID : IComparer<Boid>
    {
        public int Compare(Boid x, Boid y)
        {
            var xId = x ? x.GetInstanceID() : 0;
            var yId = y ? y.GetInstanceID() : 0;
            return xId - yId;
        }
    }

    public class Flock : MonoBehaviour
    {
        [Header("General settings")] public Boid boidBlueprint;
        public uint capacity = 40;

        [Header("Life & death")] public float spawnAtRadius = 7f;
        public float killAtRadius = 10f;
        public float spawnFrequency = 3f;
        public uint spawnMaxCount = 40;

        [Header("Behaviour")] public float alignmentDrive = 3.0f;
        public float cohesionDrive = 4.0f;
        public float separationDrive = 2.0f;
        public float survivalDrive = 10.0f;

        [Header("Dynamics")] public float minSpeed = 0.1f;
        public float maxSpeed = 3.0f;
        public float maxAcceleration = 2.0f;
        [Range(0.0f, 1.0f)] public float maxAngularSpeedTau = 0.5f;

        [Header("Perception")] public float viewRadius = 4.0f;
        [Range(0.0f, 1.0f)] public float viewAngleTau = 0.5f;
        public float avoidRadius = 2f;
        [Range(0.0f, 1.0f)] public float avoidAngleTau = 0.8f;

        [Header("Interactions")] public List<AlienFlockDrives> alienFlocks = new();
        public List<ForceWeightPair> forces = new();

        // Private members ---------------------------------------------------------------------------------------------

        // Note: SortedSets so that we can have consistent ordering
        [NonSerialized] private readonly SortedSet<Boid> _activeBoids = new(new ByID());
        [NonSerialized] private readonly SortedSet<Boid> _spawnQueue = new(new ByID());
        [NonSerialized] private readonly SortedSet<Boid> _killQueue = new(new ByID());
        [NonSerialized] private float _lastSpawnTime = float.NegativeInfinity;
        [NonSerialized] private ObjectPool<Boid> _boidsPool;

        [NonSerialized] private ComputeShader _updateShader;

        [NonSerialized] private readonly DualBuffer<IO.BoidData> _boidsBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.FlockConfigData> _flockConfigBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.FlockDrivesData> _flockDrivesBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.ForceData> _forcesBuffer = new();

        // Compute shader names ----------------------------------------------------------------------------------------
        private static readonly int IDFlockConfig = Shader.PropertyToID("flockConfig");
        private static readonly int IDBoids = Shader.PropertyToID("boids");
        private static readonly int IDBoidsCount = Shader.PropertyToID("boidsCount");
        private static readonly int IDForces = Shader.PropertyToID("forces");
        private static readonly int IDForcesCount = Shader.PropertyToID("forcesCount");
        private static readonly int IDFlockDrives = Shader.PropertyToID("flockDrives");
        private static readonly int IDTime = Shader.PropertyToID("time");
        private static readonly int IDDeltaTime = Shader.PropertyToID("deltaTime");

        // Events ------------------------------------------------------------------------------------------------------

        private void Start()
        {
            _boidsPool = new ObjectPool<Boid>(
                _poolCreateBoid,
                _poolSetupBoid,
                _poolReleaseBoid,
                _poolDestroyBoid,
                true,
                (int)spawnMaxCount,
                (int)capacity
            );
            _updateShader = Instantiate(Resources.Load<ComputeShader>("Shaders/FlockUpdate"));
        }

        private void OnDestroy()
        {
            _boidsPool.Dispose();
            _boidsBuffer.Release();
        }

        private void Update()
        {
            _bufferPopulateConfig();
            _bufferPopulateFlockBoids();
            _bufferPopulateForces();
            _dispatchUpdate();
        }

        private void LateUpdate()
        {
            _applyUpdate();
        }

        // Buffer management -------------------------------------------------------------------------------------------

        private int _bufferCountBoids()
        {
            return _activeBoids.Count 
                   + alienFlocks.Sum(interactionData => interactionData.flock._activeBoids.Count);
        }

        private void _bufferPopulateFlockBoids()
        {
            var flockDrivesData = _flockDrivesBuffer.Resize(alienFlocks.Count + 1);
            var boidData = _boidsBuffer.Resize(_bufferCountBoids());

            var boidIndex = 0;
            foreach (var boid in _activeBoids)
            {
                boidData[boidIndex++] = IO.BoidData.From(boid, 0);
            }

            var flockIndex = 0;
            flockDrivesData[flockIndex++] = IO.FlockDrivesData.From(this);
            
            foreach (var interactionData in alienFlocks)
            {
                flockDrivesData[flockIndex++] = IO.FlockDrivesData.From(interactionData);

                foreach (var boid in interactionData.flock._activeBoids)
                {
                    boidData[boidIndex++] = IO.BoidData.From(boid, flockIndex);
                }
            }
            
            _flockDrivesBuffer.LocalToCompute();
            _boidsBuffer.LocalToCompute();
        }

        private void _bufferPopulateConfig()
        {
            _flockConfigBuffer.Fill(new[] { IO.FlockConfigData.From(this) });
            _flockConfigBuffer.LocalToCompute();
        }

        private void _bufferPopulateForces()
        {
            var forcesData = _forcesBuffer.Resize(forces.Count);
            var forceIndex = 0;
            foreach (var forceWeight in forces)
            {
                forcesData[forceIndex++] = IO.ForceData.From(forceWeight.force, forceWeight.weight);
            }
            _forcesBuffer.LocalToCompute();
        }

        private void _dispatchUpdate()
        {
            _boidsBuffer.Bind(_updateShader, 0, IDBoids, IDBoidsCount);
            _flockDrivesBuffer.Bind(_updateShader, 0, IDFlockDrives);
            _flockConfigBuffer.Bind(_updateShader, 0, IDFlockConfig);
            _forcesBuffer.Bind(_updateShader, 0, IDForces, IDForcesCount);
            _updateShader.SetFloat(IDTime, Time.time);
            _updateShader.SetFloat(IDDeltaTime, Time.deltaTime);

            var threadGroups = (_boidsBuffer.Count + 31) / 32;
            
            _updateShader.Dispatch(0, threadGroups, 1, 1);
        }

        private void _applyUpdate()
        {
            _boidsBuffer.ComputeToLocal();
    
            var boidData = _boidsBuffer.Data;
            var boidIndex = 0;
            foreach (var boid in _activeBoids)
            {
                boidData[boidIndex++].ApplyTo(boid);
            }
        }

        // Boids management methods ------------------------------------------------------------------------------------

        private Boid _poolCreateBoid()
        {
#if UNITY_EDITOR
            var instance = PrefabUtility.InstantiatePrefab(boidBlueprint, transform) as GameObject;
#else
            var instance = Instantiate(boidBlueprint, transform) as GameObject;
#endif
            if (!instance!.TryGetComponent<Boid>(out var boid))
            {
                throw new MissingComponentException("Boid");
            }

            return boid;
        }

        private void _poolSetupBoid(Boid boid)
        {
            boid.flock = this;

            // Randomize position and orientation
            Vector3 deltaPos = spawnAtRadius * Random.insideUnitCircle.normalized;
            boid.transform.position = transform.position + deltaPos;
            boid.transform.up = -(Quaternion.AngleAxis(Random.Range(-90, +90), Vector3.back) * deltaPos.normalized);
            boid.speed = minSpeed + Random.value * (maxSpeed - minSpeed);

            _activeBoids.Add(boid);
            boid.gameObject.SetActive(true);
        }

        private void _poolReleaseBoid(Boid boid)
        {
            boid.gameObject.SetActive(false);
            _activeBoids.Remove(boid);
            boid.flock = null;
        }

        private void _poolDestroyBoid(Boid boid)
        {
            _poolReleaseBoid(boid);
            Destroy(boid.gameObject);
        }
    }
}