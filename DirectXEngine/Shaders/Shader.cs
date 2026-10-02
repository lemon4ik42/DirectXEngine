using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Drawing;
using Device = SharpDX.Direct3D11.Device;

namespace DirectXEngine
{
    public class Shader
    {
        public Shader(InputElement[] vertexShaderInput, PrimitiveTopology topology, IReadOnlyList<int> constantBuffersSize)
        {
            ExceptionHelper.ThrowIfNull(vertexShaderInput);
            CheckConstantBufferSizes(constantBuffersSize);
            VertexShaderInput = vertexShaderInput;
            Topology = topology;
            ConstantBuffersSize = constantBuffersSize;

            ConstantBuffer[] constantBuffers = new ConstantBuffer[constantBuffersSize.Count];

            for (int i = 0; i < constantBuffers.Length; i++)
                constantBuffers[i] = new ConstantBuffer(EngineCore.Current.Device, constantBuffersSize[i], i + 1);

            _ConstantBuffers = constantBuffers;
        }

        public Shader()
        {

        }

        public InputElement[] VertexShaderInput { get; private set; }
        public PrimitiveTopology Topology { get; private set; }
        public IReadOnlyList<int> ConstantBuffersSize { get; private set; }
        public ShaderData VertexShader
        {
            get => _VertexShaderData;
            set
            {
                CheckShader(value);
                _VertexShaderData = value;
                _VertexShader?.Dispose();
                _Layout?.Dispose();
                _VertexShader = new VertexShader(_Device, value.ByteCode);
                _Layout = new InputLayout(_Device, value.ByteCode, VertexShaderInput);
            }
        }
        public ShaderData GeometryShader
        {
            get => _GeometryShaderData;
            set
            {
                CheckShader(value);
                _GeometryShaderData = value;
                _GeometryShader?.Dispose();
                _GeometryShader = new GeometryShader(_Device, value.ByteCode);
            }
        }
        public ShaderData PixelShader
        {
            get => _PixelShaderData;
            set
            {
                CheckShader(value);
                _PixelShaderData = value;
                _PixelShader?.Dispose();
                _PixelShader = new PixelShader(_Device, value.ByteCode);
            }
        }
        public static readonly Shader Default = CreateDefaultShader();
        internal IReadOnlyList<ConstantBuffer> ConstantBuffers => _ConstantBuffers;
        private ConstantBuffer[] _ConstantBuffers = new ConstantBuffer[0];
        private ShaderData _VertexShaderData;
        private ShaderData _PixelShaderData;
        private ShaderData _GeometryShaderData;
        private VertexShader _VertexShader;
        private PixelShader _PixelShader;
        private GeometryShader _GeometryShader;
        private InputLayout _Layout;
        private Device _Device => EngineCore.Current.Device;
        private const string _ShaderNotCompiledException = "Shader not compiled";

        internal void Set(DeviceContext context)
        {
            context.InputAssembler.PrimitiveTopology = Topology;
            context.InputAssembler.InputLayout = _Layout;
            context.VertexShader.Set(_VertexShader);
            context.GeometryShader.Set(_GeometryShader);
            context.PixelShader.Set(_PixelShader);

            foreach (ConstantBuffer buffer in _ConstantBuffers)
                buffer.Set();
        }

        private void CheckShader(ShaderData data)
        {
            ExceptionHelper.ThrowIfNull(data);
            ExceptionHelper.ThrowByCondition(!data.IsCompiled, _ShaderNotCompiledException);
        }

        private void CheckConstantBufferSizes(IReadOnlyList<int> constantBuffersSize)
        {
            foreach (int size in constantBuffersSize)
            {
                ExceptionHelper.ThrowIfOutOfRange(size, 0, double.PositiveInfinity);
                ExceptionHelper.ThrowByCondition(size % 16 != 0);
            }
        }

        private static Shader CreateDefaultShader()
        {
            InputElement[] inputElements = new InputElement[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0),
                new InputElement("NORMAL", 0, Format.R32G32B32_Float, 1),
                new InputElement("BINORMAL", 0, Format.R32G32B32_Float, 2),
                new InputElement("TANGENT", 0, Format.R32G32B32_Float, 3),
                new InputElement("UV", 0, Format.R32G32_Float, 4),
            };

            int constantBufferSize = EngineUtilities.CalculateAlignedSize(Utilities.SizeOf<ConstantBufferInput>());

            Shader meshShader = new Shader(inputElements, PrimitiveTopology.TriangleList, new int[] { constantBufferSize });

            ShaderData vertexShader = new ShaderData(ShaderProfile.vs_5_0, "VS");
            ShaderData pixelShader = new ShaderData(ShaderProfile.ps_5_0, "PS");

            string shaderPath = "C:\\sisharp\\DirectXEngine\\DirectXEngine\\Shaders\\MeshShader.fx";

            vertexShader.CompileFromFile(shaderPath);
            pixelShader.CompileFromFile(shaderPath);

            meshShader.VertexShader = vertexShader;
            meshShader.PixelShader = pixelShader;

            return meshShader;
        }
    }
}
