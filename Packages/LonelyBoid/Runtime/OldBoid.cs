using UnityEngine;


public struct BoidData
{
    public uint FlockIndex;
    public Vector2 Position;
    public Vector2 Direction;
    public float Speed;
}


public class OldBoid : MonoBehaviour
{
    public float speed;

    [HideInInspector] public OldFlock flock;

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
}