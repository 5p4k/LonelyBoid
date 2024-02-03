using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Boid : MonoBehaviour
{
    public float speed = 0.0f;

    [HideInInspector]
    public Flock flock = null;
}


public class BoidGizmoDrawer 
{
    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    static void DrawGizmoForBoid(Boid boid, GizmoType gizmoType)
    {
        if (boid.flock != null) {
            // Draw the region where the boid can see
            Color color = UnityEditor.Handles.color;
            color.a = 0.1f;
            UnityEditor.Handles.color = color;

            UnityEditor.Handles.DrawSolidArc(boid.transform.position, Vector3.back,
                boid.transform.up, +boid.flock.viewAngleTau * 180.0f, boid.flock.viewRadius);
            UnityEditor.Handles.DrawSolidArc(boid.transform.position, Vector3.back,
                boid.transform.up, -boid.flock.viewAngleTau * 180.0f, boid.flock.viewRadius);

            UnityEditor.Handles.color = new Color(1, 0, 0, 0.1f);

            // Draw the region where the boid can avoid
            UnityEditor.Handles.DrawSolidArc(boid.transform.position, Vector3.back,
                boid.transform.up, +boid.flock.avoidAngleTau * 180.0f, boid.flock.avoidRadius);
            UnityEditor.Handles.DrawSolidArc(boid.transform.position, Vector3.back,
                boid.transform.up, -boid.flock.avoidAngleTau * 180.0f, boid.flock.avoidRadius);
        }
        
    }
}

