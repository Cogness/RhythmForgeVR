using UnityEngine;
using UnityEngine.Rendering;

namespace RhythmForge.Bootstrap
{
    /// <summary>
    /// Builds the "Immersed" background at runtime: a few very low-opacity nebula
    /// patches + sparse star dots + a faint horizon ring. Placed far from the user
    /// (~30-50 m) so it never competes with shape strokes drawn within arm's reach.
    ///
    /// All geometry is unlit, shadow-free, and uses a deterministic random seed so
    /// layout is stable across sessions.
    /// </summary>
    public static class ImmersiveEnvironmentFactory
    {
        private const int DeterministicSeed = 1337;

        private const int NebulaCount = 5;
        private const float NebulaDistance = 48f;
        private const float NebulaSize = 22f;

        private const int StarCount = 160;
        private const float StarDistance = 45f;
        private const float StarSizeMin = 0.18f;
        private const float StarSizeMax = 0.42f;

        private const float HorizonRadius = 30f;
        private const int HorizonSegments = 96;

        // Desaturated deep-space palette for nebula tints (pre-multiplied by low alpha in the shader tint).
        private static readonly Color[] NebulaTints = new[]
        {
            new Color(0.36f, 0.20f, 0.52f, 0.12f), // dim violet
            new Color(0.14f, 0.28f, 0.42f, 0.10f), // deep teal
            new Color(0.44f, 0.22f, 0.36f, 0.09f), // mauve
            new Color(0.18f, 0.22f, 0.44f, 0.11f), // indigo
            new Color(0.24f, 0.30f, 0.36f, 0.08f), // slate blue
        };

        private static Texture2D _sharedNebulaTex;
        private static Texture2D _sharedStarTex;
        private static Shader _transparentShader;

        /// <summary>
        /// Builds and returns the environment root. Parented to <paramref name="parent"/>.
        /// </summary>
        public static GameObject Build(Transform parent)
        {
            var root = new GameObject("ImmersiveEnvironment");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            // Keep a single deterministic stream so layouts match between sessions.
            var prevState = Random.state;
            Random.InitState(DeterministicSeed);
            try
            {
                BuildNebulae(root.transform);
                BuildStars(root.transform);
                BuildHorizonRing(root.transform);
            }
            finally
            {
                Random.state = prevState;
            }

            return root;
        }

        // ───────────────────────────── NEBULAE ─────────────────────────────

        private static void BuildNebulae(Transform parent)
        {
            var tex = GetNebulaTexture();
            for (int i = 0; i < NebulaCount; i++)
            {
                var go = CreateBillboardQuad($"Nebula_{i}", parent);
                var tint = NebulaTints[i % NebulaTints.Length];
                var mat = new Material(GetTransparentShader())
                {
                    mainTexture = tex,
                    color = tint,
                    renderQueue = (int)RenderQueue.Transparent - 100, // render before strokes
                };
                // Sprites/Default renders regardless, but ensure proper blend even on URP.
                mat.SetInt("_ZWrite", 0);

                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.allowOcclusionWhenDynamic = false;

                // Random azimuth, mild vertical variance, fixed distance.
                float azimuth = Random.Range(0f, Mathf.PI * 2f);
                float elevation = Random.Range(-0.18f, 0.35f); // slight upward bias
                float dist = NebulaDistance * Random.Range(0.9f, 1.15f);
                Vector3 dir = new Vector3(Mathf.Cos(azimuth), elevation, Mathf.Sin(azimuth)).normalized;
                go.transform.localPosition = dir * dist;
                go.transform.localRotation = Quaternion.LookRotation(dir); // face user (origin)

                float scale = NebulaSize * Random.Range(0.7f, 1.4f);
                go.transform.localScale = new Vector3(scale, scale * Random.Range(0.6f, 1.1f), 1f);
            }
        }

        // ────────────────────────────── STARS ──────────────────────────────

        private static void BuildStars(Transform parent)
        {
            var starsRoot = new GameObject("Stars");
            starsRoot.transform.SetParent(parent, false);

            var tex = GetStarTexture();
            var mat = new Material(GetTransparentShader())
            {
                mainTexture = tex,
                color = new Color(1f, 1f, 1f, 0.55f),
                renderQueue = (int)RenderQueue.Transparent - 50,
            };
            mat.SetInt("_ZWrite", 0);

            for (int i = 0; i < StarCount; i++)
            {
                var go = CreateBillboardQuad($"Star_{i}", starsRoot.transform);
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.allowOcclusionWhenDynamic = false;

                Vector3 dir = Random.onUnitSphere;
                // Push slightly upward distribution so floor isn't starry (horizon ring handles that).
                if (dir.y < -0.3f) dir.y = -dir.y * 0.4f;
                dir.Normalize();

                float dist = StarDistance * Random.Range(0.9f, 1.15f);
                go.transform.localPosition = dir * dist;
                go.transform.localRotation = Quaternion.LookRotation(dir);

                float size = Random.Range(StarSizeMin, StarSizeMax);
                go.transform.localScale = new Vector3(size, size, 1f);
            }
        }

        // ─────────────────────────── HORIZON RING ──────────────────────────

        private static void BuildHorizonRing(Transform parent)
        {
            var go = new GameObject("HorizonRing");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = HorizonSegments;
            lr.widthMultiplier = 0.05f; // very thin
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.allowOcclusionWhenDynamic = false;

            var mat = new Material(GetTransparentShader())
            {
                color = new Color(0.55f, 0.70f, 0.90f, 0.06f),
                renderQueue = (int)RenderQueue.Transparent - 30,
            };
            mat.SetInt("_ZWrite", 0);
            lr.sharedMaterial = mat;

            for (int i = 0; i < HorizonSegments; i++)
            {
                float t = (i / (float)HorizonSegments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(t) * HorizonRadius, -0.6f, Mathf.Sin(t) * HorizonRadius));
            }

            var color = new Color(0.55f, 0.70f, 0.90f, 0.06f);
            lr.startColor = color;
            lr.endColor = color;
        }

        // ──────────────────────────── UTILITIES ────────────────────────────

        private static GameObject CreateBillboardQuad(string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);

            // Strip the auto-added collider — environment is purely visual.
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            return go;
        }

        private static Shader GetTransparentShader()
        {
            if (_transparentShader != null) return _transparentShader;
            // Sprites/Default is a reliable, always-transparent, double-sided unlit shader
            // that exists in both built-in and URP pipelines (via URP's built-in fallback).
            _transparentShader = Shader.Find("Sprites/Default");
            if (_transparentShader == null)
                _transparentShader = Shader.Find("Unlit/Transparent");
            return _transparentShader;
        }

        private static Texture2D GetNebulaTexture()
        {
            if (_sharedNebulaTex != null) return _sharedNebulaTex;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: false)
            {
                name = "NebulaGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
            };

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxR = center;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxR;
                    float dy = (y - center) / maxR;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Smooth Gaussian-ish falloff: bright core, very soft edges.
                    float a = Mathf.Exp(-r * r * 3.2f);
                    // Subtle radial noise to break perfect symmetry without being obvious.
                    float n = 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.07f, y * 0.07f);
                    a *= n;
                    a = Mathf.Clamp01(a);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            _sharedNebulaTex = tex;
            return tex;
        }

        private static Texture2D GetStarTexture()
        {
            if (_sharedStarTex != null) return _sharedStarTex;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: false)
            {
                name = "StarDot",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
            };

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxR = center;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxR;
                    float dy = (y - center) / maxR;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Tight core + soft halo.
                    float core = Mathf.Exp(-r * r * 14f);
                    float halo = Mathf.Exp(-r * r * 2.5f) * 0.25f;
                    float a = Mathf.Clamp01(core + halo);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            _sharedStarTex = tex;
            return tex;
        }
    }
}
