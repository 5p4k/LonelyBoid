using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

public struct FlockData
{
    public float ViewRadius;
    public float ViewAngleTau;

    public float AvoidRadius;
    public float AvoidAngleTau;

    public float SeparationWeight;
    public float AlignmentWeight;
    public float CohesionWeight;
    public float SurvivalWeight;

    public float MaxAcceleration;
    public float MinSpeed;
    public float MaxSpeed;
    public float MaxAngularSpeedTau;

    public Vector2 Center;
    public float SpawnRadius;
    public float KillRadius;
}

public class BoidComparer : IComparer<Boid>
{
    // Compares by Height, Length, and Width.
    public int Compare(Boid x, Boid y)
    {
        return (x == null ? 0 : x.GetInstanceID()) - (y == null ? 0 : y.GetInstanceID());
    }
}

public class Flock : MonoBehaviour
{
    [Header("Spawn")] public GameObject prefab;
    public float spawnFrequency = 3.0f;
    public float spawnRadius = 7.0f;
    public uint maxCount = 40;
    public uint maxPoolCapacity = 40;
    public float killRadius = 10.0f;

    [Header("Behaviour")] public float separationWeight = 2.0f;
    public float alignmentWeight = 3.0f;
    public float cohesionWeight = 4.0f;
    public float survivalWeight = 10.0f;

    [Header("Dynamics")] public float maxAcceleration = 2.0f;
    public float minSpeed = 0.1f;
    public float maxSpeed = 3.0f;
    public float maxAngularSpeedTau = 0.5f;

    [Header("Perception")] public float viewRadius = 4.0f;
    public float viewAngleTau = 0.5f;
    public float avoidRadius = 2f;
    public float avoidAngleTau = 0.8f;

    public readonly SortedSet<Boid> Boids = new(new BoidComparer());

    [Header("Visualization")] public bool includeForces = true;

    private float _lastSpawn;

    private ObjectPool<Boid> _boidsPool;

    public FlockData ToBufferData()
    {
        return new FlockData
        {
            ViewRadius = viewRadius,
            ViewAngleTau = viewAngleTau,
            AvoidRadius = avoidRadius,
            AvoidAngleTau = avoidAngleTau,
            SeparationWeight = separationWeight,
            AlignmentWeight = alignmentWeight,
            CohesionWeight = cohesionWeight,
            SurvivalWeight = survivalWeight,
            MaxAcceleration = maxAcceleration,
            MinSpeed = minSpeed,
            MaxSpeed = maxSpeed,
            MaxAngularSpeedTau = maxAngularSpeedTau,
            Center = transform.position,
            SpawnRadius = spawnRadius,
            KillRadius = killRadius
        };
    }

    private Boid CreateBoid()
    {
        var instance = PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
        var boid = instance!.GetComponent<Boid>();

        if (boid == null)
        {
            boid = instance.AddComponent(typeof(Boid)) as Boid;
        }

        return boid;
    }

    private void OnSpawn(Boid boid)
    {
        _lastSpawn = Time.time;
        boid.flock = this;

        // Randomize position and orientation
        Vector3 deltaPos = spawnRadius * Random.insideUnitCircle.normalized;
        boid.transform.position = transform.position + deltaPos;
        boid.transform.up = -(Quaternion.AngleAxis(Random.Range(-90, +90), Vector3.back) * deltaPos.normalized);
        boid.speed = minSpeed + Random.value * (maxSpeed - minSpeed);

        Boids.Add(boid);
        boid.gameObject.SetActive(true);
    }

    private void OnKill(Boid boid)
    {
        boid.gameObject.SetActive(false);
        Boids.Remove(boid);
        boid.flock = null;
    }

    private void OnKillDestroy(Boid boid)
    {
        OnKill(boid);
        Destroy(boid.gameObject);
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public Boid Spawn()
    {
        return _boidsPool.Get();
    }

    private bool ShouldKill(Boid boid)
    {
        var radius = Vector3.Distance(boid.transform.position, transform.position);
        return (radius < killRadius) == (killRadius < spawnRadius);
    }


    // ReSharper disable once MemberCanBePrivate.Global
    public void Kill(Boid boid)
    {
        _boidsPool.Release(boid);
    }

    // ReSharper disable once UnusedMethodReturnValue.Global
    public uint KillStrayBoids()
    {
        var toKill = Boids.Where(ShouldKill).ToList();
        foreach (var boid in toKill)
        {
            Kill(boid);
        }

        return (uint)toKill.Count;
    }


    // ReSharper disable once UnusedMethodReturnValue.Global
    public Boid SpawnIfNeeded()
    {
        var spawnPeriod = 1.0f / spawnFrequency;
        if (Time.time - _lastSpawn > spawnPeriod && Boids.Count < maxCount)
        {
            return Spawn();
        }

        return null;
    }

    private void Start()
    {
        _boidsPool = new ObjectPool<Boid>(
            CreateBoid,
            OnSpawn,
            OnKill,
            OnKillDestroy,
            true,
            (int)maxCount,
            (int)maxPoolCapacity
        );
    }

    private void OnDestroy()
    {
        _boidsPool.Dispose();
    }


#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var active = Selection.Contains(gameObject);

        using (new Handles.DrawingScope(active ? Color.green : Handles.color))
        {
            Handles.DrawWireDisc(transform.position, Vector3.back, spawnRadius);
        }

        using (new Handles.DrawingScope(active ? Color.red : Handles.color))
        {
            Handles.DrawWireDisc(transform.position, Vector3.back, killRadius);
        }
    }
#endif
}


#if UNITY_EDITOR
[CustomEditor(typeof(Flock))]
public class FlockEditor : Editor
{
    private static void HandleRadius(Component flock, ref float radius, string description)
    {
        EditorGUI.BeginChangeCheck();
        var newRadius = Handles.RadiusHandle(Quaternion.identity, flock.transform.position, radius, true);
        if (!EditorGUI.EndChangeCheck()) return;
        Undo.RecordObject(flock, description);
        radius = newRadius;
    }

    public void OnSceneGUI()
    {
        var flock = target as Flock;

        using (new Handles.DrawingScope(Color.green))
        {
            HandleRadius(flock, ref flock!.spawnRadius, "Change spawn radius");
        }

        using (new Handles.DrawingScope(Color.red))
        {
            HandleRadius(flock, ref flock!.killRadius, "Change kill radius");
        }
    }
}
#endif