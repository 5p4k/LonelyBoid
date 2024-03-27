using saccardi.lonelyboid;
using UnityEngine;

public class KillOnCollide : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<Boid>(out var boid))
        {
            boid.flock.Kill(boid);
        }
    }
}