using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
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
        public struct FlockDrivesData
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
        public struct FlockConfigData
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
        public bool useKinematics;

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
        [NonSerialized] private CommandBuffer _commandBuffer;

        [NonSerialized] private readonly DualBuffer<IO.BoidData> _boidsBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.FlockConfigData> _flockConfigBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.FlockDrivesData> _flockDrivesBuffer = new();
        [NonSerialized] private readonly DualBuffer<IO.ForceData> _forcesBuffer = new();

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
            _commandBuffer = new CommandBuffer();
        }

        private void OnDestroy()
        {
            _boidsPool.Dispose();
            _boidsBuffer.Release();
            _flockConfigBuffer.Release();
            _flockDrivesBuffer.Release();
            _forcesBuffer.Release();
        }

        private void FixedUpdate()
        {
            if (!useKinematics) return;
            // Do a full update cycle
            BufferPopulateConfig(_flockConfigBuffer);
            BufferPopulateFlockBoids(_boidsBuffer, _flockDrivesBuffer);
            BufferPopulateForces(_forcesBuffer);
            _dispatchUpdate();
            _applyUpdate();
        }

        private void Update()
        {
            SpawnIfNeeded();

            // Accept newly spawned boids
            _activeBoids.UnionWith(_spawnQueue);
            _spawnQueue.Clear();

            // Early update if not using kinematics
            if (useKinematics) return;
            BufferPopulateConfig(_flockConfigBuffer);
            BufferPopulateFlockBoids(_boidsBuffer, _flockDrivesBuffer);
            BufferPopulateForces(_forcesBuffer);
            _dispatchUpdate();
        }

        private void LateUpdate()
        {
            // Late apply if not using kinematics
            if (!useKinematics) _applyUpdate();

            // Kill stray and pending boids
            KillStrayBoids();
            _activeBoids.ExceptWith(_killQueue);
            _killQueue.Clear();
        }

        // Buffer management -------------------------------------------------------------------------------------------

        private int _bufferCountBoids()
        {
            return _activeBoids.Count
                   + alienFlocks.Sum(interactionData =>
                       interactionData.flock ? interactionData.flock._activeBoids.Count : 0);
        }

        public void BufferPopulateFlockBoids(DualBuffer<IO.BoidData> boidsBuffer,
            DualBuffer<IO.FlockDrivesData> flockDrivesBuffer)
        {
            var flockDrivesData = flockDrivesBuffer.Resize(alienFlocks.Count + 1);
            var boidData = boidsBuffer.Resize(_bufferCountBoids());

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

                if (!interactionData.flock) continue;

                foreach (var boid in interactionData.flock._activeBoids)
                {
                    boidData[boidIndex++] = IO.BoidData.From(boid, flockIndex - 1);
                }
            }

            Debug.Assert(boidIndex == boidData.Count);
            Debug.Assert(flockIndex == flockDrivesData.Count);

            flockDrivesBuffer.LocalToCompute();
            boidsBuffer.LocalToCompute();
        }

        public void BufferPopulateConfig(DualBuffer<IO.FlockConfigData> flockConfigBuffer)
        {
            flockConfigBuffer.Fill(new[] { IO.FlockConfigData.From(this) });
            flockConfigBuffer.LocalToCompute();
        }

        internal void BufferPopulateForces(DualBuffer<IO.ForceData> forcesBuffer)
        {
            var forcesData = forcesBuffer.Resize(forces.Count);
            var forceIndex = 0;
            foreach (var forceWeight in forces)
            {
                forcesData[forceIndex++] = IO.ForceData.From(forceWeight.force, forceWeight.weight);
            }

            forcesBuffer.LocalToCompute();
        }

        private void _dispatchUpdate()
        {
            _boidsBuffer.Bind(_updateShader, 0, ShaderNames.IDBoids, ShaderNames.IDBoidsCount);
            _flockDrivesBuffer.Bind(_updateShader, 0, ShaderNames.IDFlockDrives);
            _flockConfigBuffer.Bind(_updateShader, 0, ShaderNames.IDFlockConfig);
            _forcesBuffer.Bind(_updateShader, 0, ShaderNames.IDForces, ShaderNames.IDForcesCount);
            _updateShader.SetFloat(ShaderNames.IDTime, useKinematics ? Time.fixedTime : Time.time);
            _updateShader.SetFloat(ShaderNames.IDDeltaTime, useKinematics ? Time.fixedDeltaTime : Time.deltaTime);

            var threadGroups = (_activeBoids.Count + 31) / 32;
            
            if (threadGroups == 0) return;

            _commandBuffer.Clear();
            _commandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            _commandBuffer.DispatchCompute(_updateShader, 0, threadGroups, 1, 1);
            Graphics.ExecuteCommandBufferAsync(_commandBuffer, ComputeQueueType.Default);
        }

        private void _applyUpdate()
        {
            _boidsBuffer.ComputeToLocal();

            var boidData = _boidsBuffer.Data;
            var boidIndex = 0;
            foreach (var boid in _activeBoids)
            {
                Debug.Assert(boidData[boidIndex].flockIndex <= 0);
                boidData[boidIndex++].ApplyTo(boid);
            }

            Debug.Assert(boidIndex >= boidData.Count || boidData[boidIndex].flockIndex != 0);
        }

        // Boids management methods ------------------------------------------------------------------------------------

        private Boid _poolCreateBoid()
        {
            if (!boidBlueprint)
            {
                throw new NullReferenceException("Boid blueprint must not be null.");
            }
#if UNITY_EDITOR
            var boid = PrefabUtility.InstantiatePrefab(boidBlueprint, transform) as Boid;
#else
            var boid = Instantiate(boidBlueprint, transform) as Boid;
#endif
            return boid;
        }

        private void _poolSetupBoid(Boid boid)
        {
            if (boid.flock != null)
            {
                throw new ArgumentException("This boid is not inactive.");
            }

            boid.flock = this;

            // Randomize position and orientation
            Vector3 deltaPos = spawnAtRadius * Random.insideUnitCircle.normalized;
            var position = transform.position + deltaPos;
            var direction = -(Quaternion.AngleAxis(Random.Range(-90, +90), Vector3.back) * deltaPos.normalized);
            var speed = minSpeed + Random.value * (maxSpeed - minSpeed);
            boid.ApplyChange(position, direction, speed);

            boid.gameObject.SetActive(true);

            _spawnQueue.Add(boid);
            _killQueue.Remove(boid);
        }

        private void _poolReleaseBoid(Boid boid)
        {
            if (boid.flock != this)
            {
                throw new ArgumentException("This boid is not active on this flock.");
            }

            boid.gameObject.SetActive(false);
            boid.flock = null;

            _spawnQueue.Remove(boid);
            _killQueue.Add(boid);
        }

        private void _poolDestroyBoid(Boid boid)
        {
            boid.gameObject.SetActive(false);
            boid.flock = null;
            _spawnQueue.Remove(boid);
            _killQueue.Remove(boid);
            _activeBoids.Remove(boid);
            Destroy(boid.gameObject);
        }

        // Public methods ----------------------------------------------------------------------------------------------

        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public IEnumerable<Boid> Boids => _activeBoids
            .Where(boid => !_killQueue.Contains(boid) && !_spawnQueue.Contains(boid)).Concat(_spawnQueue);

        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public Boid Spawn()
        {
            return _boidsPool.Get();
        }

        public void Kill(Boid boid)
        {
            _boidsPool.Release(boid);
        }

        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public bool IsStray(Boid boid)
        {
            var radius = Vector3.Distance(boid.transform.position, transform.position);
            return radius < killAtRadius == killAtRadius < spawnAtRadius;
        }

        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public Boid[] KillStrayBoids()
        {
            var strayBoids = new List<Boid>();
            strayBoids.AddRange(_activeBoids.Where(IsStray));
            strayBoids.AddRange(_spawnQueue.Where(IsStray));

            foreach (var boid in strayBoids)
            {
                _boidsPool.Release(boid);
            }

            return strayBoids.ToArray();
        }

        [return: MaybeNull]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public Boid SpawnIfNeeded()
        {
            if (Boids.Count() >= spawnMaxCount) return null;
            var spawnPeriod = 1.0f / spawnFrequency;
            if (!(Time.time - _lastSpawnTime > spawnPeriod)) return null;
            _lastSpawnTime = Time.time;
            return Spawn();
        }
    }
}