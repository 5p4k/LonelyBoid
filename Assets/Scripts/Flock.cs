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
    public float survivalWeight = 1.0f;

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

    public bool ShouldKill(Boid boid) {
        float radius = Vector3.Distance(boid.transform.position, transform.position);
        return (radius < killRadius) == (killRadius < spawnRadius);
    }


    private void KillInstance(Boid boid) {
        boid.flock = null;
        boid.transform.parent = null;
        boid.name = "[dead] " + boid.name;
        Destroy(boid);
    }

    public void Kill(Boid boid) {
        if (boids.Remove(boid)) {
            KillInstance(boid);
        }
    }

    public uint KillStrayBoids() {
        uint killed = 0;
        for (int i = boids.Count - 1; i >= 0; --i) {
            Boid boid = boids[i];
            if (ShouldKill(boid)) {
                ++killed;
                boids.RemoveAt(i);
                KillInstance(boid);
            }
        }
        return killed;
    }

    void Update() {
        // Spawn a boid if enough time has passed
        if (Time.time - _lastSpawn > spawnPeriod && boids.Count < maxCount) {
            Spawn();
        }
    }

    void OnDrawGizmos() {
        bool active = Selection.Contains(gameObject);

        for (uint i = 0; i < 36; ++i) {
            Vector3 dir = Quaternion.AngleAxis(i * 10.0f, Vector3.back) * Vector3.up;
            Handles.DrawLine(transform.position + spawnRadius * dir,
                             transform.position + killRadius * dir);
        }

        using (new Handles.DrawingScope(active ? Color.green : Handles.color)) {
            Handles.DrawWireDisc(transform.position, Vector3.back, spawnRadius, active ? 2.0f : 0.0f);
        }
        using (new Handles.DrawingScope(active ? Color.red : Handles.color)) {
            Handles.DrawWireDisc(transform.position, Vector3.back, killRadius, active ? 2.0f : 0.0f);
        }
    }
}
