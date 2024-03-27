using System;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace saccardi.lonelyboid.Editor
{
    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public abstract class EditorWithOrbits<T, TManager> : UnityEditor.Editor
        where TManager : OrbitsManager<T> where T : MonoBehaviour
    {
        // Last-type properties to ensure minimal updates -------------------------------------------------------------- 
        [field: NonSerialized] protected static Rect LastWindow = Rect.zero;
        [field: NonSerialized] protected static int LastFrame = -1;
        [field: NonSerialized] protected static T LastTarget { get; private set; }

        [field: NonSerialized] protected static EditorWithOrbits<T, TManager> LastEditor;

        // Orbits manager and backing storage --------------------------------------------------------------------------
        [field: NonSerialized] protected static TManager Manager { get; private set; }
        [field: NonSerialized] protected static bool OrbitsDirty { get; set; }
        [field: NonSerialized] protected static Vector2[][] Orbits;

        // Other helper objects ----------------------------------------------------------------------------------------
        private UnityEditor.Editor _orbitsManagerEditor;
        [SerializeField] public bool liveUpdate = true;

        // Public interfaces to implement ------------------------------------------------------------------------------

        protected virtual void DrawGizmos(T forTarget, GizmoType gizmoType)
        {
        }

        // Events ------------------------------------------------------------------------------------------------------
        protected virtual void OnEnable()
        {
            if (!Manager) Manager = CreateInstance<TManager>();
            EditorApplication.update += Update;
            LastEditor = this;
            CreateCachedEditor(Manager, null, ref _orbitsManagerEditor);
        }

        protected virtual void OnDisable()
        {
            EditorApplication.update -= Update;
            LastEditor = null;
        }

        protected virtual void OnDestroy()
        {
            if (!Manager) return;
            Manager.Release();
            Manager = null;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Orbits preview", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            liveUpdate = EditorGUILayout.Toggle("Live Update", liveUpdate);
            if (EditorGUI.EndChangeCheck())
            {
                OrbitsDirty = true;
            }

            if (_orbitsManagerEditor.DrawDefaultInspector())
            {
                OrbitsDirty = true;
            }
        }

        public static bool IsPlaying => Application.isPlaying && !EditorApplication.isPaused;

        [SuppressMessage("ReSharper", "VirtualMemberNeverOverridden.Global")]
        protected virtual void Update()
        {
            if (!IsPlaying)
            {
                if (LastFrame < 0) return;

                // Did we just stop playing instead?
                LastFrame = -1;
                OrbitsDirty = true;
                RequestNewOrbitsIfNeeded(LastTarget);
                return;
            }

            // During playing, request one update every frame.
            if (!liveUpdate || Time.frameCount == LastFrame) return;
            LastFrame = Time.frameCount;
            OrbitsDirty = true;
            RequestNewOrbitsIfNeeded(LastTarget);
        }

        protected virtual void OnSceneGUI()
        {
            SeenTarget(target as T);
        }

        protected static void OnDrawGizmos(T target, GizmoType gizmoType)
        {
            var active = (gizmoType & GizmoType.Active) != 0;

            // When not playing, request one every time gizmos need to be redrawn
            if (LastEditor)
            {
                if (!IsPlaying && active) RequestNewOrbitsIfNeeded(target);
                LastEditor.DrawGizmos(target, gizmoType);
            }

            if (active) FetchOrbitsIfNeededAndDraw();
        }

        // Helper management methods -----------------------------------------------------------------------------------

        private static void SeenTarget([CanBeNull] T theTarget)
        {
            if (LastTarget == theTarget) return;
            LastTarget = theTarget;
            OrbitsDirty = true;
        }


        private static void RequestNewOrbitsIfNeeded([CanBeNull] T forTarget)
        {
            var cam = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.camera : null;
            // Check if we have changed the camera location
            if (cam)
            {
                var llc = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0));
                var urc = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0));
                var window = new Rect(llc, urc - llc);
                if (window != LastWindow)
                {
                    OrbitsDirty = true;
                    LastWindow = window;
                }
            }

            // Check if any change requires us to request new orbits
            if (forTarget != null && OrbitsDirty && Manager)
            {
                OrbitsDirty = !Manager.RequestNewOrbits(forTarget, LastWindow);
            }
        }

        private static void FetchOrbitsIfNeededAndDraw()
        {
            // Check if we have any orbits pending to fetch, and if so, update
            if (Manager && Manager.NeedsFetch)
            {
                Orbits = Manager.FetchOrbits();
            }

            // Draw orbits
            if (Orbits is not { Length: > 0 }) return;

            Vector2 pxSize = Camera.current.ScreenToWorldPoint(new Vector3(1, 1, 0))
                             - Camera.current.ScreenToWorldPoint(Vector3.zero);

            var discRadius = 2.0f * Mathf.Max(pxSize.x, pxSize.y);
            foreach (var orbit in Orbits)
            {
                Handles.DrawSolidDisc(orbit[0], Vector3.back, discRadius);
                for (var i = 1; i < orbit.Length; ++i)
                {
                    Handles.DrawLine(orbit[i - 1], orbit[i]);
                }
            }
        }
    }
}