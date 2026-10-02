using System;
using System.Windows.Forms;
using SharpDX;
using System.Collections.Generic;
using System.Linq;
using Assimp;
using System.Windows.Forms.VisualStyles;

namespace DirectXEngine
{
    public class Mesh : ICloneable
    {
        public Vector3[] Vertices
        {
            get => _Vertices;
            set
            {
                ExceptionHelper.ThrowIfNull(value);
                _Vertices = value;
            }
        }
        public int[] Triangles
        {
            get => _Triangles;
            set
            {
                ExceptionHelper.ThrowIfNull(value);
                ExceptionHelper.ThrowByCondition(value.Length % 3 != 0, "Triangles count must be multiple of 3");
                _Triangles = value;
            }
        }
        public Vector3[] Normals
        {
            get => _Normals;
            set
            {
                ExceptionHelper.ThrowIfNull(value);
                _Normals = value;
            }
        }
        public Vector2[] UVs
        {
            get => _UVs;
            set 
            {
                ExceptionHelper.ThrowIfNull(value);
                _UVs = value;
            }
        }
        public Vector3[] Tangents
        {
            get => _Tangents;
            set
            {
                ExceptionHelper.ThrowIfNull(value);
                _Tangents = value;
            }
        }
        public Vector3[] BiNormals
        {
            get => _BiNormals;
            set
            {
                ExceptionHelper.ThrowIfNull(value);
                _BiNormals = value;
            }
        }
        public int TrianglesCount => Triangles.Length / 3;
        public Bounds Bounds
        {
            get => _Bounds;
            set
            {
                ExceptionHelper.ThrowIfNull(value);
                _Bounds = value;
            }
        }
        private Vector3[] _Vertices = new Vector3[0];
        private int[] _Triangles = new int[0];
        private Vector3[] _Normals = new Vector3[0];
        private Vector2[] _UVs = new Vector2[0];
        private Vector3[] _Tangents = new Vector3[0];
        private Vector3[] _BiNormals = new Vector3[0];
        private Bounds _Bounds = new Bounds(Vector3.Zero, Vector3.One);

        public void RecalculateNormals(bool useFastCalculate = false)
        {
            _Normals = new Vector3[_Vertices.Length];

            for (int i = 0; i < _Vertices.Length; i++)
            {
                _Normals[i] = useFastCalculate ? CalculateNearestTriangleNormal(i) : CalculateAverageTrianglesNormal(i);
            }
        }

        public object Clone() => new Mesh
        {
            _Vertices = (Vector3[])_Vertices.Clone(),
            _Triangles = (int[])_Triangles.Clone(),
            _Normals = (Vector3[])_Normals.Clone(),
            _UVs = (Vector2[])_UVs.Clone(),
        };

        internal static Mesh FromAssimpMesh(Assimp.Mesh assimpMesh) => FromAssimpMesh(new Assimp.Mesh[] { assimpMesh });

        internal static Mesh FromAssimpMesh(IReadOnlyList<Assimp.Mesh> assimpMeshes)
        {
            MeshData data = GetAssimpMeshData(assimpMeshes);

            Vector3[] vertices = new Vector3[data.VerticesCount];
            int[] triangles = new int[data.TrianglesCount];
            Vector3[] normals = new Vector3[data.VerticesCount];
            Vector3[] biNormals = new Vector3[data.VerticesCount];
            Vector3[] tangents = new Vector3[data.VerticesCount];
            Vector2[] uvs = new Vector2[data.VerticesCount];

            int vertexIndex = 0;
            int triangleIndex = 0;

            for (int i = 0; i < assimpMeshes.Count; i++)
            {
                Assimp.Mesh currentMesh = assimpMeshes[i];
                List<Vector3D> textureCoordinates = new List<Vector3D>();

                foreach (List<Vector3D> currentUVs in currentMesh.TextureCoordinateChannels)
                {
                    if (currentUVs.Count != 0)
                    {
                        textureCoordinates = currentUVs;
                        break;
                    }
                }

                for (int j = 0; j < currentMesh.Vertices.Count; j++)
                {
                    Vector3D vertex = currentMesh.Vertices[j];
                    Vector3D normal = j < currentMesh.Normals.Count ? currentMesh.Normals[j] : new Vector3D(1, 1, 1);
                    Vector2 uv = j < textureCoordinates.Count ? new Vector2(textureCoordinates[j].X, textureCoordinates[j].Y) : Vector2.Zero;
                    Vector3D biNormal = j < currentMesh.BiTangents.Count ? currentMesh.BiTangents[j] : new Vector3D();
                    Vector3D tangent = j < currentMesh.Tangents.Count ? currentMesh.Tangents[j] : new Vector3D();

                    vertices[vertexIndex] = ToVector3(vertex);
                    normals[vertexIndex] = ToVector3(normal);
                    uvs[vertexIndex] = uv;
                    biNormals[vertexIndex] = ToVector3(biNormal);
                    tangents[vertexIndex] = ToVector3(tangent);
                    vertexIndex++;
                }

                for (int j = 0; j < currentMesh.Faces.Count; j++)
                {
                    Face face = currentMesh.Faces[j];
                    CopyTriangles(face.Indices, triangleIndex);
                    triangleIndex += face.Indices.Count;
                }
            }

            Mesh mesh = new Mesh();
            mesh.Vertices = vertices;
            mesh.Normals = normals;
            mesh.BiNormals = biNormals;
            mesh.Tangents = tangents;
            mesh.Triangles = triangles;
            mesh.UVs = uvs;

            return mesh;

            void CopyTriangles(List<int> meshTriangles, int startIndex)
            {
                for (int i = 0; i < meshTriangles.Count; i++)
                {
                    triangles[startIndex + i] = meshTriangles[i];
                }
            }

            Vector3 ToVector3(Vector3D vector) => new Vector3(vector.X, vector.Y, vector.Z);
        }

        private static MeshData GetAssimpMeshData(IReadOnlyList<Assimp.Mesh> assimpMeshes)
        {
            int verticesCount = 0;
            int trianglesCount = 0;

            for (int i = 0; i < assimpMeshes.Count; i++)
            {
                Assimp.Mesh currentMesh = assimpMeshes[i];
                verticesCount += currentMesh.Vertices.Count;

                for (int j = 0; j < currentMesh.Faces.Count; j++)
                {
                    Face face = currentMesh.Faces[j];
                    trianglesCount += face.Indices.Count;
                }
            }

            return new MeshData
            {
                VerticesCount = verticesCount,
                TrianglesCount = trianglesCount
            };
        }

        private Vector3 CalculateAverageTrianglesNormal(int vertexIndex)
        {
            Vector3 normal = Vector3.Zero;
            int trianglePosition = 0;
            int count = 0;

            for (int i = 0; i < _Triangles.Length; i++)
            {
                if (_Triangles[i] == vertexIndex)
                {
                    int triangleIndex1 = _Triangles[i - trianglePosition];
                    int triangleIndex2 = _Triangles[i - (trianglePosition - 1)];
                    int triangleIndex3 = _Triangles[i - (trianglePosition - 2)];

                    Vector3 vertex1 = _Vertices[triangleIndex1];
                    Vector3 vertex2 = _Vertices[triangleIndex2];
                    Vector3 vertex3 = _Vertices[triangleIndex3];

                    Vector3 currentNormal = Vector3.Cross(vertex3 - vertex2, vertex1 - vertex2);

                    normal += currentNormal;
                    count++;
                }

                trianglePosition++;

                if (trianglePosition > 2)
                    trianglePosition = 0;
            }
            
            normal.Normalize();

            return normal;
        }

        private Vector3 CalculateNearestTriangleNormal(int vertexIndex)
        {
            int trianglePosition = 0;

            for (int i = 0; i < _Triangles.Length; i++)
            {
                if (_Triangles[i] == vertexIndex)
                {
                    int triangleIndex1 = _Triangles[i - trianglePosition];
                    int triangleIndex2 = _Triangles[i - (trianglePosition - 1)];
                    int triangleIndex3 = _Triangles[i - (trianglePosition - 2)];

                    Vector3 vertex1 = _Vertices[triangleIndex1];
                    Vector3 vertex2 = _Vertices[triangleIndex2];
                    Vector3 vertex3 = _Vertices[triangleIndex3];

                    return Vector3.Cross(vertex3 - vertex2, vertex1 - vertex2);
                }

                trianglePosition++;

                if (trianglePosition > 2)
                    trianglePosition = 0;
            }

            return Vector3.Zero;
        }

        private struct MeshData
        {
            public int VerticesCount;
            public int TrianglesCount;
            public int UVsCount;
        }
    }
}
