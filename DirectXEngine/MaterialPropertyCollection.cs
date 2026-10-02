using SharpDX;
using SharpDX.Mathematics.Interop;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DirectXEngine
{
    public class MaterialPropertyCollection : IReadOnlyList<MaterialProperty>
    {
        public MaterialPropertyCollection(IEnumerable<MaterialProperty> properties)
        {
            ExceptionHelper.ThrowIfNull(properties);

            int size = _Vector4Size;
            int count = properties.Count();
            _Properties = new List<MaterialProperty>(count);

            for (int i = 0; i < count; i++)
            {
                MaterialProperty referenceProperty = properties.ElementAt(i);
                MaterialProperty property = new MaterialProperty(referenceProperty.Name, referenceProperty.Type);

                _NamedProperties[property.Name] = new PropertyIndex { Property = property, StartIndex = size };
                size += property.Size;

                _Properties.Add(property);

                property.ValueChanged += (value) =>
                {
                    value.WriteToByteArray(_RawData, _NamedProperties[value.Name].StartIndex);
                    UpdateConstantBuffer();
                };
            }

            int alignedSize = EngineUtilities.CalculateAlignedSize(size);

            _RawData = new byte[alignedSize];
            WriteColorInRawData();
            ConstantBuffer = new ConstantBuffer(EngineCore.Current.Device, _RawData, 0);
        }

        public MaterialPropertyCollection()
        {
            _RawData = new byte[_Vector4Size];
            WriteColorInRawData();
            ConstantBuffer = new ConstantBuffer(EngineCore.Current.Device, _RawData, 0);
        }

        public Color Color
        {
            get => _Color;
            set
            {
                _Color = value;
                WriteColorInRawData();
                UpdateConstantBuffer();
            }
        }
        public int Count => _Properties.Count;
        public IReadOnlyList<byte> RawData => _RawData;
        internal ConstantBuffer ConstantBuffer { get; }
        private Color _Color = Color.White;
        [SerializeMember] private Dictionary<string, PropertyIndex> _NamedProperties = new Dictionary<string, PropertyIndex>();
        [SerializeMember] private List<MaterialProperty> _Properties = new List<MaterialProperty>();
        [SerializeMember] private byte[] _RawData;
        private static readonly int _Vector4Size = Utilities.SizeOf<Vector4>();

        public IEnumerator<MaterialProperty> GetEnumerator() => _Properties.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _Properties.GetEnumerator();

        private void WriteColorInRawData()
        {
            RawColor4 color = _Color;
            byte[] colorBytes = EngineUtilities.ToByteArray(ref color);

            for (int i = 0; i < colorBytes.Length; i++)
                _RawData[i] = colorBytes[i];
        }

        private void UpdateConstantBuffer() => ConstantBuffer.Update(_RawData);

        public MaterialProperty this[int index] => _Properties[index];

        public MaterialProperty this[string name]
        {
            get
            {
                bool isHaveProperty = _NamedProperties.TryGetValue(name, out PropertyIndex property);
                ExceptionHelper.ThrowByCondition(!isHaveProperty, $"Can not find property with name {name}");
                return property.Property;
            }
        }

        private struct PropertyIndex
        {
            public MaterialProperty Property;
            public int StartIndex;
        }
    }
}
