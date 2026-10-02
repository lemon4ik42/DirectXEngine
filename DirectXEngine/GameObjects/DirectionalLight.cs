using SharpDX;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using SharpDX.Mathematics.Interop;

namespace DirectXEngine
{
    public class DirectionalLight : Light, IShaderResource
    {
        public DirectionalLight(GameObject attachedGameObject) : base(attachedGameObject)
        {
            FillViewProjectionMatrices();
        }

        public float ProjectionOffset
        {
            get => _ProjectionOffset;
            set
            {
                ExceptionHelper.ThrowIfOutOfRange(value, 0, double.PositiveInfinity);
                _ProjectionOffset = value;
            }
        }
        public float MaxShadowCastDistance
        {
            get => _MaxShadowCastDistance;
            set
            {
                ExceptionHelper.ThrowIfOutOfRange(value, 0, double.PositiveInfinity);
                _MaxShadowCastDistance = value;
            }
        }
        public int ShadowMapsCount
        {
            get => ShadowMapTexturesCount;
            set
            {
                ExceptionHelper.ThrowIfOutOfRange(value, 1, _MaxShadowMapsCount);
                ShadowMapTexturesCount = value;
                UpdateGraphics(value);
            }
        }
        public float ResolutionScaleFactor
        {
            get => _ResolutionScaleFactor;
            set
            {
                ExceptionHelper.ThrowIfOutOfRange01(value);
                _ResolutionScaleFactor = value;
                UpdateGraphics(ShadowMapsCount);
            }
        }
        internal static IReadOnlyList<DirectionalLight> DirectionalLights => _DirectionalLights;
        protected internal override byte[] ShaderResourceData
        {
            get
            {
                float maxShadowCastDistance = MathUtil.Clamp(_MainCamera.FarClipPlane, 0, MaxShadowCastDistance);
                float frustumLength = maxShadowCastDistance / ShadowMapsCount;
                
                DirectionalLightInput input = new DirectionalLightInput
                {
                    BaseInput = BaseInput,
                    ForwardDirection = Transform.Forward,
                    FarClipPlane = Camera.FarClipPlane,
                    StartTextureIndex = StartTextureIndex,
                    TexturesCount = ShadowMapsCount,
                    MaxShadowCastDistance = maxShadowCastDistance,
                    FrustumLength = frustumLength,
                    ViewProjectionMatrices = _ViewProjectionMatrices,
                    ShadowMapsResolutionFactor = _ShadowMapsResolutionFactor,
                };

                return EngineUtilities.ToByteArray(ref input);
            }
        }
        private static List<DirectionalLight> _DirectionalLights = new List<DirectionalLight>();
        private Matrix[] _ViewProjectionMatrices = new Matrix[_MaxShadowMapsCount];
        private Vector2[] _ShadowMapsResolutionFactor = new Vector2[_MaxShadowMapsCount];
        private Graphics[] _Graphics = new Graphics[0];
        private Camera _MainCamera;
        private float _ResolutionScaleFactor = 0.5f;
        private float _ProjectionOffset = 20;
        private float _MaxShadowCastDistance = 200;
        private const int _DefaultShadowMapsCount = 3;
        private const int _MaxShadowMapsCount = 10;

        public override void WriteShadowMapInTexture(IReadOnlyList<MeshRenderer> meshRenderers)
        {
            float farClipPlane = _MainCamera.FarClipPlane;
            farClipPlane = MathUtil.Clamp(farClipPlane, 0, _MaxShadowCastDistance);
            Frustum[] frustums = Frustum.CalculateSubFrustums(_MainCamera, _MainCamera.NearClipPlane, farClipPlane, ShadowMapsCount);

            for (int i = 0; i < frustums.Length; i++)
                WriteFrustumShadowMapInTexture(meshRenderers, frustums[i], i);
        }

        protected override void OnStart()
        {
            ShadowMapTexturesCount = _DefaultShadowMapsCount;
            base.OnStart();
            _DirectionalLights.Add(this);
            UpdateGraphics(_DefaultShadowMapsCount);
            _MainCamera = Camera.Main;
        }

        private void WriteFrustumShadowMapInTexture(IReadOnlyList<MeshRenderer> meshRenderers, Frustum frustum, int index)
        {
            Dictionary<Renderer, ManualDrawDescription> rendererDescriptions = new Dictionary<Renderer, ManualDrawDescription>();
            Matrix viewProjectionMatrix = UpdateViewProjectionMatrix(frustum, index);

            foreach (MeshRenderer meshRenderer in meshRenderers)
            {
                Matrix worldViewProjection = meshRenderer.Transform.LocalToWorldMatrix * viewProjectionMatrix;

                ConstantBufferData input = new ConstantBufferData
                {
                    LightViewProjection = worldViewProjection,
                    TransformZ = 0
                };

                byte[] constantBufferData = EngineUtilities.ToByteArray(ref input, true);
                rendererDescriptions[meshRenderer] = new ManualDrawDescription(ShadowMapShader, new byte[][] { constantBufferData });
            }

            _Graphics[index].DrawAll(rendererDescriptions, DepthViews[index]);
        }

        private Matrix UpdateViewProjectionMatrix(Frustum frustum, int index)
        {
            IReadOnlyList<Vector3> corners = frustum.AllCorners;

            if (!IsFrustumInsideProjectionSpace(corners, ref _ViewProjectionMatrices[index]))
                _ViewProjectionMatrices[index] = CalculateViewProjectionMatrix(frustum, index == 0);

            return _ViewProjectionMatrices[index];
        }

        private Matrix CalculateViewProjectionMatrix(Frustum frustum, bool isFirstFrustum)
        {
            Vector3 forward = Transform.Forward;
            Vector3 cameraRight = _MainCamera.Transform.Right;
            Vector3 frustumCenter = frustum.Center;

            Vector3.Cross(ref forward, ref cameraRight, out Vector3 viewUp);
            Vector3.Add(ref frustumCenter, ref forward, out Vector3 target);
            Matrix.LookAtLH(ref frustumCenter, ref target, ref viewUp, out Matrix viewMatrix);

            Vector3 min = new Vector3(float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity);
            Vector3 transformedCorner;

            IReadOnlyList<Vector3> corners = frustum.AllCorners;

            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 corner = corners[i];
                Vector3.Transform(ref corner, ref viewMatrix, out transformedCorner);
                Vector3.Min(ref min, ref transformedCorner, out min);
                Vector3.Max(ref max, ref transformedCorner, out max);
            }
            
            if (isFirstFrustum)
            {
                Vector3.Add(ref max, ref _ProjectionOffset, out max);
                Vector3.Subtract(ref min, ref _ProjectionOffset, out min);
            }

            Matrix projection = Matrix.OrthoOffCenterLH(min.X, max.X, min.Y, max.Y, min.Z, max.Z);
            Matrix.Multiply(ref viewMatrix, ref projection, out Matrix viewProjection);

            return viewProjection;
        }

        private bool IsFrustumInsideProjectionSpace(IReadOnlyList<Vector3> corners, ref Matrix viewProjectionMatrix)
        {
            Vector3 transformedCorner;

            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 corner = corners[i];
                Vector3.Transform(ref corner, ref viewProjectionMatrix, out transformedCorner);
                Vector3.Abs(ref transformedCorner, out Vector3 absTransformedCorner);
                
                if (absTransformedCorner.X > 1 || absTransformedCorner.Y > 1 || absTransformedCorner.Z > 1)
                    return false;
            }

            return true;
        }

        private void UpdateGraphics(int count)
        {
            foreach (Graphics graphics in _Graphics)
                graphics?.Dispose();

            _Graphics = new Graphics[count];

            Size referenceResolution = ShadowMap.Textures.Resolution;
            Size resolution = referenceResolution;
            int depthBias = 200000;

            for (int i = 0; i < count; i++)
            {
                Graphics graphics = new Graphics();
                graphics.Resolution = resolution;
                _Graphics[i] = graphics;

                float widthFactor = resolution.Width / (float)referenceResolution.Width;
                float heightFactor = resolution.Height / (float)referenceResolution.Height;

                _ShadowMapsResolutionFactor[i] = new Vector2(widthFactor, heightFactor);

                resolution.Width = (int)(resolution.Width * _ResolutionScaleFactor);
                resolution.Height = (int)(resolution.Height * _ResolutionScaleFactor);

                RasterizerStateDescription rasterizerDescription = graphics.RasterizerDescription;
                rasterizerDescription.DepthBias = depthBias;
                graphics.RasterizerDescription = rasterizerDescription;
                graphics.OutputMode = OutputMode.DepthBuffer;

                float averageFactor = (widthFactor + heightFactor) / 2;
                depthBias = (int)(depthBias * (2 + averageFactor));
            }
        }

        private void FillViewProjectionMatrices()
        {
            for (int i = 0; i < _ViewProjectionMatrices.Length; i++)
                _ViewProjectionMatrices[i] = Matrix.Identity;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct DirectionalLightInput
        {
            public BaseLightInput BaseInput;
            public Vector3 ForwardDirection;
            public float FarClipPlane;
            public int StartTextureIndex;
            public int TexturesCount;
            public float MaxShadowCastDistance;
            public float FrustumLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public Matrix[] ViewProjectionMatrices;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public Vector2[] ShadowMapsResolutionFactor;
        }
    }
}
