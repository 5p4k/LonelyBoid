using UnityEngine;
using UnityEditor;


public struct BoidData
{
    public uint FlockIndex;
    public Vector2 Position;
    public Vector2 Direction;
    public float Speed;
}


public class Boid : MonoBehaviour
{
    public float speed;

    [HideInInspector] public Flock flock;

    public BoidData ToBufferData(uint flockIndex)
    {
        var t = transform;
        return new BoidData
        {
            FlockIndex = flockIndex,
            Position = t.position,
            Direction = t.up,
            Speed = speed
        };
    }

    public void FromBufferData(BoidData data)
    {
        var t = transform;
        t.position = data.Position;
        t.up = data.Direction;
        speed = data.Speed;
    }
    
#if UNITY_EDITOR
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

    private void OnDrawGizmosSelected()
    {
        if (!flock) return;

        var active = Selection.activeGameObject == gameObject;

        var color = Handles.color;
        color.a = active ? 0.05f : 0.01f;

        using (new Handles.DrawingScope(color))
        {
            DrawSector(transform, flock.viewRadius, flock.viewAngleTau, true);
        }

        using (new Handles.DrawingScope(new Color(1f, 0.8f, 0f, color.a)))
        {
            DrawSector(transform, flock.avoidRadius, flock.avoidAngleTau, true);
        }

        if (!active) return;
        color.a = 1.0f;

        using (new Handles.DrawingScope(color))
        {
            DrawSector(transform, flock.viewRadius, flock.viewAngleTau, false);
        }

        using (new Handles.DrawingScope(new Color(1f, 0.8f, 0f, color.a)))
        {
            DrawSector(transform, flock.avoidRadius, flock.avoidAngleTau, false);
        }
    }
#endif
}