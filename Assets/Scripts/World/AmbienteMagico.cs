using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlugOut.World
{
    /// <summary>
    /// Ambientación mágica por región: motas de polvo/ceniza que caen con deriva, brillos
    /// flotantes (luciérnagas, esporas, brasas) y volutas de niebla rastrera que se desplazan.
    /// Todo procedural, sin assets: una textura radial generada por código y tres meshes
    /// cuyos vértices se reescriben en LateUpdate con billboard manual (barato para GT 640:
    /// ~500 vértices en 3 draw calls). Además pulsa las PointLights de los microhábitats.
    ///
    /// Vive en el GameObject del terreno (no bajo Region_*) para sobrevivir a los viajes
    /// entre cuevas: en cada Aplicar solo reconfigura colores y reinicia partículas.
    /// </summary>
    public class AmbienteMagico : MonoBehaviour
    {
        public enum TipoAmbiente { PolvoCalido, EsporasEco, PolenVerde, Ceniza, Petalos }

        [Header("Cantidades (GT 640)")]
        [SerializeField] private int cantidadMotas = 90;
        [SerializeField] private int cantidadBrillos = 64;
        [SerializeField] private int cantidadVolutas = 7;
        [SerializeField] private float radioAmbiente = 22f;

        // Estado por capa de partículas: cada partícula es un quad (4 vértices).
        private struct Particula
        {
            public Vector3 pos;
            public Vector3 vel;
            public float fase;
            public float tam;
            public float vida;      // para brillos: tiempo restante antes de reaparecer
            public float baseY;     // altura base sobre el terreno (brillos)
        }

        private Particula[] _motas;
        private Particula[] _brillos;
        private Particula[] _volutas;

        private Mesh _meshMotas, _meshBrillos, _meshVolutas;
        private MeshFilter _filtroMotas, _filtroBrillos, _filtroVolutas;
        private Material _matMotas, _matBrillos, _matNiebla;
        private static Texture2D _texRadial;

        private TipoAmbiente _tipo;
        private Color _colorMota;
        private Color _colorBrillo;
        private Color _colorNiebla;
        private float _caidaMotas;      // velocidad de caída base de las motas
        private float _turbulencia;      // amplitud de la deriva
        private float _brilloMinY, _brilloMaxY;
        private float _t;

        // PointLights de los hábitats para el pulso (se recolectan en cada Aplicar).
        private struct LuzPulso { public Light luz; public float baseI; public float fase; }
        private readonly List<LuzPulso> _luces = new List<LuzPulso>();

        public TipoAmbiente Tipo { get { return _tipo; } }
        public int MotasActivas { get { return _motas != null ? _motas.Length : 0; } }
        public int BrillosActivos { get { return _brillos != null ? _brillos.Length : 0; } }

        private void Awake()
        {
            Inicializar();
        }

        /// Inicialización perezosa: también la usa Aplicar para que un test en EditMode
        /// pueda hacer AddComponent + Aplicar sin depender de que Unity llame Awake.
        private void Inicializar()
        {
            if (_meshMotas != null) return;
            if (_texRadial == null) _texRadial = TexturaRadial();
            _matMotas = MaterialMota();
            _matBrillos = MaterialBrillo();
            _matNiebla = MaterialNiebla();

            GameObject root = new GameObject("AmbienteMagico");
            root.transform.SetParent(transform, false);

            _meshMotas = CrearMesh("amb_motas", cantidadMotas, out _filtroMotas, root.transform, _matMotas);
            _meshBrillos = CrearMesh("amb_brillos", cantidadBrillos, out _filtroBrillos, root.transform, _matBrillos);
            _meshVolutas = CrearMesh("amb_volutas", cantidadVolutas, out _filtroVolutas, root.transform, _matNiebla);

            _motas = new Particula[cantidadMotas];
            _brillos = new Particula[cantidadBrillos];
            _volutas = new Particula[cantidadVolutas];
        }

        /// <summary>Configura el tipo y los colores según la región y reinicia las partículas.</summary>
        public void Aplicar(RegionDefinicion d, Transform rootRegion, int indiceRegion = -1)
        {
            if (d == null) return;
            Inicializar();
            if (_meshMotas == null) return;
            int indice = indiceRegion >= 0 ? indiceRegion : RegionCuevaSeca.Actual;
            switch (indice)
            {
                case 1: _tipo = TipoAmbiente.EsporasEco; break;
                case 2: _tipo = TipoAmbiente.PolenVerde; break;
                case 3: _tipo = TipoAmbiente.Ceniza; break;
                case 4: _tipo = TipoAmbiente.Petalos; break;
                default: _tipo = TipoAmbiente.PolvoCalido; break;
            }

            _colorMota = Color.Lerp(d.ambiente, d.niebla, 0.5f) * 1.4f;
            _colorBrillo = d.emisionAcento;
            _colorNiebla = d.niebla;

            switch (_tipo)
            {
                case TipoAmbiente.PolvoCalido:
                    _caidaMotas = 0.22f; _turbulencia = 0.35f; _brilloMinY = 0.3f; _brilloMaxY = 1.5f;
                    _colorBrillo = d.emisionAcento;
                    break;
                case TipoAmbiente.EsporasEco:
                    _caidaMotas = -0.10f; _turbulencia = 0.25f; _brilloMinY = 0.4f; _brilloMaxY = 2.2f;
                    _colorBrillo = Color.Lerp(d.luzHabitat3, d.emisionCristal, 0.5f) * 1.6f;
                    break;
                case TipoAmbiente.PolenVerde:
                    _caidaMotas = 0.12f; _turbulencia = 0.45f; _brilloMinY = 0.2f; _brilloMaxY = 1.8f;
                    _colorBrillo = d.emisionCristal * 1.5f;
                    break;
                case TipoAmbiente.Ceniza:
                    _caidaMotas = 0.55f; _turbulencia = 0.8f; _brilloMinY = 0.8f; _brilloMaxY = 3.2f;
                    _colorMota = Color.Lerp(Color.gray, d.emisionAcento, 0.35f);
                    _colorBrillo = d.emisionAcento * 2.2f;
                    break;
                case TipoAmbiente.Petalos:
                    // Templo Olvidado: petalos de cerezo rosa cayendo en espiral lenta,
                    // brillos calidos entre dorado y rosa (linternas lejanas).
                    _caidaMotas = 0.09f; _turbulencia = 0.6f; _brilloMinY = 0.3f; _brilloMaxY = 2.6f;
                    _colorMota = new Color(1f, 0.75f, 0.85f) * 1.25f;
                    _colorBrillo = Color.Lerp(new Color(1f, 0.60f, 0.70f), d.emisionCristal, 0.5f) * 1.8f;
                    break;
            }

            // Materiales toman el color de la región.
            if (_matMotas != null)
            {
                _matMotas.SetColor("_BaseColor", _colorMota);
                _matMotas.SetColor("_Color", _colorMota);
            }
            if (_matBrillos != null)
            {
                _matBrillos.SetColor("_BaseColor", _colorBrillo);
                _matBrillos.SetColor("_EmissionColor", _colorBrillo * 3.2f);
                _matBrillos.EnableKeyword("_EMISSION");
            }
            if (_matNiebla != null)
            {
                Color nc = _colorNiebla * 0.6f;
                nc.a = 0.10f;
                _matNiebla.SetColor("_BaseColor", nc);
                _matNiebla.SetColor("_Color", nc);
            }

            ReiniciarMotas();
            ReiniciarBrillos();
            ReiniciarVolutas();
            RecolectarLuces(rootRegion);
            Debug.Log("[Ambiente] " + d.nombre + " tipo=" + _tipo
                + " motas=" + _motas.Length + " brillos=" + _brillos.Length);
        }

        private void ReiniciarMotas()
        {
            var rnd = new System.Random(1234 + (int)_tipo);
            for (int i = 0; i < _motas.Length; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rad = Mathf.Sqrt((float)rnd.NextDouble()) * radioAmbiente;
                float x = Mathf.Cos(ang) * rad, z = Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z) + 0.5f + (float)rnd.NextDouble() * 5.5f;
                _motas[i] = new Particula
                {
                    pos = new Vector3(x, y, z),
                    vel = Vector3.zero,
                    fase = (float)rnd.NextDouble() * Mathf.PI * 2f,
                    tam = 0.06f + (float)rnd.NextDouble() * 0.12f,
                    vida = 0f,
                    baseY = 0f
                };
            }
        }

        private void ReiniciarBrillos()
        {
            var rnd = new System.Random(777 + (int)_tipo);
            for (int i = 0; i < _brillos.Length; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rad = 3f + Mathf.Sqrt((float)rnd.NextDouble()) * (radioAmbiente - 4f);
                float x = Mathf.Cos(ang) * rad, z = Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z) + _brilloMinY
                    + (float)rnd.NextDouble() * (_brilloMaxY - _brilloMinY);
                _brillos[i] = new Particula
                {
                    pos = new Vector3(x, y, z),
                    vel = Vector3.zero,
                    fase = (float)rnd.NextDouble() * Mathf.PI * 2f,
                    tam = 0.16f + (float)rnd.NextDouble() * 0.22f,
                    vida = 4f + (float)rnd.NextDouble() * 8f,
                    baseY = y
                };
            }
        }

        private void ReiniciarVolutas()
        {
            var rnd = new System.Random(4321 + (int)_tipo);
            for (int i = 0; i < _volutas.Length; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rad = 6f + (float)rnd.NextDouble() * (radioAmbiente - 8f);
                float x = Mathf.Cos(ang) * rad, z = Mathf.Sin(ang) * rad;
                _volutas[i] = new Particula
                {
                    pos = new Vector3(x, Terrain3D.Altura(x, z) + 0.6f + (float)rnd.NextDouble() * 0.9f, z),
                    vel = Vector3.zero,
                    fase = (float)rnd.NextDouble() * Mathf.PI * 2f,
                    tam = 5f + (float)rnd.NextDouble() * 7f,
                    vida = 0f,
                    baseY = 0f
                };
            }
        }

        private void RecolectarLuces(Transform rootRegion)
        {
            _luces.Clear();
            if (rootRegion == null) return;
            var rnd = new System.Random(99);
            foreach (Light l in rootRegion.GetComponentsInChildren<Light>(true))
            {
                if (l == null || l.type != LightType.Point) continue;
                _luces.Add(new LuzPulso { luz = l, baseI = l.intensity, fase = (float)rnd.NextDouble() * Mathf.PI * 2f });
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            MoverMotas(dt);
            MoverBrillos(dt);
            MoverVolutas(dt);
            PulsarLuces();
        }

        private void LateUpdate()
        {
            if (_filtroMotas == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            EscribirBillboard(_meshMotas, _motas, cam, false);
            EscribirBillboard(_meshBrillos, _brillos, cam, false);
            EscribirBillboard(_meshVolutas, _volutas, cam, true); // cilíndrico: solo gira en Y
        }

        private void MoverMotas(float dt)
        {
            for (int i = 0; i < _motas.Length; i++)
            {
                Particula p = _motas[i];
                float s = _t * 0.7f + p.fase;
                // Deriva lateral sinus + caída según tipo (negativa = suben, como esporas).
                p.vel.x = Mathf.Sin(s * 1.3f) * _turbulencia;
                p.vel.z = Mathf.Cos(s * 0.9f) * _turbulencia;
                p.vel.y = -_caidaMotas + Mathf.Sin(s * 2.1f) * 0.08f;
                p.pos += p.vel * dt;
                float suelo = Terrain3D.Altura(p.pos.x, p.pos.z);
                // Respawn: si cayó bajo el piso (o subió muy alto en eco) o salió del radio.
                if (p.pos.y < suelo + 0.15f || p.pos.y > suelo + 7f
                    || p.pos.magnitude > radioAmbiente + 3f)
                {
                    float ang = p.fase * 3.1f;
                    float rad = Mathf.Sqrt((i + 0.5f) / _motas.Length) * radioAmbiente;
                    float nx = Mathf.Cos(ang) * rad, nz = Mathf.Sin(ang) * rad;
                    p.pos = new Vector3(nx, Terrain3D.Altura(nx, nz) + 3f + (i % 5) * 0.8f, nz);
                }
                // Las motas se hacen más chicas al bajar (perspectiva suave).
                p.tam = Mathf.Lerp(p.tam, 0.05f, dt * 0.2f);
                _motas[i] = p;
            }
        }

        private void MoverBrillos(float dt)
        {
            for (int i = 0; i < _brillos.Length; i++)
            {
                Particula p = _brillos[i];
                p.vida -= dt;
                float s = _t + p.fase;
                switch (_tipo)
                {
                    case TipoAmbiente.EsporasEco:
                        // Ondas de eco: suben y bajan en vertical con deriva circular.
                        p.pos.y = p.baseY + Mathf.Sin(s * 1.8f) * 0.45f;
                        p.pos.x += Mathf.Cos(s * 0.6f) * 0.12f * dt * 10f;
                        p.pos.z += Mathf.Sin(s * 0.6f) * 0.12f * dt * 10f;
                        break;
                    case TipoAmbiente.PolenVerde:
                        // Deriva circular lenta alrededor de su punto base.
                        p.pos.x += Mathf.Cos(s * 0.5f) * 0.25f * dt;
                        p.pos.z += Mathf.Sin(s * 0.5f) * 0.25f * dt;
                        p.pos.y = p.baseY + Mathf.Sin(s * 1.2f) * 0.25f;
                        break;
                    case TipoAmbiente.Ceniza:
                        // Brasas que suben con turbulencia y tiemblan.
                        p.pos.y += (0.25f + Mathf.Sin(s * 3f) * 0.15f) * dt;
                        p.pos.x += Mathf.Sin(s * 2.2f) * 0.4f * dt;
                        p.pos.z += Mathf.Cos(s * 1.7f) * 0.4f * dt;
                        break;
                    case TipoAmbiente.Petalos:
                        // Luciernagas del templo: espiral descendente lenta y tenue.
                        p.pos.x += Mathf.Cos(s * 1.1f) * 0.28f * dt;
                        p.pos.z += Mathf.Sin(s * 1.1f) * 0.28f * dt;
                        p.pos.y = p.baseY + Mathf.Sin(s * 0.9f) * 0.5f - (s * 0.05f % 1.2f);
                        break;
                    default: // PolvoCalido: flotación errática lenta cerca del suelo
                        p.pos.x += Mathf.Sin(s * 0.8f) * 0.3f * dt;
                        p.pos.z += Mathf.Cos(s * 0.6f) * 0.3f * dt;
                        p.pos.y = p.baseY + Mathf.Sin(s * 1.1f) * 0.3f;
                        break;
                }
                // Pulso de tamaño: el brillo late.
                p.tam = (0.16f + 0.14f * (0.5f + 0.5f * Mathf.Sin(s * 2.4f)));
                if (p.vida <= 0f)
                {
                    // Reaparece en otro punto aleatorio.
                    float ang = p.fase * 5.7f + _t;
                    float rad = 4f + ((i * 7 + 3) % 17) / 17f * (radioAmbiente - 5f);
                    float nx = Mathf.Cos(ang) * rad, nz = Mathf.Sin(ang) * rad;
                    p.pos = new Vector3(nx, Terrain3D.Altura(nx, nz) + _brilloMinY
                        + ((i * 5) % 10) / 10f * (_brilloMaxY - _brilloMinY), nz);
                    p.baseY = p.pos.y;
                    p.vida = 5f + (i % 7) * 1.3f;
                    p.fase = _t + i;
                }
                float suelo = Terrain3D.Altura(p.pos.x, p.pos.z);
                if (p.pos.y < suelo + 0.1f) p.pos.y = suelo + 0.1f;
                _brillos[i] = p;
            }
        }

        private void MoverVolutas(float dt)
        {
            for (int i = 0; i < _volutas.Length; i++)
            {
                Particula p = _volutas[i];
                float s = _t * 0.12f + p.fase;
                // Deriva circular lenta alrededor del centro.
                float ang = p.fase + _t * 0.05f;
                float rad = 8f + (i % 4) * 3.5f;
                float nx = Mathf.Cos(ang) * rad;
                float nz = Mathf.Sin(ang) * rad;
                p.pos.x = Mathf.Lerp(p.pos.x, nx, dt * 0.3f);
                p.pos.z = Mathf.Lerp(p.pos.z, nz, dt * 0.3f);
                p.pos.y = Terrain3D.Altura(p.pos.x, p.pos.z) + 0.5f + Mathf.Sin(s) * 0.25f;
                // La voluta rota lentamente sobre sí misma (tamaño fijo, deriva de fase).
                _volutas[i] = p;
            }
        }

        private void PulsarLuces()
        {
            for (int i = 0; i < _luces.Count; i++)
            {
                LuzPulso lp = _luces[i];
                if (lp.luz == null) continue;
                lp.luz.intensity = lp.baseI * (0.82f + 0.18f * Mathf.Sin(_t * 0.9f + lp.fase));
            }
        }

        /// <summary>Reescribe los vértices de un mesh de quads con billboard a cámara.</summary>
        private static void EscribirBillboard(Mesh mesh, Particula[] ps, Camera cam, bool cilindrico)
        {
            if (mesh == null || ps == null) return;
            Vector3[] verts = mesh.vertices;
            if (verts.Length < ps.Length * 4) return;
            Vector3 camPos = cam.transform.position;
            Vector3 derecha, arriba;
            for (int i = 0; i < ps.Length; i++)
            {
                Particula p = ps[i];
                float h = p.tam * 0.5f;
                if (cilindrico)
                {
                    // Billboard cilíndrico: solo rota alrededor de Y hacia la cámara.
                    Vector3 dir = camPos - p.pos;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
                    dir.Normalize();
                    derecha = new Vector3(-dir.z, 0f, dir.x) * h;
                    arriba = Vector3.up * h * 0.45f; // achatada: niebla rastrera
                }
                else
                {
                    derecha = cam.transform.right * h;
                    arriba = cam.transform.up * h;
                }
                int b = i * 4;
                verts[b] = p.pos - derecha - arriba;
                verts[b + 1] = p.pos + derecha - arriba;
                verts[b + 2] = p.pos + derecha + arriba;
                verts[b + 3] = p.pos - derecha + arriba;
            }
            mesh.vertices = verts;
            mesh.RecalculateBounds();
        }

        private Mesh CrearMesh(string nombre, int cantidad, out MeshFilter filtro, Transform padre, Material mat)
        {
            var verts = new Vector3[cantidad * 4];
            var uvs = new Vector2[cantidad * 4];
            var tris = new int[cantidad * 6];
            for (int i = 0; i < cantidad; i++)
            {
                int b = i * 4;
                verts[b] = Vector3.zero; verts[b + 1] = Vector3.zero;
                verts[b + 2] = Vector3.zero; verts[b + 3] = Vector3.zero;
                uvs[b] = new Vector2(0f, 0f); uvs[b + 1] = new Vector2(1f, 0f);
                uvs[b + 2] = new Vector2(1f, 1f); uvs[b + 3] = new Vector2(0f, 1f);
                int t = i * 6;
                tris[t] = b; tris[t + 1] = b + 1; tris[t + 2] = b + 2;
                tris[t + 3] = b; tris[t + 4] = b + 2; tris[t + 5] = b + 3;
            }
            Mesh m = new Mesh { name = nombre };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            filtro = go.AddComponent<MeshFilter>();
            filtro.sharedMesh = m;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return m;
        }

        // ---------------------------------------------------------------- texturas y materiales

        /// Textura radial suave 64x64: alpha = exp(-d²), blanca. Sirve de "mota" blanda.
        private static Texture2D TexturaRadial()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "amb_radial",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x / (float)(n - 1)) * 2f - 1f;
                    float v = (y / (float)(n - 1)) * 2f - 1f;
                    float d2 = u * u + v * v;
                    float a = Mathf.Exp(-d2 * 2.6f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        private static Material MaterialBase(string nombre, bool additive)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            var m = new Material(s) { name = nombre, renderQueue = 3000 };
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (additive)
            {
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f); // Add
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
            }
            else
            {
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f); // Alpha
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            if (_texRadial != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", _texRadial);
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", _texRadial);
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            return m;
        }

        private Material MaterialMota()
        {
            Material m = MaterialBase("amb_motas_mat", false);
            Color c = new Color(1f, 1f, 1f, 0.22f);
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            return m;
        }

        private Material MaterialBrillo()
        {
            Material m = MaterialBase("amb_brillos_mat", true);
            m.EnableKeyword("_EMISSION");
            return m;
        }

        private Material MaterialNiebla()
        {
            Material m = MaterialBase("amb_niebla_mat", false);
            Color c = new Color(1f, 1f, 1f, 0.07f);
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            return m;
        }
    }
}
