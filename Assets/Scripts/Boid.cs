using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;




public class Boid : MonoBehaviour
{
    public float speed = 0.0f;

    [HideInInspector]
    public Flock flock = null;

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

    void OnDrawGizmosSelected() {
        bool active = Selection.activeGameObject == gameObject;

        Color color = UnityEditor.Handles.color;
        color.a = active ? 0.05f : 0.01f;

        using (new Handles.DrawingScope(color)) {
            DrawSector(transform, flock.viewRadius, flock.viewAngleTau, true);
        }

        using (new Handles.DrawingScope(new Color(1f, 0.8f, 0f, color.a))) {
            DrawSector(transform, flock.avoidRadius, flock.avoidAngleTau, true);
        }

        if (active) {
            color.a = 1.0f;

            using (new Handles.DrawingScope(color)) {
                DrawSector(transform, flock.viewRadius, flock.viewAngleTau, false);
            }

            using (new Handles.DrawingScope(new Color(1f, 0.8f, 0f, color.a))) {
                DrawSector(transform, flock.avoidRadius, flock.avoidAngleTau, false);
            }
        }
    }
}
