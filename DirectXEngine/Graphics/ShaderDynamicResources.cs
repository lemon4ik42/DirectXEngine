namespace DirectXEngine
{
    public class ShaderDynamicResources
    {
        public ShaderDynamicResources(byte[][] constantBufferData, ShaderResource[] resources, ShaderSampler[] samplers)
        {
            ConstantBufferData = constantBufferData;
            Resources = resources;
            Samplers = samplers;
        }

        public ShaderDynamicResources(byte[][] constantBufferData, IShaderResource[] resources, ShaderSampler[] samplers)
        {
            ConstantBufferData = constantBufferData;
            Samplers = samplers;

            Resources = new ShaderResource[resources.Length];

            for (int i = 0; i < Resources.Length; i++)
                Resources[i] = resources[i].ToShaderResource(i, false);
        }

        public byte[][] ConstantBufferData { get; }
        public ShaderResource[] Resources { get; }
        public ShaderSampler[] Samplers { get; }
    }
}
