using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class BoidsContainer : MonoBehaviour
{
    private enum ChildType
    {
        None,
        Flock,
        Force
    }

    [Header("Shader override")] public ComputeShader updateShaderOverride;
    public ComputeShader boidsFlowFieldShaderOverride;
    public ComputeShader forceFlowFieldShaderOverride;
    [NonSerialized] private ComputeShader _updateShader;
    [NonSerialized] private ComputeShader _boidsFlowFieldShader;
    [NonSerialized] private ComputeShader _forceFlowFieldShader;

    private static readonly int TimeID = Shader.PropertyToID("time");
    private static readonly int DeltaTimeID = Shader.PropertyToID("delta_time");
    private static readonly int ForceIndexID = Shader.PropertyToID("force_index");
    private static readonly int FlockIndexID = Shader.PropertyToID("flock_index");
    private static readonly int ForceDataID = Shader.PropertyToID("forces");
    private static readonly int ForceCountID = Shader.PropertyToID("force_count");
    private static readonly int FlockDataID = Shader.PropertyToID("flocks");
    private static readonly int FlockCountID = Shader.PropertyToID("flock_count");
    private static readonly int BoidDataID = Shader.PropertyToID("boids");
    private static readonly int BoidCountID = Shader.PropertyToID("boid_count");
    private static readonly int StrideID = Shader.PropertyToID("stride");
    private static readonly int OrbitsID = Shader.PropertyToID("orbits");

    [NonSerialized] private readonly DualBuffer<BoidData> _boidBuffer = new();

    [NonSerialized] private Flock[] _flocks;
    [NonSerialized] private readonly DualBuffer<FlockData> _flockBuffer = new();

    [NonSerialized] private Force[] _forces;
    [NonSerialized] private readonly DualBuffer<ForceData> _forceBuffer = new();

    [Header("Visualization")] public uint orbitLength = 5;
    public uint orbitDensity = 20;
    public float orbitTimeStep = 0.05f;
    public bool liveUpdate = true;

    [NonSerialized] private uint _boidsCount;

#if UNITY_EDITOR
    [NonSerialized] public bool shouldDisplayFlowField = true;
    [NonSerialized] private ChildType _selectedChildType = ChildType.None;
    [NonSerialized] private int _selectedChildIndex = -1;
    
    [NonSerialized] private readonly DualBuffer<Vector2> _orbitsBuffer = new();
    
    [NonSerialized] private uint _orbitCount;
    
    [NonSerialized] private Rect _sceneViewWindow = Rect.zero;
    [NonSerialized] private bool _doUpdateFlowField;
    
    public uint FlockCount => (uint)(_flocks?.Length ?? 0);
    public uint ForceCount => (uint)(_forces?.Length ?? 0);
    
    public uint OrbitCount => _orbitCount;
    public Vector2[] OrbitBuffer => _orbitsBuffer.Data;
#endif

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
                foreach (var boid in flock.Boids)
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

#if UNITY_EDITOR
    private void PopulateOrbitsBuffer(out uint orbitsCount)
    {
        _orbitsBuffer.Ensure(orbitDensity * orbitDensity * orbitLength);

        var delta = new Vector2(_sceneViewWindow.width / orbitDensity, _sceneViewWindow.height / orbitDensity);
        var origin = _sceneViewWindow.min + delta * 0.5f;

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
#endif

    private void UpdateBoids(uint boidsCount)
    {
        uint flockIndex = 0;
        uint boidIndex = 0;
        foreach (var flock in _flocks)
        {
            using IEnumerator<Boid> enumerator = flock.Boids.GetEnumerator();
            for (; boidIndex < boidsCount; ++boidIndex)
            {
                if (_boidBuffer.Data[boidIndex].FlockIndex > flockIndex) break;

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
        _forceBuffer.Release();
#if UNITY_EDITOR
        _orbitsBuffer.Release();
#endif
    }

    private void Awake()
    {
        _updateShader = updateShaderOverride != null 
            ? updateShaderOverride
            : Resources.Load<ComputeShader>("Shaders/BoidsUpdate.compute");
#if UNITY_EDITOR
        _boidsFlowFieldShader = boidsFlowFieldShaderOverride != null 
            ? boidsFlowFieldShaderOverride
            : Resources.Load<ComputeShader>("Shaders/BoidsFlowField.compute");
        _forceFlowFieldShader = forceFlowFieldShaderOverride != null 
            ? forceFlowFieldShaderOverride
            : Resources.Load<ComputeShader>("Shaders/ForceFlowField.compute");
#endif
    }

    public static BoidsContainer FindParent(GameObject go)
    {
        for (var t = go.transform; t; t = t.parent)
        {
            var container = t.gameObject.GetComponent<BoidsContainer>();
            if (container) return container;
        }

        return null;
    }

    private void ComputeBoidsUpdate(uint boidsCount)
    {
        if (boidsCount == 0) return;

        _flockBuffer.Bind(_updateShader, 0, FlockDataID, FlockCountID);
        _boidBuffer.Bind(_updateShader, 0, BoidDataID, boidsCount, BoidCountID);
        _forceBuffer.Bind(_updateShader, 0, ForceDataID, ForceCountID);

        _updateShader.SetFloat(TimeID, Time.time);
        _updateShader.SetFloat(DeltaTimeID, Time.deltaTime);

        _updateShader.Dispatch(0, (int)boidsCount, 1, 1);

        _boidBuffer.ToLocal();
        UpdateBoids(boidsCount);
    }

#if UNITY_EDITOR
    private void ComputeFlockFlowField(uint flockIndex, uint boidsCount, uint orbitsCount)
    {
        if (!_boidsFlowFieldShader) return;

        _flockBuffer.Bind(_boidsFlowFieldShader, 0, FlockDataID, FlockCountID);
        _boidBuffer.Bind(_boidsFlowFieldShader, 0, BoidDataID, boidsCount, BoidCountID);
        _forceBuffer.Bind(_boidsFlowFieldShader, 0, ForceDataID);
        _orbitsBuffer.Bind(_boidsFlowFieldShader, 0, OrbitsID);

        _boidsFlowFieldShader.SetInt(ForceCountID, _flocks[flockIndex].includeForces ? _forces.Length : 0);

        _boidsFlowFieldShader.SetInt(FlockIndexID, (int)flockIndex);
        _boidsFlowFieldShader.SetFloat(TimeID, Application.isPlaying ? Time.time : 0.0f);
        _boidsFlowFieldShader.SetFloat(DeltaTimeID, orbitTimeStep);
        _boidsFlowFieldShader.SetInt(StrideID, (int)orbitLength);

        _boidsFlowFieldShader.Dispatch(0, (int)orbitsCount, 1, 1);

        _orbitsBuffer.ToLocal();
    }

    private void ComputeForceFlowField(uint forceIndex, uint orbitsCount)
    {
        if (!_forceFlowFieldShader) return;

        _forceBuffer.Bind(_forceFlowFieldShader, 0, ForceDataID);
        _orbitsBuffer.Bind(_forceFlowFieldShader, 0, OrbitsID);

        _forceFlowFieldShader.SetInt(ForceIndexID, (int)forceIndex);
        _forceFlowFieldShader.SetFloat(TimeID, Application.isPlaying ? Time.time : 0.0f);
        _forceFlowFieldShader.SetFloat(DeltaTimeID, orbitTimeStep);
        _forceFlowFieldShader.SetInt(StrideID, (int)orbitLength);

        _forceFlowFieldShader.Dispatch(0, (int)orbitsCount, 1, 1);

        _orbitsBuffer.ToLocal();
    }

    private bool ShouldDisplayFlowField(out ChildType type, out int index)
    {
        for (uint i = 0; i < _flocks.Length; ++i)
        {
            if (!Selection.Contains(_flocks[i].gameObject)) continue;
            type = ChildType.Flock;
            index = (int)i;
            return true;
        }

        for (uint i = 0; i < _forces.Length; ++i)
        {
            if (!Selection.Contains(_forces[i].gameObject)) continue;
            type = ChildType.Force;
            index = (int)i;
            return true;
        }

        if (Selection.Contains(gameObject))
        {
            if (_flocks.Length > 0)
            {
                type = ChildType.Flock;
                index = 0;
                return true;
            }

            if (_forces.Length > 0)
            {
                type = ChildType.Force;
                index = 0;
                return true;
            }
        }
        type = ChildType.None;
        index = -1;
        return false;
    }

    private bool ShouldUpdateFlowField()
    {
        var oldChildType = _selectedChildType;
        var oldChildIndex = _selectedChildIndex;
        shouldDisplayFlowField = ShouldDisplayFlowField(out _selectedChildType, out _selectedChildIndex);
        if (!shouldDisplayFlowField) return false;
        if (oldChildType != _selectedChildType || oldChildIndex != _selectedChildIndex) return true;
        return !Application.isPlaying || liveUpdate;
    }

    public void RequestFlowFieldUpdate()
    {
        if (!shouldDisplayFlowField || _selectedChildType == ChildType.None) return;

        if (_selectedChildType == ChildType.Flock)
        {
            PopulateFlocksBuffer(out var boidsAllocSize);
            PopulateBoidsBuffer(boidsAllocSize, out _boidsCount);
        }

        PopulateForcesBuffer();
        _doUpdateFlowField = true;
    }

    private void UpdateFlowFieldNoPopulate(uint boidsCount)
    {
        _doUpdateFlowField = false;
        if (!shouldDisplayFlowField || _selectedChildType == ChildType.None) return;
        if (_forceBuffer.AllocSize == 0 && _flockBuffer.AllocSize == 0) return;
        PopulateOrbitsBuffer(out _orbitCount);

        switch (_selectedChildType)
        {
            case ChildType.Flock:
                ComputeFlockFlowField((uint)_selectedChildIndex, boidsCount, _orbitCount);
                break;
            case ChildType.Force:
                ComputeForceFlowField((uint)_selectedChildIndex, _orbitCount);
                break;
            case ChildType.None:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    private void OnSceneGUI(SceneView sv)
    {
        if (!sv.camera) return;

        var llc = sv.camera.ViewportToWorldPoint(new Vector3(0f, 0f, 0));
        var urc = sv.camera.ViewportToWorldPoint(new Vector3(1f, 1f, 0));
        var window = new Rect(llc, urc - llc);

        if (window == _sceneViewWindow && !_doUpdateFlowField) return;

        _sceneViewWindow = window;
        UpdateFlowFieldNoPopulate(_boidsCount);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }
#endif

    private void Update()
    {
        if (!Application.isPlaying && Application.isEditor)
        {
            // This has been generated by the editor, so maybe some children has been added or deleted
            Reload();
#if UNITY_EDITOR
            if (ShouldUpdateFlowField()) RequestFlowFieldUpdate();
#endif
        }
        else
        {
            // Assume immutable flocks and forces, but properties might have changed
            PopulateFlocksBuffer(out var boidsAllocSize);
            PopulateBoidsBuffer(boidsAllocSize, out _boidsCount);
            PopulateForcesBuffer();
            ComputeBoidsUpdate(_boidsCount);

            // Post update actions
            foreach (var flock in _flocks)
            {
                flock.KillStrayBoids();
                flock.SpawnIfNeeded();
            }

#if UNITY_EDITOR
            if (ShouldUpdateFlowField()) UpdateFlowFieldNoPopulate(_boidsCount);
#endif
        }
    }
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