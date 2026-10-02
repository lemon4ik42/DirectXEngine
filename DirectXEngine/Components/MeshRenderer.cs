using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.D3DCompiler;
using System;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Reflection;
using Assimp;

namespace DirectXEngine
{
    public class MeshRenderer : Renderer, ICloneableComponent
    {
        public MeshRenderer(GameObject attachedGameObject) : base(attachedGameObject)
        {
            
        }

        public Mesh Mesh
        {
            get => _Mesh;
            set
            {
                ExceptionHelper.ThrowIfNull(value);
                _Mesh = value;

                if (GameObject.IsInstantiated)
                    UpdateGraphicsSettings(GetUpdate(value));
            }
        }
        public bool UseFlatShading
        {
            get => _UseFlatShading;
            set
            {
                if (value == _UseFlatShading)
                    return;

                _UseFlatShading = value;

                if (_Mesh == null)
                    return;

                if (GameObject.IsInstantiated)
                    UpdateGraphicsSettings(GetUpdate(_Mesh));
            }
        }
        private bool _UseFlatShading = false;
        private Mesh _Mesh;
        private readonly int _Vector3Size = Utilities.SizeOf<Vector3>();
        private readonly int _Vector2Size = Utilities.SizeOf<Vector2>();
        private static ShaderResource _DirectionalLightsInput;
        private static ShaderResource _PointLightsInput;
        private static ShaderResource _SpotlightsInput;
        private static bool _IsUpdateInitialized = false;
        private static ShaderSampler _ShadowMapSampler = GetShadowMapSamler();
        private ShaderResource _FrameTexture;
        private ShaderSampler _TextureSampler;
        private static List<MeshRenderer> _MeshRenderers = new List<MeshRenderer>();
        private ShaderResource _Texture;
        private ShaderResource _HeightMap;
        private ShaderSampler _HeightMapSampler;

        public Component Clone()
        {
            MeshRenderer componentCopy = MemberwiseClone() as MeshRenderer;
            componentCopy._Mesh = Mesh?.Clone() as Mesh;
            return componentCopy;
        }

        protected override bool NeedToDraw(Camera camera) => camera.Frustum.CheckForIntersection(Transform, _Mesh.Bounds);

        protected override ShaderDynamicResources GetResources(Camera camera)
        {
            Matrix localToWorld = Transform.LocalToWorldMatrix;

            if (_FrameTexture != null)
                _FrameTexture.SetResource(camera.Graphics.Frame);
            
            _FrameTexture ??= new ShaderResource(camera.Graphics.Frame, 7, false);

            ConstantBufferInput input = new ConstantBufferInput
            {
                ModelViewProjection = localToWorld * camera.WorldToScreenMatrix,
                ModelLocalToWorldDirection = Transform.RotationMatrix,
                ModelLocalToWorld = localToWorld,
                CameraPosition = camera.Transform.WorldPosition,
                GlobalIllumination = Light.GlobalIllumination,
                HasTexture = _Texture.IsDataValid ? 1 : 0,
                HasHeightMap = _HeightMap.IsDataValid ? 1 : 0,
            };

            ShaderResource[] shaderResource = new ShaderResource[]
            {
                _PointLightsInput,
                _DirectionalLightsInput,
                _SpotlightsInput,
                Light.GetShadowMap<PointLight>().ToShaderResource(3, false),
                Light.GetShadowMap<DirectionalLight>().ToShaderResource(4, false),
                Light.GetShadowMap<Spotlight>().ToShaderResource(5, false),
                _Texture,
                _FrameTexture,
                _HeightMap
            };
            
            ShaderSampler[] samplers = new ShaderSampler[]
            {
                _ShadowMapSampler,
                _TextureSampler,
                _HeightMapSampler
            };

            byte[][] constantBufferData = new byte[][] { EngineUtilities.ToByteArray(ref input, true) };
            
            return new ShaderDynamicResources(constantBufferData, shaderResource, samplers);
        }

        protected override int RenderSortComparator(Renderer other, Camera camera)
        {
            Vector3 cameraPosition = camera.Transform.WorldPosition;
            float distanceToCurrent = Vector3.Distance(Transform.WorldPosition, cameraPosition);
            float distanceToOther = Vector3.Distance(other.Transform.WorldPosition, cameraPosition);

            if (distanceToCurrent > distanceToOther)
                return -1;

            if (distanceToCurrent < distanceToOther)
                return 1;

            return 0;
        }

        protected override void OnStart()
        {
            if (_Mesh != null)
                UpdateGraphicsSettings(GetUpdate(_Mesh));
            
            _MeshRenderers.Add(this);
            
            UpdateTexture(Material.Texture);
            UpdateHeightMap(Material.HeightMap);
            Material.TextureChanged += UpdateTexture;
            Material.HeightMapChanged += UpdateHeightMap;
        }

        protected override void OnUpdate()
        {
            if (!_IsUpdateInitialized)
            {
                OnFrameRenderStart();
                _IsUpdateInitialized = true;
            }
        }

        protected override void OnEndUpdate()
        {
            _IsUpdateInitialized = false;
        }

        protected override void OnRemove() => RemoveFromRender();

        protected override void OnDestroy() => RemoveFromRender();

        private void UpdateTexture(Texture texture)
        {
            _Texture = texture != null ? texture.ToShaderResource(6, false) : ShaderResource.Invalid;
            _TextureSampler = texture != null ? texture.GetSampler(1) : ShaderSampler.Invalid;
        }

        private void UpdateHeightMap(Texture texture)
        {
            _HeightMap = texture != null ? texture.ToShaderResource(8, false) : ShaderResource.Invalid;
            _HeightMapSampler = texture != null ? texture.GetSampler(2) : ShaderSampler.Invalid;
        }

        private static void OnFrameRenderStart()
        {
            _PointLightsInput?.Dispose();
            _DirectionalLightsInput?.Dispose();
            _SpotlightsInput?.Dispose();

            IReadOnlyList<PointLight> pointLights = PointLight.PointLights;
            IReadOnlyList<DirectionalLight> directionalLights = DirectionalLight.DirectionalLights;
            IReadOnlyList<Spotlight> spotlights = Spotlight.Spotlights;

            Light.WriteShadowMapsInTexture(pointLights, _MeshRenderers);
            Light.WriteShadowMapsInTexture(directionalLights, _MeshRenderers);
            Light.WriteShadowMapsInTexture(spotlights, _MeshRenderers);

            _PointLightsInput = Light.ToShaderResource(pointLights, 0);
            _DirectionalLightsInput = Light.ToShaderResource(directionalLights, 1);
            _SpotlightsInput = Light.ToShaderResource(spotlights, 2);
        }

        private static ShaderSampler GetShadowMapSamler()
        {
            SamplerStateDescription description = new SamplerStateDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Border,
                AddressV = TextureAddressMode.Border,
                AddressW = TextureAddressMode.Border,
                BorderColor = new SharpDX.Mathematics.Interop.RawColor4(1, 1, 1, 1),
                MaximumAnisotropy = 0,
            };

            return new ShaderSampler(description, 0);
        }

        private RendererGraphicsSettings GetUpdate(Mesh mesh)
        {
            byte[] indexBufferData = EngineUtilities.ToByteArray(mesh.Triangles);
            Format format = Format.R32_UInt;

            if (_UseFlatShading)
                return RendererGraphicsSettings.DefaultDraw(GetVertexBufferDataForFlatShading(mesh), mesh.Triangles.Length);

            return RendererGraphicsSettings.IndexedDraw(GetVertexBufferData(mesh), mesh.Vertices.Length, indexBufferData, format);
        }

        private StructuredBufferDescription[] GetVertexBufferData(Mesh mesh)
        {
            StructuredBufferDescription[] vertexBuffers = new StructuredBufferDescription[5];

            vertexBuffers[0] = StructuredBufferDescription.Create(mesh.Vertices);
            vertexBuffers[1] = StructuredBufferDescription.Create(mesh.Normals);
            vertexBuffers[2] = StructuredBufferDescription.Create(mesh.BiNormals);
            vertexBuffers[3] = StructuredBufferDescription.Create(mesh.Tangents);
            vertexBuffers[4] = StructuredBufferDescription.Create(mesh.UVs);

            return vertexBuffers;
        }

        private StructuredBufferDescription[] GetVertexBufferDataForFlatShading(Mesh mesh)
        {
            int trianglesCount = mesh.Triangles.Length;

            byte[] verticesData = new byte[_Vector3Size * trianglesCount];
            byte[] normalsData = new byte[verticesData.Length];
            byte[] biNormalsData = new byte[verticesData.Length];
            byte[] tangentsData = new byte[verticesData.Length];
            byte[] uvsData = new byte[_Vector2Size * trianglesCount];

            int vertexIndex = 0;
            int uvsIndex = 0;
            int trianglePosition = 0;

            Vector3[] vertices = mesh.Vertices;
            int[] triangles = mesh.Triangles;

            for (int i = 0; i < trianglesCount; i++)
            {
                int triangleIndex = triangles[i];
                byte[] verticesBytes = EngineUtilities.ToByteArray(ref mesh.Vertices[triangleIndex]);

                int triangleIndex1 = triangles[i - trianglePosition];
                int triangleIndex2 = triangles[i - (trianglePosition - 1)];
                int triangleIndex3 = triangles[i - (trianglePosition - 2)];

                Vector3 vertex1 = vertices[triangleIndex1];
                Vector3 vertex2 = vertices[triangleIndex2];
                Vector3 vertex3 = vertices[triangleIndex3];

                Vector3 normal = Vector3.Cross(vertex3 - vertex2, vertex1 - vertex2);
                normal.Normalize();

                byte[] normalBytes = EngineUtilities.ToByteArray(ref normal);
                byte[] biNormalBytes = triangleIndex < mesh.BiNormals.Length ? 
                    EngineUtilities.ToByteArray(ref mesh.BiNormals[triangleIndex]) : new byte[_Vector3Size];
                byte[] tangentBytes = triangleIndex < mesh.Tangents.Length ? 
                    EngineUtilities.ToByteArray(ref mesh.Tangents[triangleIndex]) : new byte[_Vector3Size];
                byte[] uvsBytes = triangleIndex < mesh.UVs.Length ?
                    EngineUtilities.ToByteArray(ref mesh.UVs[triangleIndex]) : new byte[_Vector2Size];
                
                for (int j = 0; j < _Vector3Size; j++)
                {
                    verticesData[vertexIndex] = verticesBytes[j];
                    normalsData[vertexIndex] = normalBytes[j];
                    biNormalsData[vertexIndex] = biNormalBytes[j];
                    tangentsData[vertexIndex] = tangentBytes[j];

                    vertexIndex++;
                }

                for (int j = 0; j < _Vector2Size; j++)
                {
                    uvsData[uvsIndex] = uvsBytes[j];

                    uvsIndex++;
                }

                trianglePosition++;

                if (trianglePosition > 2)
                    trianglePosition = 0;
            }

            StructuredBufferDescription[] vertexBuffers = new StructuredBufferDescription[5];

            vertexBuffers[0] = new StructuredBufferDescription(verticesData, _Vector3Size);
            vertexBuffers[1] = new StructuredBufferDescription(normalsData, _Vector3Size);
            vertexBuffers[2] = new StructuredBufferDescription(biNormalsData, _Vector3Size);
            vertexBuffers[3] = new StructuredBufferDescription(tangentsData, _Vector3Size);
            vertexBuffers[4] = new StructuredBufferDescription(uvsData, _Vector2Size);
            
            return vertexBuffers;
        }
    }
}
