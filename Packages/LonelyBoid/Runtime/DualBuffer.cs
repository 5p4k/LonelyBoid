using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class DualBuffer<T>
    {
        private static int EntrySize => System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        private ComputeBuffer _computeBuffer;
        private T[] _localBuffer;

        public int Count { get; private set; }

        public ArraySegment<T> Data
        {
            get
            {
                EnsureLocalBuffer();
                return new ArraySegment<T>(_localBuffer, 0, Count);
            }
        }

        private void EnsureLocalBuffer()
        {
            if (_localBuffer != null && _localBuffer.Length >= Count) return;
            _localBuffer = new T[Math.Max(1, Count)];
        }

        private void EnsureComputeBuffer()
        {
            if (_computeBuffer != null && _computeBuffer.count >= Count) return;
            _computeBuffer?.Release();
            _computeBuffer = new ComputeBuffer(Math.Max(1, Count), EntrySize);
        }

        public void Release()
        {
            _computeBuffer?.Release();
            _localBuffer = null;
            _computeBuffer = null;
        }

        public void LocalToCompute()
        {
            EnsureLocalBuffer();
            EnsureComputeBuffer();
            _computeBuffer.SetData(_localBuffer, 0, 0, Count);
        }

        public void ComputeToLocal()
        {
            EnsureLocalBuffer();
            EnsureComputeBuffer();
            _computeBuffer.GetData(_localBuffer, 0, 0, Count);
        }

        public ArraySegment<T> Resize(int count)
        {
            Count = count;
            EnsureLocalBuffer();
            return Data;
        }

        public ArraySegment<T> Fill(IEnumerable<T> enumerable)
        {
            _localBuffer = enumerable.ToArray();
            Count = _localBuffer.Length;
            return Data;
        }

        public void Bind(ComputeShader shader, int kernelIndex, int bufferID, bool update = false)
        {
            if (update) LocalToCompute();
            else EnsureComputeBuffer();
            shader.SetBuffer(kernelIndex, bufferID, _computeBuffer);
        }

        public void Bind(ComputeShader shader, int kernelIndex, int bufferID, int sizeID, bool update = false)
        {
            Bind(shader, kernelIndex, bufferID, update);
            shader.SetInt(sizeID, Count);
        }
    }
}