using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization.Metadata;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DirectXEngine
{
    public abstract class RendererGraphicsSettings
    {
        public RendererGraphicsSettings(IReadOnlyList<StructuredBufferDescription> vertexBuffers, int verticesCount)
        {
            VertexBuffersDescriptions = new List<StructuredBufferDescription>(vertexBuffers);
            VerticesCount = verticesCount;
        }

        public static NonIndexedNonInstancedDraw DefaultDraw(IReadOnlyList<StructuredBufferDescription> vertexBuffers, int verticesCount) => 
            new NonIndexedNonInstancedDraw(vertexBuffers, verticesCount);

        public static IndexedDraw IndexedDraw(IReadOnlyList<StructuredBufferDescription> vertexBuffers, int verticesCount,
            byte[] indexBufferData, Format format) => 
            new IndexedDraw(vertexBuffers, verticesCount, indexBufferData, format);

        public abstract DrawMode DrawMode { get; }
        public int VerticesCount { get; }
        IReadOnlyList<StructuredBufferDescription> VertexBuffersDescriptions { get; }
        internal protected VertexBufferBinding[] VertexBufferBindings { get; private set; }
        private Buffer[] _VertexBuffers;

        internal abstract void Draw(DeviceContext context);

        internal virtual void InitializeBuffers(Device device)
        {
            _VertexBuffers = new Buffer[VertexBuffersDescriptions.Count];
            VertexBufferBindings = new VertexBufferBinding[VertexBuffersDescriptions.Count];

            for (int i = 0; i < _VertexBuffers.Length; i++)
            {
                StructuredBufferDescription description = VertexBuffersDescriptions[i];
                Buffer buffer = Buffer.Create(device, BindFlags.VertexBuffer, description.Data);

                _VertexBuffers[i] = buffer;
                VertexBufferBindings[i] = new VertexBufferBinding(buffer, description.Stride, 0);
            }
        }

        internal virtual void DisposeBuffersData()
        {
            foreach (StructuredBufferDescription description in VertexBuffersDescriptions)
                description.Dispose();
        }

        internal virtual void Dispose()
        {
            foreach (Buffer buffer in _VertexBuffers)
                buffer.Dispose();
        }
    }

    public class StructuredBufferDescription
    {
        public StructuredBufferDescription(byte[] data, int stride)
        {
            ExceptionHelper.ThrowIfNull(data);
            ExceptionHelper.ThrowIfOutOfRange(stride, 0, double.MaxValue);
            ExceptionHelper.ThrowByCondition(data.Length % stride != 0, "Buffer data length must a multiple of vertex buffer stride");

            Data = data.Length >= _MinElenetsCount ? data : new byte[_MinElenetsCount];
            Stride = stride;
            ElementsCount = data.Length / stride;
        }

        public byte[] Data { get; private set; }
        public int Stride { get; }
        public int ElementsCount { get; }
        private const int _MinElenetsCount = 16;

        public static StructuredBufferDescription Create<T>(IReadOnlyList<T> data) where T : struct => 
            new StructuredBufferDescription(EngineUtilities.ToByteArray(data), Marshal.SizeOf<T>());

        internal void Dispose() => Data = null;
    }
}
