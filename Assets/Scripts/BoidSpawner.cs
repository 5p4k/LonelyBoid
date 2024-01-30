using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BoidSpawner : MonoBehaviour
{
    public Boid prefab;
    public uint maxCount = 40;
    public float spawnSpeed = 1.0f;
    public float spawnRadius = 10.0f;

    void Awake () {
        // for (int i = 0; i < spawnCount; i++) {
        //     Vector3 pos = transform.position + Random.insideUnitSphere * spawnRadius;
        //     Boid boid = Instantiate (prefab);
        //     boid.transform.position = pos;
        //     boid.transform.forward = Random.insideUnitSphere;

        //     boid.SetColour (colour);
        // }
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
