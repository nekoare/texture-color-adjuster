using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TexColAdjuster
{
    // High-precision mode: Analyze mesh UV mapping to determine which texture areas are actually used
    public static class MeshUVAnalyzer
    {
        // UV usage data structure
        [Serializable]
        public class UVUsageData
        {
            public BitArray usedPixels;
            public int textureWidth;
            public int textureHeight;
            public float usagePercentage;
            public Bounds uvBounds;
            public List<Vector2> usedUVs;
            
            public UVUsageData(int width, int height)
            {
                textureWidth = width;
                textureHeight = height;
                usedPixels = new BitArray(width * height, false);
                usedUVs = new List<Vector2>();
                uvBounds = new Bounds();
            }
        }

        // Analyze GameObject's mesh to determine which UV areas are actually used
        public static UVUsageData AnalyzeGameObjectUVUsage(GameObject gameObject, Texture2D targetTexture, 
            int materialIndex = 0, int uvChannel = 0)
        {
            if (gameObject == null || targetTexture == null)
                return null;

            Debug.Log($"[High-precision] Starting enhanced UV analysis for GameObject '{gameObject.name}' with texture '{targetTexture.name}', materialIndex: {materialIndex}, uvChannel: {uvChannel}");

            var uvUsage = new UVUsageData(targetTexture.width, targetTexture.height);
            
            // Get all material slot indices that use the same material (MeshDeleterWithTexture approach)
            var materialSlotIndices = GetMaterialSlotIndices(gameObject, materialIndex);
            if (materialSlotIndices.Count == 0)
            {
                Debug.LogWarning($"[High-precision] No valid material slots found for material index {materialIndex}");
                return uvUsage;
            }

            Debug.Log($"[High-precision] Found {materialSlotIndices.Count} material slots using the same material: [{string.Join(", ", materialSlotIndices)}]");
            
            // Get mesh info using improved method from MeshDeleterWithTexture approach
            var meshInfo = UVMapGenerator.GetMeshInfo(gameObject, targetTexture, materialIndex);
            if (meshInfo == null)
            {
                Debug.LogWarning($"[High-precision] Failed to get mesh info for GameObject '{gameObject.name}'");
                return uvUsage;
            }

            Debug.Log($"[High-precision] Processing mesh '{meshInfo.mesh.name}' with {meshInfo.triangleCount} triangles");
            
            // Generate UV map for better visualization
            meshInfo.uvMap = UVMapGenerator.GenerateUVMap(meshInfo.mesh, targetTexture, materialIndex);
            
            // Process all submeshes that use the same material (MeshDeleterWithTexture approach)
            ProcessMultipleSubmeshes(meshInfo.mesh, materialSlotIndices, uvChannel, uvUsage);
            
            // Also check child objects
            var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>();
            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            Debug.Log($"[High-precision] Found {meshRenderers.Length} MeshRenderers and {skinnedMeshRenderers.Length} SkinnedMeshRenderers in children");
            
            // Process MeshRenderer components
            foreach (var meshRenderer in meshRenderers)
            {
                if (meshRenderer.gameObject == gameObject) continue; // Skip main object (already processed)
                Debug.Log($"[High-precision] Processing child MeshRenderer on '{meshRenderer.gameObject.name}'");
                ProcessMeshRenderer(meshRenderer, targetTexture, materialIndex, uvChannel, uvUsage);
            }
            
            // Process SkinnedMeshRenderer components
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.gameObject == gameObject) continue; // Skip main object (already processed)
                Debug.Log($"[High-precision] Processing child SkinnedMeshRenderer on '{skinnedMeshRenderer.gameObject.name}'");
                ProcessSkinnedMeshRenderer(skinnedMeshRenderer, targetTexture, materialIndex, uvChannel, uvUsage);
            }
            
            // Calculate usage statistics
            CalculateUsageStatistics(uvUsage);
            
            Debug.Log($"[High-precision] Enhanced UV analysis complete. Usage: {uvUsage.usagePercentage:F1}%, Used UVs: {uvUsage.usedUVs.Count}");
            
            return uvUsage;
        }

        // Get all material slot indices that use the same material (MeshDeleterWithTexture approach)
        private static List<int> GetMaterialSlotIndices(GameObject gameObject, int materialIndex)
        {
            var materialSlotIndices = new List<int>();
            
            var meshRenderer = gameObject.GetComponent<MeshRenderer>();
            var skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            
            Material[] materials = null;
            if (meshRenderer != null)
            {
                materials = meshRenderer.sharedMaterials;
            }
            else if (skinnedMeshRenderer != null)
            {
                materials = skinnedMeshRenderer.sharedMaterials;
            }
            
            if (materials == null || materialIndex >= materials.Length || materials[materialIndex] == null)
            {
                return materialSlotIndices;
            }
            
            var targetMaterial = materials[materialIndex];
            
            // Find all slots that use the same material (by name, like MeshDeleterWithTexture)
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].name == targetMaterial.name)
                {
                    materialSlotIndices.Add(i);
                }
            }
            
            return materialSlotIndices;
        }

        // Process multiple submeshes that use the same material
        private static void ProcessMultipleSubmeshes(Mesh mesh, List<int> materialSlotIndices, int uvChannel, UVUsageData uvUsage)
        {
            if (mesh == null || materialSlotIndices.Count == 0) return;

            Vector2[] uvs = null;
            
            // Get UV coordinates based on channel
            switch (uvChannel)
            {
                case 0:
                    uvs = mesh.uv;
                    break;
                case 1:
                    uvs = mesh.uv2;
                    break;
                case 2:
                    uvs = mesh.uv3;
                    break;
                case 3:
                    uvs = mesh.uv4;
                    break;
                default:
                    uvs = mesh.uv;
                    break;
            }
            
            if (uvs == null || uvs.Length == 0) return;
            
            // Apply UV wrapping like MeshDeleterWithTexture
            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] = new Vector2(Mathf.Repeat(uvs[i].x, 1.0f), Mathf.Repeat(uvs[i].y, 1.0f));
            }
            
            int totalTriangles = 0;
            
            // Process triangles from all material slots that use the same material
            foreach (int slotIndex in materialSlotIndices)
            {
                if (slotIndex >= mesh.subMeshCount) continue;
                
                int[] triangles = mesh.GetTriangles(slotIndex);
                if (triangles == null || triangles.Length == 0) continue;
                
                Debug.Log($"[High-precision] Processing submesh {slotIndex}: {triangles.Length / 3} triangles");
                totalTriangles += triangles.Length / 3;
                
                // Process each triangle with enhanced precision
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    if (i + 2 >= triangles.Length) break;
                    
                    Vector2 uv0 = uvs[triangles[i]];
                    Vector2 uv1 = uvs[triangles[i + 1]];
                    Vector2 uv2 = uvs[triangles[i + 2]];
                    
                    // Rasterize triangle in UV space with enhanced precision
                    RasterizeTriangleEnhanced(uv0, uv1, uv2, uvUsage);
                }
            }
            
            Debug.Log($"[High-precision] Processed {totalTriangles} triangles across {materialSlotIndices.Count} submeshes");
            
            // Calculate usage statistics to verify correctness
            int usedCount = 0;
            for (int i = 0; i < uvUsage.usedPixels.Length; i++)
            {
                if (uvUsage.usedPixels[i])
                    usedCount++;
            }
            Debug.Log($"[High-precision] Enhanced processing marked {usedCount} pixels as used out of {uvUsage.usedPixels.Length} total pixels");
            
            // Debug coordinate conversion with test points
            TestCoordinateConversion(uvUsage.textureWidth, uvUsage.textureHeight);
        }

        private static void ProcessMeshRenderer(MeshRenderer meshRenderer, Texture2D targetTexture, 
            int materialIndex, int uvChannel, UVUsageData uvUsage)
        {
            if (meshRenderer == null || meshRenderer.sharedMaterials == null)
            {
                Debug.Log($"[High-precision] MeshRenderer or sharedMaterials is null");
                return;
            }
            
            Debug.Log($"[High-precision] MeshRenderer has {meshRenderer.sharedMaterials.Length} materials");
            
            // Check if the material uses the target texture
            if (materialIndex >= meshRenderer.sharedMaterials.Length)
            {
                Debug.LogWarning($"[High-precision] Material index {materialIndex} out of range (only {meshRenderer.sharedMaterials.Length} materials)");
                return;
            }
            
            var material = meshRenderer.sharedMaterials[materialIndex];
            if (material == null)
            {
                Debug.LogWarning($"[High-precision] Material at index {materialIndex} is null");
                return;
            }
            
            if (!DoeseMaterialUseTexture(material, targetTexture))
            {
                Debug.Log($"[High-precision] Material '{material.name}' does not use target texture");
                return;
            }
            
            // Get the mesh
            var meshFilter = meshRenderer.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                Debug.LogWarning($"[High-precision] No MeshFilter found on GameObject '{meshRenderer.gameObject.name}'");
                return;
            }
            
            if (meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"[High-precision] MeshFilter has no sharedMesh on GameObject '{meshRenderer.gameObject.name}'");
                return;
            }
            
            Debug.Log($"[High-precision] Processing mesh '{meshFilter.sharedMesh.name}' with {meshFilter.sharedMesh.triangles.Length / 3} triangles");
            ProcessMesh(meshFilter.sharedMesh, uvChannel, uvUsage);
        }

        private static void ProcessMeshEnhanced(Mesh mesh, int materialIndex, int uvChannel, UVUsageData uvUsage)
        {
            if (mesh == null) return;

            Vector2[] uvs = null;
            
            // Get UV coordinates based on channel
            switch (uvChannel)
            {
                case 0:
                    uvs = mesh.uv;
                    break;
                case 1:
                    uvs = mesh.uv2;
                    break;
                case 2:
                    uvs = mesh.uv3;
                    break;
                case 3:
                    uvs = mesh.uv4;
                    break;
                default:
                    uvs = mesh.uv;
                    break;
            }
            
            if (uvs == null || uvs.Length == 0) return;
            
            // Get triangles for the specific submesh
            int[] triangles = mesh.GetTriangles(materialIndex);
            if (triangles == null || triangles.Length == 0) return;
            
            Debug.Log($"[High-precision] Processing {triangles.Length / 3} triangles from submesh {materialIndex}");
            
            // Process each triangle with enhanced precision
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (i + 2 >= triangles.Length) break;
                
                Vector2 uv0 = uvs[triangles[i]];
                Vector2 uv1 = uvs[triangles[i + 1]];
                Vector2 uv2 = uvs[triangles[i + 2]];
                
                // Rasterize triangle in UV space with enhanced precision
                RasterizeTriangleEnhanced(uv0, uv1, uv2, uvUsage);
            }
        }

        private static void ProcessSkinnedMeshRenderer(SkinnedMeshRenderer skinnedMeshRenderer, Texture2D targetTexture, 
            int materialIndex, int uvChannel, UVUsageData uvUsage)
        {
            if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMaterials == null) return;
            
            // Check if the material uses the target texture
            if (materialIndex >= skinnedMeshRenderer.sharedMaterials.Length) return;
            
            var material = skinnedMeshRenderer.sharedMaterials[materialIndex];
            if (material == null || !DoeseMaterialUseTexture(material, targetTexture)) return;
            
            // Get the mesh
            if (skinnedMeshRenderer.sharedMesh == null) return;
            
            ProcessMesh(skinnedMeshRenderer.sharedMesh, uvChannel, uvUsage);
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
            
            Debug.Log($"[High-precision] Checking material '{material.name}' for texture '{targetTexture.name}'");
            
            foreach (string propertyName in textureProperties)
            {
                if (material.HasProperty(propertyName))
                {
                    var texture = material.GetTexture(propertyName) as Texture2D;
                    if (texture != null)
                    {
                        Debug.Log($"[High-precision] Property '{propertyName}' has texture '{texture.name}'");
                        // Check exact match
                        if (texture == targetTexture)
                        {
                            Debug.Log($"[High-precision] ✓ Exact texture match found on property '{propertyName}'");
                            return true;
                        }
                        
                        // Check name match (for cases where texture instances differ but represent same asset)
                        if (texture.name == targetTexture.name && 
                            texture.width == targetTexture.width && 
                            texture.height == targetTexture.height)
                        {
                            Debug.Log($"[High-precision] ✓ Name/size texture match found on property '{propertyName}'");
                            return true;
                        }
                    }
                }
            }
            
            Debug.LogWarning($"[High-precision] No texture match found for '{targetTexture.name}' in material '{material.name}'");
            return false;
        }

        private static void ProcessMesh(Mesh mesh, int uvChannel, UVUsageData uvUsage)
        {
            Vector2[] uvs = null;
            
            // Get UV coordinates based on channel
            switch (uvChannel)
            {
                case 0:
                    uvs = mesh.uv;
                    break;
                case 1:
                    uvs = mesh.uv2;
                    break;
                case 2:
                    uvs = mesh.uv3;
                    break;
                case 3:
                    uvs = mesh.uv4;
                    break;
                default:
                    uvs = mesh.uv;
                    break;
            }
            
            if (uvs == null || uvs.Length == 0) return;
            
            // Get triangles
            int[] triangles = mesh.triangles;
            if (triangles == null || triangles.Length == 0) return;
            
            // Process each triangle
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (i + 2 >= triangles.Length) break;
                
                Vector2 uv0 = uvs[triangles[i]];
                Vector2 uv1 = uvs[triangles[i + 1]];
                Vector2 uv2 = uvs[triangles[i + 2]];
                
                // Rasterize triangle in UV space
                RasterizeTriangle(uv0, uv1, uv2, uvUsage);
            }
        }

        private static void RasterizeTriangle(Vector2 uv0, Vector2 uv1, Vector2 uv2, UVUsageData uvUsage)
        {
            // Convert UV coordinates to pixel coordinates
            Vector2Int p0 = UVToPixel(uv0, uvUsage.textureWidth, uvUsage.textureHeight);
            Vector2Int p1 = UVToPixel(uv1, uvUsage.textureWidth, uvUsage.textureHeight);
            Vector2Int p2 = UVToPixel(uv2, uvUsage.textureWidth, uvUsage.textureHeight);
            
            // Find bounding box
            int minX = Mathf.Max(0, Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x)));
            int maxX = Mathf.Min(uvUsage.textureWidth - 1, Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x)));
            int minY = Mathf.Max(0, Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y)));
            int maxY = Mathf.Min(uvUsage.textureHeight - 1, Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y)));
            
            // Rasterize pixels within triangle
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (IsPointInTriangle(new Vector2(x, y), p0, p1, p2))
                    {
                        // Convert pixel space y to texture array index y (same as Enhanced version)
                        int textureY = (uvUsage.textureHeight - 1) - y;
                        int index = textureY * uvUsage.textureWidth + x;
                        if (index >= 0 && index < uvUsage.usedPixels.Length)
                        {
                            uvUsage.usedPixels[index] = true;
                        }
                    }
                }
            }
            
            // Store UV coordinates for bounds calculation
            uvUsage.usedUVs.Add(uv0);
            uvUsage.usedUVs.Add(uv1);
            uvUsage.usedUVs.Add(uv2);
        }

        private static void RasterizeTriangleEnhanced(Vector2 uv0, Vector2 uv1, Vector2 uv2, UVUsageData uvUsage)
        {
            // Skip degenerate triangles
            float area = Mathf.Abs((uv1.x - uv0.x) * (uv2.y - uv0.y) - (uv2.x - uv0.x) * (uv1.y - uv0.y));
            if (area < 1e-6f) return;
            
            // Convert UV coordinates to pixel coordinates with enhanced precision
            Vector2Int p0 = UVToPixelEnhanced(uv0, uvUsage.textureWidth, uvUsage.textureHeight);
            Vector2Int p1 = UVToPixelEnhanced(uv1, uvUsage.textureWidth, uvUsage.textureHeight);
            Vector2Int p2 = UVToPixelEnhanced(uv2, uvUsage.textureWidth, uvUsage.textureHeight);
            
            // Find bounding box with enhanced edge detection
            int minX = Mathf.Max(0, Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x)) - 1);
            int maxX = Mathf.Min(uvUsage.textureWidth - 1, Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x)) + 1);
            int minY = Mathf.Max(0, Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y)) - 1);
            int maxY = Mathf.Min(uvUsage.textureHeight - 1, Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y)) + 1);
            
            // Enhanced rasterization with subpixel accuracy and triangle filling
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // Test multiple sample points per pixel for better accuracy
                    bool pixelCovered = false;
                    
                    // Sample at pixel center and corners for better coverage
                    Vector2[] samplePoints = {
                        new Vector2(x + 0.5f, y + 0.5f), // Center
                        new Vector2(x + 0.25f, y + 0.25f), // Top-left
                        new Vector2(x + 0.75f, y + 0.25f), // Top-right
                        new Vector2(x + 0.25f, y + 0.75f), // Bottom-left
                        new Vector2(x + 0.75f, y + 0.75f)  // Bottom-right
                    };
                    
                    foreach (var samplePoint in samplePoints)
                    {
                        if (IsPointInTriangleEnhanced(samplePoint, p0, p1, p2))
                        {
                            pixelCovered = true;
                            break;
                        }
                    }
                    
                    if (pixelCovered)
                    {
                        // Now y is in pixel space (top to bottom), but GetPixels() expects bottom to top
                        // Convert pixel space y to texture array index y
                        int textureY = (uvUsage.textureHeight - 1) - y;
                        int index = textureY * uvUsage.textureWidth + x;
                        if (index >= 0 && index < uvUsage.usedPixels.Length)
                        {
                            uvUsage.usedPixels[index] = true;
                        }
                    }
                }
            }
            
            // Store UV coordinates for bounds calculation
            uvUsage.usedUVs.Add(uv0);
            uvUsage.usedUVs.Add(uv1);
            uvUsage.usedUVs.Add(uv2);
        }

        private static Vector2Int UVToPixelEnhanced(Vector2 uv, int width, int height)
        {
            // Handle UV wrapping and clamping more precisely
            float wrappedU = Mathf.Repeat(uv.x, 1.0f); // Ensure 0-1 range with proper wrapping
            float wrappedV = Mathf.Repeat(uv.y, 1.0f); // Ensure 0-1 range with proper wrapping
            
            // Convert UV coordinates to pixel coordinates
            // UV space: (0,0) = bottom-left, (1,1) = top-right
            // Pixel space: (0,0) = top-left, (width-1,height-1) = bottom-right
            // We need to flip Y to convert from UV space to pixel space
            int x = Mathf.FloorToInt(wrappedU * width);
            int y = Mathf.FloorToInt((1.0f - wrappedV) * height); // Flip Y: UV bottom becomes pixel top
            
            // Clamp to texture bounds to prevent out-of-bounds access
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);
            
            return new Vector2Int(x, y);
        }

        private static bool IsPointInTriangleEnhanced(Vector2 point, Vector2Int a, Vector2Int b, Vector2Int c)
        {
            // Use barycentric coordinates for more accurate point-in-triangle test
            Vector2 v0 = new Vector2(c.x - a.x, c.y - a.y);
            Vector2 v1 = new Vector2(b.x - a.x, b.y - a.y);
            Vector2 v2 = new Vector2(point.x - a.x, point.y - a.y);

            float dot00 = Vector2.Dot(v0, v0);
            float dot01 = Vector2.Dot(v0, v1);
            float dot02 = Vector2.Dot(v0, v2);
            float dot11 = Vector2.Dot(v1, v1);
            float dot12 = Vector2.Dot(v1, v2);

            // Handle degenerate triangles
            float denom = dot00 * dot11 - dot01 * dot01;
            if (Mathf.Abs(denom) < 1e-10f) return false;

            float invDenom = 1f / denom;
            float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            // Check if point is in triangle with small tolerance
            const float tolerance = 1e-6f;
            return (u >= -tolerance) && (v >= -tolerance) && (u + v <= 1 + tolerance);
        }

        private static Vector2Int UVToPixel(Vector2 uv, int width, int height)
        {
            // Clamp UV coordinates to [0,1] range
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);
            
            // Convert to pixel coordinates
            int x = Mathf.FloorToInt(uv.x * width);
            int y = Mathf.FloorToInt((1f - uv.y) * height); // Flip Y coordinate
            
            // Clamp to texture bounds
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);
            
            return new Vector2Int(x, y);
        }

        private static bool IsPointInTriangle(Vector2 point, Vector2Int a, Vector2Int b, Vector2Int c)
        {
            Vector2 v0 = new Vector2(c.x - a.x, c.y - a.y);
            Vector2 v1 = new Vector2(b.x - a.x, b.y - a.y);
            Vector2 v2 = new Vector2(point.x - a.x, point.y - a.y);

            float dot00 = Vector2.Dot(v0, v0);
            float dot01 = Vector2.Dot(v0, v1);
            float dot02 = Vector2.Dot(v0, v2);
            float dot11 = Vector2.Dot(v1, v1);
            float dot12 = Vector2.Dot(v1, v2);

            float invDenom = 1f / (dot00 * dot11 - dot01 * dot01);
            float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            return (u >= 0) && (v >= 0) && (u + v <= 1);
        }

        private static void CalculateUsageStatistics(UVUsageData uvUsage)
        {
            // Count used pixels
            int usedCount = 0;
            for (int i = 0; i < uvUsage.usedPixels.Length; i++)
            {
                if (uvUsage.usedPixels[i])
                    usedCount++;
            }
            
            // Calculate usage percentage
            uvUsage.usagePercentage = (float)usedCount / uvUsage.usedPixels.Length * 100f;
            
            // Calculate UV bounds
            if (uvUsage.usedUVs.Count > 0)
            {
                Vector2 min = uvUsage.usedUVs[0];
                Vector2 max = uvUsage.usedUVs[0];
                
                foreach (var uv in uvUsage.usedUVs)
                {
                    min.x = Mathf.Min(min.x, uv.x);
                    min.y = Mathf.Min(min.y, uv.y);
                    max.x = Mathf.Max(max.x, uv.x);
                    max.y = Mathf.Max(max.y, uv.y);
                }
                
                Vector2 center = (min + max) * 0.5f;
                Vector2 size = max - min;
                uvUsage.uvBounds = new Bounds(center, size);
            }
        }

        // Create a masked texture showing only the used UV areas
        public static Texture2D CreateMaskedTexture(Texture2D sourceTexture, UVUsageData uvUsage, 
            Color maskColor = default, float maskAlpha = 0.3f)
        {
            if (sourceTexture == null || uvUsage == null) return null;
            
            if (maskColor == default)
                maskColor = new Color(0.2f, 0.2f, 0.2f, maskAlpha);
            
            // Make texture readable
            var readableTexture = TextureProcessor.MakeTextureReadable(sourceTexture);
            if (readableTexture == null) return null;
            
            Color[] sourcePixels = TextureUtils.GetPixelsSafe(readableTexture);
            if (sourcePixels == null) return null;
            
            Color[] maskedPixels = new Color[sourcePixels.Length];
            
            for (int i = 0; i < sourcePixels.Length; i++)
            {
                if (i < uvUsage.usedPixels.Length && uvUsage.usedPixels[i])
                {
                    // Keep original color for used areas
                    maskedPixels[i] = sourcePixels[i];
                }
                else
                {
                    // Apply mask for unused areas
                    maskedPixels[i] = Color.Lerp(sourcePixels[i], maskColor, maskAlpha);
                }
            }
            
            var maskedTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
            if (TextureUtils.SetPixelsSafe(maskedTexture, maskedPixels))
            {
                // Debug info to verify mask creation
                int usedPixelCount = 0;
                for (int i = 0; i < uvUsage.usedPixels.Length; i++)
                {
                    if (uvUsage.usedPixels[i]) usedPixelCount++;
                }
                Debug.Log($"[CreateMaskedTexture] Created mask with {usedPixelCount} used pixels out of {uvUsage.usedPixels.Length} total");
                return maskedTexture;
            }
            
            UnityEngine.Object.DestroyImmediate(maskedTexture);
            return null;
        }

        // Get dominant colors from only the used UV areas
        public static List<Color> ExtractDominantColorsFromUsedAreas(Texture2D sourceTexture, 
            UVUsageData uvUsage, int colorCount = 5)
        {
            if (sourceTexture == null || uvUsage == null) 
                return new List<Color>();
            
            // Make texture readable
            var readableTexture = TextureProcessor.MakeTextureReadable(sourceTexture);
            if (readableTexture == null) return new List<Color>();
            
            Color[] sourcePixels = TextureUtils.GetPixelsSafe(readableTexture);
            if (sourcePixels == null) return new List<Color>();
            
            // Extract only used and opaque pixels
            List<Color> usedPixels = new List<Color>();
            for (int i = 0; i < sourcePixels.Length && i < uvUsage.usedPixels.Length; i++)
            {
                if (uvUsage.usedPixels[i])
                {
                    Color pixel = sourcePixels[i];
                    // Only include opaque pixels in color analysis
                    if (pixel.a >= 0.01f) // Use same threshold as ColorAdjuster
                    {
                        usedPixels.Add(pixel);
                    }
                }
            }
            
            if (usedPixels.Count == 0)
            {
                Debug.LogWarning("[ExtractDominantColorsFromUsedAreas] No used pixels found for color extraction");
                return new List<Color>();
            }
            
            Debug.Log($"[ExtractDominantColorsFromUsedAreas] Extracted {usedPixels.Count} pixels from used UV areas for color analysis");
            
            // Apply k-means clustering to used pixels only
            return KMeansColorClustering(usedPixels.ToArray(), colorCount);
        }

        private static List<Color> KMeansColorClustering(Color[] pixels, int k)
        {
            if (pixels.Length == 0 || k <= 0)
                return new List<Color>();
            
            var random = new System.Random();
            var centroids = new List<Color>();
            
            // Initialize centroids randomly
            for (int i = 0; i < k; i++)
            {
                centroids.Add(pixels[random.Next(pixels.Length)]);
            }
            
            // Iterate until convergence
            for (int iteration = 0; iteration < 10; iteration++)
            {
                var clusters = new List<Color>[k];
                for (int i = 0; i < k; i++)
                {
                    clusters[i] = new List<Color>();
                }
                
                // Assign pixels to clusters
                foreach (var pixel in pixels)
                {
                    int nearestCentroid = 0;
                    float minDistance = ColorSpaceConverter.ColorDistanceRGB(pixel, centroids[0]);
                    
                    for (int i = 1; i < k; i++)
                    {
                        float distance = ColorSpaceConverter.ColorDistanceRGB(pixel, centroids[i]);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearestCentroid = i;
                        }
                    }
                    
                    clusters[nearestCentroid].Add(pixel);
                }
                
                // Update centroids
                for (int i = 0; i < k; i++)
                {
                    if (clusters[i].Count > 0)
                    {
                        float r = 0, g = 0, b = 0;
                        foreach (var color in clusters[i])
                        {
                            r += color.r;
                            g += color.g;
                            b += color.b;
                        }
                        
                        centroids[i] = new Color(
                            r / clusters[i].Count,
                            g / clusters[i].Count,
                            b / clusters[i].Count,
                            1.0f
                        );
                    }
                }
            }
            
            return centroids;
        }

        // Debug function to test coordinate conversion
        private static void TestCoordinateConversion(int width, int height)
        {
            // Test UV corners
            Vector2[] testUVs = {
                new Vector2(0.0f, 0.0f), // Bottom-left
                new Vector2(1.0f, 0.0f), // Bottom-right
                new Vector2(0.0f, 1.0f), // Top-left
                new Vector2(1.0f, 1.0f)  // Top-right
            };
            
            string[] names = { "Bottom-left", "Bottom-right", "Top-left", "Top-right" };
            
            for (int i = 0; i < testUVs.Length; i++)
            {
                var pixel = UVToPixelEnhanced(testUVs[i], width, height);
                int textureY = (height - 1) - pixel.y;
                int index = textureY * width + pixel.x;
                Debug.Log($"[CoordTest] {names[i]} UV({testUVs[i].x:F1},{testUVs[i].y:F1}) -> Pixel({pixel.x},{pixel.y}) -> Index({index})");
            }
        }

    }
}