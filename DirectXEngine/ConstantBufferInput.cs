using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Mathematics;

namespace DirectXEngine
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct ConstantBufferInput
    {
        public Matrix ModelViewProjection;
        public Matrix ModelLocalToWorldDirection;
        public Matrix ModelLocalToWorld;
        public Vector3 CameraPosition;
        public float GlobalIllumination;
        public int HasTexture;
        public int HasHeightMap;
    }
}
