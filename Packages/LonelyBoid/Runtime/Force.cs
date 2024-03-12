using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class Force : Leaf<Force, Domain>, IDomainObject
    {
        public DomainObjectType Type => DomainObjectType.Force;

        public Domain Domain => ((IGuidChild)this).GuidParent as Domain;


        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        protected new static void TreeLevelRebuildInternal()
        {
            // Delegate this to flocks
            Flock.TreeLevelRebuildIfNeeded();
        }
    }
}