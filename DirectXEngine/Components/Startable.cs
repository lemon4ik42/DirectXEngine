namespace DirectXEngine
{
    public abstract class Startable : Component
    {
        protected Startable(GameObject attachedGameObject) : base(attachedGameObject)
        {

        }

        internal void InvokeOnStart() => OnStart();

        internal void InvokeOnEndUpdate() => OnEndUpdate();

        protected virtual void OnStart() { }

        protected virtual void OnEndUpdate() { }
    }
}
