using SharpDX.Direct3D11;

namespace DirectXEngine
{
    public class ShaderSampler
    {
        public ShaderSampler(SamplerStateDescription description, int slot)
        {
            Description = description;
            Slot = slot;
        }

        private ShaderSampler(bool isValid)
        {
            _IsValid = isValid;
        }

        public SamplerStateDescription Description { get; }
        public static ShaderSampler Invalid => new ShaderSampler(false);
        public int Slot { get; }
        private bool _IsValid = true;

        internal void Set(Device device)
        {
            if (!_IsValid)
                return;

            SamplerState sampler = new SamplerState(device, Description);
            device.ImmediateContext.VertexShader.SetSampler(Slot, sampler);
            device.ImmediateContext.PixelShader.SetSampler(Slot, sampler);
            sampler.Dispose();
        }
    }
}
