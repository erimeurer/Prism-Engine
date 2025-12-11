using System;
using System.IO;
using System.Threading.Tasks;
using System.Numerics;
using System.Globalization;

namespace MonoGameEditor.Core.Assets
{
    public static class ObjParser
    {
        public static async Task<AssetMetadata> ParseMetadataAsync(string filePath)
        {
            return await Task.Run(() => 
            {
                var metadata = new AssetMetadata();
                try
                {
                    if (!File.Exists(filePath)) return metadata;

                    using (var reader = new StreamReader(filePath))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            
                            // Basic parsing based on line prefix
                            var span = line.AsSpan().Trim();
                            
                            if (span.StartsWith("v ")) // Vertex
                            {
                                metadata.VertexCount++;
                                ParseVertex(span, metadata);
                            }
                            else if (span.StartsWith("f ")) // Face
                            {
                                metadata.TriangleCount++;
                                ParseFace(span, metadata);
                            }
                            else if (span.StartsWith("vn ")) // Normal
                            {
                                metadata.HasNormals = true;
                            }
                            else if (span.StartsWith("vt ")) // UV
                            {
                                metadata.HasUVs = true;
                            }
                             else if (span.StartsWith("usemtl ")) // Material Use
                            {
                                // We could count unique mats here but for now just increment occurrences? 
                                // Better to just count newmtl in .mtl file, but let's count usemtl usage for complexity
                                metadata.MaterialCount++; 
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing OBJ: {ex.Message}");
                }
                return metadata;
            });
        }

        private static void ParseVertex(ReadOnlySpan<char> line, AssetMetadata metadata)
        {
             // Line format: v x y z
             var parts = line.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
             if (parts.Length >= 4)
             {
                 if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                     float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                     float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                 {
                     if (x < metadata.Min.X) metadata.Min = new Vector3(x, metadata.Min.Y, metadata.Min.Z);
                     if (y < metadata.Min.Y) metadata.Min = new Vector3(metadata.Min.X, y, metadata.Min.Z);
                     if (z < metadata.Min.Z) metadata.Min = new Vector3(metadata.Min.X, metadata.Min.Y, z);

                     if (x > metadata.Max.X) metadata.Max = new Vector3(x, metadata.Max.Y, metadata.Max.Z);
                     if (y > metadata.Max.Y) metadata.Max = new Vector3(metadata.Max.X, y, metadata.Max.Z);
                     if (z > metadata.Max.Z) metadata.Max = new Vector3(metadata.Max.X, metadata.Max.Y, z);

                     // Add to preview vertices (limit to avoid huge memory for thumbnails)
                     if (metadata.PreviewVertices.Count < 5000)
                     {
                         metadata.PreviewVertices.Add(new Vector3(x, y, z));
                     }
                 }
             }
        }

        private static void ParseFace(ReadOnlySpan<char> line, AssetMetadata metadata)
        {
            // Limit indices to avoid memory explosion on massive models
            if (metadata.PreviewIndices.Count > 10000) return;

            // Format: f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3 ...
            var parts = line.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return; // Need at least 3 vertices (f + 3)

            // Parse indices
            var indices = new List<int>();
            for (int i = 1; i < parts.Length; i++)
            {
                var vertexPart = parts[i];
                int slashIdx = vertexPart.IndexOf('/');
                string vStr = slashIdx > 0 ? vertexPart.Substring(0, slashIdx) : vertexPart;
                
                if (int.TryParse(vStr, out int vIdx))
                {
                    // OBJ is 1-based, handle negative indices (relative) not supported here for simplicity yet
                    if (vIdx > 0) indices.Add(vIdx - 1);
                }
            }

            // Triangulate fan (0, 1, 2), (0, 2, 3), ...
            if (indices.Count >= 3)
            {
                for (int i = 1; i < indices.Count - 1; i++)
                {
                    metadata.PreviewIndices.Add(indices[0]);
                    metadata.PreviewIndices.Add(indices[i]);
                    metadata.PreviewIndices.Add(indices[i + 1]);
                }
            }
        }
    }
}
