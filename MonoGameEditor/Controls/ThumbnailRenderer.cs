using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonoGameEditor.Core.Assets;

namespace MonoGameEditor.Core.Assets
{
    public static class ThumbnailRenderer
    {
        public static byte[] RenderPixels(AssetMetadata metadata, int width, int height)
        {
            if (metadata == null || metadata.PreviewVertices.Count == 0) return null;
            
            // Calculate center and scale to fit
            var center = (metadata.Min + metadata.Max) / 2f;
            var size = metadata.Max - metadata.Min;
            float maxDimension = MathF.Max(MathF.Max(size.X, size.Y), size.Z);
            if (maxDimension <= 0) maxDimension = 1f;
            float scale = (width * 0.8f) / maxDimension;

            float rx = 30f * MathF.PI / 180f;
            float ry = 45f * MathF.PI / 180f;
            
            float cx = MathF.Cos(rx), sx = MathF.Sin(rx);
            float cy = MathF.Cos(ry), sy = MathF.Sin(ry);

            int stride = width * 4;
            byte[] pixels = new byte[height * stride];

            // Light direction
            System.Numerics.Vector3 lightDir = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(1, 1, 1));
            byte r = 180, g = 180, b = 180; 

            // Z-Buffer
            float[] zBuffer = new float[width * height];
            for (int i = 0; i < zBuffer.Length; i++) zBuffer[i] = float.MaxValue;

            // Preconvert
            System.Numerics.Vector3[] viewPoints = new System.Numerics.Vector3[metadata.PreviewVertices.Count];
            for (int i = 0; i < metadata.PreviewVertices.Count; i++)
            {
                 var v = metadata.PreviewVertices[i];
                 float x = v.X - center.X;
                 float y = v.Y - center.Y; 
                 float z = v.Z - center.Z;

                 float x2 = x * cy - z * sy;
                 float z2 = x * sy + z * cy;
                 float y3 = y * cx - z2 * sx;
                 float z3 = y * sx + z2 * cx;

                 viewPoints[i] = new System.Numerics.Vector3(x2, y3, z3);
            }

            if (metadata.PreviewIndices.Count > 0)
            {
                for (int i = 0; i < metadata.PreviewIndices.Count; i += 3)
                {
                     if (i + 2 >= metadata.PreviewIndices.Count) break;
                     int idx1 = metadata.PreviewIndices[i];
                     int idx2 = metadata.PreviewIndices[i+1];
                     int idx3 = metadata.PreviewIndices[i+2];

                     if (idx1 >= viewPoints.Length || idx2 >= viewPoints.Length || idx3 >= viewPoints.Length) continue;

                     System.Numerics.Vector3 v1 = viewPoints[idx1];
                     System.Numerics.Vector3 v2 = viewPoints[idx2];
                     System.Numerics.Vector3 v3 = viewPoints[idx3];

                     // Calculate Face Normal
                     System.Numerics.Vector3 edge1 = v2 - v1;
                     System.Numerics.Vector3 edge2 = v3 - v1;
                     System.Numerics.Vector3 normal = System.Numerics.Vector3.Cross(edge1, edge2);
                     normal = System.Numerics.Vector3.Normalize(normal);

                     // Backface culling
                     // if (normal.Z <= 0) continue; // Disabled to support double-sided / inconsistent winding

                     // Lighting
                     float ndotl = System.Numerics.Vector3.Dot(normal, lightDir);
                     ndotl = MathF.Abs(ndotl); // Double sided lighting
                     if (ndotl < 0.2f) ndotl = 0.2f; // Ambient
                     
                     byte intensity = (byte)Math.Clamp(ndotl * 255, 0, 255);
                     byte finalR = (byte)((r * intensity) / 255);
                     byte finalG = (byte)((g * intensity) / 255);
                     byte finalB = (byte)((b * intensity) / 255);

                     // Project
                     float x1 = v1.X * scale + width / 2f;
                     float y1 = -v1.Y * scale + height / 2f;
                     float x2 = v2.X * scale + width / 2f;
                     float y2 = -v2.Y * scale + height / 2f;
                     float x3 = v3.X * scale + width / 2f;
                     float y3 = -v3.Y * scale + height / 2f;

                     DrawTriangle(pixels, zBuffer, width, height, stride, 
                                  (int)x1, (int)y1, v1.Z,
                                  (int)x2, (int)y2, v2.Z,
                                  (int)x3, (int)y3, v3.Z,
                                  finalR, finalG, finalB);
                }
            }
            return pixels;
        }

        public static BitmapSource CreateBitmap(byte[] pixels, int width, int height)
        {
            if (pixels == null) return null;
            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            return bitmap;
        }

        private static void DrawTriangle(byte[] pixels, float[] zBuffer, int w, int h, int stride, 
                                         int x1, int y1, float z1,
                                         int x2, int y2, float z2,
                                         int x3, int y3, float z3,
                                         byte r, byte g, byte b)
        {
            int minX = Math.Max(0, Math.Min(x1, Math.Min(x2, x3)));
            int minY = Math.Max(0, Math.Min(y1, Math.Min(y2, y3)));
            int maxX = Math.Min(w - 1, Math.Max(x1, Math.Max(x2, x3)));
            int maxY = Math.Min(h - 1, Math.Max(y1, Math.Max(y2, y3)));

            float area = (x2 - x1) * (y3 - y1) - (x3 - x1) * (y2 - y1);
            if (area == 0) return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float w1 = ((x2 - x) * (y3 - y) - (x3 - x) * (y2 - y)) / area;
                    float w2 = ((x3 - x) * (y1 - y) - (x1 - x) * (y3 - y)) / area;
                    float w3 = 1.0f - w1 - w2;

                    if (w1 >= 0 && w2 >= 0 && w3 >= 0)
                    {
                        float z = w1 * z1 + w2 * z2 + w3 * z3;
                        int idx = y * w + x;

                        if (z < zBuffer[idx])
                        {
                            zBuffer[idx] = z;
                            int pIdx = y * stride + x * 4;
                            pixels[pIdx] = b; 
                            pixels[pIdx + 1] = g; 
                            pixels[pIdx + 2] = r; 
                            pixels[pIdx + 3] = 255;
                        }
                    }
                }
            }
        }
    }
}
