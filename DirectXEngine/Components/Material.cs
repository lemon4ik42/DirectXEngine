using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
using SharpDX.Mathematics.Interop;
using SharpDX.DXGI;

namespace DirectXEngine
{
    public class Material
    {
        public Material()
        {
            Properties = new MaterialPropertyCollection();
        }

        public Material(IEnumerable<MaterialProperty> properties)
        {
            Properties = new MaterialPropertyCollection(properties);
        }

        public Shader Shader
        {
            get
            {
                if (_Shader == null)
                    _Shader = Shader.Default;
                return _Shader;
            }
            set
            {
                ExceptionHelper.ThrowIfNull(value);
                ExceptionHelper.ThrowByCondition(value.VertexShader == null && value.PixelShader == null, _ShaderMissException);
                _Shader = value;
            }
        }
        public Color Color
        {
            get => Properties.Color;
            set => Properties.Color = value;
        }
        public Texture Texture
        {
            get => _Texture;
            set
            {
                _Texture = value;
                TextureChanged?.Invoke(_Texture);
            }
        }
        public Texture HeightMap
        {
            get => _HeightMap;
            set
            {
                _HeightMap = value;
                HeightMapChanged?.Invoke(_HeightMap);
            }
        }
        public MaterialPropertyCollection Properties { get; }
        public static Material Default
        {
            get
            {
                MaterialProperty[] properties = new MaterialProperty[]
                {
                    new MaterialProperty("DIFFUSE", MaterialPropertyType.Float),
                    new MaterialProperty("SPECULAR", MaterialPropertyType.Float),
                    new MaterialProperty("SPECULAR_SHININESS", MaterialPropertyType.Float),
                    new MaterialProperty("COLOR_DIFFUSE", MaterialPropertyType.Vector3),
                    new MaterialProperty("COLOR_AMBIENT", MaterialPropertyType.Vector3),
                    new MaterialProperty("COLOR_SPECULAR", MaterialPropertyType.Vector3),
                    new MaterialProperty("COLOR_EMISSIVE", MaterialPropertyType.Vector3),
                };

                Material material = new Material(properties);

                material.Shader = Shader.Default;

                return material;
            }
        }
        internal event Action<Texture> TextureChanged;
        internal event Action<Texture> HeightMapChanged;
        private Texture _Texture;
        private Texture _HeightMap;
        private Shader _Shader;
        private const string _ShaderMissException = "Material must have at least vertex shader or pixel shader";
    }
}
