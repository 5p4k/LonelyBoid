using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BoidManager : MonoBehaviour
{

    public struct BoidData {
        public uint flockIndex;
        public Vector2 position;
        public Vector2 direction;
        public float speed;
    }

    public struct FlockData {
        public float viewRadius;
        public float viewAngleTau;

        public float avoidRadius;
        public float avoidAngleTau;

        public float separationWeight;
        public float alignmentWeight;
        public float cohesionWeight;
        public float survivalWeight;

        public float maxAcceleration;
        public float minSpeed;
        public float maxSpeed;
        public float maxAngularSpeedTau;

        public Vector2 center;
        public float spawnRadius;
        public float killRadius;
    }

    public List<Flock> flocks = new List<Flock>();

    public ComputeShader updateShader;
    public ComputeShader visualizeShader;

    BoidData[] _boidData = null;
    ComputeBuffer _boidDataBuffer = null;

    FlockData[] _flockData = null;
    ComputeBuffer _flockDataBuffer = null;

    uint GetMaxBoidsCount() {
        uint retval = 0;
        foreach (Flock flock in flocks) {
            if (flock != null) {
                retval += flock.maxCount;
            }
        }
        return retval;
    }


    static void EnsureBuffer<T>(ref ComputeBuffer computeBuffer, ref T[] localBuffer, uint length) {
        if (length == 0) {
            return;
        }
        if (localBuffer == null || localBuffer.Length < length) {
            localBuffer = new T[length];
        }
        if (computeBuffer == null || computeBuffer.count < length) {
            if (computeBuffer != null) {
                computeBuffer.Release();
            }
            int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
            computeBuffer = new ComputeBuffer((int)length, size);
        }
    }

    void PopulateLocalBuffers(out uint boidCount, out uint flockCount) {
        EnsureBuffer<BoidData>(ref _boidDataBuffer, ref _boidData, GetMaxBoidsCount());
        EnsureBuffer<FlockData>(ref _flockDataBuffer, ref _flockData, (uint)flocks.Count);

        uint boidIndex = 0;
        uint flockIndex = 0;
        foreach (Flock flock in flocks) {
            if (flock != null) {
                foreach (Boid boid in flock.boids) {
                    _boidData[boidIndex].flockIndex = flockIndex;
                    _boidData[boidIndex].position = boid.transform.position;
                    _boidData[boidIndex].direction = boid.transform.up;
                    _boidData[boidIndex].speed = boid.speed;
                    ++boidIndex;
                }
                _flockData[flockIndex].viewRadius = flock.viewRadius;
                _flockData[flockIndex].viewAngleTau = flock.viewAngleTau;
                _flockData[flockIndex].avoidRadius = flock.avoidRadius;
                _flockData[flockIndex].avoidAngleTau = flock.avoidAngleTau;
                _flockData[flockIndex].separationWeight = flock.separationWeight;
                _flockData[flockIndex].alignmentWeight = flock.alignmentWeight;
                _flockData[flockIndex].cohesionWeight = flock.cohesionWeight;
                _flockData[flockIndex].survivalWeight = flock.survivalWeight;
                _flockData[flockIndex].maxAcceleration = flock.maxAcceleration;
                _flockData[flockIndex].minSpeed = flock.minSpeed;
                _flockData[flockIndex].maxSpeed = flock.maxSpeed;
                _flockData[flockIndex].maxAngularSpeedTau = flock.maxAngularSpeedTau;
                _flockData[flockIndex].center = flock.transform.position;
                _flockData[flockIndex].spawnRadius = flock.spawnRadius;
                _flockData[flockIndex].killRadius = flock.killRadius;
            }
            ++flockIndex;
        }
        boidCount = boidIndex;
        flockCount = flockIndex;
    }

    void RunComputeShader(uint boidCount, uint flockCount, float deltaTime) {
        if (boidCount == 0) {
            return;
        }
        Debug.Assert(_boidDataBuffer != null && _boidDataBuffer.count >= boidCount);
        Debug.Assert(_flockDataBuffer != null && _flockDataBuffer.count >= flockCount);
        Debug.Assert(_boidData != null && _boidData.Length >= boidCount);
        Debug.Assert(_flockData != null && _flockData.Length >= flockCount);

        _boidDataBuffer.SetData(_boidData);
        _flockDataBuffer.SetData(_flockData);

        updateShader.SetBuffer(0, "boidData", _boidDataBuffer);
        updateShader.SetBuffer(0, "flockData", _flockDataBuffer);

        updateShader.SetInt("boidCount", (int)boidCount);
        updateShader.SetInt("flockCount", (int)flockCount);
        updateShader.SetFloat("deltaTime", deltaTime);

        updateShader.Dispatch(0, Mathf.Max(1, Mathf.CeilToInt(boidCount / 1024.0f)), 1, 1);

        _boidDataBuffer.GetData(_boidData);
        _flockDataBuffer.GetData(_flockData);
    }


    void Update() {
        uint boidCount;
        uint flockCount;
        PopulateLocalBuffers(out boidCount, out flockCount);

        RunComputeShader(boidCount, flockCount, Time.deltaTime);

        // Apply update
        uint boidIndex = 0;
        foreach (Flock flock in flocks) {
            if (flock != null) {
                foreach (Boid boid in flock.boids) {
                    boid.transform.position = _boidData[boidIndex].position;
                    boid.transform.up = _boidData[boidIndex].direction;
                    boid.speed = _boidData[boidIndex].speed;
                    ++boidIndex;
                }
                flock.KillStrayBoids();
            }
        }
    }

    public void RenderFlockField(Flock flock, Rect window, RenderTexture texture) {
        int flockIndex = flocks.IndexOf(flock);
        if (flockIndex < 0) {
            return;
        }

        uint boidCount;
        uint flockCount;
        PopulateLocalBuffers(out boidCount, out flockCount);

        _boidDataBuffer.SetData(_boidData);
        _flockDataBuffer.SetData(_flockData);

        visualizeShader.SetBuffer(0, "boidData", _boidDataBuffer);
        visualizeShader.SetBuffer(0, "flockData", _flockDataBuffer);

        visualizeShader.SetInt("boidCount", (int)boidCount);
        visualizeShader.SetInt("flockIndex", flockIndex);

        float[] texWin = new float[4] {window.xMin, window.yMin, window.width, window.height};
        int[] texSz = new int[2] {texture.width, texture.height};

        visualizeShader.SetFloats("textureWindow", texWin);
        visualizeShader.SetInts("textureSize", texSz);

        visualizeShader.SetFloat("maxValue", flock.visualizationMaxValue);

        visualizeShader.SetTexture(0, "textureOutput", texture);
        visualizeShader.Dispatch(0, texture.width, texture.height, 1);
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
