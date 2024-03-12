using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine.Assertions;

namespace saccardi.lonelyboid
{
    public enum DomainObjectType
    {
        Flock,
        Force
    }

    public interface IDomainObject : IGuidChild
    {
        public DomainObjectType Type { get; }
    }

    public class Domain : Root<Domain>
    {
        public IEnumerable<IDomainObject> Children =>
            ((IGuidParent)this).GuidChildren.Select(child => child as IDomainObject);

        public string TreeToString()
        {
            Boid.TreeLevelRebuildIfNeeded();
            Flock.TreeLevelRebuildIfNeeded();
            TreeLevelRebuildIfNeeded();

            var retval = gameObject.name;
            var children = Children.ToArray();

            for (var i = 0; i < children.Length; ++i)
            {
                retval += "\n" + (i + 1) + ". " + ((Component)children[i]).gameObject.name;
                if (children[i].Type != DomainObjectType.Flock) continue;
                var flock = children[i] as Flock;
                var boids = flock!.Boids.ToArray();
                for (var j = 0; j < boids.Length; ++j)
                {
                    retval += "\n    " + (j + 1) + ". " + boids[j].gameObject.name;
                }
            }

            return retval;
        }
    }

    [CustomEditor(typeof(Domain))]
    public class DomainEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var domain = target as Domain;
            DrawDefaultInspector();
            if (GUILayout.Button("Print me"))
            {
                Debug.Log(domain!.TreeToString());
            }
        }
    }
}