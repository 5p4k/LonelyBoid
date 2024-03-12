using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public interface IPersistentGuid : IComparable
    {
        public GUID PersistentGuid { get; }
        internal Transform GuidTransform { get; }

        int IComparable.CompareTo(object obj)
        {
            if (obj is IPersistentGuid objGuid)
            {
                return PersistentGuid.CompareTo(objGuid.PersistentGuid);
            }

            return 1;
        }
    }

    public interface IGuidNode
    {
        public IGuidParent GuidRoot { get; }
    }

    public interface IGuidChild : IPersistentGuid, IGuidNode
    {
        public IGuidParent GuidParent { get; internal set; }
        IGuidParent IGuidNode.GuidRoot => GuidParent?.GuidRoot;

        public static Dictionary<GameObject, IGuidParent> GetParents<TParent>() where TParent : IGuidParent
        {
            return new Dictionary<GameObject, IGuidParent>(
                GuidTracker.GetAll<TParent>().Select(
                    parent => new KeyValuePair<GameObject, IGuidParent>(parent.GuidTransform.gameObject, parent)
                )
            );
        }

        public IGuidParent FindParent<TParent>(IReadOnlyDictionary<GameObject, IGuidParent> parents = null)
            where TParent : IGuidParent
        {
            parents ??= GetParents<TParent>();
            for (var t = GuidTransform; t; t = t.parent)
            {
                if (parents.TryGetValue(t.gameObject, out var newParent))
                {
                    return newParent;
                }
            }

            return null;
        }

        public bool UpdateParent<TParent>(IReadOnlyDictionary<GameObject, IGuidParent> parents = null)
            where TParent : IGuidParent
        {
            parents ??= GetParents<TParent>();
            var oldParent = GuidParent;
            var newParent = FindParent<TParent>(parents);

            if (oldParent == newParent) return false;

            oldParent?.GuidChildren.Remove(this);
            newParent?.GuidChildren.Add(this);
            GuidParent = newParent;

            return true;
        }

        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
        public static Dictionary<GameObject, IGuidParent> UpdateParents<TParent, TChild>(ref bool anyChange,
            IEnumerable<TChild> children = null,
            Dictionary<GameObject, IGuidParent> parents = null)
            where TParent : IGuidParent
            where TChild : IGuidChild
        {
            children ??= GuidTracker.GetAll<TChild>();
            parents ??= GetParents<TParent>();

            anyChange |= children.Aggregate(false,
                (current, child) => current | child.UpdateParent<TParent>(parents));

            return parents;
        }
    }

    public interface IGuidParent : IPersistentGuid, IGuidNode
    {
        protected internal SortedSet<IGuidChild> GuidChildren { get; }

        protected internal void UpdateChildren()
        {
            // Remove potential null references or broken relationships
            GuidChildren.RemoveWhere(child => child == null || child.GuidParent != this || !GuidTracker.IsAlive(child));
        }
    }

    public abstract class GuidTracker
    {
        [NonSerialized] private static readonly Dictionary<GUID, Tuple<Type, IPersistentGuid>> ByGuid = new();
        [NonSerialized] private static readonly Dictionary<Type, HashSet<IPersistentGuid>> ByType = new();
        [NonSerialized] private static readonly object Lock = new();

        public static void Register<T>(T obj) where T : IPersistentGuid
        {
            lock (Lock)
            {
                ByGuid.Add(obj.PersistentGuid, new Tuple<Type, IPersistentGuid>(typeof(T), obj));
                if (!ByType.TryGetValue(typeof(T), out var objs))
                {
                    objs = new HashSet<IPersistentGuid>();
                    ByType.Add(typeof(T), objs);
                }

                objs.Add(obj);
            }
        }

        public static void Deregister<T>(T obj) where T : IPersistentGuid
        {
            lock (Lock)
            {
                ByGuid.Remove(obj.PersistentGuid);
                ByType[typeof(T)].Remove(obj);
            }
        }

        public static bool IsAlive(IPersistentGuid obj)
        {
            lock (Lock)
            {
                return ByGuid.TryGetValue(obj.PersistentGuid, out _);
            }
        }

        public static bool TryGet<T>(GUID guid, out T obj) where T : IPersistentGuid
        {
            lock (Lock)
            {
                if (ByGuid.TryGetValue(guid, out var typeObj))
                {
                    if (typeof(T) == typeObj.Item1)
                    {
                        obj = (T)typeObj.Item2;
                        return true;
                    }
                }
            }

            obj = default;
            return false;
        }

        public static T[] GetAll<T>() where T : IPersistentGuid
        {
            lock (Lock)
            {
                if (ByType.TryGetValue(typeof(T), out var objs))
                {
                    return objs.Select(obj => (T)obj).ToArray();
                }
            }

            return Array.Empty<T>();
        }
    }

    [ExecuteInEditMode]
    public class Tracked<T> : MonoBehaviour, IPersistentGuid
        where T : Tracked<T>
    {
        [field: SerializeField] public GUID PersistentGuid { get; private set; }
        Transform IPersistentGuid.GuidTransform => transform;

        protected virtual void Awake()
        {
            if (PersistentGuid.Empty()) PersistentGuid = GUID.Generate();
            GuidTracker.Register((T)this);
        }

        protected virtual void OnDestroy()
        {
            GuidTracker.Deregister((T)this);
        }
    }

    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    public abstract class TreeLevelCapable<T> : Tracked<T>
        where T : TreeLevelCapable<T>
    {
        [NonSerialized] private static bool _treeLevelDirty = true;
        [NonSerialized] private static readonly object TreeLock = new();
        [NonSerialized] private static MethodInfo _treeLevelRebuildInternalImplementation;

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        protected static void TreeLevelRebuildInternal()
        {
            throw new NotImplementedException("You must implement a TreeLevelRebuildInternal static method.");
        }

        public static void TreeLevelRebuildIfNeeded()
        {
            lock (TreeLock)
            {
                if (!_treeLevelDirty) return;
                // Do we have the implementation?
                if (_treeLevelRebuildInternalImplementation == null)
                {
                    _treeLevelRebuildInternalImplementation = typeof(T).GetMethod(
                        "TreeLevelRebuildInternal",
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static |
                        BindingFlags.FlattenHierarchy
                    );
                }

                if (_treeLevelRebuildInternalImplementation == null)
                {
                    throw new NotImplementedException("You must implement a TreeLevelRebuildInternal static method.");
                }

                _treeLevelRebuildInternalImplementation.Invoke(null, new object[] { });
                _treeLevelDirty = false;
            }
        }

        public static void TreeLevelMarkDirty()
        {
            lock (TreeLock)
            {
                _treeLevelDirty = true;
            }
        }

        protected static void TreeDifferentLevelMarkDirty<TLevel>() where TLevel : IGuidNode
        {
            typeof(TLevel).GetMethod(
                    "TreeLevelMarkDirty",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                ?.Invoke(null, new object[] { });
        }
        
        protected override void Awake()
        {
            base.Awake();
            Debug.Log("Tree level " + typeof(T).Name + " marking dirty: creation");
            TreeLevelMarkDirty();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Debug.Log("Tree level " + typeof(T).Name + " marking dirty: destruction");
            TreeLevelMarkDirty();
        }
    }

    public class Leaf<T, TParent> : TreeLevelCapable<T>, IGuidChild
        where T : Leaf<T, TParent>
        where TParent : IGuidParent
    {
        IGuidParent IGuidChild.GuidParent { get; set; } = null;

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        protected new static void TreeLevelRebuildInternal()
        {
            var anyChange = false;
            IGuidChild.UpdateParents<TParent, T>(ref anyChange);
            if (!anyChange) return;
            Debug.Log("Tree level " + typeof(TParent).Name + " marking dirty: children parents changed");
            TreeDifferentLevelMarkDirty<TParent>();
        }
        
        protected virtual void Update()
        {
            if (!Application.isPlaying && Application.isEditor)
            {
                ((IGuidChild)this).UpdateParent<TParent>();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("Tree level " + typeof(TParent).Name + " marking dirty: child creation");
            TreeDifferentLevelMarkDirty<TParent>();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Debug.Log("Tree level " + typeof(TParent).Name + " marking dirty: child removal");
            TreeDifferentLevelMarkDirty<TParent>();
        }
    }

    public class Root<T> : TreeLevelCapable<T>, IGuidParent
        where T : Root<T>
    {
        SortedSet<IGuidChild> IGuidParent.GuidChildren { get; } = new();

        IGuidParent IGuidNode.GuidRoot => this;

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        protected new static void TreeLevelRebuildInternal()
        {
            var nodes = GuidTracker.GetAll<T>();
            foreach (var node in nodes)
            {
                ((IGuidParent)node).UpdateChildren();
            }
        }
    }

    public class Node<T, TParent> : TreeLevelCapable<T>, IGuidParent, IGuidChild
        where T : Node<T, TParent>
        where TParent : IGuidParent
    {
        IGuidParent IGuidChild.GuidParent { get; set; } = null;
        SortedSet<IGuidChild> IGuidParent.GuidChildren { get; } = new();

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        protected new static void TreeLevelRebuildInternal()
        {
            var nodes = GuidTracker.GetAll<T>();
            foreach (var node in nodes)
            {
                ((IGuidParent)node).UpdateChildren();
            }

            var anyChange = false;
            IGuidChild.UpdateParents<TParent, T>(ref anyChange, nodes);
            if (!anyChange) return;
            Debug.Log("Tree level " + typeof(TParent).Name + " marking dirty: children parents changed");
            TreeDifferentLevelMarkDirty<TParent>();
        }

        protected virtual void Update()
        {
            if (!Application.isPlaying && Application.isEditor)
            {
                ((IGuidChild)this).UpdateParent<TParent>();
            }
        }
        protected override void Awake()
        {
            base.Awake();
            Debug.Log("Tree level " + typeof(TParent).Name + " marking dirty: child creation");
            TreeDifferentLevelMarkDirty<TParent>();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Debug.Log("Tree level " + typeof(TParent).Name + " marking dirty: child removal");
            TreeDifferentLevelMarkDirty<TParent>();
        }
    }
}