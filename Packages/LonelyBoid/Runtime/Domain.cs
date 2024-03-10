using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace saccardi.lonelyboid
{
    public class Domain : TrackableMonoBehavior<Domain>
    {
        [NonSerialized] internal Flock[] flocks;
        [NonSerialized] internal Force[] forces;
        [NonSerialized] internal Boid[] boids;


        private void RecacheLocal()
        {
            for (var i = 0; i < forces.Length; ++i)
            {
                forces[i].domain = this;
                forces[i].indexInDomain = i;
            }

            var boidsCount = 0;
            for (var i = 0; i < flocks.Length; ++i)
            {
                flocks[i].domain = this;
                flocks[i].indexInDomain = i;
                for (var j = 0; j < flocks[i].boids.Length; ++j)
                {
                    flocks[i].boids[j].domain = this;
                    flocks[i].boids[j].flock = flocks[i];
                    flocks[i].boids[j].indexInFlock = j;
                    flocks[i].boids[j].indexInDomain = boidsCount++;
                }
            }

            boids = flocks.SelectMany(flock => flock.boids).ToArray();
            Debug.Assert(boids.Length == boidsCount);
        }

        public static void Recache()
        {
            var missingForces = new List<Force>();
            var missingFlocks = new List<KeyValuePair<Flock, List<Boid>>>();
            var missingBoids = new List<Boid>();
            
            // Pre-collect all domains
            var domains = Tracker.Collect<Domain>();
            
            // Forces are simple
            foreach (var kvp in Tracker.CollectAllChildren(domains, missingForces))
            {
                kvp.Key.forces = kvp.Value.ToArray();
            }

            // Two-stage reloading of boids
            var domainToFlockToBoid = Tracker.CollectChildren(
                Tracker.CollectAllChildren<Flock, Boid>(null, missingBoids), domains, missingFlocks
            );

            foreach (var domainToFlock in domainToFlockToBoid)
            {
                domainToFlock.Key.flocks = domainToFlock.Value.Select(kvp => kvp.Key).ToArray();
                foreach (var flockToBoid in domainToFlock.Value)
                {
                    flockToBoid.Key.boids = flockToBoid.Value.ToArray();
                }
            }

            // Update cached indices, arrays and parent references
            foreach (var domain in domains)
            {
                domain.RecacheLocal();
            }
            
            // Clear all missing stuff
            foreach (var force in missingForces)
            {
                force.domain = null;
                force.indexInDomain = -1;
            }
            foreach (var boid in missingBoids)
            {
                boid.domain = null;
                boid.indexInDomain = -1;
                boid.flock = null;
                boid.indexInFlock = -1;
            }

            foreach (var flockToBoid in missingFlocks)
            {
                flockToBoid.Key.domain = null;
                flockToBoid.Key.indexInDomain = -1;
                for (var i = 0; i < flockToBoid.Value.Count; ++i)
                {
                    flockToBoid.Value[i].flock = flockToBoid.Key;
                    flockToBoid.Value[i].indexInFlock = i;
                    flockToBoid.Value[i].domain = null;
                    flockToBoid.Value[i].indexInDomain = -1;
                }
            }
        }
    }
}