using Assimp;
using System.Collections.Generic;
using Node = Assimp.Node;
using SharpDX;
using System.Windows.Forms;
using DirectXEngine.FBXModelParser;
using SharpDX.Direct2D1;
using System.Text;
using System.Drawing.Imaging;
using System.IO;
using SharpDX.Direct3D11;

namespace DirectXEngine
{
    public class ModelDecoder
    {
        public ModelDecoder(Assimp.Scene scene, string fullPath)
        {
            ExceptionHelper.ThrowIfNull(scene);
            _Scene = scene;
            _FullPath = fullPath;
            _DirectoryName = Path.GetDirectoryName(_FullPath);
        }

        private Assimp.Scene _Scene;
        private string _FullPath;
        private string _DirectoryName;
        private static readonly Dictionary<TextureWrapMode, TextureAddressMode> _WrapModeConveter = new Dictionary<TextureWrapMode, TextureAddressMode>
        {
            { TextureWrapMode.Mirror, TextureAddressMode.Mirror },
            { TextureWrapMode.Wrap, TextureAddressMode.Wrap },
            { TextureWrapMode.Clamp, TextureAddressMode.Clamp },
            { TextureWrapMode.Decal, TextureAddressMode.Border },
        };

        public static GameObject LoadFromPath(string path)
        {
            Assimp.Scene scene = new AssimpContext().ImportFile(path, PostProcessSteps.Triangulate);
            ModelDecoder decoder = new ModelDecoder(scene, path);
            return decoder.Decode();
        }

        public GameObject Decode()
        {
            Node node = _Scene.RootNode;
            GameObject gameObject = NodeToGameObject(node);
            TransformData data = TransformData.FromTransform(node.Transform);
            data.ApplyOnTransform(gameObject.Transform);
            CreateChildrens(node, gameObject.Transform);
            return gameObject;
        }

        private GameObject NodeToGameObject(Node node)
        {
            List<Assimp.Mesh> meshes = new List<Assimp.Mesh>(node.MeshIndices.Count);
            node.MeshIndices.ForEach(x => meshes.Add(_Scene.Meshes[x]));
            
            List<Assimp.Material> materials = new List<Assimp.Material>();
            meshes.ForEach(x => materials.Add(_Scene.Materials[x.MaterialIndex]));

            GameObject gameObject = Scene.Current.Instantiate();

            if (meshes.Count == 0)
                return gameObject;
            
            Mesh mesh = Mesh.FromAssimpMesh(meshes);
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Mesh = mesh;

            if (materials.Count == 0)
                return gameObject;

            Assimp.Material material = materials[0];
            
            Material meshMaterial = meshRenderer.Material;

            meshMaterial.Texture = TextureSlotToTexture(material.TextureDiffuse);
            meshMaterial.HeightMap = TextureSlotToTexture(material.TextureHeight);

            meshMaterial.Properties["DIFFUSE"].SetFloat(1f);
            meshMaterial.Properties["SPECULAR"].SetFloat(1.5f);
            meshMaterial.Properties["SPECULAR_SHININESS"].SetFloat(25);//material.Shininess

            //if (material.HasTextureSpecular)
            //    MessageBox.Show(material.TextureSpecular.FilePath.ToString());

            Color4D color = materials[0].ColorDiffuse;

            meshRenderer.Material.Color = ToColor(color);

            return gameObject;
        }

        private Color ToColor(Color4D color) => new Color(color.R, color.G, color.B, color.A);

        private void CreateChildrens(Node parentNode, Transform parent)
        {
            parentNode.Children.SafetyForEach(childrenNode =>
            {
                GameObject children = NodeToGameObject(childrenNode);
                Transform transform = children.Transform;
                transform.Parent = parent;
                TransformData data = TransformData.FromTransform(childrenNode.Transform);
                data.ApplyOnLocalTransform(transform);
                CreateChildrens(childrenNode, transform);
            });
        }

        private Texture TextureSlotToTexture(TextureSlot textureSlot)
        {
            string texturePath = Path.Combine(_DirectoryName, textureSlot.FilePath ?? "");

            if (!File.Exists(texturePath))
                return null;

            Texture texture = Texture.FromImagePath(texturePath);

            texture.AddressU = _WrapModeConveter[textureSlot.WrapModeU];
            texture.AddressV = _WrapModeConveter[textureSlot.WrapModeV];

            return texture;
        }

        private struct TransformData
        {
            public Vector3 Position;
            public SharpDX.Quaternion Rotation;
            public Vector3 Scale;
            
            public static TransformData FromTransform(Matrix4x4 transform)
            {
                transform.Decompose(out Vector3D scaling, out Assimp.Quaternion rotation, out Vector3D translation);
                return new TransformData
                {
                    Position = ToVector3(translation),
                    Rotation = new SharpDX.Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W),
                    Scale = ToVector3(scaling)
                };
            }

            public void ApplyOnTransform(Transform transform)
            {
                transform.WorldPosition = Position;
                transform.WorldRotation = Rotation;
                transform.WorldScale = Scale;
            }

            public void ApplyOnLocalTransform(Transform transform)
            {
                transform.LocalPosition = Position;
                transform.LocalRotation = Rotation;
                transform.LocalScale = Scale;
            }

            private static Vector3 ToVector3(Vector3D vector) => new Vector3(vector.X, vector.Y, vector.Z);
        }
    }
}
