using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Relay.EditorTools
{
    /// <summary>
    /// Makes the bench's models and textures in code, so the project needs no downloaded assets. Round objects
    /// are lathed: a side profile spun around the vertical axis. Profiles run so the surface faces outward
    /// when travelling up the outside, in across a top, and down the inside.
    /// </summary>
    public static class RelayMeshes
    {
        const string MeshFolder = "Assets/RELAY/Generated/Meshes";
        const string TextureFolder = "Assets/RELAY/Generated/Textures";
        const int Segments = 48;
        const float SmoothAngle = 35f;

        // Saving a mesh again would break everything already using it, so each is made once per build.
        static readonly Dictionary<string, Mesh> Built = new Dictionary<string, Mesh>();

        public static void BeginBuild() => Built.Clear();

        public static Mesh Bottle() => Lathe("Bottle", new[]
        {
            P(0f, 0f), P(.030f, 0f), P(.035f, .004f), P(.035f, .125f), P(.0345f, .132f), P(.0325f, .142f),
            P(.029f, .151f), P(.024f, .159f), P(.019f, .165f), P(.0155f, .170f), P(.015f, .175f), P(.015f, .198f),
            P(.0165f, .199f), P(.0165f, .205f), P(.0125f, .205f), P(.0125f, .190f)
        });

        public static Mesh BottleLabel() => Lathe("BottleLabel", new[] { P(.0356f, .045f), P(.0356f, .115f) });

        // Ridges around the side make the twist visible, which a smooth cap would hide.
        public static Mesh Cap() => Lathe("Cap", new[]
        {
            P(0f, -.011f), P(.0165f, -.011f), P(.018f, -.0105f), P(.018f, .0095f), P(.0165f, .011f), P(0f, .011f)
        }, new[] { 2, 3 });

        // Open at the top, with an inner wall: the water inside shows through.
        public static Mesh Glass() => Lathe("Glass", new[]
        {
            P(0f, 0f), P(.036f, 0f), P(.0375f, .002f), P(.0385f, .115f), P(.0385f, .116f), P(.0372f, .116f),
            P(.0355f, .115f), P(.0350f, .006f), P(.034f, .005f), P(0f, .005f)
        });

        public static Mesh FillLine() => Lathe("FillLine", new[] { P(.0392f, -.0008f), P(.0392f, .0008f) });

        public static Mesh Tray() => Lathe("Tray", new[]
        {
            P(0f, 0f), P(.06f, 0f), P(.065f, .012f), P(.062f, .013f), P(.056f, .004f), P(0f, .004f)
        });

        public static Mesh SpotRing() => Lathe("SpotRing", new[] { P(.047f, .0005f), P(.038f, .0005f) });

        public static Mesh ButtonBase() => Lathe("ButtonBase", new[]
        {
            P(0f, 0f), P(.05f, 0f), P(.05f, .02f), P(0f, .02f)
        });

        public static Mesh ButtonTop() => Lathe("ButtonTop", new[]
        {
            P(0f, 0f), P(.035f, 0f), P(.035f, .015f), P(.03f, .02f), P(0f, .02f)
        });

        static Vector2 P(float radius, float height) => new Vector2(radius, height);

        static Mesh Lathe(string name, Vector2[] profile, int[] ridgedPoints = null)
        {
            if (Built.TryGetValue(name, out Mesh existing)) return existing;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int k = 0; k < profile.Length - 1; k++)
            {
                Vector2 startNormal = BlendedNormal(profile, k, k);
                Vector2 endNormal = BlendedNormal(profile, k, k + 1);
                int first = vertices.Count;

                for (int j = 0; j <= Segments; j++)
                {
                    float angle = 2f * Mathf.PI * j / Segments;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);
                    float u = (float)j / Segments;

                    vertices.Add(RingPoint(profile[k], k, j, cos, sin, ridgedPoints));
                    normals.Add(new Vector3(startNormal.x * cos, startNormal.y, startNormal.x * sin));
                    uvs.Add(new Vector2(u, (float)k / (profile.Length - 1)));

                    vertices.Add(RingPoint(profile[k + 1], k + 1, j, cos, sin, ridgedPoints));
                    normals.Add(new Vector3(endNormal.x * cos, endNormal.y, endNormal.x * sin));
                    uvs.Add(new Vector2(u, (float)(k + 1) / (profile.Length - 1)));
                }

                for (int j = 0; j < Segments; j++)
                {
                    int a = first + j * 2;
                    int d = a + 1;
                    int b = a + 2;
                    int c = a + 3;
                    triangles.AddRange(new[] { a, d, c, a, c, b });
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            if (ridgedPoints != null) mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            EnsureFolder(MeshFolder);
            string path = $"{MeshFolder}/{name}.asset";
            AssetDatabase.CreateAsset(mesh, path);
            Built[name] = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            return Built[name];
        }

        static Vector3 RingPoint(Vector2 point, int pointIndex, int segment, float cos, float sin, int[] ridgedPoints)
        {
            float radius = point.x;
            bool ridged = ridgedPoints != null && System.Array.IndexOf(ridgedPoints, pointIndex) >= 0;
            if (ridged && segment % 2 == 0) radius *= 1.05f;
            return new Vector3(radius * cos, point.y, radius * sin);
        }

        // Smooths across a gentle bend in the profile and keeps a sharp edge at a hard one.
        static Vector2 BlendedNormal(Vector2[] profile, int segment, int point)
        {
            Vector2 own = SegmentNormal(profile, segment);
            int other = point == segment ? segment - 1 : segment + 1;
            if (other < 0 || other >= profile.Length - 1) return own;

            Vector2 neighbour = SegmentNormal(profile, other);
            return Vector2.Angle(own, neighbour) < SmoothAngle ? (own + neighbour).normalized : own;
        }

        static Vector2 SegmentNormal(Vector2[] profile, int segment)
        {
            Vector2 direction = (profile[segment + 1] - profile[segment]).normalized;
            return new Vector2(direction.y, -direction.x);
        }

        /// <summary>Wood grain: long wavy bands with fine streaks.</summary>
        public static Texture2D WoodTexture()
        {
            return SaveTexture("Wood", 1024, 512, (x, y) =>
            {
                float wave = Mathf.PerlinNoise(x * 3f, y * 6f) * 2.2f;
                float bands = 0.5f + 0.5f * Mathf.Sin((y * 34f + wave) * Mathf.PI);
                float streak = Mathf.PerlinNoise(x * 6f, y * 180f);
                float shade = 0.8f + 0.06f * bands + 0.1f * (streak - 0.5f);
                return new Color(0.66f * shade, 0.47f * shade, 0.31f * shade);
            });
        }

        /// <summary>Plain floor tiles with thin grout lines, deliberately low in contrast.</summary>
        public static Texture2D TileTexture()
        {
            return SaveTexture("Tile", 512, 512, (x, y) =>
            {
                bool grout = x < 0.008f || x > 0.992f || y < 0.008f || y > 0.992f;
                if (grout) return new Color(0.48f, 0.49f, 0.51f);
                float speckle = (Mathf.PerlinNoise(x * 60f, y * 60f) - 0.5f) * 0.03f;
                return new Color(0.56f + speckle, 0.57f + speckle, 0.59f + speckle);
            });
        }

        delegate Color Shade(float x, float y);

        static Texture2D SaveTexture(string name, int width, int height, Shade shade)
        {
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) pixels[y * width + x] = shade((float)x / width, (float)y / height);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGB24, true);
            texture.SetPixels(pixels);
            texture.Apply();

            EnsureFolder(TextureFolder);
            string path = $"{TextureFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
