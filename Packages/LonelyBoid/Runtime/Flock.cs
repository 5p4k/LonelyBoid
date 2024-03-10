using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class Flock : TrackableMonoBehavior<Flock>
    {
        [NonSerialized] internal Domain domain;
        [NonSerialized] internal Boid[] boids;
        [NonSerialized] internal int indexInDomain = -1;
    }
}
