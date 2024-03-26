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
                var t = boid.transform;
                return new BoidData
                {
                    flockIndex = flockIndex,
                    position = t.position,
                    direction = t.up,
                    speed = boid.speed
                };
            }

            public void ApplyTo(Boid boid)
            {
                var t = boid.transform;
                t.position = position;
                t.up = direction;
                boid.speed = speed;
            }
        }
    }

    public class Boid : MonoBehaviour
    {
        [NonSerialized] public Flock flock;
        [NonSerialized] public float speed;
    }
}