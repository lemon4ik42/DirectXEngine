namespace DirectXEngine
{
    internal class MainCamera : Camera
    {
        public MainCamera(GameObject attachedGameObject) : base(attachedGameObject)
        {

        }

        protected override void OnStart()
        {
            _Graphics = new MainCameraGraphics(this);
            UpdateFieldOfView(60);
        }

        internal override Graphics Graphics => _Graphics;
        private Graphics _Graphics;
    }
}
