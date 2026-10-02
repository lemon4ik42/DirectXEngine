using System;

namespace DirectXEngine
{
    public interface IShaderResource : IDisposable
    {
        public ShaderResource ToShaderResource(int slot, bool disposeAfterSet);
    }
}
