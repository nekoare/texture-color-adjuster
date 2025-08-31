using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;

namespace TexColAdjuster
{
    // UV map generation utility inspired by MeshDeleterWithTexture
    public static class UVMapGenerator
    {
        private const string CS_VARIABLE_UVMAP = "UVMap";
        private const string CS_VARIABLE_WIDTH = "Width";
        private const string CS_VARIABLE_HEIGHT = "Height";
        private const string CS_VARIABLE_TRIANGLES = "Triangles";
        private const string CS_VARIABLE_UVS = "UVs";

        public static Texture2D GenerateUVMap(Mesh mesh, Texture2D referenceTexture, int materialIndex = 0)
        {
            if (mesh == null || referenceTexture == null) return null;

            var triangles = mesh.GetTriangles(materialIndex);
            var uvs = mesh.uv;

            if (uvs == null || uvs.Length == 0 || triangles == null || triangles.Length == 0) 
                return null;

            // Normalize UVs to 0-1 range
            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] = new Vector2(Mathf.Repeat(uvs[i].x, 1.0f), Mathf.Repeat(uvs[i].y, 1.0f));
            }

            // Create UV map texture using CPU-based rendering (fallback if no ComputeShader)
            return GenerateUVMapCPU(triangles, uvs, referenceTexture.width, referenceTexture.height);
        }

        private static Texture2D GenerateUVMapCPU(int[] triangles, Vector2[] uvs, int width, int height)
        {
            var uvMapTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var pixels = new Color[width * height];

            // Clear to black
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.black;

            // Draw UV triangles
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (i + 2 < triangles.Length)
                {
                    Vector2 uv0 = uvs[triangles[i]];
                    Vector2 uv1 = uvs[triangles[i + 1]];
                    Vector2 uv2 = uvs[triangles[i + 2]];

                    DrawTriangleUV(pixels, uv0, uv1, uv2, width, height, Color.white);
                }
            }

            uvMapTexture.SetPixels(pixels);
            uvMapTexture.Apply();
            return uvMapTexture;
        }

        private static void DrawTriangleUV(Color[] pixels, Vector2 uv0, Vector2 uv1, Vector2 uv2, 
            int width, int height, Color color)
        {
            // Convert UV to pixel coordinates
            Vector2Int p0 = new Vector2Int((int)(uv0.x * width), (int)((1f - uv0.y) * height));
            Vector2Int p1 = new Vector2Int((int)(uv1.x * width), (int)((1f - uv1.y) * height));
            Vector2Int p2 = new Vector2Int((int)(uv2.x * width), (int)((1f - uv2.y) * height));

            // Clamp to texture bounds
            p0.x = Mathf.Clamp(p0.x, 0, width - 1);
            p0.y = Mathf.Clamp(p0.y, 0, height - 1);
            p1.x = Mathf.Clamp(p1.x, 0, width - 1);
            p1.y = Mathf.Clamp(p1.y, 0, height - 1);
            p2.x = Mathf.Clamp(p2.x, 0, width - 1);
            p2.y = Mathf.Clamp(p2.y, 0, height - 1);

            // Draw triangle edges
            DrawLine(pixels, p0, p1, width, height, color);
            DrawLine(pixels, p1, p2, width, height, color);
            DrawLine(pixels, p2, p0, width, height, color);
        }

        private static void DrawLine(Color[] pixels, Vector2Int start, Vector2Int end, 
            int width, int height, Color color)
        {
            int dx = Mathf.Abs(end.x - start.x);
            int dy = Mathf.Abs(end.y - start.y);
            int x = start.x;
            int y = start.y;
            int x_inc = (end.x > start.x) ? 1 : -1;
            int y_inc = (end.y > start.y) ? 1 : -1;
            int error = dx - dy;

            dx *= 2;
            dy *= 2;

            for (int n = 0; n <= Mathf.Max(Mathf.Abs(end.x - start.x), Mathf.Abs(end.y - start.y)); n++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    int index = y * width + x;
                    if (index >= 0 && index < pixels.Length)
                        pixels[index] = color;
                }

                if (error > 0)
                {
                    x += x_inc;
                    error -= dy;
                }
                else
                {
                    y += y_inc;
                    error += dx;
                }
            }
        }

        public static MeshInfo GetMeshInfo(GameObject gameObject, Texture2D targetTexture, int materialIndex)
        {
            if (gameObject == null) return null;

            var meshRenderer = gameObject.GetComponent<MeshRenderer>();
            var skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            
            Mesh mesh = null;
            Material[] materials = null;

            if (meshRenderer != null)
            {
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    mesh = meshFilter.sharedMesh;
                    materials = meshRenderer.sharedMaterials;
                }
            }
            else if (skinnedMeshRenderer != null)
            {
                mesh = skinnedMeshRenderer.sharedMesh;
                materials = skinnedMeshRenderer.sharedMaterials;
            }

            if (mesh == null || materials == null)
                return null;

            // Auto-detect correct material index if specified index doesn't use target texture
            int correctMaterialIndex = materialIndex;
            if (materialIndex >= materials.Length || !DoeseMaterialUseTexture(materials[materialIndex], targetTexture))
            {
                correctMaterialIndex = FindMaterialIndexUsingTexture(materials, targetTexture);
                if (correctMaterialIndex == -1)
                {
                    Debug.LogWarning($"[High-precision] No material in GameObject '{gameObject.name}' uses texture '{targetTexture.name}'");
                    // Still return info but mark as not using texture
                    correctMaterialIndex = Mathf.Clamp(materialIndex, 0, materials.Length - 1);
                }
            }

            var triangles = mesh.subMeshCount > correctMaterialIndex ? mesh.GetTriangles(correctMaterialIndex) : new int[0];

            return new MeshInfo
            {
                mesh = mesh,
                material = materials[correctMaterialIndex],
                materialIndex = correctMaterialIndex,
                triangleCount = triangles.Length / 3,
                vertexCount = mesh.vertexCount,
                uvCount = mesh.uv != null ? mesh.uv.Length : 0,
                usesTargetTexture = DoeseMaterialUseTexture(materials[correctMaterialIndex], targetTexture)
            };
        }

        public static int FindMaterialIndexUsingTexture(Material[] materials, Texture2D targetTexture)
        {
            if (materials == null || targetTexture == null) return -1;

            for (int i = 0; i < materials.Length; i++)
            {
                if (DoeseMaterialUseTexture(materials[i], targetTexture))
                {
                    Debug.Log($"[High-precision] Found texture '{targetTexture.name}' at material index {i}");
                    return i;
                }
            }

            return -1;
        }

        private static bool DoeseMaterialUseTexture(Material material, Texture2D targetTexture)
        {
            if (material == null || targetTexture == null) return false;

            // Extended list of texture properties for better compatibility
            string[] textureProperties = {
                "_MainTex", "_BaseMap", "_BaseColorMap",
                "_AlbedoMap", "_DiffuseMap", "_ColorMap",
                "_Tex", "_Texture", "_AlbedoTexture",
                "_MainTexture", "_BaseTexture", "_SkinTex",
                "_ClothTex", "_FaceTex", "_BodyTex", "_EyeTex"
            };

            foreach (string propertyName in textureProperties)
            {
                if (material.HasProperty(propertyName))
                {
                    var texture = material.GetTexture(propertyName) as Texture2D;
                    if (texture != null)
                    {
                        // Check exact match
                        if (texture == targetTexture)
                            return true;
                        
                        // Check name match (for cases where texture instances differ but represent same asset)
                        if (texture.name == targetTexture.name && 
                            texture.width == targetTexture.width && 
                            texture.height == targetTexture.height)
                            return true;
                    }
                }
            }

            return false;
        }
    }

    [System.Serializable]
    public class MeshInfo
    {
        public Mesh mesh;
        public Material material;
        public int materialIndex;
        public int triangleCount;
        public int vertexCount;
        public int uvCount;
        public bool usesTargetTexture;
        public Texture2D uvMap;

        public string GetInfoString()
        {
            return $"Mesh: {(mesh != null ? mesh.name : "None")}\n" +
                   $"Material: {(material != null ? material.name : "None")}\n" +
                   $"Triangles: {triangleCount}\n" +
                   $"Vertices: {vertexCount}\n" +
                   $"UVs: {uvCount}\n" +
                   $"Uses Target Texture: {(usesTargetTexture ? "Yes" : "No")}";
        }
    }
}