using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Flock : MonoBehaviour
{
    [Header("Spawn")]
    public Boid prefab;
    public float spawnFrequency = 3.0f;
    public float spawnRadius = 10.0f;
    public uint maxCount = 40;
    public float killRadius = 13.0f;

    [Header("Behaviour")]
    public float separationWeight = 6.0f;
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 4.0f;

    [Header("Dynamics")]
    public float maxAcceleration = 5.0f;
    public float minSpeed = 0.1f;
    public float maxSpeed = 10.0f;
    public float maxAngularSpeedTau = 0.5f;

    [Header("Perception")]
    public float viewRadius = 5f;
    public float viewAngleTau = 0.5f;
    public float avoidRadius = 2f;
    public float avoidAngleTau = 0.8f;
    
    float _lastSpawn = 0.0f;

    [HideInInspector]
    public List<Boid> boids = new List<Boid>();

    float spawnPeriod {
        get {
            return 1.0f / spawnFrequency;
        }
    }

    public Boid Spawn() {
        _lastSpawn = Time.time;
        Boid boid = Instantiate(prefab, this.transform);
        boid.flock = this;

        // Randomize position and orientation
        Vector3 deltaPos = spawnRadius * Random.insideUnitCircle.normalized;
        boid.transform.position = transform.position + deltaPos;
        boid.transform.up = -(Quaternion.AngleAxis(Random.Range(-90, +90), Vector3.back) * deltaPos.normalized);
        boid.speed = minSpeed + Random.value * (maxSpeed - minSpeed);

        boids.Add(boid);
        return boid;
    }

    public void Kill(Boid boid) {
        if (boids.Remove(boid)) {
            Destroy(boid);
        }
    }

    void Update() {
        // Spawn a boid if enough time has passed
        if (Time.time - _lastSpawn > spawnPeriod && boids.Count < maxCount) {
            Spawn();
        }
    }
}


public class FlockGizmoDrawer 
{
    [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy)]
    public static void DrawGizmoForFlock(Flock flock, GizmoType gizmoType) {
        bool isSelected = (gizmoType & GizmoType.InSelectionHierarchy) != 0;
        bool isActive = (gizmoType & GizmoType.Active) != 0;
        if (isSelected) {
            using (new Handles.DrawingScope(Color.red)) {
                UnityEditor.Handles.DrawWireDisc(flock.transform.position, Vector3.back, flock.killRadius);
            }
        }

        using (new Handles.DrawingScope(isActive ? Color.yellow : Handles.color)) {
            UnityEditor.Handles.DrawWireDisc(flock.transform.position, Vector3.back, flock.spawnRadius);
        }

        if (isActive) {
            // Manually draw the outline of the boids
            foreach (Boid boid in flock.boids) {
                BoidGizmoDrawer.DrawGizmoForBoid(boid, GizmoType.InSelectionHierarchy);
            }
        }
    }
}
