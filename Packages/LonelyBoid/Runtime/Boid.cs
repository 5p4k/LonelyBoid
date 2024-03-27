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
                    speed = boid.Speed
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

        [field: NonSerialized]
        public virtual float Speed
        {
            get;
            [SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
            set;
        }

        public virtual void ApplyChange(Vector2 position, Vector2 direction, float speed)
        {
            var t = transform;
            t.position = position;
            t.up = direction;
            Speed = speed;
        }
    }
}