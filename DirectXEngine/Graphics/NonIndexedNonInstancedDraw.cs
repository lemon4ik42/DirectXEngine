using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DirectXEngine
{
    public class NonIndexedNonInstancedDraw : RendererGraphicsSettings
    {
        public NonIndexedNonInstancedDraw(IReadOnlyList<StructuredBufferDescription> vertexBuffers, int verticesCount) : base(vertexBuffers, verticesCount)
        {

        }

        public override DrawMode DrawMode => DrawMode.Default;

        internal override void Draw(DeviceContext context)
        {
            context.InputAssembler.SetVertexBuffers(0, VertexBufferBindings);
            context.Draw(VerticesCount, 0);
        }
    }
}
