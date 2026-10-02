using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Collections.Generic;
using Device = SharpDX.Direct3D11.Device;

namespace DirectXEngine
{
    public class IndexedDraw : RendererGraphicsSettings
    {
        public IndexedDraw(IReadOnlyList<StructuredBufferDescription> vertexBuffers, int verticesCount, byte[] indexBufferData, Format format) 
            : base(vertexBuffers, verticesCount)
        {
            ExceptionHelper.ThrowIfNull(indexBufferData);
            int stride = format.SizeOfInBytes();
            Format = format;

            IndexBufferDescription = new StructuredBufferDescription(indexBufferData, stride);
        }

        public Format Format { get; private set; }
        public override DrawMode DrawMode => DrawMode.Indexed;
        public StructuredBufferDescription IndexBufferDescription { get; }
        private Buffer _IndexBuffer;

        internal override void Draw(DeviceContext context)
        {
            context.InputAssembler.SetVertexBuffers(0, VertexBufferBindings);
            context.InputAssembler.SetIndexBuffer(_IndexBuffer, Format, 0);
            context.DrawIndexed(IndexBufferDescription.ElementsCount, 0, 0);
        }

        internal override void InitializeBuffers(Device device)
        {
            base.InitializeBuffers(device);
            _IndexBuffer = Buffer.Create(device, BindFlags.IndexBuffer, IndexBufferDescription.Data);
        }

        internal override void DisposeBuffersData()
        {
            base.DisposeBuffersData();
            IndexBufferDescription.Dispose();
        }

        internal override void Dispose()
        {
            base.Dispose();
            _IndexBuffer?.Dispose();
        }
    }
}
