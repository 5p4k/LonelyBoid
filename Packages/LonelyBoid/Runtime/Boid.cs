using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace saccardi.lonelyboid
{
    namespace IO
    {
        [SuppressMessage("ReSharper", "NotAccessedField.Global")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public struct BoidData
        {
            public int flockIndex;
            public Vector2 position;
            public Vector2 direction;
            public float speed;

            public static BoidData From(Boid boid, int flockIndex)
            {
                var active = boid.flock && boid.gameObject.activeSelf && boid.flock.gameObject.activeSelf;
                var t = boid.transform;
                return new BoidData
                {
                    flockIndex = active ? flockIndex : -1,
                    position = t.position,
                    direction = t.up,
                    speed = boid.ActualSpeed
                };
            }

            public void ApplyTo(Boid boid)
            {
                if (!boid.gameObject.activeSelf) return;
                boid.ApplyChange(position, direction, speed);
            }
        }
    }

    [SuppressMessage("ReSharper", "ClassWithVirtualMembersNeverInherited.Global")]
    public class Boid : MonoBehaviour
    {
        [NonSerialized] public Flock flock;
        [NonSerialized] public float speed; 
        
        public float ActualSpeed
        {
            get
            {
                if (Time.inFixedTimeStep && flock && flock.useKinematics &&
                    TryGetComponent<Rigidbody2D>(out var rigidBody) && !rigidBody.isKinematic)
                {
                    return rigidBody.velocity.magnitude;
                }

                return speed;
            }
        }

        public virtual void ApplyChange(Vector2 position, Vector2 direction, float newSpeed)
        {
            var t = transform;
            // Always store the theoretical speed
            if (flock && flock.useKinematics && TryGetComponent<Rigidbody2D>(out var rigidBody))
            {
                if (Time.inFixedTimeStep)
                {
                    // Can only support kinematics objects
                    rigidBody.MovePosition(position);
                    rigidBody.MoveRotation(Quaternion.FromToRotation(Vector2.up, direction));
                }
                else
                {
                    // Just update the speed and then the transform like usual
                    rigidBody.velocity = direction * speed;
                    t.position = position;
                    t.up = direction;
                }
            }
            else
            {
                // Usual transformation
                t.position = position;
                t.up = direction;
            }
            speed = newSpeed;
        }
    }
}