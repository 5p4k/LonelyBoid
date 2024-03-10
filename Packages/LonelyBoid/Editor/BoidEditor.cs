using UnityEngine;
using UnityEditor;

namespace Editor
{
    [CustomEditor(typeof(OldBoid))]
    public class BoidEditor : UnityEditor.Editor
    {
        private static void DrawSector(Transform transform, float radius, float angleTau, bool solid)
        {
            var angleDeg = angleTau * 360.0f;
            var start = Quaternion.AngleAxis(-0.5f * angleDeg, Vector3.back) * transform.up;
            if (solid)
            {
                Handles.DrawSolidArc(transform.position, Vector3.back, start, angleDeg, radius);
            }
            else
            {
                var position = transform.position;
                Handles.DrawWireArc(position, Vector3.back, start, angleDeg, radius);
                Handles.DrawLine(position, position + start * radius);
                var end = Quaternion.AngleAxis(angleDeg, Vector3.back) * start;
                Handles.DrawLine(position, position + end * radius);
            }
        }

        [DrawGizmo(GizmoType.InSelectionHierarchy)]
        private static void OnDrawGizmos(OldBoid boid, GizmoType gizmoType)
        {
            if (!boid.flock) return;

            var active = (gizmoType & GizmoType.Active) != 0;

            var color = Handles.color;
            color.a = active ? 0.05f : 0.01f;

            using (new Handles.DrawingScope(color))
            {
                DrawSector(boid.transform, boid.flock.viewRadius, boid.flock.viewAngleTau, true);
            }

            using (new Handles.DrawingScope(new Color(1f, 0.8f, 0f, color.a)))
            {
                DrawSector(boid.transform, boid.flock.avoidRadius, boid.flock.avoidAngleTau, true);
            }

            if (!active) return;
            color.a = 1.0f;

            using (new Handles.DrawingScope(color))
            {
                DrawSector(boid.transform, boid.flock.viewRadius, boid.flock.viewAngleTau, false);
            }

            using (new Handles.DrawingScope(new Color(1f, 0.8f, 0f, color.a)))
            {
                DrawSector(boid.transform, boid.flock.avoidRadius, boid.flock.avoidAngleTau, false);
            }
        }
    }
}