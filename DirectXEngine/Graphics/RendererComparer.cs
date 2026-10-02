using System.Collections.Generic;

namespace DirectXEngine
{
    internal class RendererComparer : IComparer<RendererData>
    {
        public RendererComparer(Camera camera)
        {
            _Camera = camera;
        }

        private Camera _Camera;

        public int Compare(RendererData x, RendererData y)
        {
            return x.Renderer.InvokeRenderSortComparator(y.Renderer, _Camera);
        }
    }
}
