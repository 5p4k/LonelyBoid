using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class Force : TrackableMonoBehavior<Force>
    {
        [NonSerialized] internal Domain domain;
        [NonSerialized] internal int indexInDomain = -1;
    }
}
