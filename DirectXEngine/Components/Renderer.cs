using System;

namespace DirectXEngine
{
    public abstract class Renderer : Updatable
    {
        public Renderer(GameObject attachedGameObject) : base(attachedGameObject)
        {
            
        }

        [SerializeMember]
        public Material Material
        {
            get => _Material;
            set
            {
                ExceptionHelper.ThrowIfNull(value);
                _Material = value;
                MaterialChanged?.Invoke(value);
            }
        }
        internal event Action<Material> MaterialChanged;
        private const string _NotInstantiatedException = "Game object must be instantiated on scene before call UpdateGraphicsSettings";
        private Material _Material = Material.Default;

        internal ShaderDynamicResources InvokeGetResources(Camera camera) => GetResources(camera);

        internal bool InvokeNeedToDraw(Camera camera) => NeedToDraw(camera);

        internal int InvokeRenderSortComparator(Renderer other, Camera camera) => RenderSortComparator(other, camera);

        internal void InvokeOnDraw() => OnDraw();

        protected abstract ShaderDynamicResources GetResources(Camera camera);

        protected abstract bool NeedToDraw(Camera camera);

        protected virtual int RenderSortComparator(Renderer other, Camera camera) => 0;

        protected virtual void OnDraw() { }

        protected void UpdateGraphicsSettings(RendererGraphicsSettings update)
        {
            ExceptionHelper.ThrowByCondition(!GameObject.IsInstantiated, _NotInstantiatedException);
            Graphics.UpdateRenderer(this, update);
        }

        protected bool RemoveFromRender() => Graphics.RemoveRenderer(this);
    }
}
