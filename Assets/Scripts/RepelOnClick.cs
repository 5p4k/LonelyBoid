using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RepelOnClick : MonoBehaviour
{
    public GameObject repelForce;
    public GameObject flock;

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
                forceAnim.Play("PeakRepel", -1, 0.0f);
            }
        }

        if (flock)
        {
            var flockAnim = flock.GetComponent<Animator>();
            if (flockAnim)
            {
                flockAnim.Play("Scatter", -1, 0.0f);
            }
        }
    }
}