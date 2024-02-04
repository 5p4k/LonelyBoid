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
    static void DrawSector(Transform transform, float radius, float angleTau, bool solid) {
        float angleDeg = angleTau * 360.0f;
        Vector3 start = Quaternion.AngleAxis(-0.5f * angleDeg, Vector3.back) * transform.up;
        if (solid) {
            UnityEditor.Handles.DrawSolidArc(transform.position, Vector3.back, start, angleDeg, radius);
        } else {
            UnityEditor.Handles.DrawWireArc(transform.position, Vector3.back, start, angleDeg, radius);
            UnityEditor.Handles.DrawLine(transform.position, transform.position + start * radius);
            Vector3 end = Quaternion.AngleAxis(angleDeg, Vector3.back) * start;
            UnityEditor.Handles.DrawLine(transform.position, transform.position + end * radius);
        }
    }

    [DrawGizmo(GizmoType.Active)]
    public static void DrawGizmoForBoid(Boid boid, GizmoType gizmoType) {
        if (boid.flock == null) {
            return;
        }
        bool isActive = (gizmoType & GizmoType.Active) != 0;

        // Draw the region where the boid can see
        Color color = UnityEditor.Handles.color;
        color.a = isActive ? 0.1f : 0.5f;

        using (new Handles.DrawingScope(color)) {
            DrawSector(boid.transform, boid.flock.viewRadius, boid.flock.viewAngleTau, isActive);
        }

        color.r = 1; color.g = 0; color.b = 0;

        // Draw the region where the boid can avoid
        using (new Handles.DrawingScope(color)) {
            DrawSector(boid.transform, boid.flock.avoidRadius, boid.flock.avoidAngleTau, isActive);
        }

        if (isActive) {
            FlockGizmoDrawer.DrawGizmoForFlock(boid.flock, GizmoType.InSelectionHierarchy);
            foreach (Boid otherBoid in boid.flock.boids) {
                if (boid != otherBoid) {
                    DrawGizmoForBoid(otherBoid, GizmoType.InSelectionHierarchy);
                }
            }
        }
    }
}

