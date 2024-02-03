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

    void Update() {
        // Spawn a boid if enough time has passed
        if (Time.time - _lastSpawn > spawnPeriod && boids.Count < maxCount) {
            _lastSpawn = Time.time;
            Boid boid = Instantiate(prefab);
            boid.flock = this;

            // Randomize position and orientation
            Vector3 deltaPos = spawnRadius * Random.insideUnitCircle.normalized;
            boid.transform.position = transform.position + deltaPos;
            boid.transform.up = Random.insideUnitCircle;
            boid.speed = minSpeed + Random.value * (maxSpeed - minSpeed);

            boids.Add(boid);
        }
    }
}


public class FlockGizmoDrawer 
{
    [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy)]
    static void DrawGizmoForFlock(Flock flock, GizmoType gizmoType)
    {
        if ((gizmoType & GizmoType.InSelectionHierarchy) != 0) {
            UnityEditor.Handles.color = Color.yellow;
        }
        UnityEditor.Handles.DrawWireDisc(flock.transform.position, Vector3.back, flock.spawnRadius);
    }
}
