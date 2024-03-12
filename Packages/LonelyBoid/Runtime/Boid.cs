using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class Boid : Leaf<Boid, Flock>
    {
        public Flock Flock => ((IGuidChild)this).GuidParent as Flock;
    }
}
