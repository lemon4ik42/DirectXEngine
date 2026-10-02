using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;
using System.Collections.Generic;
using SharpDX;
using System.Drawing;
using System;

namespace DirectXEngine
{
    internal class Graphics : IDisposable
    {
        public Graphics()
        {
            UpdateViewport();
            UpdateViews();
            UpdateDepthStencilState();
            UpdateRasterizerState();
            _Camera = Camera.Main;
            _Comparer = new RendererComparer(_Camera);
        }

        public Graphics(Camera camera) : this()
        {
            _Camera = camera;
            _Comparer = new RendererComparer(_Camera);
        }

        public Texture2D DepthBufferTexture => _DepthView.ResourceAs<Texture2D>();
        public Texture2D Frame
        {
            get
            {
                _Frame ??= TextureDescription.RenderTarget(_Resolution).ToTexture2D(_Device);
                _DeviceContext.CopyResource(_RenderTarget, _Frame);
                return _Frame;
            }
        }
        public virtual Size Resolution
        {
            get => _Resolution;
            set
            {
                if (value == _Resolution)
                    return;

                ExceptionHelper.ThrowIfOutOfRange(value.Width, 0, double.PositiveInfinity);
                ExceptionHelper.ThrowIfOutOfRange(value.Height, 0, double.PositiveInfinity);
                _Resolution = value;
                UpdateViewport();
                UpdateViews();
            }
        }
        public float DefaultDepth
        {
            get => _DefaultDepth;
            set
            {
                ExceptionHelper.ThrowIfOutOfRange01(value);
                _DefaultDepth = value;
            }
        }
        public DepthStencilStateDescription DepthStateDescription
        {
            get => _DepthStateDescription;
            set
            {
                _DepthStateDescription = value;
                UpdateDepthStencilState();
            }
        }
        public RasterizerStateDescription RasterizerDescription
        {
            get => _RasterizerDescription;
            set
            {
                _RasterizerDescription = value;
                UpdateRasterizerState();
            }
        }
        public BlendStateDescription BlendStateDescription
        {
            get => _BlendStateDescription;
            set
            {
                _BlendStateDescription = value;
                UpdateBlendState();
            }
        }
        public OutputMode OutputMode = OutputMode.RenderTargetDepthBuffer;
        public virtual float Aspect { get; private set; }
        private DepthStencilView _DepthView;
        private RenderTargetView _RenderView;
        private Texture2D _RenderTarget;
        private Texture2D _DepthBuffer;
        private DepthStencilStateDescription _DepthStateDescription = DepthStencilStateDescription.Default();
        private RasterizerStateDescription _RasterizerDescription = RasterizerStateDescription.Default();
        private BlendStateDescription _BlendStateDescription = BlendStateDescription.Default();
        private DepthStencilState _DepthState;
        private RasterizerState _RasterizerState;
        private BlendState _BlendState;
        private Camera _Camera;
        private Size _Resolution = new Size(1920, 1080);
        private Viewport _Viewport;
        private float _DefaultDepth = 1f;
        private RendererComparer _Comparer;
        private Texture2D _Frame;
        private static Device _Device => EngineCore.Current.Device;
        private static DeviceContext _DeviceContext => _Device.ImmediateContext;
        private static List<RendererData> _Renderers = new List<RendererData>();
        
        public static RendererData UpdateRenderer(Renderer renderer, RendererGraphicsSettings settings)
        {
            RendererData rendererData = _Renderers.Find(X => X.Renderer == renderer);
            RendererData updatedRendererData = UpdateRenderer(renderer);

            settings.InitializeBuffers(_Device);
            settings.DisposeBuffersData();

            if (rendererData == null)
                rendererData = updatedRendererData;
            else
                rendererData.Settings.Dispose();

            rendererData.Settings = settings;
            return rendererData;
        }

        public static RendererData UpdateRenderer(Renderer renderer)
        {
            RendererData rendererData = _Renderers.Find(X => X.Renderer == renderer);
            if (rendererData == null)
            {
                rendererData = new RendererData
                {
                    Renderer = renderer,
                };

                _Renderers.Add(rendererData);
                return rendererData;
            }

            return rendererData;
        }

        public static bool RemoveRenderer(Renderer renderer)
        {
            int index = _Renderers.FindIndex(x => x.Renderer == renderer);

            if (index == -1)
                return false;

            RendererData rendererData = _Renderers[index];

            _Renderers.RemoveAt(index);
            return true;
        }

        public void Dispose()
        {
            _DepthBuffer?.Dispose();
            _DepthView?.Dispose();
            _DepthState?.Dispose();

            _RenderTarget?.Dispose();
            _RenderView?.Dispose();
            _BlendState?.Dispose();
            _RasterizerState?.Dispose();

            _Frame?.Dispose();
        }

        public void CopyDepthBuffer(Texture2D destenation) => _DeviceContext.CopyResource(_DepthBuffer, destenation);

        public void CopyRenderTarget(Texture2D destenation) => _DeviceContext.CopyResource(_RenderTarget, destenation);

        public void DrawAll()
        {
            OnDraw();
            
            foreach (RendererData data in _Renderers)
                Draw(data);      
        }

        public void DrawAll<T>() where T : Renderer
        {
            OnDraw();

            foreach (RendererData data in _Renderers)
            {
                if (data is not T)
                    continue;

                Draw(data);
            }
        }

        public void DrawAll<T>(Shader shader, byte[] constantBufferData) where T : Renderer
        {
            OnDraw();

            shader.ConstantBuffers[0].Update(constantBufferData);
            shader.Set(_DeviceContext);

            foreach (RendererData data in _Renderers)
                Draw<T>(data);
        }

        public void DrawAll(Dictionary<Renderer, ManualDrawDescription> rendererDescriptions, DepthStencilView depthView = null, RenderTargetView renderView = null)
        {
            depthView ??= _DepthView;
            renderView ??= _RenderView;

            ClearViews(depthView, renderView);
            SetOutput(depthView, renderView);

            foreach (RendererData data in _Renderers)
            {
                if (!rendererDescriptions.TryGetValue(data.Renderer, out ManualDrawDescription description))
                    continue;

                for (int i = 0; i < description.ConstantBufferData.Length; i++)
                    description.Shader.ConstantBuffers[i].Update(description.ConstantBufferData[i]);

                description.Shader.Set(_DeviceContext);

                data.Settings.Draw(_DeviceContext);
            }
        }

        private void Draw<T>(RendererData data) where T : Renderer
        {
            if (data.Renderer is not T)
                return;

            data.Settings.Draw(_DeviceContext);
        }

        private void OnDraw()
        {
            ClearViews(_DepthView, _RenderView);
            SetOutput(_DepthView, _RenderView);
            _Renderers.Sort(_Comparer);
        }

        private void ClearViews(DepthStencilView depthView, RenderTargetView renderView)
        {
            _DeviceContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, _DefaultDepth, 0);
            _DeviceContext.ClearRenderTargetView(renderView, _Camera.SkyColor);
        }

        private void SetOutput(DepthStencilView depthView, RenderTargetView renderView)
        {
            switch (OutputMode)
            {
                case OutputMode.RenderTargetDepthBuffer:
                    _DeviceContext.OutputMerger.SetTargets(_DepthView, _RenderView);
                    break;
                case OutputMode.RenderTarget:
                    DepthStencilView depthViewNull = null;
                    _DeviceContext.OutputMerger.SetTargets(depthViewNull, renderView);
                    break;
                case OutputMode.DepthBuffer:
                    RenderTargetView renderViewNull = null;
                    _DeviceContext.OutputMerger.SetTargets(depthView, renderViewNull);
                    break;
            }

            _DeviceContext.OutputMerger.SetDepthStencilState(_DepthState);
            _DeviceContext.OutputMerger.SetBlendState(_BlendState);
            _DeviceContext.Rasterizer.SetViewport(_Viewport);
            _DeviceContext.Rasterizer.State = _RasterizerState;
        }

        private void Draw(RendererData rendererData)
        {
            Renderer renderer = rendererData.Renderer;
            renderer.InvokeOnDraw();

            Material material = renderer.Material;
            Shader shader = material.Shader;

            //if (!renderer.NeedToDrawInternal(_Camera))
            //    return;

            renderer.Material.Properties.ConstantBuffer.Set();

            ShaderDynamicResources shaderResources = renderer.InvokeGetResources(_Camera);

            for (int i = 0; i < shader.ConstantBuffers.Count; i++)
                shader.ConstantBuffers[i].Update(shaderResources.ConstantBufferData[i]);

            shader.Set(_DeviceContext);

            SetShaderResources(shaderResources.Resources);
            SetShaderSamplers(shaderResources.Samplers);
            
            rendererData.Settings.Draw(_DeviceContext);
        }

        private void SetShaderResources(ShaderResource[] shaderResources)
        {
            foreach (ShaderResource shaderResource in shaderResources)
                shaderResource.Set(_Device);
        }

        private void SetShaderSamplers(ShaderSampler[] samplers)
        {
            foreach (ShaderSampler sampler in samplers)
                sampler.Set(_Device);
        }

        protected void UpdateViewport()
        {
            int width = _Resolution.Width;
            int height = _Resolution.Height;

            _Viewport = new Viewport(0, 0, width, height, 0.0f, 1.0f);
            Aspect = width / (float)height;
        }

        private void UpdateViews()
        {
            Utilities.Dispose(ref _RenderTarget);
            Utilities.Dispose(ref _DepthBuffer);
            Utilities.Dispose(ref _RenderView);
            Utilities.Dispose(ref _DepthView);

            _DepthBuffer = UpdateDepthBuffer(_Resolution);
            _RenderTarget = UpdateRenderTarget(_Resolution);

            _DepthView = new DepthStencilView(_Device, _DepthBuffer);
            _RenderView = new RenderTargetView(_Device, _RenderTarget);
        }

        protected virtual Texture2D UpdateDepthBuffer(Size resolution) => 
            TextureDescription.DepthBuffer(resolution).ToTexture2D(_Device);

        protected virtual Texture2D UpdateRenderTarget(Size resolution) => 
            TextureDescription.RenderTarget(resolution).ToTexture2D(_Device);

        private void UpdateDepthStencilState()
        {
            Utilities.Dispose(ref _DepthState);
            _DepthState = new DepthStencilState(_Device, _DepthStateDescription);
        }

        private void UpdateRasterizerState()
        {
            Utilities.Dispose(ref _RasterizerState);
            _RasterizerState = new RasterizerState(_Device, _RasterizerDescription);
        }

        private void UpdateBlendState()
        {
            Utilities.Dispose(ref _BlendState);
            _BlendState = new BlendState(_Device, _BlendStateDescription);
        }
    }
}
