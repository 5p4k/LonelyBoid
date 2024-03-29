using saccardi.lonelyboid;
using UnityEngine;

public class RepelOnClick : MonoBehaviour
{
    public Force repelForce;
    public Flock flock;

    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void OnMouseDown()
    {
        Vector2 position = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        if (repelForce)
        {
            repelForce.transform.position = position;

            var forceAnim = repelForce.GetComponent<Animator>();
            if (forceAnim)
            {
                forceAnim.Play("ForceScatter", -1, 0.0f);
            }
        }

        if (!flock) return;
        var flockAnim = flock.GetComponent<Animator>();
        if (flockAnim)
        {
            flockAnim.Play("FlockScatter", -1, 0.0f);
        }
    }
}