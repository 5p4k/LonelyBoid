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
        private int _localBufferUseCount;

        public ArraySegment<T> Data
        {
            get
            {
                EnsureLocalBuffer();
                return new ArraySegment<T>(_localBuffer, 0, _localBufferUseCount);
            }
        }

        private void EnsureLocalBuffer()
        {
            if (_localBuffer != null && _localBuffer.Length >= _localBufferUseCount) return;
            _localBuffer = new T[Math.Max(1, _localBufferUseCount)];
        }

        private void EnsureComputeBuffer()
        {
            if (_computeBuffer != null && _computeBuffer.count >= _localBufferUseCount) return;
            _computeBuffer?.Release();
            _computeBuffer = new ComputeBuffer(Math.Max(1, _localBufferUseCount), EntrySize);
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
            _computeBuffer.SetData(_localBuffer, 0, 0, _localBufferUseCount);
        }

        public void ComputeToLocal()
        {
            EnsureLocalBuffer();
            EnsureComputeBuffer();
            _computeBuffer.GetData(_localBuffer, 0, 0, _localBufferUseCount);
        }

        public void Resize(int count)
        {
            _localBufferUseCount = count;
            EnsureLocalBuffer();
        }

        public void Fill(IEnumerable<T> enumerable)
        {
            _localBuffer = enumerable.ToArray();
            _localBufferUseCount = _localBuffer.Length;
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
            shader.SetInt(sizeID, _localBufferUseCount);
        }
    }
}