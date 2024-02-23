using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BoidsContainer : MonoBehaviour
{
    [Header("Shaders")] public ComputeShader updateShader;
    public ComputeShader fieldShader;

    private static readonly int TimeID = Shader.PropertyToID("time");
    private static readonly int DeltaTimeID = Shader.PropertyToID("delta_time");
    private static readonly int ForceIndexID = Shader.PropertyToID("force_index");
    private static readonly int TextureWindowID = Shader.PropertyToID("texture_window");
    private static readonly int TextureSizeID = Shader.PropertyToID("texture_size");
    private static readonly int TextureOutputID = Shader.PropertyToID("texture_output");
    private static readonly int ForceDataID = Shader.PropertyToID("force_data");
    private static readonly int ForceCountID = Shader.PropertyToID("force_count");
    private static readonly int FlockDataID = Shader.PropertyToID("flock_data");
    private static readonly int FlockCountID = Shader.PropertyToID("flock_count");
    private static readonly int BoidDataID = Shader.PropertyToID("boid_data");
    private static readonly int BoidCountID = Shader.PropertyToID("boid_count");

    private readonly DualBuffer<BoidData> _boidBuffer = new();

    private Flock[] _flocks;
    private readonly DualBuffer<FlockData> _flockBuffer = new();

    private Force[] _forces;
    private readonly DualBuffer<ForceData> _forceBuffer = new();

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

        if (fieldShader == null)
        {
            fieldShader = (ComputeShader)AssetDatabase.LoadAssetAtPath(
                "Assets/Scripts/BoidsField.compute", typeof(ComputeShader));
        }
    }
#endif

    private void ComputeBoidsUpdate()
    {
        // Update all buffers
        PopulateFlocksBuffer(out var boidsAllocSize);
        PopulateBoidsBuffer(boidsAllocSize, out var boidsCount);
        PopulateForcesBuffer();

        _flockBuffer.Bind(updateShader, 0, FlockDataID, FlockCountID);
        _boidBuffer.Bind(updateShader, 0, BoidDataID, boidsCount, BoidCountID);
        _forceBuffer.Bind(updateShader, 0, ForceDataID, ForceCountID);

        updateShader.SetFloat(TimeID, Time.time);
        updateShader.SetFloat(DeltaTimeID, Time.deltaTime);

        updateShader.Dispatch(0, Mathf.Max(1, Mathf.CeilToInt(boidsCount / 1024.0f)), 1, 1);

        _boidBuffer.ToLocal();
        UpdateBoids(boidsCount);
    }

    private void Update()
    {
        ComputeBoidsUpdate();

        // Post update actions
        foreach (var flock in _flocks)
        {
            flock.KillStrayBoids();
            flock.SpawnIfNeeded();
        }
    }

    public void ComputeAccelerationField(Flock flock, Rect window, RenderTexture texture)
    {
        // bool didReload = false;
        // if (_flockData == null)
        // {
        //     didReload = true;
        //     Recache();
        // }
        //
        // uint boidCount = 0;
        // int flockIndex = GeometryToBuffers(out boidCount, flock);
        //
        // if (flockIndex < 0 && !didReload)
        // {
        //     didReload = true;
        //     Recache();
        //     // Retry
        //     flockIndex = GeometryToBuffers(out boidCount, flock);
        // }
        //
        // if (flockIndex < 0)
        // {
        //     Debug.LogError("Flock not found in this manager.");
        //     return;
        // }
        //
        // _boidDataBuffer.SetData(_boidData);
        // _flockDataBuffer.SetData(_flockData);
        // _forceDataBuffer.SetData(_forceData);
        //
        // fieldShader.SetBuffer(0, "boidData", _boidDataBuffer);
        // fieldShader.SetBuffer(0, "flockData", _flockDataBuffer);
        // fieldShader.SetBuffer(0, "forceData", _forceDataBuffer);
        //
        // fieldShader.SetInt("boidCount", (int)boidCount);
        // fieldShader.SetInt("flockIndex", flockIndex);
        // fieldShader.SetInt("forceCount", (int)_forceData.Length);
        //
        // fieldShader.SetFloat("time", Time.time);
        //
        // float[] texWin = new float[4] { window.xMin, window.yMin, window.width, window.height };
        // int[] texSz = new int[2] { texture.width, texture.height };
        //
        // fieldShader.SetFloats("textureWindow", texWin);
        // fieldShader.SetInts("textureSize", texSz);
        //
        // fieldShader.SetTexture(0, "textureOutput", texture);
        // fieldShader.Dispatch(0, texture.width, texture.height, 1);
    }

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
    }
#endif
}


public class DualBuffer<T>
{
    private static int EntrySize => System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));

    public T[] Data { get; private set; }

    private ComputeBuffer _computeBuffer;

    private uint AllocSize => (uint)(Data?.Length ?? 0);

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