using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Minimal Wavefront OBJ reader (same as Warden's, plus gzip): positions, UVs, normals, polygon
    /// faces (fan-triangulated). Converts from OBJ's right-handed space to Unity's left-handed space
    /// by mirroring X. Resources ending in ".gz" are decompressed on the fly.
    /// </summary>
    internal static class ObjLoader
    {
        public static Mesh LoadEmbedded(string resourceName, string meshName)
        {
            using (var stream = typeof(ObjLoader).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Jotunn.Logger.LogWarning($"Embedded model '{resourceName}' not found.");
                    return null;
                }
                var source = resourceName.EndsWith(".gz") ? new GZipStream(stream, CompressionMode.Decompress) : stream;
                using (var reader = new StreamReader(source)) return Parse(reader, meshName);
            }
        }

        private static Mesh Parse(TextReader reader, string meshName)
        {
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();

            var outVertices = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var outNormals = new List<Vector3>();
            var triangles = new List<int>();
            var vertexCache = new Dictionary<string, int>();
            var face = new List<int>();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                switch (parts[0])
                {
                    case "v":
                        positions.Add(new Vector3(-F(parts[1]), F(parts[2]), F(parts[3])));
                        break;
                    case "vt":
                        uvs.Add(new Vector2(F(parts[1]), F(parts[2])));
                        break;
                    case "vn":
                        normals.Add(new Vector3(-F(parts[1]), F(parts[2]), F(parts[3])));
                        break;
                    case "f":
                        face.Clear();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (!vertexCache.TryGetValue(parts[i], out int index))
                            {
                                var refs = parts[i].Split('/');
                                index = outVertices.Count;
                                outVertices.Add(positions[Index(refs[0], positions.Count)]);
                                outUvs.Add(refs.Length > 1 && refs[1].Length > 0 ? uvs[Index(refs[1], uvs.Count)] : Vector2.zero);
                                outNormals.Add(refs.Length > 2 && refs[2].Length > 0 ? normals[Index(refs[2], normals.Count)] : Vector3.zero);
                                vertexCache[parts[i]] = index;
                            }
                            face.Add(index);
                        }

                        // Mirroring X flips handedness, so emit each fan triangle in reverse order.
                        for (int i = 1; i + 1 < face.Count; i++)
                        {
                            triangles.Add(face[0]);
                            triangles.Add(face[i + 1]);
                            triangles.Add(face[i]);
                        }
                        break;
                }
            }

            var mesh = new Mesh { name = meshName };
            if (outVertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(outVertices);
            mesh.SetUVs(0, outUvs);
            mesh.SetTriangles(triangles, 0);
            if (normals.Count > 0) mesh.SetNormals(outNormals);
            else mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        // OBJ indices are 1-based; negative values count back from the end.
        private static int Index(string s, int count)
        {
            int i = int.Parse(s, CultureInfo.InvariantCulture);
            return i > 0 ? i - 1 : count + i;
        }
    }
}
