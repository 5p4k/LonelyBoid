using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BoidSpawner : MonoBehaviour
{
    public Boid prefab;
    public uint maxCount = 40;
    public float spawnFrequency = 1.0f;
    public float spawnRadius = 10.0f;

    float lastSpawn = 0.0f;
    List<Boid> boids = new List<Boid>();

    public float spawnPeriod {
        get {
            return 1.0f / spawnFrequency;
        }
    }

    void Update() {
        if (Time.time - lastSpawn > spawnPeriod && boids.Count < maxCount) {
            lastSpawn = Time.time;
            Vector3 deltaPos = spawnRadius * Random.insideUnitCircle.normalized;
            Boid boid = Instantiate(prefab);
            boid.transform.position = transform.position + deltaPos;
            boid.transform.up = Random.insideUnitCircle;
            boids.Add(boid);
        }
    }
}


public class BoidSpawnerGizmoDrawer 
{
    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    static void DrawGizmoForBoidSpawner(BoidSpawner spawner, GizmoType gizmoType)
    {
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(spawner.transform.position, Vector3.back, spawner.spawnRadius);
    }
}
