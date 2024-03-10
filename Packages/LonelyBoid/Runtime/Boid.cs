using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class Boid : TrackableMonoBehavior<Boid>
    {
        [NonSerialized] internal Flock flock;
        [NonSerialized] internal Domain domain;
        [NonSerialized] internal int indexInFlock = -1;
        [NonSerialized] internal int indexInDomain = -1;
    }
}
