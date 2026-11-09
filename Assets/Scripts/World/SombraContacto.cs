using UnityEngine;
using UnityEngine.Rendering;

namespace SlugOut.World
{
    /// Sombra de contacto barata: un quad multiplicativo sobre el terreno. No usa
    /// luces ni shadow maps; la GT 640 no soporta sombras en tiempo real.
    public class SombraContacto : MonoBehaviour
    {
        public Transform objetivo;
        public float radio = 0.4f;

        public static void Agregar(Transform objetivo, float radio)
        {
            if (objetivo == null) return;
            GameObject go = new GameObject("Sombra_Contacto");
            SombraContacto s = go.AddComponent<SombraContacto>();
            s.objetivo = objetivo;
            s.radio = radio;
        }

        private void Awake()
        {
            Material mat = MaterialSombra();
            if (mat == null)
            {
                Destroy(gameObject);
                return;
            }
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Disco_Sombra";
            Collider c = quad.GetComponent<Collider>();
            if (c != null) Destroy(c);
            quad.transform.SetParent(transform, false);
            quad.transform.localScale = new Vector3(radio * 2.2f, radio * 1.7f, 1f);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            MeshRenderer mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void LateUpdate()
        {
            if (objetivo == null)
            {
                Destroy(gameObject);
                return;
            }
            Vector3 p = objetivo.position;
            p.y = Terrain3D.Altura(p.x, p.z) + 0.08f;
            transform.position = p;
        }

        private static Material _mat;
        private static Texture2D _tex;

        private static Material MaterialSombra()
        {
            if (_mat != null) return _mat;
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Lit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) return null;
            Material m = new Material(s);
            Texture2D t = TexturaSombra();
            m.mainTexture = t;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            m.color = Color.white;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 3f);   // multiply
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.DstColor);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.renderQueue = 3000;
            _mat = m;
            return m;
        }

        private static Texture2D TexturaSombra()
        {
            if (_tex != null) return _tex;
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "sombra_contacto";
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            var px = new Color32[n * n];
            float c0 = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c0) / c0, dy = (y - c0) / c0;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f - Mathf.SmoothStep(0.2f, 1f, r);
                    byte g = (byte)Mathf.RoundToInt(255f * (1f - 0.45f * a));
                    px[y * n + x] = new Color32(g, g, g, 255);
                }
            }
            t.SetPixels32(px);
            t.Apply();
            _tex = t;
            return t;
        }
    }
}
