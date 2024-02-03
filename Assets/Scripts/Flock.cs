using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Flock : MonoBehaviour
{
    [Header("Spawn")]
    public Boid prefab;
    public float spawnFrequency = 1.0f;
    public float spawnRadius = 10.0f;
    public uint maxCount = 40;

    [Header("Behaviour")]
    public float separationWeight = 1.0f;
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;

    [Header("Dynamics")]
    public float maxAcceleration = 10.0f;
    public float minSpeed = 0.1f;
    public float maxSpeed = 10.0f;
    public float maxAngularSpeedTau = 1.0f;

    [Header("Perception")]
    public float viewRadius = 1.0f;
    public float viewAngleTau = 0.5f;
    public float avoidRadius = 0.2f;
    public float avoidAngleTau = 0.5f;
    
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

            // Randomize position and orientatino
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
    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    static void DrawGizmoForFlock(Flock spawner, GizmoType gizmoType)
    {
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(spawner.transform.position, Vector3.back, spawner.spawnRadius);
    }
}
