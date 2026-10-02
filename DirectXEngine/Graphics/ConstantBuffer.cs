using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;
using System;

namespace DirectXEngine
{
    public class ConstantBuffer : IDisposable
    {
        public ConstantBuffer(Device device, int size, int slot) 
        {
            ExceptionHelper.ThrowIfNull(device);
            ExceptionHelper.ThrowIfOutOfRange(size, 0, double.PositiveInfinity);
            ExceptionHelper.ThrowIfOutOfRange(slot, 0, double.PositiveInfinity);
            _Device = device;
            Buffer = new Buffer(device, size, ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            Size = size;
            Slot = slot;
        }

        public ConstantBuffer(Device device, byte[] data, int slot)
        {
            ExceptionHelper.ThrowIfNull(device);
            ExceptionHelper.ThrowIfNull(data);
            ExceptionHelper.ThrowIfOutOfRange(slot, 0, double.PositiveInfinity);
            _Device = device;
            Buffer = Buffer.Create(device, BindFlags.ConstantBuffer, data);
            Size = data.Length;
            Slot = slot;
        }

        public int Size { get; }
        public Buffer Buffer { get; }
        public int Slot { get; }
        private Device _Device;

        public void Dispose()
        {
            Buffer?.Dispose();
        }

        public void Set()
        {
            DeviceContext context = _Device.ImmediateContext;
            context.VertexShader.SetConstantBuffer(Slot, Buffer);
            context.GeometryShader.SetConstantBuffer(Slot, Buffer);
            context.PixelShader.SetConstantBuffer(Slot, Buffer);
        }

        public void Update<T>(T[] data) where T : struct => _Device.ImmediateContext.UpdateSubresource(data, Buffer);
    }
}
