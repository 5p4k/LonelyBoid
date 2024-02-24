using System;
using System.Collections.Generic;
using Unity.Burst;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

public class BoidsContainer : MonoBehaviour
{
    [Header("Shaders")] public ComputeShader updateShader;
    public ComputeShader boidsFlowFieldShader;

    private static readonly int TimeID = Shader.PropertyToID("time");
    private static readonly int DeltaTimeID = Shader.PropertyToID("delta_time");
    private static readonly int ForceIndexID = Shader.PropertyToID("force_index");
    private static readonly int FlockIndexID = Shader.PropertyToID("flock_index");
    private static readonly int ForceDataID = Shader.PropertyToID("force_data");
    private static readonly int ForceCountID = Shader.PropertyToID("force_count");
    private static readonly int FlockDataID = Shader.PropertyToID("flock_data");
    private static readonly int FlockCountID = Shader.PropertyToID("flock_count");
    private static readonly int BoidDataID = Shader.PropertyToID("boid_data");
    private static readonly int BoidCountID = Shader.PropertyToID("boid_count");
    private static readonly int StrideID = Shader.PropertyToID("stride");
    private static readonly int OrbitsID = Shader.PropertyToID("orbits");

    private readonly DualBuffer<BoidData> _boidBuffer = new();

    private Flock[] _flocks;
    private readonly DualBuffer<FlockData> _flockBuffer = new();

    private Force[] _forces;
    private readonly DualBuffer<ForceData> _forceBuffer = new();

    private readonly DualBuffer<Vector2> _orbitsBuffer = new();

    [Header("Visualization")] public uint orbitLength = 10;
    public float orbitDensity = 10.0f;
    public float orbitTimeStep = 0.1f;
    public bool liveUpdate = true;

    private uint _orbitCount;
    private static Rect _lastEditorScreen = Rect.zero;

    private void PopulateFlocksBuffer(out uint boidsAllocSize)
    {
        _flockBuffer.Ensure((uint)_flocks.Length);
        uint flockIndex = 0;
        boidsAllocSize = 0;
        foreach (var flock in _flocks)
        {
            boidsAllocSize += flock.maxCount;
            _flockBuffer.Data[flockIndex++] = flock.ToBufferData();
        }
    }

    private void PopulateBoidsBuffer(uint boidsAllocSize, out uint boidsCount)
    {
        _boidBuffer.Ensure(boidsAllocSize);
        uint flockIndex = 0;
        uint boidIndex = 0;
        foreach (var flock in _flocks)
        {
            if (flock.enabled)
            {
                foreach (var boid in flock.boids)
                {
                    _boidBuffer.Data[boidIndex++] = boid.ToBufferData(flockIndex);
                }
            }

            ++flockIndex;
        }

        boidsCount = boidIndex;
    }

    private void PopulateForcesBuffer()
    {
        _forceBuffer.Ensure((uint)_forces.Length);
        uint forceIndex = 0;
        foreach (var force in _forces)
        {
            _forceBuffer.Data[forceIndex++] = force.ToBufferData();
        }
    }

    private void PopulateOrbitsBuffer(Rect window, out uint orbitsCount)
    {
        _orbitsBuffer.Ensure((uint)(orbitDensity * orbitDensity * orbitLength));

        var delta = new Vector2(window.width / orbitDensity, window.height / orbitDensity);
        var origin = window.min + delta * 0.5f;

        uint orbitIndex = 0;
        for (uint x = 0; x < orbitDensity; ++x)
        {
            for (uint y = 0; y < orbitDensity; ++y)
            {
                var position = origin + new Vector2(x, y) * delta;
                _orbitsBuffer.Data[orbitIndex++ * orbitLength] = position;
            }
        }

        orbitsCount = orbitIndex;
    }

    private void UpdateBoids(uint boidsCount)
    {
        uint flockIndex = 0;
        uint boidIndex = 0;
        foreach (var flock in _flocks)
        {
            using IEnumerator<Boid> enumerator = flock.boids.GetEnumerator();
            for (; boidIndex < boidsCount; ++boidIndex)
            {
                if (_boidBuffer.Data[boidIndex].flockIndex > flockIndex) break;

                if (enumerator.MoveNext())
                {
                    enumerator.Current!.FromBufferData(_boidBuffer.Data[boidIndex]);
                }
            }

            ++flockIndex;
        }
    }


    /**
     * Instantly recache flocks and forces. Call when programmatically adding or removing any of them.
     */
    // ReSharper disable once MemberCanBePrivate.Global
    public void Reload()
    {
        _flocks = GetComponentsInChildren<Flock>(true);
        _forces = GetComponentsInChildren<Force>(true);
    }

    private void Start()
    {
        Reload();
    }

    private void OnDestroy()
    {
        _flockBuffer.Release();
        _boidBuffer.Release();
    }

#if UNITY_EDITOR
    private void Awake()
    {
        if (updateShader == null)
        {
            updateShader = (ComputeShader)AssetDatabase.LoadAssetAtPath(
                "Assets/Scripts/BoidsUpdate.compute", typeof(ComputeShader));
        }

        if (boidsFlowFieldShader == null)
        {
            boidsFlowFieldShader = (ComputeShader)AssetDatabase.LoadAssetAtPath(
                "Assets/Scripts/BoidsFlowField.compute", typeof(ComputeShader));
        }
    }

    private void OnValidate()
    {
        BoidsContainerEditor.EditorScreenWindowChanged(out var window);
        RecomputeFlowField(window);
    }
#endif

    private void ComputeBoidsUpdate(uint boidsCount)
    {
        _flockBuffer.Bind(updateShader, 0, FlockDataID, FlockCountID);
        _boidBuffer.Bind(updateShader, 0, BoidDataID, boidsCount, BoidCountID);
        _forceBuffer.Bind(updateShader, 0, ForceDataID, ForceCountID);

        updateShader.SetFloat(TimeID, Time.time);
        updateShader.SetFloat(DeltaTimeID, Time.deltaTime);

        updateShader.Dispatch(0, Mathf.Max(1, Mathf.CeilToInt(boidsCount / 1024.0f)), 1, 1);

        _boidBuffer.ToLocal();
        UpdateBoids(boidsCount);
    }
    
    private void ComputeFlowField(uint flockIndex, uint boidsCount, uint orbitsCount)
    {
        _flockBuffer.Bind(boidsFlowFieldShader, 0, FlockDataID, FlockCountID);
        _boidBuffer.Bind(boidsFlowFieldShader, 0, BoidDataID, boidsCount, BoidCountID);
        _forceBuffer.Bind(boidsFlowFieldShader, 0, ForceDataID, ForceCountID);
        _orbitsBuffer.Bind(boidsFlowFieldShader, 0, OrbitsID);

        boidsFlowFieldShader.SetInt(FlockIndexID, (int)flockIndex);
        boidsFlowFieldShader.SetFloat(TimeID, Time.time);
        boidsFlowFieldShader.SetFloat(DeltaTimeID, orbitTimeStep);
        boidsFlowFieldShader.SetInt(StrideID, (int)orbitLength);

        boidsFlowFieldShader.Dispatch(0, (int)orbitsCount, 1, 1);

        _orbitsBuffer.ToLocal();
    }

    private void Update()
    {
        PopulateFlocksBuffer(out var boidsAllocSize);
        PopulateBoidsBuffer(boidsAllocSize, out var boidsCount);
        PopulateForcesBuffer();

        ComputeBoidsUpdate(boidsCount);

        // Post update actions
        foreach (var flock in _flocks)
        {
            flock.KillStrayBoids();
            flock.SpawnIfNeeded();
        }

#if UNITY_EDITOR
        if (!liveUpdate) return;

        BoidsContainerEditor.EditorScreenWindowChanged(out var window);
        PopulateOrbitsBuffer(window, out _orbitCount);
        ComputeFlowField(0, boidsCount, _orbitCount);
#endif
    }
    
    public void RecomputeFlowField(Rect window)
    {
        if (_flocks == null || _flocks.Length == 0)
        {
            // TODO not this
            Reload();
        }
        
        PopulateFlocksBuffer(out var boidsAllocSize);
        PopulateBoidsBuffer(boidsAllocSize, out var boidsCount);
        PopulateForcesBuffer();
        PopulateOrbitsBuffer(window, out _orbitCount);

        ComputeFlowField(0, boidsCount, _orbitCount);
    }

#if UNITY_EDITOR
    public void DrawFlowFieldHandles()
    {
        Vector2 pxSize = Camera.current.ScreenToWorldPoint(new Vector3(1, 1, 0))
                         - Camera.current.ScreenToWorldPoint(Vector3.zero);

        var discRadius = 2.0f * Mathf.Max(pxSize.x, pxSize.y);

        for (uint i = 0; i < _orbitCount * orbitLength; ++i)
        {
            if (i % orbitLength == 0)
            {
                Handles.DrawSolidDisc(_orbitsBuffer.Data[i], Vector3.back, discRadius);
            }
            else
            {
                Handles.DrawLine(_orbitsBuffer.Data[i - 1], _orbitsBuffer.Data[i]);
            }
        }
    }
#endif

#if UNITY_EDITOR
    [CustomEditor(typeof(BoidsContainer))]
    public class BoidsContainerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var container = target as BoidsContainer;

            DrawDefaultInspector();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("New flock"))
            {
                GameObject unused = new("Flock " + (container!._flocks.Length + 1), typeof(Flock))
                {
                    transform =
                    {
                        parent = container.transform
                    }
                };
            }

            if (GUILayout.Button("New force"))
            {
                GameObject unused = new("Force " + (container!._forces.Length + 1), typeof(Force))
                {
                    transform =
                    {
                        parent = container.transform
                    }
                };
            }

            EditorGUILayout.EndHorizontal();
        }

        public static bool EditorScreenWindowChanged(out Rect window)
        {
            if (!Camera.current)
            {
                window = Rect.zero;
                return false;
            }

            var llc = Camera.current.ViewportToWorldPoint(new Vector3(0f, 0f, 0));
            var urc = Camera.current.ViewportToWorldPoint(new Vector3(1f, 1f, 0));

            window = new Rect(llc, urc - llc);

            var changed = window == _lastEditorScreen;
            _lastEditorScreen = window;

            return changed;
        }

        public void OnSceneGUI()
        {
            var container = target as BoidsContainer;

            if (!Application.isPlaying)
            {
                if (EditorScreenWindowChanged(out var window))
                {
                    _lastEditorScreen = window;
                    container!.RecomputeFlowField(window);
                }
            }

            container!.DrawFlowFieldHandles();
        }
    }
#endif
}


public class DualBuffer<T>
{
    private static int EntrySize => System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));

    public T[] Data { get; private set; }

    private ComputeBuffer _computeBuffer;

    public uint AllocSize => (uint)(Data?.Length ?? 0);

    public void Ensure(uint allocSize)
    {
        if (allocSize > 0 && (Data == null || Data.Length < allocSize))
        {
            Data = new T[allocSize];
        }

        if (_computeBuffer != null && _computeBuffer.count >= allocSize) return;

        _computeBuffer?.Release();

        // Still create at least a 1-entry buffer otherwise we cannot set the compute shader up
        _computeBuffer = new ComputeBuffer((int)Mathf.Max(allocSize, 1), EntrySize);
    }

    public void Bind(ComputeShader shader, int kernelIndex, int fieldID)
    {
        if (_computeBuffer == null)
        {
            throw new InvalidOperationException("You must call Ensure before.");
        }

        _computeBuffer.SetData(Data);
        shader.SetBuffer(kernelIndex, fieldID, _computeBuffer);
    }

    public void Bind(ComputeShader shader, int kernelIndex, int fieldID, uint useSize, int useSizeFieldID)
    {
        Bind(shader, kernelIndex, fieldID);
        shader.SetInt(useSizeFieldID, (int)useSize);
    }

    public void Bind(ComputeShader shader, int kernelIndex, int fieldID, int allocSizeFieldID)
    {
        Bind(shader, kernelIndex, fieldID);
        shader.SetInt(allocSizeFieldID, (int)AllocSize);
    }

    public void ToLocal()
    {
        if (_computeBuffer == null)
        {
            throw new InvalidOperationException("You must call Ensure before.");
        }

        _computeBuffer.GetData(Data);
    }

    public void Release()
    {
        _computeBuffer?.Release();
    }
}