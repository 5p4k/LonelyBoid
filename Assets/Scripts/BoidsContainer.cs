using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BoidsContainer : MonoBehaviour
{
    [Header("Shaders")]
    public ComputeShader updateShader;
    public ComputeShader fieldShader;

    BoidData[] _boidData = null;
    ComputeBuffer _boidDataBuffer = null;

    Flock[] _flocks = null;
    FlockData[] _flockData = null;
    ComputeBuffer _flockDataBuffer = null;

    void Start() {
        ReloadFlocks();
    }

    static void EnsureBuffer<T>(ref ComputeBuffer computeBuffer, ref T[] localBuffer, uint count) {
        if (count == 0) {
            return;
        }
        if (localBuffer == null || localBuffer.Length < count) {
            localBuffer = new T[count];
        }
        if (computeBuffer == null || computeBuffer.count < count) {
            if (computeBuffer != null) {
                computeBuffer.Release();
            }
            int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
            computeBuffer = new ComputeBuffer((int)count, size);
        }
    }

    public void ReloadFlocks() {
        _flocks = GetComponentsInChildren<Flock>(true);
        uint boidsCount = 0;
        foreach (Flock flock in _flocks) {
            boidsCount += flock.maxCount;
        }
        EnsureBuffer<BoidData>(ref _boidDataBuffer, ref _boidData, boidsCount);
        EnsureBuffer<FlockData>(ref _flockDataBuffer, ref _flockData, (uint)_flocks.Length);
    }

    void Awake() {
        if (updateShader == null) {
            updateShader = (ComputeShader)AssetDatabase.LoadAssetAtPath("Assets/Scripts/BoidsUpdate.compute", typeof(ComputeShader));
        }
        if (fieldShader == null) {
            fieldShader = (ComputeShader)AssetDatabase.LoadAssetAtPath("Assets/Scripts/BoidsField.compute", typeof(ComputeShader));
        }
    }


    int GeometryToBuffers(out uint boidCount, Flock returnIndexOf = null) {
        int returnIndex = -1;
        uint flockIndex = 0;
        boidCount = 0;
        foreach (Flock flock in _flocks) {
            if (flock == returnIndexOf) {
                returnIndex = (int)flockIndex;
            }
            if (flockIndex >= _flockData.Length) {
                break;
            }
            if (flock.enabled) {
                foreach (Boid boid in flock.boids) {
                    if (boidCount >= _boidData.Length) {
                        break;
                    }
                    _boidData[boidCount++] = boid.ToBufferData(flockIndex);
                }
            }
            _flockData[flockIndex++] = flock.ToBufferData();
        }
        return returnIndex;
    }

    void BuffersToGeometry(uint boidCount) {
        uint flockIndex = 0;
        uint boidIndex = 0;
        foreach (Flock flock in _flocks) {
            IEnumerator<Boid> enumerator = flock.boids.GetEnumerator();
            for (; boidIndex < boidCount; ++boidIndex) {
                if (_boidData[boidIndex].flockIndex > flockIndex) {
                    break;
                }
                if (enumerator.MoveNext()) {
                    enumerator.Current.FromBufferData(_boidData[boidIndex]);
                }
            }
            ++flockIndex;
        }
    }

    void ComputeUpdate(uint boidCount, float deltaTime) {
        if (boidCount == 0) {
            return;
        }
        Debug.Assert(_boidDataBuffer != null && _boidDataBuffer.count >= boidCount);
        Debug.Assert(_boidData != null && _boidData.Length >= boidCount);

        _boidDataBuffer.SetData(_boidData);
        _flockDataBuffer.SetData(_flockData);

        updateShader.SetBuffer(0, "boidData", _boidDataBuffer);
        updateShader.SetBuffer(0, "flockData", _flockDataBuffer);

        updateShader.SetInt("boidCount", (int)boidCount);
        updateShader.SetInt("flockCount", (int)_flockData.Length);
        updateShader.SetFloat("deltaTime", deltaTime);

        updateShader.Dispatch(0, Mathf.Max(1, Mathf.CeilToInt(boidCount / 1024.0f)), 1, 1);

        _boidDataBuffer.GetData(_boidData);
        _flockDataBuffer.GetData(_flockData);
    }


    void Update() {
        uint boidCount = 0;

        GeometryToBuffers(out boidCount);
        ComputeUpdate(boidCount, Time.deltaTime);
        BuffersToGeometry(boidCount);

        // Post update actions
        foreach (Flock flock in _flocks) {
            flock.KillStrayBoids();
            flock.SpawnIfNeeded();
        }
    }

    public void ComputeAccelerationField(Flock flock, Rect window, RenderTexture texture) {
        bool didReload = false;
        if (_flockData == null) {
            didReload = true;
            ReloadFlocks();
        }

        uint boidCount = 0;
        int flockIndex = GeometryToBuffers(out boidCount, flock);

        if (flockIndex < 0 && !didReload) {
            didReload = true;
            ReloadFlocks();
            // Retry
            flockIndex = GeometryToBuffers(out boidCount, flock);
        }
        if (flockIndex < 0) {
            Debug.LogError("Flock not found in this manager.");
            return;
        }

        _boidDataBuffer.SetData(_boidData);
        _flockDataBuffer.SetData(_flockData);

        fieldShader.SetBuffer(0, "boidData", _boidDataBuffer);
        fieldShader.SetBuffer(0, "flockData", _flockDataBuffer);

        fieldShader.SetInt("boidCount", (int)boidCount);
        fieldShader.SetInt("flockIndex", flockIndex);

        float[] texWin = new float[4] {window.xMin, window.yMin, window.width, window.height};
        int[] texSz = new int[2] {texture.width, texture.height};

        fieldShader.SetFloats("textureWindow", texWin);
        fieldShader.SetInts("textureSize", texSz);

        fieldShader.SetTexture(0, "textureOutput", texture);
        fieldShader.Dispatch(0, texture.width, texture.height, 1);
    }

    void OnDestroy() {
        if (_flockDataBuffer != null) {
            _flockDataBuffer.Release();
        }
        if (_boidDataBuffer != null) {
            _boidDataBuffer.Release();
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BoidsContainer))]
public class BoidsContainerEditor : Editor {
    public override void OnInspectorGUI() {
        BoidsContainer container = target as BoidsContainer;

        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Add a new flock")) {
            var existingFlockCount = container.GetComponentsInChildren<Flock>(true).Length;
            GameObject empty = new GameObject("Flock " + (existingFlockCount + 1), typeof(Flock));
            empty.transform.parent = container.transform;
        }
    }
}
#endif