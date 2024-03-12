using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class Flock : Node<Flock, Domain>, IDomainObject
    {
        public DomainObjectType Type => DomainObjectType.Flock;

        public Domain Domain => ((IGuidChild)this).GuidParent as Domain;

        public IEnumerable<Boid> Boids => ((IGuidParent)this).GuidChildren.Select(boid => boid as Boid);


        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        protected new static void TreeLevelRebuildInternal()
        {
            // Do simultaneously Flocks and Forces, so that we query the domains only once
            var flocks = GuidTracker.GetAll<Flock>();
            var forces = GuidTracker.GetAll<Force>();

            foreach (var flock in flocks)
            {
                ((IGuidParent)flock).UpdateChildren();
            }

            var anyChange = false;
            var domains = IGuidChild.UpdateParents<Domain, Flock>(ref anyChange, flocks);
            IGuidChild.UpdateParents<Domain, Force>(ref anyChange, forces, domains);
            if (anyChange) Domain.TreeLevelMarkDirty();
        }
    }
}