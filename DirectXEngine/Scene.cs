using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DirectXEngine
{
    public class Scene
    {
        internal Scene()
        {
            Current = this;
            EngineCore.Current.FrameRenderStart += InvokeGameObjectsEvents;
            GameObject mainCamera = new GameObject(true);
            MainCamera = mainCamera.AddComponent<MainCamera>();
            _GameObjects.Add(mainCamera);
        }

        public static Scene Current { get; private set; }
        public IReadOnlyList<GameObject> GameObjects => _GameObjects;
        public Camera MainCamera { get; }
        private List<GameObject> _GameObjects = new List<GameObject>();

        public GameObject Instantiate(GameObject original) => original.Copy();

        public T Instantiate<T>(T original) where T : Component
        {
            GameObject copy = Instantiate(original.GameObject);
            return copy.GetComponent<T>();
        }

        public GameObject Instantiate() => Instantiate(true);

        public T Instantiate<T>() where T : GameObject
        {
            T gameObject = (T)Activator.CreateInstance(typeof(T), true);
            _GameObjects.Add(gameObject);
            return gameObject;
        }

        public void Destroy(GameObject gameObject)
        {
            ExceptionHelper.ThrowIfNull(gameObject, "GameObject is null");
            ExceptionHelper.ThrowByCondition(!gameObject.IsInstantiated, "GameObject is not instantiated");
            gameObject.InvokeOnDestroy();
            _GameObjects.Remove(gameObject);
        }

        public void Destroy<T>(T component) where T : Component =>
            Destroy(component?.GameObject);

        internal GameObject Instantiate(bool addTransform)
        {
            GameObject gameObject = new GameObject(true, addTransform);
            _GameObjects.Add(gameObject);
            return gameObject;
        }

        public IEnumerable<GameObject> FindGameObjectsOfType<T>() where T : GameObject => _GameObjects.Where(x => x is T);

        private void InvokeGameObjectsEvents()
        {
            _GameObjects.SafetyForEach(x => x.InvokeOnUpdate());
            _GameObjects.SafetyForEach(x => x.InvokeOnEndUpdate());
        }
    }
}
