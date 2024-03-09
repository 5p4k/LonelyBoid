using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class TrackerBase
    {
        private static readonly object TrackerLock = new();
        private static readonly Dictionary<Type, TrackerBase> Trackers = new();
        private readonly object _compsLock = new();
        private readonly SortedSet<Component> _components = new();

        protected static TrackerBase GetTracker<TComponent>() where TComponent : Component
        {
            lock (TrackerLock)
            {
                if (Trackers.TryGetValue(typeof(TComponent), out var tracker)) return tracker;
                tracker = new TrackerBase();
                Trackers.Add(typeof(TComponent), tracker);

                return tracker;
            }
        }

        protected internal void Add(Component comp)
        {
            lock (_compsLock)
            {
                if (comp) _components.Add(comp);
            }
        }

        protected internal void Remove(Component comp)
        {
            lock (_compsLock)
            {
                _components.Remove(comp);
            }
        }

        protected internal TComponent[] Collect<TComponent>() where TComponent : Component
        {
            lock (_compsLock)
            {
                // Remove null comps
                _components.RemoveWhere(comp => (bool)comp);
                return _components.Cast<TComponent>().ToArray();
            }
        }
    }

    public abstract class Tracker : TrackerBase
    {
        public static void Add<TComponent>(TComponent comp) where TComponent : Component
        {
            GetTracker<TComponent>().Add(comp);
        }

        public static void Remove<TComponent>(TComponent comp) where TComponent : Component
        {
            GetTracker<TComponent>().Remove(comp);
        }

        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public new static TComponent[] Collect<TComponent>() where TComponent : Component
        {
            return GetTracker<TComponent>().Collect<TComponent>();
        }

        public static IEnumerable<TComponent> CollectChildrenOf<TComponent>(Component parent)
            where TComponent : Component
        {
            var go = parent.gameObject;
            foreach (var comp in Collect<TComponent>())
            {
                for (var t = comp.transform; t; t = t.parent)
                {
                    if (t.gameObject == go) yield return comp;
                }
            }
        }

        public static TParentComponent FindParentOf<TParentComponent, TComponent>(TComponent comp)
            where TComponent : Component where TParentComponent : Component
        {
            Dictionary<GameObject, TParentComponent> goToComp = new(
                Collect<TParentComponent>().Select(parentComp =>
                    new KeyValuePair<GameObject, TParentComponent>(parentComp.gameObject, parentComp))
            );

            for (var t = comp.transform; t; t = t.parent)
            {
                if (goToComp.TryGetValue(t.gameObject, out var parentComp))
                {
                    return parentComp;
                }
            }

            return null;
        }
    }

    public class TrackableMonoBehavior<T> : MonoBehaviour where T : MonoBehaviour
    {
        private void OnDestroy()
        {
            Tracker.Remove(this as T);
        }

        private void Awake()
        {
            Tracker.Add(this as T);
        }
    }
}