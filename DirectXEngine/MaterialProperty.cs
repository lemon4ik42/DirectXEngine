using SharpDX;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DirectXEngine
{
    public class MaterialProperty
    {
        public MaterialProperty(string name, MaterialPropertyType type)
        {
            ExceptionHelper.ThrowByCondition(string.IsNullOrEmpty(name));
            Name = name;
            Type = type;
            _Data = _TypesData[type];
            _PropertyType = _Data.Type;
            _RawValue = _Data.DefaultValue;
            Size = _Data.Size;
        }

        public string Name { get; }
        public MaterialPropertyType Type { get; }
        public int Size { get; }
        public event Action<MaterialProperty> ValueChanged;
        private ValueType _RawValue;
        private Type _PropertyType;
        private PropertyData _Data;
        private static readonly Dictionary<MaterialPropertyType, PropertyData> _TypesData = new Dictionary<MaterialPropertyType, PropertyData>
        {
            { MaterialPropertyType.Float, PropertyData.Float },
            { MaterialPropertyType.Int, PropertyData.Int },
            { MaterialPropertyType.Bool, PropertyData.Bool },
            { MaterialPropertyType.Vector3, PropertyData.Vector3 },
            { MaterialPropertyType.Vector4, PropertyData.Vector4 },
        };

        public void SetFloat(float value) => SetRawValue(value);

        public void SetInt(float value) => SetRawValue(value);

        public void SetBool(bool value) => SetRawValue(value);

        public void SetVector3(Vector3 value) => SetRawValue(value);

        public void SetVector4(Vector4 value) => SetRawValue(value);

        public void SetValue<T>(T value) where T : struct => SetRawValue(value);

        public float GetFloat() => (float)GetRawValue(typeof(float));

        public int GetInt() => (int)GetRawValue(typeof(int));

        public bool GetBool() => (bool)GetRawValue(typeof(bool));

        public Vector3 GetVector3() => (Vector3)GetRawValue(typeof(Vector3));

        public Vector4 GetVector4() => (Vector4)GetRawValue(typeof(Vector4));

        public T GetValue<T>() where T : struct => (T)GetRawValue(typeof(T));

        internal void WriteToByteArray(byte[] bytes, int startIndex)
        {
            if (_RawValue == null)
                return;
            
            byte[] valueBytes = _Data.ConvertFunction.Invoke(_RawValue);          

            for (int i = 0; i < Size; i++)
                bytes[startIndex + i] = valueBytes[i];
        }

        private void SetRawValue(ValueType value)
        {
            Type type = value.GetType();
            ExceptionHelper.ThrowByCondition(type != _PropertyType, $"Can not set {type.Name} to property of type {Type}");
            _RawValue = value;
            ValueChanged?.Invoke(this);
        }

        private ValueType GetRawValue(Type type)
        {
            ExceptionHelper.ThrowByCondition(type != _PropertyType, $"Can not convert property of type {Type} to {type.Name}");
            return _RawValue;
        }

        private struct PropertyData
        {
            public Type Type;
            public int Size;
            public ValueType DefaultValue;
            public Func<object, byte[]> ConvertFunction;

            public readonly static PropertyData Float = new PropertyData
            {
                Type = typeof(float),
                Size = sizeof(float),
                DefaultValue = 0f,
                ConvertFunction = (value) => BitConverter.GetBytes((float)value)
            };
            public readonly static PropertyData Int = new PropertyData 
            {
                Type = typeof(int),
                Size = sizeof(int),
                DefaultValue = 0,
                ConvertFunction = (value) => BitConverter.GetBytes((int)value)
            };
            public readonly static PropertyData Bool = new PropertyData 
            {
                Type = typeof(bool),
                Size = sizeof(bool),
                DefaultValue = false,
                ConvertFunction = (value) => BitConverter.GetBytes((bool)value)
            };
            public readonly static PropertyData Vector3 = new PropertyData 
            {
                Type = typeof(Vector3),
                Size = Utilities.SizeOf<Vector3>(),
                DefaultValue = default(Vector3),
                ConvertFunction = (value) =>
                {
                    Vector3 vector3 = (Vector3)value;
                    return EngineUtilities.ToByteArray(ref vector3);
                }

            };
            public readonly static PropertyData Vector4 = new PropertyData
            {
                Type = typeof(Vector4),
                Size = Utilities.SizeOf<Vector4>(),
                DefaultValue = default(Vector4),
                ConvertFunction = (value) =>
                {
                    Vector4 vector4 = (Vector4)value;
                    return EngineUtilities.ToByteArray(ref vector4);
                }
            };
        }
    }
}
