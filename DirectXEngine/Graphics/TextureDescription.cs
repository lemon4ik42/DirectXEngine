using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Drawing;
using Device = SharpDX.Direct3D11.Device;

namespace DirectXEngine
{
    internal class TextureDescription
    {
        public TextureDescription(Texture2DDescription description) 
        { 
            Description = description;
        }

        public Texture2DDescription Description { get; }

        public Texture2D ToTexture2D(Device device) => new Texture2D(device, Description);

        public static TextureDescription RenderTarget(Size resolution, SampleDescription? sampleDescription = null) => 
        new TextureDescription(new Texture2DDescription
        {
            Format = Format.R8G8B8A8_UNorm,
            ArraySize = 1,
            MipLevels = 1,
            Width = resolution.Width,
            Height = resolution.Height,
            SampleDescription = sampleDescription ?? new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            CpuAccessFlags = CpuAccessFlags.None,
            OptionFlags = ResourceOptionFlags.None
        });

        public static TextureDescription DepthBuffer(Size resolution, SampleDescription? sampleDescription = null) =>
        new TextureDescription(new Texture2DDescription()
        {
            Format = Format.D32_Float,
            ArraySize = 1,
            MipLevels = 1,
            Width = resolution.Width,
            Height = resolution.Height,
            SampleDescription = sampleDescription ?? new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.DepthStencil,
            CpuAccessFlags = CpuAccessFlags.None,
            OptionFlags = ResourceOptionFlags.None
        });
    }
}
