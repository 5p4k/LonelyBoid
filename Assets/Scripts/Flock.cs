using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Pool;

public struct FlockData {
    public float viewRadius;
    public float viewAngleTau;

    public float avoidRadius;
    public float avoidAngleTau;

    public float separationWeight;
    public float alignmentWeight;
    public float cohesionWeight;
    public float survivalWeight;

    public float maxAcceleration;
    public float minSpeed;
    public float maxSpeed;
    public float maxAngularSpeedTau;

    public Vector2 center;
    public float spawnRadius;
    public float killRadius;
}

public class BoidComparer : IComparer<Boid> {
    // Compares by Height, Length, and Width.
    public int Compare(Boid x, Boid y) {
        return x.GetInstanceID() - y.GetInstanceID();
    }
}

public class Flock : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject prefab;
    public float spawnFrequency = 3.0f;
    public float spawnRadius = 7.0f;
    public uint maxCount = 40;
    public uint maxPoolCapacity = 40;
    public float killRadius = 10.0f;

    [Header("Behaviour")]
    public float separationWeight = 2.0f;
    public float alignmentWeight = 3.0f;
    public float cohesionWeight = 4.0f;
    public float survivalWeight = 10.0f;

    [Header("Dynamics")]
    public float maxAcceleration = 2.0f;
    public float minSpeed = 0.1f;
    public float maxSpeed = 3.0f;
    public float maxAngularSpeedTau = 0.5f;

    [Header("Perception")]
    public float viewRadius = 4.0f;
    public float viewAngleTau = 0.5f;
    public float avoidRadius = 2f;
    public float avoidAngleTau = 0.8f;

    [HideInInspector]
    public SortedSet<Boid> boids = new SortedSet<Boid>(new BoidComparer());

    float _lastSpawn = 0.0f;

    ObjectPool<Boid> _boidsPool;

    public FlockData ToBufferData() {
        var retval = new FlockData();
        retval.viewRadius = viewRadius;
        retval.viewAngleTau = viewAngleTau;
        retval.avoidRadius = avoidRadius;
        retval.avoidAngleTau = avoidAngleTau;
        retval.separationWeight = separationWeight;
        retval.alignmentWeight = alignmentWeight;
        retval.cohesionWeight = cohesionWeight;
        retval.survivalWeight = survivalWeight;
        retval.maxAcceleration = maxAcceleration;
        retval.minSpeed = minSpeed;
        retval.maxSpeed = maxSpeed;
        retval.maxAngularSpeedTau = maxAngularSpeedTau;
        retval.center = transform.position;
        retval.spawnRadius = spawnRadius;
        retval.killRadius = killRadius;
        return retval;
    }

    Boid CreateBoid() {
        GameObject instance = Instantiate(prefab, this.transform);
        Boid boid = instance.GetComponent<Boid>();

        if (boid == null) {
            boid = instance.AddComponent(typeof(Boid)) as Boid;
        }
        return boid;
    }

    void OnSpawn(Boid boid) {
        _lastSpawn = Time.time;
        boid.flock = this;

        // Randomize position and orientation
        Vector3 deltaPos = spawnRadius * Random.insideUnitCircle.normalized;
        boid.transform.position = transform.position + deltaPos;
        boid.transform.up = -(Quaternion.AngleAxis(Random.Range(-90, +90), Vector3.back) * deltaPos.normalized);
        boid.speed = minSpeed + Random.value * (maxSpeed - minSpeed);

        boids.Add(boid);
        boid.gameObject.SetActive(true);
    }

    void OnKill(Boid boid) {
        boid.gameObject.SetActive(false);
        boids.Remove(boid);
        boid.flock = null;
    }

    void OnKillDestroy(Boid boid) {
        OnKill(boid);
        Destroy(boid.gameObject);
    }

    public Boid Spawn() {
        return _boidsPool.Get();
    }

    public bool ShouldKill(Boid boid) {
        float radius = Vector3.Distance(boid.transform.position, transform.position);
        return (radius < killRadius) == (killRadius < spawnRadius);
    }

    public void Kill(Boid boid) {
        _boidsPool.Release(boid);
    }

    public uint KillStrayBoids() {
        List<Boid> toKill = new List<Boid>();
        foreach (Boid boid in boids) {
            if (ShouldKill(boid)) {
                toKill.Add(boid);
            }
        }
        foreach (Boid boid in toKill) {
            Kill(boid);
        }
        return (uint)toKill.Count;
    }

    public Boid SpawnIfNeeded() {
        float spawnPeriod = 1.0f / spawnFrequency;
        if (Time.time - _lastSpawn > spawnPeriod && boids.Count < maxCount) {
            return Spawn();
        }
        return null;
    }

    BoidsContainer GetContainer() {
        Transform t = transform.parent;
        while (t != null) {
            BoidsContainer container = t.gameObject.GetComponent<BoidsContainer>();
            if (container) {
                return container;
            }
            t = t.parent;
        }
        return null;
    }

    void Start() {
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

    public Rect accelerationFieldDomain {
        get {
            float radius = Mathf.Max(spawnRadius, killRadius);
            return new Rect(
                transform.position.x - radius, transform.position.y - radius,
                2 * radius, 2 * radius
            );
        }
    }

    void OnDestroy() {
        _boidsPool.Dispose();
    }
}


#if UNITY_EDITOR
[CustomEditor(typeof(Flock))]
public class FlockEditor : Editor {

    [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy)]
    static void DrawGizmo(Flock flock, GizmoType gizmoType) {
        bool active = (gizmoType & GizmoType.Active) != 0;

        using (new Handles.DrawingScope(active ? Color.green : Handles.color)) {
            Handles.DrawWireDisc(flock.transform.position, Vector3.back, flock.spawnRadius);
        }
        using (new Handles.DrawingScope(active ? Color.red : Handles.color)) {
            Handles.DrawWireDisc(flock.transform.position, Vector3.back, flock.killRadius);
        }
    }

    void HandleRadius(Flock flock, ref float radius, string description) {
        EditorGUI.BeginChangeCheck();
        float newRadius = Handles.RadiusHandle(Quaternion.identity, flock.transform.position, radius, true);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(flock, description);
            radius = newRadius;
        }
    }

    public void OnSceneGUI() {
        Flock flock = target as Flock;

        using (new Handles.DrawingScope(Color.green)) {
            HandleRadius(flock, ref flock.spawnRadius, "Change spawn radius");
        }

        using (new Handles.DrawingScope(Color.red)) {
            HandleRadius(flock, ref flock.killRadius, "Change kill radius");
        }
    }
}
#endif