using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlugOut.World
{
    /// Hongos y cristales emisivos alrededor del inicio, para que la tercera persona
    /// tenga la cueva luminosa de las referencias sin tocar el terreno ni las regiones.
    /// Sin sombras ni colliders: la GT 640 no soporta sombras en tiempo real.
    public class LookCuevaPixar : MonoBehaviour
    {
        [SerializeField] private int racimos = 10;
        [SerializeField] private float radioBase = 4.2f;

        private Material _tallo, _cap, _gills, _cristal, _cristalV, _llama, _madera, _agua, _roca, _rocaClara, _luzCalida,
            _follaje, _follajeClaro, _follajeCalido, _techo, _tierra, _espuma, _cielo, _cieloAditivo, _charcoCalido, _losa;
        private LoteMalla _lotesTallo, _lotesCap, _lotesGills;
        private Transform _padreHongos;
        private LoteMalla _lotesFarolMadera, _lotesFarolLuz, _lotesCharcoLuz;
        private Transform _padreFaroles;
        private Material _techoEsfera;
        private Light _relleno;
        private Transform _gShell, _gAgua, _gFollaje, _gCristales, _gHongos, _gLuces, _gEco, _gVena, _gCanon;
        private Transform[] _grupos;
        private string[] _nombresGrupos;
        private bool _construido;
        private int _regionAplicada = -1;

        public bool Construido { get { return _construido; } }
        public int RegionAplicada { get { return _regionAplicada; } }

        /// Espera un cuadro: Terrain3D reconstruye su mesh y sus materiales en Start. Se
        /// construye UNA vez y despues solo se alternan grupos y se recolorean materiales:
        /// destruir y recrear mallas por cada tunel filtraria meshes y seria mas caro.
        private IEnumerator Start()
        {
            yield return null;
            Vector3 centro = Vector3.zero;
            CrearMateriales();
            TechoDeCueva(centro);
            CrearRelleno();
#if SLUGOUT_URP
            ConfigurarPostProceso();
#endif

            GameObject root = new GameObject("CuevaPixar");
            _gShell = Grupo(root.transform, "Grupo_Shell");
            _gAgua = Grupo(root.transform, "Grupo_Agua");
            _gFollaje = Grupo(root.transform, "Grupo_Follaje");
            _gCristales = Grupo(root.transform, "Grupo_Cristales");
            _gHongos = Grupo(root.transform, "Grupo_Hongos");
            _gLuces = Grupo(root.transform, "Grupo_Luces");
            _gEco = Grupo(root.transform, "Grupo_Eco");
            _gVena = Grupo(root.transform, "Grupo_Vena");
            _gCanon = Grupo(root.transform, "Grupo_Canon");
            _grupos = new[] { _gShell, _gAgua, _gFollaje, _gCristales, _gHongos,
                _gLuces, _gEco, _gVena, _gCanon };
            _nombresGrupos = new[] { "Shell", "Agua", "Follaje", "Cristales", "Hongos",
                "Luces", "Eco", "Vena", "Canon" };
            _padreHongos = null;
            _padreFaroles = null;

            MantoTecho(_gShell, centro);
            CieloFondo(_gShell, centro);
            PilaresCueva(_gShell, centro);
            RocasSuelo(_gShell, centro);
            DetallesSuelo(_gCristales, centro);
            Arroyo(_gAgua, centro, _gLuces);
            ArcoRoca(_gShell, centro);
            RefugioFondo(_gShell, centro);
            Losas(_gShell, centro);
            Sendero(_gShell, centro);
            Charcos(_gAgua, centro);
            Follaje(_gFollaje, centro);
            CornisaCascada(_gAgua, centro);
            Cascada(_gAgua, centro + new Vector3(-8f, 0f, 16f), 4.4f, 3.0f);
            Puente(_gAgua, centro + new Vector3(-7f, 0f, 8f));
            PuenteCorto(_gAgua, centro + new Vector3(0f, 0f, 12.5f));
            Farol(_gLuces, centro + new Vector3(-8.2f, 0f, 8f));
            Farol(_gLuces, centro + new Vector3(-5.8f, 0f, 8f));
            // Faroles extra en la ribera y el camino al refugio: son la luz calida que
            // contrasta con el cian de los hongos, como en la referencia.
            Farol(_gLuces, centro + new Vector3(-11f, 0f, 7f));
            Farol(_gLuces, centro + new Vector3(11f, 0f, 7.5f));
            Farol(_gLuces, centro + new Vector3(-3f, 0f, 10.5f));
            Farol(_gLuces, centro + new Vector3(4f, 0f, 10.5f));
            Farol(_gLuces, centro + new Vector3(-9.5f, 0f, 17.5f));
            Farol(_gLuces, centro + new Vector3(9.5f, 0f, 17f));
            Farol(_gLuces, centro + new Vector3(-9f, 0f, 1.5f));
            Farol(_gLuces, centro + new Vector3(9f, 0f, 2f));
            CartelInicio(_gLuces, centro);
            Hongo(_gHongos, EnPiso(centro + new Vector3(-6f, 0f, 4.5f), 0f), 4.5f);
            Hongo(_gHongos, EnPiso(centro + new Vector3(6.5f, 0f, 5.5f), 0f), 5.2f);
            // Hongos chicos alrededor de la plaza: llenan el piso inmediato sin tapar la vista.
            for (int i = 0; i < 7; i++)
            {
                float ang = (i * 47f + 25f) * Mathf.Deg2Rad;
                float rad = 3.6f + (i % 3) * 1.3f;
                Vector3 p = EnPiso(centro + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad), 0f);
                Hongo(_gHongos, p, 0.7f + (i % 3) * 0.24f);
            }
            Cristal(_gCristales, EnPiso(centro + new Vector3(9f, 0f, 9f), 0f), 0.8f, 4.2f, _cristal);
            Cristal(_gCristales, EnPiso(centro + new Vector3(-8f, 0f, 9f), 0f), 0.7f, 3.6f, _cristalV);
            for (int i = 0; i < racimos; i++)
            {
                float ang = (i * (360f / racimos) + 12f) * Mathf.Deg2Rad;
                float rad = radioBase + (i % 3) * 1.6f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z);
                if (i % 2 == 0)
                {
                    Hongo(_gHongos, new Vector3(x, y, z), 1.8f + 0.35f * (i % 3));
                    Hongo(_gHongos, new Vector3(x + 0.8f, y, z + 0.5f), 1.1f);
                }
                else
                {
                    Cristal(_gCristales, new Vector3(x, y, z), 0.45f, 1.6f, _cristal);
                    Cristal(_gCristales, new Vector3(x + 0.55f, y, z - 0.3f), 0.28f, 1f, _cristalV);
                    Cristal(_gCristales, new Vector3(x - 0.45f, y, z + 0.45f), 0.2f, 0.7f, _cristal);
                }
            }
            // Hongos chicos escoltando el sendero hacia el puente.
            for (int i = 0; i < 8; i++)
            {
                float t = 0.12f + i * 0.1f;
                float offset = (i % 2 == 0 ? -1.7f : 1.7f);
                Vector3 p = new Vector3(
                    centro.x + Mathf.Sin(t * 2.3f) * 1.6f + offset, 0f,
                    centro.z - 2.5f + t * 14.2f);
                Hongo(_gHongos, EnPiso(p, 0f), 0.7f + (i % 3) * 0.2f);
            }
            CrearHongos();
            CrearFaroles();
            FirmaEco(_gEco, centro);
            FirmaVena(_gVena, centro);
            FirmaCanon(_gCanon, centro);

            _construido = true;
            Aplicar(Regiones.De(RegionCuevaSeca.Actual));
        }

        private static Transform Grupo(Transform padre, string nombre)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            return go.transform;
        }

        private void CrearRelleno()
        {
            GameObject fillGo = new GameObject("Luz_Cueva_Fill");
            _relleno = fillGo.AddComponent<Light>();
            _relleno.type = LightType.Directional;
            _relleno.color = new Color(0.6f, 0.8f, 1f);
            _relleno.intensity = 0.35f;
            _relleno.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(22f, 150f, 0f);
        }

        /// Alterna la firma de la cueva y recolorea la decoracion con la misma definicion
        /// que usan los habitats. Idempotente: se puede llamar en cada viaje sin reconstruir.
        public void Aplicar(RegionDefinicion d)
        {
            if (!_construido || d == null) return;
            _regionAplicada = RegionCuevaSeca.Actual;
            int r = _regionAplicada;
            bool agua = r == 0 || r == 2;
            bool follaje = r == 0 || r == 2;
            bool cristales = r != 3;
            bool hongos = r == 0 || r == 2;
            bool luces = r != 3;
            Activar(_gAgua, agua);
            Activar(_gFollaje, follaje);
            Activar(_gCristales, cristales);
            Activar(_gHongos, hongos);
            Activar(_gLuces, luces);
            Activar(_gEco, r == 1);
            Activar(_gVena, r == 2);
            Activar(_gCanon, r == 3);

            Tint(_roca, d.roca, d.roca * 0.18f);
            Tint(_rocaClara, d.rocaClara, d.rocaClara * 0.12f);
            Tint(_losa, Color.Lerp(d.roca, d.rocaPared, 0.55f), d.rocaPared * 0.12f);
            Tint(_techo, d.rocaPared, d.rocaPared * 0.22f);
            Tint(_madera, d.madera, d.madera * 0.10f);
            Tint(_tierra, d.madera, d.madera * 0.08f);
            Tint(_tallo, Color.Lerp(d.rocaClara, d.agua, 0.35f));
            Tint(_gills, d.roca);
            Tint(_cap, d.cristal, d.emisionCristal * 1.2f);
            Tint(_cristal, d.cristal, d.emisionCristal);
            Tint(_cristalV, Color.Lerp(d.cristal, new Color(0.6f, 0.4f, 1f), 0.55f),
                d.emisionCristal);
            Tint(_agua, d.agua, d.emisionAgua);
            Tint(_espuma, Color.Lerp(d.cristal, Color.white, 0.55f), d.emisionCristal * 0.4f);
            Tint(_llama, new Color(1f, 0.45f, 0.1f), d.emisionAcento * 1.4f);
            Tint(_luzCalida, new Color(1f, 0.62f, 0.28f), new Color(1f, 0.42f, 0.12f) * 2.2f);
            Tint(_follaje, new Color(0.08f, 0.28f, 0.16f), new Color(0.01f, 0.06f, 0.035f) * 1.5f);
            Tint(_follajeClaro, new Color(0.16f, 0.45f, 0.28f), new Color(0.03f, 0.11f, 0.06f) * 1.5f);
            Tint(_follajeCalido, new Color(0.52f, 0.24f, 0.09f), new Color(0.1f, 0.03f, 0.01f));
            Tint(_charcoCalido, new Color(1f, 0.55f, 0.18f), d.emisionAcento * 0.7f);
            Tint(_cielo, d.fondoCamara, d.niebla * 0.8f);
            Tint(_cieloAditivo, d.niebla, d.niebla * 1.2f);
            if (_techoEsfera != null) Tint(_techoEsfera, d.rocaPared, d.niebla * 0.35f);
            if (_relleno != null)
            {
                _relleno.color = d.luzDireccional;
                _relleno.intensity = 0.35f;
            }
        }

        public bool GrupoActivo(string nombre)
        {
            if (_nombresGrupos == null) return false;
            for (int i = 0; i < _nombresGrupos.Length; i++)
                if (_nombresGrupos[i] == nombre)
                    return _grupos[i] != null && _grupos[i].gameObject.activeSelf;
            return false;
        }

        private static void Activar(Transform t, bool activo)
        {
            if (t != null) t.gameObject.SetActive(activo);
        }

        private static void Tint(Material m, Color c, Color? emision = null)
        {
            if (m == null) return;
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (emision.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", emision.Value);
                m.EnableKeyword("_EMISSION");
            }
        }

        /// El Volume global del mapa tenia un perfil sin componentes. Se completa con bloom
        /// (agua/hongos/cristales), un poco de contraste y una vineta de escena; el HUD IMGUI
        /// se dibuja despues del post-proceso y queda nitido.
#if SLUGOUT_URP
        private static void ConfigurarPostProceso()
        {
            Volume vol = Object.FindAnyObjectByType<Volume>();
            if (vol == null)
            {
                GameObject go = new GameObject("PostProceso");
                vol = go.AddComponent<Volume>();
                vol.isGlobal = true;
            }
            if (vol.sharedProfile != null && vol.sharedProfile.components.Count > 0) return;

            var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            perfil.name = "CuevaPostRuntime";

            Bloom bloom = perfil.Add<Bloom>(true);
            bloom.intensity.Override(0.72f);
            bloom.threshold.Override(0.78f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(0.92f, 0.96f, 1f));

            ColorAdjustments color = perfil.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.26f);
            color.saturation.Override(13f);

            Vignette vigneta = perfil.Add<Vignette>(true);
            vigneta.intensity.Override(0.18f);
            vigneta.smoothness.Override(0.4f);
            vigneta.color.Override(new Color(0.01f, 0.02f, 0.04f));

            vol.priority = 1f;
            vol.sharedProfile = perfil;
            Debug.Log("[Cueva] post-proceso bloom activo");
        }

#endif
        private void TechoDeCueva(Vector3 centro)
        {
            GameObject cueva = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cueva.name = "CuevaPixar_Techo";
            cueva.transform.position = centro + Vector3.up * 2f;
            cueva.transform.localScale = Vector3.one * 60f;
            QuitarCollider(cueva);
            // La cupula es fondo, no techo jugable. No se puede usar Unlit: el build lo
            // elimina por stripping y el material nulo abortaba todo el look. Con Lit y el
            // mapa de piedra en emision queda autoniluminada e ignora el fill que la hacia
            // parecer cielo gris.
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            Color tono = new Color(0.02f, 0.03f, 0.045f);
            _techoEsfera = Mat(lit, tono, new Color(0.055f, 0.07f, 0.1f), 0.1f);
            if (_techoEsfera.HasProperty("_Cull")) _techoEsfera.SetFloat("_Cull", 0f);
            // La cupula tambien lleva la piedra procedural: si queda negra, las estalactitas
            // y el arco parecen rocas flotando en el vacio.
            Texture2D piedra = TexturaPiedra();
            if (_techoEsfera.HasProperty("_BaseMap"))
            {
                _techoEsfera.SetTexture("_BaseMap", piedra);
                _techoEsfera.SetTextureScale("_BaseMap", new Vector2(16f, 8f));
            }
            if (_techoEsfera.HasProperty("_MainTex"))
            {
                _techoEsfera.SetTexture("_MainTex", piedra);
                _techoEsfera.SetTextureScale("_MainTex", new Vector2(16f, 8f));
            }
            if (_techoEsfera.HasProperty("_EmissionMap"))
            {
                _techoEsfera.SetTexture("_EmissionMap", piedra);
                _techoEsfera.SetTextureScale("_EmissionMap", new Vector2(16f, 8f));
            }
            Vestir(cueva, _techoEsfera);
        }

        /// Bóveda de roca que cierra el vacío sobre el claro: anillos concéntricos altos en
        /// el centro y bajos hacia las paredes, más estalactitas ancladas a esa superficie.
        /// Dos draw calls, caras hacia abajo para que la luz las lea desde el jugador.
        private void MantoTecho(Transform padre, Vector3 centro)
        {
            const int seg = 72;
            const int anillos = 12;
            const float radioInterior = 1.2f;
            const float radioExterior = 40f;
            var verts = new Vector3[(anillos + 1) * seg + 1];
            var uvs = new Vector2[verts.Length];
            for (int r = 0; r <= anillos; r++)
            {
                float t = r / (float)anillos;
                float rad = Mathf.Lerp(radioInterior, radioExterior, t);
                for (int s = 0; s < seg; s++)
                {
                    float ang = 2f * Mathf.PI * s / seg;
                    float dx = Mathf.Cos(ang) * rad;
                    float dz = Mathf.Sin(ang) * rad;
                    int i = r * seg + s;
                    verts[i] = new Vector3(centro.x + dx, AlturaManto(dx, dz), centro.z + dz);
                    uvs[i] = new Vector2(dx / 9f, dz / 9f);
                }
            }
            int centroIdx = verts.Length - 1;
            verts[centroIdx] = new Vector3(centro.x, AlturaManto(0f, 0f), centro.z);
            uvs[centroIdx] = Vector2.zero;

            var tris = new int[seg * (anillos * 6 + 3)];
            int k = 0;
            for (int s = 0; s < seg; s++)
            {
                tris[k++] = centroIdx;
                tris[k++] = s;
                tris[k++] = (s + 1) % seg;
            }
            for (int r = 0; r < anillos; r++)
            {
                for (int s = 0; s < seg; s++)
                {
                    int a = r * seg + s;
                    int b = r * seg + (s + 1) % seg;
                    int c = (r + 1) * seg + s;
                    int d = (r + 1) * seg + (s + 1) % seg;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            }

            var manto = new Mesh { name = "Manto_techo" };
            manto.vertices = verts;
            manto.uv = uvs;
            manto.triangles = tris;
            manto.RecalculateNormals();
            manto.RecalculateBounds();
            GameObject go = new GameObject("Manto_techo");
            go.transform.SetParent(padre, false);
            go.AddComponent<MeshFilter>().sharedMesh = manto;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            Vestir(go, _techo);
            mr.receiveShadows = false;

            var puntas = new LoteMalla();
            Mesh cono = ConoColganteMesh();
            for (int i = 0; i < 22; i++)
            {
                float ang = i * 2.399f + 0.7f;
                float rad = 5.5f + (i % 6) * 2.9f;
                float dx = Mathf.Cos(ang) * rad;
                float dz = Mathf.Sin(ang) * rad;
                float y = AlturaManto(dx, dz) + 0.08f;
                float alto = 0.9f + (i % 4) * 0.55f;
                float baseR = 0.34f + (i % 3) * 0.16f;
                puntas.Agregar(cono, Matrix4x4.TRS(
                    new Vector3(centro.x + dx, y, centro.z + dz),
                    Quaternion.Euler(0f, i * 41f, 0f),
                    new Vector3(baseR, alto, baseR)));
            }
            puntas.Crear("Estalactitas_techo", _roca, padre);

            // Racimos luminosos colgando del manto: el azul de la referencia tambien viene
            // desde arriba, no solo de los hongos del piso.
            var cian = new LoteMalla();
            var violeta = new LoteMalla();
            for (int i = 0; i < 16; i++)
            {
                float ang = i * 2.399f + 1.3f;
                float rad = 4.5f + (i % 5) * 2.6f;
                float dx = Mathf.Cos(ang) * rad;
                float dz = Mathf.Sin(ang) * rad;
                float y = AlturaManto(dx, dz) + 0.06f;
                float alto = 0.75f + (i % 3) * 0.35f;
                float baseR = 0.12f + (i % 2) * 0.06f;
                (i % 4 == 0 ? violeta : cian).Agregar(cono, Matrix4x4.TRS(
                    new Vector3(centro.x + dx, y, centro.z + dz),
                    Quaternion.Euler(0f, i * 47f, 0f),
                    new Vector3(baseR, alto, baseR)));
            }
            cian.Crear("Cristales_techo", _cristal, padre);
            violeta.Crear("Cristales_techo_violeta", _cristalV, padre);
        }

        /// Altura de la bóveda en un punto relativo al centro: alta en el medio y baja
        /// hacia las paredes, con ruido para que no parezca un plato liso.
        private static float AlturaManto(float dx, float dz)
        {
            float rad = Mathf.Sqrt(dx * dx + dz * dz);
            float t = Mathf.Clamp01((rad - 1.2f) / 38.8f);
            float ruido = Terrain3D.Valor(dx * 0.16f + 37f, dz * 0.16f + 11f) * 0.5f + 0.5f;
            return Mathf.Lerp(14.6f, 12.0f, t * t) + (ruido - 0.5f) * 0.9f;
        }

        static Texture2D _piedra;

        /// La comparten la region y los props nuevos para que todas las rocas tengan la
        /// misma veta, en vez de colores planos que delatan la primitiva.
        public static Texture2D PiedraCueva { get { return TexturaPiedra(); } }

        public static void AplicarPiedra(Material m, Texture2D tex, float tiling)
        {
            if (m == null || tex == null) return;
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            }
            if (m.HasProperty("_MainTex"))
            {
                m.SetTexture("_MainTex", tex);
                m.SetTextureScale("_MainTex", new Vector2(tiling, tiling));
            }
        }

        /// Piedra de la arena: dos octavas del mismo ruido del terreno y una grieta fina.
        /// Se repite con tiling; sin UV en el mesh no habia forma de romper el plano liso.
        private static Texture2D TexturaPiedra()
        {
            if (_piedra != null) return _piedra;
            const int n = 256;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "cueva_piedra";
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a = Terrain3D.Valor(x * 0.035f, y * 0.035f) * 0.5f + 0.5f;
                    float b = Terrain3D.Valor(x * 0.13f + 11f, y * 0.13f + 7f) * 0.5f + 0.5f;
                    float v = 0.32f + a * 0.6f + b * 0.16f;
                    if (Mathf.Abs(b - 0.5f) < 0.025f) v *= 0.42f;
                    if (b > 0.82f) v = Mathf.Min(1f, v + 0.16f);
                    px[y * n + x] = new Color(v * 0.9f, v * 0.95f, v, 1f);
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            _piedra = t;
            return t;
        }

        private void CrearMateriales()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            _tallo = Mat(lit, new Color(0.55f, 0.85f, 0.9f), new Color(0.06f, 0.35f, 0.45f), 0.45f);
            _cap = Mat(lit, new Color(0.1f, 0.55f, 0.85f), new Color(0.1f, 0.65f, 1f) * 1.8f, 0.3f);
            _gills = Mat(lit, new Color(0.04f, 0.26f, 0.36f), new Color(0.02f, 0.18f, 0.28f), 0.55f);
            Texture2D capTex = TexturaHongoCap();
            if (_cap.HasProperty("_BaseMap"))
            {
                _cap.SetTexture("_BaseMap", capTex);
                _cap.SetTextureScale("_BaseMap", Vector2.one);
            }
            if (_cap.HasProperty("_MainTex"))
            {
                _cap.SetTexture("_MainTex", capTex);
                _cap.SetTextureScale("_MainTex", Vector2.one);
            }
            if (_cap.HasProperty("_EmissionMap"))
            {
                _cap.SetTexture("_EmissionMap", capTex);
                _cap.SetTextureScale("_EmissionMap", Vector2.one);
            }
            _cristal = Mat(lit, new Color(0.45f, 0.85f, 1f), new Color(0.25f, 0.75f, 1f) * 1.6f, 0.2f);
            _cristalV = Mat(lit, new Color(0.6f, 0.4f, 1f), new Color(0.45f, 0.2f, 1f) * 1.5f, 0.2f);
            _llama = Mat(lit, new Color(1f, 0.45f, 0.1f), new Color(1f, 0.35f, 0.05f) * 1.8f, 0.3f);
            _madera = Mat(lit, new Color(0.18f, 0.12f, 0.08f), new Color(0.04f, 0.025f, 0.02f), 0.55f);
            _agua = Mat(lit, new Color(0.25f, 0.75f, 0.95f), new Color(0.06f, 0.45f, 0.7f), 0.25f);
            if (_agua.HasProperty("_Cull")) _agua.SetFloat("_Cull", 0f);
            Texture2D aguaTex = TexturaAgua();
            _agua.mainTexture = aguaTex;
            if (_agua.HasProperty("_BaseMap"))
            {
                _agua.SetTexture("_BaseMap", aguaTex);
                _agua.SetTextureScale("_BaseMap", new Vector2(3f, 1f));
            }
            if (_agua.HasProperty("_MainTex"))
            {
                _agua.SetTexture("_MainTex", aguaTex);
                _agua.SetTextureScale("_MainTex", new Vector2(3f, 1f));
            }
            _roca = Mat(lit, new Color(0.09f, 0.11f, 0.15f), new Color(0.01f, 0.02f, 0.04f), 0.45f);
            _rocaClara = Mat(lit, new Color(0.14f, 0.17f, 0.23f), new Color(0.02f, 0.03f, 0.05f), 0.35f);
            Texture2D piedra = TexturaPiedra();
            AplicarPiedra(_roca, piedra, 2.2f);
            AplicarPiedra(_rocaClara, piedra, 2.2f);
            // Las losas del piso reflejaban el fill y se veian como discos palidos
            // pegados; con menos especular y albedo quedan enterradas en la piedra.
            _losa = Mat(lit, new Color(0.055f, 0.07f, 0.095f), new Color(0.004f, 0.008f, 0.015f), 0.12f);
            AplicarPiedra(_losa, piedra, 2.2f);
            _techo = Mat(lit, new Color(0.24f, 0.29f, 0.38f), new Color(0.075f, 0.11f, 0.15f), 0.85f);
            if (_techo.HasProperty("_Cull")) _techo.SetFloat("_Cull", 0f);
            AplicarPiedra(_techo, piedra, 7f);
            _luzCalida = Mat(lit, new Color(1f, 0.62f, 0.28f), new Color(1f, 0.42f, 0.12f) * 2.2f, 0.25f);
            _charcoCalido = MatCharcoCalido();
            _follaje = Mat(lit, new Color(0.08f, 0.28f, 0.16f), new Color(0.01f, 0.06f, 0.035f) * 1.5f, 0.75f);
            _follajeClaro = Mat(lit, new Color(0.16f, 0.45f, 0.28f), new Color(0.03f, 0.11f, 0.06f) * 1.5f, 0.7f);
            _follajeCalido = Mat(lit, new Color(0.52f, 0.24f, 0.09f), new Color(0.1f, 0.03f, 0.01f), 0.7f);
            _tierra = Mat(lit, new Color(0.44f, 0.33f, 0.22f), new Color(0.02f, 0.015f, 0.01f), 0.85f);
            AplicarPiedra(_tierra, piedra, 1f);
            _espuma = Mat(lit, new Color(0.7f, 0.9f, 1f), new Color(0.35f, 0.6f, 0.8f), 0.2f);
            _cielo = Mat(lit, new Color(0.03f, 0.08f, 0.14f), Color.black, 0.1f);
            Texture2D cielo = TexturaCielo();
            _cielo.SetTexture("_BaseMap", cielo);
            _cielo.SetTexture("_EmissionMap", cielo);
            _cielo.EnableKeyword("_EMISSION");
            _cielo.SetColor("_EmissionColor", new Color(0.7f, 1f, 1.15f));
            _cieloAditivo = MatCieloAditivo(cielo);
        }

        private Vector3 EnPiso(Vector3 p, float altura)
        {
            p.y = Terrain3D.Altura(p.x, p.z) + altura;
            return p;
        }

        /// Pilares de roca en el anillo exterior: enmarcan el claro como paredes de caverna
        /// sin tapar los tuneles (200 y 320 grados) ni el camino al refugio (90 grados).
        private void PilaresCueva(Transform padre, Vector3 centro)
        {
            var roca = new LoteMalla();
            var clara = new LoteMalla();
            Mesh piedra = RocaMesh();
            Transform muros = MurosCueva.Raiz(padre);
            Vector3[] bocas = BocasRegion();
            // Angulos intercalados entre microhabitats, tuneles y accesos de ESTA cueva. Se
            // rotan con la region: si no, al girar la arena los pilares tapaban una boca.
            float baseAng = Regiones.De(RegionCuevaSeca.Actual).anguloBase;
            float[] angulos = { 30f + baseAng, 55f + baseAng, 70f + baseAng,
                105f + baseAng, 120f + baseAng, 165f + baseAng, 220f + baseAng,
                275f + baseAng, 300f + baseAng, 340f + baseAng };
            for (int i = 0; i < angulos.Length; i++)
            {
                float ang = angulos[i] * Mathf.Deg2Rad;
                float rad = 16f + (i % 4) * 1.8f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z);
                float alto = 5.2f + (i % 3) * 1.4f;
                for (int k = 0; k < 3; k++)
                {
                    float h = alto / 3f * (1.15f - k * 0.12f);
                    float w = 2.4f - k * 0.45f + (i % 2) * 0.3f;
                    Vector3 pos = new Vector3(x + (k % 2 == 0 ? 0.1f : -0.1f),
                        y + h * 0.5f, z + (k % 2 == 0 ? 0f : 0.15f));
                    Quaternion rot = Quaternion.Euler((k - 1) * 4f, i * 47f + k * 23f,
                        (i % 3 - 1) * 5f);
                    Vector3 tam = new Vector3(w, h, w * 0.9f);
                    (k % 2 == 0 ? roca : clara).Agregar(piedra, Matrix4x4.TRS(pos, rot, tam));
                    // Proxy solido con el mismo TRS: el jugador no atraviesa el pilar.
                    if (!SobreAcceso(pos, centro, bocas, 1.2f + w * 0.5f))
                        MurosCueva.Caja(muros, "muro_pilar_" + i + "_" + k, pos, rot, tam,
                            ColisionJugador.CapaMuros);
                    y += h * 0.9f;
                }
            }
            roca.Crear("Pilares_cueva", _roca, padre);
            clara.Crear("Pilares_cueva_claros", _rocaClara, padre);
            Physics.SyncTransforms();
        }

        private void RocasSuelo(Transform padre, Vector3 centro)
        {
            var roca = new LoteMalla();
            var rocaClara = new LoteMalla();
            Mesh piedra = RocaMesh();
            for (int i = 0; i < 14; i++)
            {
                float ang = i * 2.1f + 0.3f;
                float rad = 6.5f + (i % 6) * 2.6f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z);
                float alto = 0.45f + (i % 4) * 0.22f;
                (i % 3 == 0 ? rocaClara : roca).Agregar(piedra, Matrix4x4.TRS(
                    new Vector3(x, y + alto * 0.25f, z),
                    Quaternion.Euler(0f, i * 47f, (i % 3 - 1) * 8f),
                    new Vector3(1.2f + (i % 3) * 0.6f, alto, 0.9f + ((i + 1) % 3) * 0.5f)));
            }
            // Segunda fila, grande y lejana: rompe el horizonte liso de la cupula.
            Transform muros = MurosCueva.Raiz(padre);
            Vector3[] bocas = BocasRegion();
            for (int i = 0; i < 14; i++)
            {
                float ang = i * 1.37f + 0.8f;
                float rad = 18f + (i % 4) * 2.6f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float sx = 2.6f + (i % 4) * 0.9f;
                float sy = 1.8f + (i % 3) * 0.9f;
                float sz = 2.2f + (i % 5) * 0.7f;
                Vector3 pos = new Vector3(x, Terrain3D.Altura(x, z) + sy * 0.15f, z);
                Quaternion rot = Quaternion.Euler((i % 3 - 1) * 7f, i * 53f, 0f);
                Vector3 tam = new Vector3(sx, sy, sz);
                (i % 4 == 0 ? rocaClara : roca).Agregar(piedra, Matrix4x4.TRS(pos, rot, tam));
                // Solo las rocas grandes del fondo: las chicas de la primera fila
                // estan sobre los accesos a los microhabitats y deben seguir decorativas.
                if (!SobreAcceso(pos, centro, bocas, 1.2f + Mathf.Max(sx, sz) * 0.5f))
                    MurosCueva.Caja(muros, "muro_roca_" + i, pos, rot, tam,
                        ColisionJugador.CapaMuros);
            }
            roca.Crear("Rocas_suelo", _roca, padre);
            rocaClara.Crear("Rocas_suelo_claras", _rocaClara, padre);
            Physics.SyncTransforms();
        }

        /// Bocas de tunel de la region que esta de pie, proyectadas al suelo.
        Vector3[] BocasRegion()
        {
            RegionCuevaSeca region = Object.FindAnyObjectByType<RegionCuevaSeca>();
            Transform[] raices = region != null ? region.TunelesRaiz : null;
            if (raices == null) return new Vector3[0];
            var v = new Vector3[raices.Length];
            int n = 0;
            for (int i = 0; i < raices.Length; i++)
            {
                if (raices[i] == null) continue;
                Vector3 p = raices[i].position;
                p.y = 0f;
                v[n++] = p;
            }
            System.Array.Resize(ref v, n);
            return v;
        }

        /// Una roca o un pilar NO puede quedar sobre el pasillo centro-boca: el jugador se
        /// desvia, queda afuera del radio de viaje (1,7 m) y el tunel se vuelve inusable.
        /// Medido en Play: `muro_roca_11` frenaba al jugador a 2,24 m de la boca de la tercera
        /// cueva. La malla sigue ahi, solo pierde el collider.
        static bool SobreAcceso(Vector3 p, Vector3 centro, Vector3[] bocas, float margen)
        {
            for (int i = 0; i < bocas.Length; i++)
            {
                Vector3 ab = bocas[i] - centro;
                ab.y = 0f;
                float l2 = ab.sqrMagnitude;
                if (l2 < 0.0001f) continue;
                Vector3 ap = p - centro;
                ap.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / l2);
                Vector3 pie = new Vector3(centro.x + ab.x * t, 0f, centro.z + ab.z * t);
                float dx = p.x - pie.x, dz = p.z - pie.z;
                if (dx * dx + dz * dz <= margen * margen) return true;
            }
            return false;
        }

        private class LoteMalla
        {
            private readonly System.Collections.Generic.List<Vector3> _v =
                new System.Collections.Generic.List<Vector3>();
            private readonly System.Collections.Generic.List<int> _t =
                new System.Collections.Generic.List<int>();
            private readonly System.Collections.Generic.List<Vector2> _uv =
                new System.Collections.Generic.List<Vector2>();

            public void Agregar(Mesh m, Matrix4x4 tr)
            {
                if (m == null) return;
                int baseIdx = _v.Count;
                Vector3[] verts = m.vertices;
                Vector2[] uvs = m.uv;
                bool conUv = uvs != null && uvs.Length == verts.Length;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 p = tr.MultiplyPoint3x4(verts[i]);
                    _v.Add(p);
                    // Las rocas comparten la textura de la cueva: sin UV todas muestreaban
                    // el mismo pixel y se veian planas.
                    _uv.Add(conUv ? uvs[i] : new Vector2(p.x * 0.22f, p.z * 0.22f));
                }
                int[] tris = m.triangles;
                for (int i = 0; i < tris.Length; i++) _t.Add(baseIdx + tris[i]);
            }

            public GameObject Crear(string nombre, Material mat, Transform padre)
            {
                if (_v.Count == 0) return null;
                Mesh m = new Mesh();
                m.name = nombre;
                m.SetVertices(_v);
                m.SetUVs(0, _uv);
                m.SetTriangles(_t, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                GameObject go = new GameObject(nombre);
                go.transform.SetParent(padre, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                return go;
            }
        }

        static Mesh _rocaMesh;

        /// Piedra facetada de 12 triangulos: no necesita textura y da mejor silueta que
        /// una esfera lisa. Los vertices se perturban una sola vez; cada instancia se escala.
        private static Mesh RocaMesh()
        {
            if (_rocaMesh != null) return _rocaMesh;
            var rnd = new System.Random(731);
            var v = new Vector3[8];
            int idx = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        v[idx++] = new Vector3(
                            x * 0.5f + (float)(rnd.NextDouble() - 0.5) * 0.16f,
                            y * 0.5f + (float)(rnd.NextDouble() - 0.5) * 0.16f,
                            z * 0.5f + (float)(rnd.NextDouble() - 0.5) * 0.16f);
                    }
            // Orden del recorrido: 0(-,-,-) 1(-,-,+) 2(-,+,-) 3(-,+,+)
            //                        4(+,-,-) 5(+,-,+) 6(+,+,-) 7(+,+,+)
            int[] t = {
                0,1,2, 0,2,3,   // -x
                4,6,5, 4,7,6,   // +x
                2,3,6, 2,6,7,   // +y
                0,4,1, 1,4,5,   // -y
                1,5,3, 3,5,7,   // +z
                0,2,4, 2,6,4    // -z
            };
            var m = new Mesh();
            m.name = "roca_facetada";
            m.vertices = v;
            m.triangles = t;
            m.RecalculateNormals();
            _rocaMesh = m;
            return m;
        }

        /// Esquirlas bajas alrededor del jugador: llenan el piso vacio con brillo barato
        /// (dos draw calls, mismo batch que las estalactitas).
        private void DetallesSuelo(Transform padre, Vector3 centro)
        {
            var cian = new LoteMalla();
            var violeta = new LoteMalla();
            Mesh cono = ConoColganteMesh();
            for (int i = 0; i < 26; i++)
            {
                float ang = i * 2.17f + 0.6f;
                float rad = 3.2f + (i % 7) * 1.1f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z);
                float alto = 0.28f + (i % 4) * 0.2f;
                float baseR = 0.09f + (i % 3) * 0.04f;
                (i % 3 == 0 ? violeta : cian).Agregar(cono, Matrix4x4.TRS(
                    new Vector3(x, y, z), Quaternion.Euler(180f, i * 37f, 8f),
                    new Vector3(baseR, alto, baseR)));
            }
            cian.Crear("Detalles_cristal", _cristal, padre);
            violeta.Crear("Detalles_cristal_violeta", _cristalV, padre);
        }

        /// Arroyo que cruza el claro: una cinta de agua emisiva con textura que corre, mas
        /// rocas de ribera. Es el punto de fuga que la cueva plana no tenia.
        private void Arroyo(Transform padre, Vector3 centro, Transform faroles)
        {
            const int n = 28;
            var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                pts[i] = new Vector3(centro.x - 14f + t * 28f, 0f,
                    centro.z + 12.5f + Mathf.Sin(t * 3.4f) * 2.2f);
            }

            var verts = new Vector3[(n + 1) * 2];
            var uvs = new Vector2[(n + 1) * 2];
            var nor = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                Vector3 tan = i < n ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1];
                tan.Normalize();
                nor[i] = new Vector3(tan.z, 0f, -tan.x);
                float half = 1.7f + 0.3f * Mathf.Sin(i * 0.5f);
                Vector3 izq = pts[i] - nor[i] * half;
                Vector3 der = pts[i] + nor[i] * half;
                izq.y = Terrain3D.Altura(izq.x, izq.z) + 0.04f;
                der.y = Terrain3D.Altura(der.x, der.z) + 0.04f;
                verts[i * 2] = izq;
                verts[i * 2 + 1] = der;
                uvs[i * 2] = new Vector2(i / (float)n * 5f, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)n * 5f, 1f);
            }
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris[i * 6] = a; tris[i * 6 + 1] = c; tris[i * 6 + 2] = b;
                tris[i * 6 + 3] = b; tris[i * 6 + 4] = c; tris[i * 6 + 5] = d;
            }
            var mesh = new Mesh { name = "Arroyo" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            GameObject agua = new GameObject("Arroyo");
            agua.transform.SetParent(padre, false);
            agua.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer aguaR = agua.AddComponent<MeshRenderer>();
            Vestir(agua, _agua);
            aguaR.receiveShadows = false;
            var flujo = agua.AddComponent<FlujoAgua>();
            flujo.material = _agua;
            flujo.velocidad = new Vector2(0.35f, -0.6f);

            var roca = new LoteMalla();
            var clara = new LoteMalla();
            Mesh piedra = RocaMesh();
            for (int i = 2; i < n; i += 2)
            {
                for (int lado = -1; lado <= 1; lado += 2)
                {
                    float half = 1.9f + 0.3f * Mathf.Sin(i * 0.5f);
                    Vector3 p = pts[i] + nor[i] * half * lado;
                    p.y = Terrain3D.Altura(p.x, p.z) + 0.12f;
                    float sx = 0.5f + (i % 3) * 0.18f;
                    float sy = 0.28f + (i % 4) * 0.08f;
                    (i % 4 == 0 ? clara : roca).Agregar(piedra, Matrix4x4.TRS(
                        p, Quaternion.Euler(0f, i * 43f + lado * 17f, (i % 3 - 1) * 7f),
                        new Vector3(sx, sy, sx * 0.85f)));
                }
            }
            roca.Crear("Ribera", _roca, padre);
            clara.Crear("Ribera_clara", _rocaClara, padre);

            Farol(faroles, new Vector3(centro.x - 3.2f, 0f, centro.z + 12.8f));
            Farol(faroles, new Vector3(centro.x + 3.4f, 0f, centro.z + 13.6f));
        }

        /// Refugio lejano: dos torres de roca con ventanas/luces y una pasarela de madera.
        /// Es la silueta que le faltaba al fondo para que la cueva tenga un destino.
        private void RefugioFondo(Transform padre, Vector3 centro)
        {
            var roca = new LoteMalla();
            var clara = new LoteMalla();
            var madera = new LoteMalla();
            var luz = new LoteMalla();
            Mesh piedra = RocaMesh();
            Mesh caja = CajaMesh();

            // Techo a dos aguas con viga cumbrera, todo en el lote de madera.
            void Techo(float x, float z, float baseY, float alto, float ancho)
            {
                float y = baseY + alto + 0.3f;
                for (int lado = -1; lado <= 1; lado += 2)
                {
                    madera.Agregar(caja, Matrix4x4.TRS(
                        new Vector3(x + lado * ancho * 0.24f, y + 0.3f, z),
                        Quaternion.Euler(0f, 0f, -lado * 30f),
                        new Vector3(ancho * 0.72f, 0.14f, ancho * 0.95f)));
                }
                madera.Agregar(caja, Matrix4x4.TRS(new Vector3(x, y + 0.72f, z),
                    Quaternion.identity, new Vector3(0.18f, 0.16f, ancho * 1.05f)));
            }

            float[] torreX = { -7.2f, 8.4f };
            for (int t = 0; t < torreX.Length; t++)
            {
                float x = centro.x + torreX[t];
                float z = centro.z + 22f + t * 1.2f;
                float baseY = Terrain3D.Altura(x, z);
                float alto = t == 0 ? 7.4f : 9.2f;
                for (int k = 0; k < 5; k++)
                {
                    float y = baseY + k * (alto / 5f) + alto / 10f;
                    float esc = 2.1f - k * 0.22f;
                    (k % 2 == 0 ? roca : clara).Agregar(piedra, Matrix4x4.TRS(
                        new Vector3(x, y, z), Quaternion.Euler(0f, t * 33f + k * 19f, 0f),
                        new Vector3(esc, alto / 5f * 1.25f, esc)));
                }
                for (int k = 1; k <= 5; k++)
                {
                    float y = baseY + 0.9f + k * 1.15f;
                    luz.Agregar(caja, Matrix4x4.TRS(new Vector3(x, y, z - 1.05f), Quaternion.identity,
                        new Vector3(0.38f, 0.48f, 0.12f)));
                }
                // Franja cálida bajo el alero: se lee como ventana corrida desde lejos.
                luz.Agregar(caja, Matrix4x4.TRS(new Vector3(x, baseY + alto - 0.15f, z - 1.12f),
                    Quaternion.identity, new Vector3(1.4f, 0.14f, 0.1f)));
                Techo(x, z, baseY, alto, 3.4f);
            }

            // Tercera torre al centro: cierra el poblado y hace de puerta al fondo.
            float xc = centro.x + 0.3f;
            float zc = centro.z + 23.6f;
            float baseC = Terrain3D.Altura(xc, zc);
            float altoC = 6.6f;
            for (int k = 0; k < 4; k++)
            {
                float y = baseC + k * (altoC / 4f) + altoC / 8f;
                float esc = 1.9f - k * 0.24f;
                (k % 2 == 0 ? roca : clara).Agregar(piedra, Matrix4x4.TRS(
                    new Vector3(xc, y, zc), Quaternion.Euler(0f, k * 37f, 0f),
                    new Vector3(esc, altoC / 4f * 1.3f, esc)));
            }
            for (int k = 1; k <= 4; k++)
            {
                float y = baseC + 1.0f + k * 1.2f;
                luz.Agregar(caja, Matrix4x4.TRS(new Vector3(xc, y, zc - 0.95f), Quaternion.identity,
                    new Vector3(0.34f, 0.44f, 0.1f)));
            }
            luz.Agregar(caja, Matrix4x4.TRS(new Vector3(xc, baseC + altoC - 0.15f, zc - 1.02f),
                Quaternion.identity, new Vector3(1.5f, 0.14f, 0.1f)));
            Techo(xc, zc, baseC, altoC, 3.8f);
            // Estandarte con rombo encendido sobre la puerta.
            madera.Agregar(caja, Matrix4x4.TRS(new Vector3(xc, baseC + 4.55f, zc - 1.06f),
                Quaternion.identity, new Vector3(0.78f, 1.9f, 0.08f)));
            luz.Agregar(caja, Matrix4x4.TRS(new Vector3(xc, baseC + 4.7f, zc - 1.13f),
                Quaternion.Euler(0f, 0f, 45f), new Vector3(0.34f, 0.34f, 0.05f)));

            // Torres cercanas a los bordes del claro: enmarcan el puente y agrandan
            // el poblado sin tapar el camino central.
            float[] torresX = { -10.5f, -9f, 9f, 10.5f };
            for (int t = 0; t < torresX.Length; t++)
            {
                float xt = centro.x + torresX[t];
                float zt = centro.z + 17.5f + (t % 2) * 1.3f;
                float baseT = Terrain3D.Altura(xt, zt);
                float altoT = 5.0f + (t % 2) * 1.2f;
                for (int k = 0; k < 3; k++)
                {
                    float y = baseT + k * (altoT / 3f) + altoT / 6f;
                    float esc = 0.95f - k * 0.12f;
                    (k % 2 == 0 ? roca : clara).Agregar(piedra, Matrix4x4.TRS(
                        new Vector3(xt, y, zt), Quaternion.Euler(0f, t * 31f + k * 17f, 0f),
                        new Vector3(esc, altoT / 3f * 1.3f, esc)));
                }
                for (int k = 1; k <= 2; k++)
                {
                    float y = baseT + 0.7f + k * 1.1f;
                    luz.Agregar(caja, Matrix4x4.TRS(new Vector3(xt, y, zt - 0.8f),
                        Quaternion.identity, new Vector3(0.28f, 0.36f, 0.1f)));
                }
                Techo(xt, zt, baseT, altoT, 2f);
            }

            float zPuente = centro.z + 20.5f;
            float yPuente = Terrain3D.Altura(centro.x, zPuente) + 4.3f;
            for (int i = 0; i < 9; i++)
            {
                float x = centro.x - 8f + i * 2f;
                madera.Agregar(caja, Matrix4x4.TRS(new Vector3(x, yPuente, zPuente),
                    Quaternion.identity, new Vector3(1.9f, 0.1f, 0.95f)));
            }
            for (int lado = -1; lado <= 1; lado += 2)
            {
                float z = zPuente + lado * 0.52f;
                madera.Agregar(caja, Matrix4x4.TRS(new Vector3(centro.x, yPuente + 0.95f, z),
                    Quaternion.identity, new Vector3(16.2f, 0.08f, 0.08f)));
                for (int i = 0; i < 5; i++)
                {
                    float x = centro.x - 7f + i * 3.5f;
                    madera.Agregar(caja, Matrix4x4.TRS(new Vector3(x, yPuente + 0.55f, z),
                        Quaternion.identity, new Vector3(0.12f, 1.1f, 0.12f)));
                }
            }
            for (int i = 0; i < 3; i++)
            {
                float x = centro.x - 4f + i * 4f;
                luz.Agregar(caja, Matrix4x4.TRS(new Vector3(x, yPuente + 1.15f, zPuente - 0.55f),
                    Quaternion.identity, new Vector3(0.22f, 0.3f, 0.22f)));
            }
            // Soportes a piso en los extremos, para que la pasarela no flote.
            for (int lado = -1; lado <= 1; lado += 2)
            {
                float x = centro.x + lado * 7.5f;
                float baseY = Terrain3D.Altura(x, zPuente);
                madera.Agregar(caja, Matrix4x4.TRS(new Vector3(x, (baseY + yPuente) * 0.5f, zPuente),
                    Quaternion.identity, new Vector3(0.22f, yPuente - baseY, 0.22f)));
            }

            // Cajas de suministros junto al arroyo y escalones laterales a la pasarela.
            for (int i = 0; i < 3; i++)
            {
                float x = centro.x + 3.2f + i * 0.82f;
                float z = centro.z + 10.2f + (i % 2) * 0.55f;
                float baseY = Terrain3D.Altura(x, z);
                madera.Agregar(caja, Matrix4x4.TRS(new Vector3(x, baseY + 0.31f, z),
                    Quaternion.Euler(0f, i * 31f, 0f), Vector3.one * 0.62f));
            }
            madera.Agregar(caja, Matrix4x4.TRS(
                new Vector3(centro.x + 3.55f, Terrain3D.Altura(centro.x + 3.55f, centro.z + 10.4f) + 0.9f,
                    centro.z + 10.4f), Quaternion.Euler(0f, 17f, 0f), Vector3.one * 0.46f));
            for (int i = 0; i < 9; i++)
            {
                float y = 0.3f + i * 0.5f;
                float z = centro.z + 19.2f + i * 0.28f;
                madera.Agregar(caja, Matrix4x4.TRS(new Vector3(centro.x - 8.6f, y, z),
                    Quaternion.identity, new Vector3(1.3f, 0.16f, 0.9f)));
            }

            roca.Crear("Refugio_roca", _roca, padre);
            clara.Crear("Refugio_roca_clara", _rocaClara, padre);
            madera.Crear("Refugio_madera", _madera, padre);
            luz.Crear("Refugio_luces", _luzCalida, padre);
        }

        /// Matas de helechos bajos: tres conos inclinados por mata, batcheados por tono.
        private void Follaje(Transform padre, Vector3 centro)
        {
            var verde = new LoteMalla();
            var claro = new LoteMalla();
            Mesh cono = ConoColganteMesh();
            for (int i = 0; i < 34; i++)
            {
                float ang = i * 2.17f + 0.4f;
                float rad = 3.2f + (i % 8) * 1.45f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                MataFollaje(verde, claro, cono, x, z, i);
            }
            // Matas al borde del sendero: rompen la cinta de tierra y guian la vista.
            for (int i = 0; i < 20; i++)
            {
                float t = 0.06f + i * 0.045f;
                float x0 = centro.x + Mathf.Sin(t * 2.3f) * 1.6f;
                float z0 = centro.z - 2.5f + t * 14.2f;
                float lado = (i % 2 == 0 ? -1f : 1f);
                float x = x0 + lado * (1.55f + (i % 3) * 0.25f);
                float z = z0 + (((i * 3) % 5) - 2) * 0.2f;
                MataFollaje(verde, claro, cono, x, z, 100 + i);
            }
            // Musgo al pie de los pilares del perímetro.
            float[] angulos = { 30f, 55f, 70f, 105f, 120f, 165f, 220f, 275f, 300f, 340f };
            for (int i = 0; i < angulos.Length; i++)
            {
                float ang = angulos[i] * Mathf.Deg2Rad;
                float rad = 16f + (i % 4) * 1.8f - 1.3f;
                for (int l = -1; l <= 1; l += 2)
                {
                    float a = ang + l * 0.07f;
                    float x = centro.x + Mathf.Cos(a) * rad;
                    float z = centro.z + Mathf.Sin(a) * rad;
                    MataFollaje(verde, claro, cono, x, z, 200 + i * 2 + (l + 1) / 2);
                }
            }
            // Matas de hojas anchas, algunas cálidas: el acento naranja de la referencia.
            var calido = new LoteMalla();
            for (int i = 0; i < 16; i++)
            {
                float t = 0.08f + i * 0.055f;
                float x0 = centro.x + Mathf.Sin(t * 2.3f) * 1.6f;
                float z0 = centro.z - 2.5f + t * 14.2f;
                float lado = (i % 2 == 0 ? -1f : 1f);
                float x = x0 + lado * (2.3f + (i % 3) * 0.35f);
                float z = z0 + (((i * 3) % 5) - 2) * 0.35f;
                if (i % 4 == 0)
                    MataAncha(calido, verde, cono, x, z, 300 + i);
                else
                    MataAncha(verde, claro, cono, x, z, 300 + i);
            }
            verde.Crear("Follaje_verde", _follaje, padre);
            claro.Crear("Follaje_claro", _follajeClaro, padre);
            calido.Crear("Follaje_calido", _follajeCalido, padre);
        }

        /// Hoja ancha: cono aplastado y inclinado. Cinco por mata, batcheadas por tono.
        private void MataAncha(LoteMalla principal, LoteMalla secundario, Mesh cono,
            float x, float z, int semilla)
        {
            float y = Terrain3D.Altura(x, z);
            for (int k = 0; k < 5; k++)
            {
                float ang = semilla * 29f + k * 72f;
                float rad = 0.1f + (k % 2) * 0.07f;
                float alto = 0.3f + 0.1f * ((semilla + k) % 3);
                Vector3 p = new Vector3(
                    x + Mathf.Cos(ang * Mathf.Deg2Rad) * rad, y,
                    z + Mathf.Sin(ang * Mathf.Deg2Rad) * rad);
                (k % 3 == 2 ? secundario : principal).Agregar(cono, Matrix4x4.TRS(
                    p,
                    Quaternion.Euler(198f, ang, 0f),
                    new Vector3(0.17f, alto, 0.055f)));
            }
        }

        private void MataFollaje(LoteMalla verde, LoteMalla claro, Mesh cono,
            float x, float z, int semilla)
        {
            float y = Terrain3D.Altura(x, z);
            for (int k = 0; k < 3; k++)
            {
                float dx = (k - 1) * 0.18f;
                float dz = ((k * 3 + semilla) % 3 - 1) * 0.14f;
                float alto = 0.2f + 0.12f * ((semilla + k) % 4);
                float baseR = 0.035f + 0.018f * (k % 2);
                (k == 0 ? claro : verde).Agregar(cono, Matrix4x4.TRS(
                    new Vector3(x + dx, y, z + dz),
                    Quaternion.Euler(180f + (k == 1 ? 16f : -10f), semilla * 37f + k * 23f, 0f),
                    new Vector3(baseR, alto, baseR)));
            }
        }

        static Mesh _cajaMesh;

        private static Mesh CajaMesh()
        {
            if (_cajaMesh != null) return _cajaMesh;
            var v = new Vector3[8];
            int idx = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        v[idx++] = new Vector3(x * 0.5f, y * 0.5f, z * 0.5f);
            // Mismo orden de vertices que RocaMesh: 0(-,-,-),1(-,-,+),2(-,+,-),3(-,+,+),4(+,-,-),5(+,-,+),6(+,+,-),7(+,+,+)
            var t = new int[] {
                0,1,2, 0,2,3, 4,6,5, 4,7,6, 2,3,6, 2,6,7,
                0,4,1, 1,4,5, 1,5,3, 3,5,7, 0,2,4, 2,6,4
            };
            var m = new Mesh { name = "caja" };
            m.vertices = v;
            m.triangles = t;
            m.RecalculateNormals();
            _cajaMesh = m;
            return m;
        }

        /// Puerta de piedra al fondo del arroyo: dos pilares apoyados en el piso y un dintel.
        /// Reemplaza al anillo de rocas que flotaba sobre la escena.
        private void ArcoRoca(Transform padre, Vector3 centro)
        {
            var roca = new LoteMalla();
            var clara = new LoteMalla();
            Mesh piedra = RocaMesh();
            float z = centro.z + 16.5f;
            for (int lado = -1; lado <= 1; lado += 2)
            {
                float x = centro.x + lado * 4.2f;
                float baseY = Terrain3D.Altura(x, z);
                for (int k = 0; k < 3; k++)
                {
                    float y = baseY + k * 1.05f + 0.4f;
                    float esc = 1.3f + (k % 2) * 0.22f;
                    (k % 2 == 0 ? roca : clara).Agregar(piedra, Matrix4x4.TRS(
                        new Vector3(x, y, z), Quaternion.Euler(0f, k * 41f + lado * 19f, 0f),
                        new Vector3(esc, 1.1f, esc)));
                }
            }
            for (int i = 0; i < 4; i++)
            {
                float x = centro.x - 3.3f + i * 2.2f;
                float y = Terrain3D.Altura(x, z) + 3.4f;
                (i % 2 == 0 ? roca : clara).Agregar(piedra, Matrix4x4.TRS(
                    new Vector3(x, y, z), Quaternion.Euler((i % 3 - 1) * 6f, i * 33f, 0f),
                    new Vector3(1.4f, 0.7f + (i % 2) * 0.15f, 1.2f)));
            }
            roca.Crear("Puerta_roca", _roca, padre);
            clara.Crear("Puerta_roca_clara", _rocaClara, padre);
        }

        /// Losas planas alrededor del claro: rompen el piso liso con silueta, no con textura.
        /// Cilindros chatos de 10 caras: las rocas facetadas en Y fino parecian esquirlas.
        private void Losas(Transform padre, Vector3 centro)
        {
            var roca = new LoteMalla();
            Mesh losa = LosaMesh();
            for (int i = 0; i < 16; i++)
            {
                float ang = i * 2.4f + 0.5f;
                float rad = 5.2f + (i % 6) * 1.3f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z) + 0.02f;
                float sx = 0.55f + (i % 4) * 0.2f;
                roca.Agregar(losa, Matrix4x4.TRS(
                    new Vector3(x, y, z), Quaternion.Euler(0f, i * 37f, 0f),
                    new Vector3(sx, 0.28f + (i % 2) * 0.08f, sx * 0.72f)));
            }
            roca.Crear("Losas", _losa, padre);
        }

        static Mesh _losaMesh;

        /// Disco de piedra de 10 caras, centro en y=0: al escalarlo en Y bajo queda una
        /// laja enterrada en el piso en vez de una cara triangular gigante. El aro del
        /// borde va duplicado para que el canto quede recto y no acolchado.
        private static Mesh LosaMesh()
        {
            if (_losaMesh != null) return _losaMesh;
            const int seg = 10;
            var v = new Vector3[seg * 3 + 2];
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                v[i] = new Vector3(x, 0.5f, z);
                v[seg + i] = new Vector3(x, 0.5f, z);
                v[seg * 2 + i] = new Vector3(x, -0.5f, z);
            }
            int top = seg * 3, bottom = top + 1;
            v[top] = new Vector3(0f, 0.5f, 0f);
            v[bottom] = new Vector3(0f, -0.5f, 0f);
            var t = new int[seg * 12];
            int k = 0;
            for (int i = 0; i < seg; i++)
            {
                int a = i, b = (i + 1) % seg;
                t[k++] = top; t[k++] = b; t[k++] = a;
                t[k++] = bottom; t[k++] = seg * 2 + a; t[k++] = seg * 2 + b;
                t[k++] = seg * 2 + a; t[k++] = seg + a; t[k++] = seg + b;
                t[k++] = seg * 2 + a; t[k++] = seg + b; t[k++] = seg * 2 + b;
            }
            var m = new Mesh { name = "losa" };
            m.vertices = v;
            m.triangles = t;
            m.RecalculateNormals();
            _losaMesh = m;
            return m;
        }

        /// Camino de tierra cálida desde la plaza hasta la pasarela del arroyo, como el
        /// sendero de la referencia. Cinta con UVs y altura del terreno, sin collider.
        private void Sendero(Transform padre, Vector3 centro)
        {
            const int n = 26;
            var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                pts[i] = new Vector3(centro.x + Mathf.Sin(t * 2.3f) * 1.6f, 0f,
                    centro.z - 2.5f + t * 14.2f);
            }
            var verts = new Vector3[(n + 1) * 2];
            var uvs = new Vector2[(n + 1) * 2];
            var nor = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                Vector3 tan = i < n ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1];
                tan.Normalize();
                nor[i] = new Vector3(tan.z, 0f, -tan.x);
                float half = 1.25f + 0.22f * Mathf.Sin(i * 0.7f);
                Vector3 izq = pts[i] - nor[i] * half;
                Vector3 der = pts[i] + nor[i] * half;
                izq.y = Terrain3D.Altura(izq.x, izq.z) + 0.035f;
                der.y = Terrain3D.Altura(der.x, der.z) + 0.035f;
                verts[i * 2] = izq;
                verts[i * 2 + 1] = der;
                uvs[i * 2] = new Vector2(0f, i * 0.38f);
                uvs[i * 2 + 1] = new Vector2(1f, i * 0.38f);
            }
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris[i * 6] = a; tris[i * 6 + 1] = c; tris[i * 6 + 2] = b;
                tris[i * 6 + 3] = b; tris[i * 6 + 4] = c; tris[i * 6 + 5] = d;
            }
            var m = new Mesh { name = "Sendero" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            GameObject go = new GameObject("Sendero");
            go.transform.SetParent(padre, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            Vestir(go, _tierra);
            r.receiveShadows = false;
        }

        /// Charcos bajos de agua emisiva alrededor del claro: devuelven el reflejo cian
        /// de la referencia y comparten el material animado del arroyo.
        private void Charcos(Transform padre, Vector3 centro)
        {
            var lote = new LoteMalla();
            Mesh disco = CharcoMesh();
            float[] angulos = { 205f, 235f, 260f, 295f, 330f, 25f, 70f, 115f };
            for (int i = 0; i < angulos.Length; i++)
            {
                float ang = angulos[i] * Mathf.Deg2Rad;
                float rad = 4.9f + (i % 3) * 1.5f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z) + 0.03f;
                float sx = 0.9f + (i % 3) * 0.35f;
                lote.Agregar(disco, Matrix4x4.TRS(
                    new Vector3(x, y, z), Quaternion.Euler(0f, i * 47f, 0f),
                    new Vector3(sx, 1f, sx * (0.65f + (i % 2) * 0.2f))));
            }
            lote.Crear("Charcos", _agua, padre);
        }

        static Mesh _charcoMesh;

        private static Mesh CharcoMesh()
        {
            if (_charcoMesh != null) return _charcoMesh;
            const int seg = 12;
            var v = new Vector3[seg + 1];
            var uv = new Vector2[seg + 1];
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                v[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                uv[i] = new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f);
            }
            v[seg] = Vector3.zero;
            uv[seg] = new Vector2(0.5f, 0.5f);
            var t = new int[seg * 3];
            for (int i = 0; i < seg; i++)
            {
                t[i * 3] = seg;
                t[i * 3 + 1] = (i + 1) % seg;
                t[i * 3 + 2] = i;
            }
            var m = new Mesh { name = "charco" };
            m.vertices = v;
            m.uv = uv;
            m.triangles = t;
            m.RecalculateNormals();
            _charcoMesh = m;
            return m;
        }

        /// Fondo luminoso detrás del refugio: un plano con gradiente cian y bordes
        /// oscuros que simula el cañón iluminado de la referencia, sin point lights.
        private void CieloFondo(Transform padre, Vector3 centro)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Cielo_Fondo";
            go.transform.SetParent(padre, false);
            go.transform.position = centro + new Vector3(0f, 6f, 19.5f);
            go.transform.localScale = new Vector3(24f, 9f, 1f);
            QuitarCollider(go);
            Vestir(go, _cieloAditivo != null ? _cieloAditivo : _cielo);
        }

        /// Variante aditiva del cielo: se dibuja delante del refugio sin tapar sus
        /// torres, solo suma el resplandor cian del cañón.
        private static Material MatCieloAditivo(Texture2D t)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) return null;
            Material m = new Material(s);
            Color negro = new Color(0f, 0f, 0f, 1f);
            m.color = negro;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", negro);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", t);
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", new Color(0.75f, 1f, 1.15f) * 1.15f);
                m.EnableKeyword("_EMISSION");
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.renderQueue = 3000;
            return m;
        }

        static Texture2D _cieloTex;

        private static Texture2D TexturaCielo()
        {
            if (_cieloTex != null) return _cieloTex;
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "cielo_cueva";
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                float v = y / (float)(n - 1);
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)(n - 1);
                    float borde = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 7f)
                        * Mathf.Clamp01(Mathf.Min(v, 1f - v) * 5f);
                    float nube = (Terrain3D.Valor(x * 0.07f, y * 0.07f + 11f) - 0.5f) * 0.25f;
                    Color c = Color.Lerp(
                        new Color(0.06f, 0.20f, 0.32f),
                        new Color(0.40f, 0.80f, 1.00f), Mathf.Pow(v, 0.55f)) * (0.9f + nube);
                    // Siluetas lejanas de agujas de roca.
                    float aguja = 0f;
                    for (int i = 0; i < 5; i++)
                    {
                        float cx = 0.12f + i * 0.19f;
                        aguja = Mathf.Max(aguja,
                            Mathf.Exp(-Mathf.Pow((u - cx) * 26f, 2f)) * Mathf.Clamp01(v * 1.4f));
                    }
                    c = Color.Lerp(c, c * 0.45f, aguja * 0.75f);
                    c = Color.Lerp(Color.black, c, borde);
                    px[y * n + x] = c;
                }
            }
            t.SetPixels32(px);
            t.Apply();
            _cieloTex = t;
            return t;
        }

        static Texture2D _aguaTex;

        private static Texture2D TexturaAgua()
        {
            if (_aguaTex != null) return _aguaTex;
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "cueva_agua";
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float onda = Mathf.Sin((y + Mathf.Sin(x * 0.15f) * 6f) * 0.34f) * 0.5f + 0.5f;
                    float ruido = Terrain3D.Valor(x * 0.08f, y * 0.16f) * 0.5f + 0.5f;
                    float v = 0.5f + onda * 0.34f + ruido * 0.14f;
                    if (onda > 0.86f) v = 1f;
                    px[y * n + x] = new Color(v * 0.65f, v * 0.92f, v, 1f);
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            _aguaTex = t;
            return t;
        }

        /// Firma de las Galerias: cristales altos con anillos en el piso. Es la unica cueva
        /// con una silueta vertical de cristal alrededor de la plaza; tres draw calls y sin
        /// luces point (GT 640).
        private void FirmaEco(Transform padre, Vector3 centro)
        {
            var cristales = new LoteMalla();
            var violetas = new LoteMalla();
            var marcas = new LoteMalla();
            Mesh cono = ConoColganteMesh();
            Mesh caja = CajaMesh();
            for (int i = 0; i < 10; i++)
            {
                float ang = (i * 137f + 18f) * Mathf.Deg2Rad;
                float rad = 8f + (i % 4) * 1.6f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z);
                float alto = 2.2f + (i % 4) * 0.9f;
                float baseR = 0.22f + (i % 2) * 0.1f;
                (i % 3 == 0 ? violetas : cristales).Agregar(cono, Matrix4x4.TRS(
                    new Vector3(x, y, z), Quaternion.Euler(180f, i * 37f, 0f),
                    new Vector3(baseR, alto, baseR)));
            }
            // El "eco" se lee como ondas cortadas en la piedra, no como una laguna pintada:
            // discos rellenos tapaban la plaza y se veian como un charco gigante.
            for (int a = 0; a < 3; a++)
            {
                int lados = 10 + a * 2;
                float radio = 2.4f + a * 0.9f;
                for (int i = 0; i < lados; i++)
                {
                    float ang = i / (float)lados * Mathf.PI * 2f;
                    float x = centro.x + Mathf.Cos(ang) * radio;
                    float z = centro.z + Mathf.Sin(ang) * radio;
                    float y = Terrain3D.Altura(x, z) + 0.035f;
                    marcas.Agregar(caja, Matrix4x4.TRS(new Vector3(x, y, z),
                        Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f),
                        new Vector3(0.07f, 0.035f, 0.38f)));
                }
            }
            cristales.Crear("Eco_cristales", _cristal, padre);
            violetas.Crear("Eco_cristales_violeta", _cristalV, padre);
            marcas.Crear("Eco_marcas", _rocaClara, padre);
        }

        /// Firma de la Vena Verde: lianas que caen y luciernagas emisivas, sin agregar
        /// luces reales. Dos draw calls.
        private void FirmaVena(Transform padre, Vector3 centro)
        {
            var lianas = new LoteMalla();
            var luces = new LoteMalla();
            Mesh caja = CajaMesh();
            Mesh cono = ConoColganteMesh();
            for (int i = 0; i < 16; i++)
            {
                float ang = (i * 97f + 11f) * Mathf.Deg2Rad;
                float rad = 7.5f + (i % 5) * 1.5f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z);
                float alto = 3.2f + (i % 4) * 0.8f;
                for (int s = 0; s < 3; s++)
                {
                    float t = (s + 1) / 3f;
                    lianas.Agregar(caja, Matrix4x4.TRS(
                        new Vector3(x + Mathf.Sin(i + s) * 0.35f, y + alto * t, z),
                        Quaternion.Euler(0f, i * 31f, 7f + s * 4f),
                        new Vector3(0.13f, alto / 3f, 0.13f)));
                }
            }
            for (int i = 0; i < 24; i++)
            {
                float ang = (i * 73f + 5f) * Mathf.Deg2Rad;
                float rad = 5f + (i % 6) * 1.3f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z) + 0.8f + (i % 5) * 0.35f;
                luces.Agregar(cono, Matrix4x4.TRS(
                    new Vector3(x, y, z), Quaternion.identity,
                    new Vector3(0.08f, 0.22f, 0.08f)));
            }
            lianas.Crear("Vena_lianas", _madera, padre);
            luces.Crear("Vena_luciernagas", _cristal, padre);
        }

        /// Firma del Canon: grietas de lava, brasas y un puente roto, todo emisivo y sin
        /// luces point. Tres draw calls.
        private void FirmaCanon(Transform padre, Vector3 centro)
        {
            var grietas = new LoteMalla();
            var brasas = new LoteMalla();
            var puente = new LoteMalla();
            Mesh caja = CajaMesh();
            Mesh cono = ConoColganteMesh();
            for (int i = 0; i < 10; i++)
            {
                float ang = (i * 111f + 7f) * Mathf.Deg2Rad;
                float rad = 6f + (i % 5) * 1.8f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z) + 0.03f;
                grietas.Agregar(caja, Matrix4x4.TRS(
                    new Vector3(x, y, z), Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f),
                    new Vector3(0.20f, 0.035f, 2.2f + (i % 3) * 0.8f)));
            }
            for (int i = 0; i < 20; i++)
            {
                float ang = (i * 53f + 23f) * Mathf.Deg2Rad;
                float rad = 5f + (i % 6) * 1.4f;
                float x = centro.x + Mathf.Cos(ang) * rad;
                float z = centro.z + Mathf.Sin(ang) * rad;
                float y = Terrain3D.Altura(x, z) + 0.18f;
                brasas.Agregar(cono, Matrix4x4.TRS(
                    new Vector3(x, y, z), Quaternion.identity,
                    new Vector3(0.10f, 0.25f, 0.10f)));
            }
            for (int i = 0; i < 4; i++)
            {
                float x = centro.x - 2.4f + i * 1.6f;
                float z = centro.z + 12.5f + (i % 2) * 0.4f;
                float y = Terrain3D.Altura(x, z) + 0.75f;
                puente.Agregar(caja, Matrix4x4.TRS(new Vector3(x, y, z),
                    Quaternion.Euler(0f, 12f * i, (i % 2 == 0 ? 8f : -11f)),
                    new Vector3(1.7f, 0.35f, 1.2f)));
            }
            grietas.Crear("Canon_grietas", _llama, padre);
            brasas.Crear("Canon_brasas", _charcoCalido, padre);
            puente.Crear("Canon_puente_roto", _roca, padre);
        }

        /// Faroles batcheados: poste y techo en un lote de madera y la luz en otro. Antes
        /// cada farol eran 3 objetos y sumar luces costaba draw calls.
        private void Farol(Transform padre, Vector3 pos)
        {
            if (_lotesFarolMadera == null)
            {
                _lotesFarolMadera = new LoteMalla();
                _lotesFarolLuz = new LoteMalla();
                _padreFaroles = padre;
            }
            if (_lotesCharcoLuz == null) _lotesCharcoLuz = new LoteMalla();
            float y = Terrain3D.Altura(pos.x, pos.z);
            Mesh caja = CajaMesh();
            _lotesFarolMadera.Agregar(caja, Matrix4x4.TRS(
                new Vector3(pos.x, y + 1.05f, pos.z), Quaternion.identity,
                new Vector3(0.12f, 2.1f, 0.12f)));
            _lotesFarolMadera.Agregar(caja, Matrix4x4.TRS(
                new Vector3(pos.x, y + 2.16f, pos.z), Quaternion.identity,
                new Vector3(0.42f, 0.08f, 0.42f)));
            _lotesFarolLuz.Agregar(caja, Matrix4x4.TRS(
                new Vector3(pos.x, y + 1.92f, pos.z), Quaternion.identity,
                Vector3.one * 0.24f));
            _lotesCharcoLuz.Agregar(DiscoCharcoMesh(), Matrix4x4.TRS(
                new Vector3(pos.x, y + 0.32f, pos.z), Quaternion.identity,
                new Vector3(3.6f, 1f, 3.6f)));
        }

        /// Cartel de madera junto al inicio con el rombo encendido, como el cartel de
        /// la referencia. Comparte los lotes de los faroles (cero draw calls nuevos).
        private void CartelInicio(Transform padre, Vector3 centro)
        {
            if (_lotesFarolMadera == null)
            {
                _lotesFarolMadera = new LoteMalla();
                _lotesFarolLuz = new LoteMalla();
                _padreFaroles = padre;
            }
            if (_lotesCharcoLuz == null) _lotesCharcoLuz = new LoteMalla();
            float x = centro.x - 4.3f;
            float z = centro.z + 1.6f;
            float y = Terrain3D.Altura(x, z);
            _lotesCharcoLuz.Agregar(DiscoCharcoMesh(), Matrix4x4.TRS(
                new Vector3(x, y + 0.32f, z), Quaternion.identity,
                new Vector3(2.4f, 1f, 2.4f)));
            Mesh caja = CajaMesh();
            for (int lado = -1; lado <= 1; lado += 2)
            {
                _lotesFarolMadera.Agregar(caja, Matrix4x4.TRS(
                    new Vector3(x + lado * 0.85f, y + 1.05f, z), Quaternion.identity,
                    new Vector3(0.16f, 2.1f, 0.16f)));
            }
            _lotesFarolMadera.Agregar(caja, Matrix4x4.TRS(
                new Vector3(x, y + 1.55f, z), Quaternion.identity,
                new Vector3(2.1f, 0.95f, 0.12f)));
            _lotesFarolMadera.Agregar(caja, Matrix4x4.TRS(
                new Vector3(x, y + 2.12f, z), Quaternion.identity,
                new Vector3(2.4f, 0.12f, 0.3f)));
            _lotesFarolLuz.Agregar(caja, Matrix4x4.TRS(
                new Vector3(x, y + 1.6f, z - 0.09f), Quaternion.Euler(0f, 0f, 45f),
                new Vector3(0.4f, 0.4f, 0.06f)));
            _lotesFarolLuz.Agregar(caja, Matrix4x4.TRS(
                new Vector3(x - 0.58f, y + 1.25f, z - 0.09f), Quaternion.Euler(0f, 0f, 45f),
                new Vector3(0.14f, 0.14f, 0.05f)));
            _lotesFarolLuz.Agregar(caja, Matrix4x4.TRS(
                new Vector3(x + 0.58f, y + 1.25f, z - 0.09f), Quaternion.Euler(0f, 0f, 45f),
                new Vector3(0.14f, 0.14f, 0.05f)));
        }

        private void CrearFaroles()
        {
            if (_lotesFarolMadera == null) return;
            _lotesFarolMadera.Crear("Faroles_madera", _madera, _padreFaroles);
            _lotesFarolLuz.Crear("Faroles_luz", _luzCalida, _padreFaroles);
            if (_lotesCharcoLuz != null && _charcoCalido != null)
                _lotesCharcoLuz.Crear("Faroles_charco", _charcoCalido, _padreFaroles);
        }

        static Mesh _conoColgante;

        private static Mesh ConoColganteMesh()
        {
            if (_conoColgante != null) return _conoColgante;
            const int seg = 8;
            var v = new Vector3[seg + 2];
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                v[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            v[seg] = new Vector3(0f, -1f, 0f);
            v[seg + 1] = Vector3.zero;
            var t = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                t[i * 3] = i;
                t[i * 3 + 1] = (i + 1) % seg;
                t[i * 3 + 2] = seg;
                t[seg * 3 + i * 3] = seg + 1;
                t[seg * 3 + i * 3 + 1] = (i + 1) % seg;
                t[seg * 3 + i * 3 + 2] = i;
            }
            var m = new Mesh();
            m.name = "cono_colgante";
            m.vertices = v;
            m.triangles = t;
            m.RecalculateNormals();
            _conoColgante = m;
            return m;
        }

        private void Cascada(Transform padre, Vector3 pos, float alto = 7f, float ancho = 2.5f)
        {
            float y = Terrain3D.Altura(pos.x, pos.z);
            GameObject agua = GameObject.CreatePrimitive(PrimitiveType.Quad);
            agua.name = "Cascada";
            agua.transform.SetParent(padre, false);
            agua.transform.position = new Vector3(pos.x, y + alto * 0.5f, pos.z);
            agua.transform.localScale = new Vector3(ancho, alto, 1f);
            QuitarCollider(agua);
            Vestir(agua, _agua);

            // Segundo salto: el agua nace arriba del acantilado, no del techo.
            GameObject alta = GameObject.CreatePrimitive(PrimitiveType.Quad);
            alta.name = "Cascada_alta";
            alta.transform.SetParent(padre, false);
            alta.transform.position = new Vector3(pos.x + 0.32f, y + alto + 1.15f, pos.z + 0.35f);
            alta.transform.localScale = new Vector3(ancho * 0.68f, 2.4f, 1f);
            QuitarCollider(alta);
            Vestir(alta, _agua);

            GameObject brillo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            brillo.name = "Cascada_brillo";
            brillo.transform.SetParent(padre, false);
            brillo.transform.position = new Vector3(pos.x - ancho * 0.2f, y + alto * 0.5f, pos.z - 0.02f);
            brillo.transform.localScale = new Vector3(ancho * 0.18f, alto * 0.95f, 1f);
            QuitarCollider(brillo);
            Vestir(brillo, _cristal);

            GameObject charco = GameObject.CreatePrimitive(PrimitiveType.Quad);
            charco.name = "Charco";
            charco.transform.SetParent(padre, false);
            charco.transform.position = new Vector3(pos.x, y + 0.04f, pos.z - 1.4f);
            charco.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            charco.transform.localScale = new Vector3(ancho * 2.8f, ancho * 1.6f, 1f);
            QuitarCollider(charco);
            Vestir(charco, _agua);

            // Espuma en la caida: un disco emisivo apenas por encima del charco.
            GameObject espuma = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            espuma.name = "Cascada_espuma";
            espuma.transform.SetParent(padre, false);
            espuma.transform.position = new Vector3(pos.x, y + 0.09f, pos.z - 1.3f);
            espuma.transform.localScale = new Vector3(ancho * 1.3f, 0.03f, ancho * 0.85f);
            QuitarCollider(espuma);
            Vestir(espuma, _espuma);
        }

        /// Repisa y paredones que enmarcan la cascada: sin ellos el agua parece un cartel
        /// vertical flotando sobre el piso.
        private void CornisaCascada(Transform padre, Vector3 centro)
        {
            var roca = new LoteMalla();
            var clara = new LoteMalla();
            Mesh piedra = RocaMesh();
            for (int i = 0; i < 6; i++)
            {
                float x = centro.x - 10.4f + i * 1.1f;
                float z = centro.z + 15.6f + (i % 2) * 0.4f;
                float y = Terrain3D.Altura(x, z) + 3.8f + (i % 3) * 0.25f;
                float esc = 1.25f + (i % 3) * 0.3f;
                (i % 2 == 0 ? roca : clara).Agregar(piedra, Matrix4x4.TRS(
                    new Vector3(x, y, z), Quaternion.Euler(0f, i * 43f, (i % 3 - 1) * 8f),
                    new Vector3(esc, 0.85f, esc)));
            }
            for (int lado = -1; lado <= 1; lado += 2)
            {
                for (int k = 0; k < 3; k++)
                {
                    float x = centro.x - 8f + lado * (1.8f + k * 0.25f);
                    float z = centro.z + 15.6f + k * 0.25f;
                    float y = Terrain3D.Altura(x, z) + 1.1f + k * 1.1f;
                    roca.Agregar(piedra, Matrix4x4.TRS(
                        new Vector3(x, y, z), Quaternion.Euler(0f, k * 37f + lado * 13f, 0f),
                        new Vector3(0.9f, 1.1f, 0.8f)));
                }
            }
            roca.Crear("Cornisa_cascada", _roca, padre);
            clara.Crear("Cornisa_cascada_clara", _rocaClara, padre);
        }

        /// Pasarela corta perpendicular al arroyo: tablones sobre el agua y dos barandas.
        private void PuenteCorto(Transform padre, Vector3 pos)
        {
            float y = Terrain3D.Altura(pos.x, pos.z);
            GameObject root = new GameObject("Puente_arroyo");
            root.transform.SetParent(padre, false);

            for (int i = 0; i < 5; i++)
            {
                GameObject plancha = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plancha.name = "Plancha_arroyo";
                plancha.transform.SetParent(root.transform, false);
                plancha.transform.position = new Vector3(pos.x + (i - 2) * 0.62f, y + 0.48f, pos.z);
                plancha.transform.localScale = new Vector3(0.52f, 0.08f, 2.7f);
                QuitarCollider(plancha);
                Vestir(plancha, _madera);
            }
            for (int lado = -1; lado <= 1; lado += 2)
            {
                GameObject riel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                riel.name = "Riel_arroyo";
                riel.transform.SetParent(root.transform, false);
                riel.transform.position = new Vector3(pos.x, y + 0.96f, pos.z + lado * 1.15f);
                riel.transform.localScale = new Vector3(3.3f, 0.08f, 0.08f);
                QuitarCollider(riel);
                Vestir(riel, _madera);
                for (int i = 0; i < 2; i++)
                {
                    GameObject poste = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    poste.name = "Poste_arroyo";
                    poste.transform.SetParent(root.transform, false);
                    poste.transform.position = new Vector3(pos.x + (i == 0 ? -1.5f : 1.5f),
                        y + 0.72f, pos.z + lado * 1.15f);
                    poste.transform.localScale = new Vector3(0.1f, 1.35f, 0.1f);
                    QuitarCollider(poste);
                    Vestir(poste, _madera);
                }
            }
        }

        private void Puente(Transform padre, Vector3 pos)
        {
            float y = Terrain3D.Altura(pos.x, pos.z);
            GameObject root = new GameObject("Puente");
            root.transform.SetParent(padre, false);
            for (int i = 0; i < 7; i++)
            {
                GameObject plancha = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plancha.name = "Plancha";
                plancha.transform.SetParent(root.transform, false);
                plancha.transform.position = new Vector3(pos.x, y + 0.55f, pos.z + (i - 3) * 0.62f);
                plancha.transform.localScale = new Vector3(1.7f, 0.08f, 0.52f);
                QuitarCollider(plancha);
                Vestir(plancha, _madera);
            }
            for (int lado = -1; lado <= 1; lado += 2)
            {
                GameObject riel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                riel.name = "Riel";
                riel.transform.SetParent(root.transform, false);
                riel.transform.position = new Vector3(pos.x + lado * 0.8f, y + 1.05f, pos.z);
                riel.transform.localScale = new Vector3(0.08f, 0.08f, 4.3f);
                QuitarCollider(riel);
                Vestir(riel, _madera);
                for (int i = 0; i < 4; i++)
                {
                    GameObject poste = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    poste.name = "Poste";
                    poste.transform.SetParent(root.transform, false);
                    poste.transform.position = new Vector3(pos.x + lado * 0.8f, y + 0.8f, pos.z + (i - 1.5f) * 1.3f);
                    poste.transform.localScale = new Vector3(0.1f, 0.9f, 0.1f);
                    QuitarCollider(poste);
                    Vestir(poste, _madera);
                }
            }
        }

        private static Material Mat(Shader sh, Color color, Color emission, float smooth)
        {
            Material m = new Material(sh);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", emission);
                m.EnableKeyword("_EMISSION");
            }
            return m;
        }

        static Mesh _planoMesh;

        /// Disco con UV radial: el degradado se reparte en círculo y el borde del
        /// quad nunca se ve aunque el plano corte el terreno.
        private static Mesh DiscoCharcoMesh()
        {
            if (_planoMesh != null) return _planoMesh;
            const int seg = 24;
            var v = new Vector3[seg + 1];
            var uv = new Vector2[seg + 1];
            var tris = new int[seg * 3];
            v[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                v[i + 1] = new Vector3(x * 0.5f, 0f, z * 0.5f);
                uv[i + 1] = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % seg + 1;
            }
            var m = new Mesh { name = "disco_charco" };
            m.vertices = v;
            m.uv = uv;
            m.triangles = tris;
            var n = new Vector3[v.Length];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
            m.normals = n;
            _planoMesh = m;
            return m;
        }

        /// Charco de luz calida bajo cada farol: aditivo barato en vez de una luz
        /// dinamica (la GT 640 pierde ~8 FPS con dos point lights). Usa URP Lit
        /// con emision y base negra: el quad de particulas salia blanco en build.
        private static Material MatCharcoCalido()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) return null;
            Material m = new Material(s);
            Texture2D t = TexturaCharcoCalido();
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            Color negro = new Color(0f, 0f, 0f, 1f);
            m.color = negro;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", negro);
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", t);
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", new Color(1f, 0.92f, 0.75f) * 1.6f);
                m.EnableKeyword("_EMISSION");
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.renderQueue = 3000;
            return m;
        }

        private static Texture2D _charcoTex;

        private static Texture2D TexturaCharcoCalido()
        {
            if (_charcoTex != null) return _charcoTex;
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "charco_calido";
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
                    float a = 1f - Mathf.SmoothStep(0f, 1f, r);
                    a *= a;
                    // El degradado va en RGB, no en alfa: con blend aditivo el negro
                    // del borde no aporta y el charco se funde aunque el shader
                    // ignore la transparencia.
                    byte rr = (byte)Mathf.RoundToInt(155f * a);
                    byte gg = (byte)Mathf.RoundToInt(82f * a);
                    byte bb = (byte)Mathf.RoundToInt(30f * a);
                    px[y * n + x] = new Color32(rr, gg, bb, 255);
                }
            }
            t.SetPixels32(px);
            t.Apply(false, true);
            _charcoTex = t;
            return t;
        }

        /// Los hongos se acumulan en tres lotes (tallos, laminas y sombreros) en vez de
        /// crear dos primitivas por hongo: pasan de ~38 draw calls a 3 y el sombrero
        /// puede tener textura moteada y laminas sin costo extra.
        private void Hongo(Transform padre, Vector3 pos, float escala)
        {
            if (_lotesTallo == null)
            {
                _lotesTallo = new LoteMalla();
                _lotesCap = new LoteMalla();
                _lotesGills = new LoteMalla();
                _padreHongos = padre;
            }
            Quaternion giro = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            _lotesTallo.Agregar(TalloHongoMesh(), Matrix4x4.TRS(pos, giro,
                new Vector3(0.055f * escala, 0.6f * escala, 0.055f * escala)));
            _lotesGills.Agregar(LaminasHongoMesh(), Matrix4x4.TRS(
                pos + Vector3.up * (0.56f * escala), giro,
                new Vector3(0.2f * escala, 0.055f * escala, 0.2f * escala)));
            _lotesCap.Agregar(CapHongoMesh(), Matrix4x4.TRS(
                pos + Vector3.up * (0.6f * escala), giro,
                new Vector3(0.21f * escala, 0.14f * escala, 0.21f * escala)));
        }

        private void CrearHongos()
        {
            if (_lotesTallo == null) return;
            _lotesTallo.Crear("Hongos_tallos", _tallo, _padreHongos);
            _lotesGills.Crear("Hongos_laminas", _gills, _padreHongos);
            _lotesCap.Crear("Hongos_sombreros", _cap, _padreHongos);
        }

        static Mesh _talloHongo, _capHongo, _laminasHongo;

        /// Tallo afinado, abierto arriba para que lo tape el sombrero.
        private static Mesh TalloHongoMesh()
        {
            if (_talloHongo != null) return _talloHongo;
            const int seg = 10;
            var v = new Vector3[seg * 2];
            var uv = new Vector2[seg * 2];
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                v[i] = new Vector3(x, 0f, z);
                v[seg + i] = new Vector3(x * 0.62f, 1f, z * 0.62f);
                uv[i] = new Vector2(i / (float)seg, 0f);
                uv[seg + i] = new Vector2(i / (float)seg, 1f);
            }
            var t = new int[seg * 6];
            int k = 0;
            for (int i = 0; i < seg; i++)
            {
                int a = i, b = (i + 1) % seg;
                t[k++] = a; t[k++] = seg + a; t[k++] = b;
                t[k++] = b; t[k++] = seg + a; t[k++] = seg + b;
            }
            var m = new Mesh { name = "hongo_tallo" };
            m.vertices = v;
            m.uv = uv;
            m.triangles = t;
            m.RecalculateNormals();
            _talloHongo = m;
            return m;
        }

        /// Cúpula de 4 anillos y 12 caras: el sombrero deja de ser una esfera aplastada.
        private static Mesh CapHongoMesh()
        {
            if (_capHongo != null) return _capHongo;
            const int seg = 12;
            const int aros = 4;
            var v = new Vector3[aros * seg + 1];
            var uv = new Vector2[aros * seg + 1];
            for (int r = 0; r < aros; r++)
            {
                float fila = r / (float)aros;
                float rad = Mathf.Cos(fila * Mathf.PI * 0.5f);
                float y = Mathf.Sin(fila * Mathf.PI * 0.5f);
                for (int s = 0; s < seg; s++)
                {
                    float a = 2f * Mathf.PI * s / seg;
                    int i = r * seg + s;
                    v[i] = new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad);
                    uv[i] = new Vector2(Mathf.Cos(a) * rad * 0.5f + 0.5f,
                        Mathf.Sin(a) * rad * 0.5f + 0.5f);
                }
            }
            int top = aros * seg;
            v[top] = new Vector3(0f, 1f, 0f);
            uv[top] = new Vector2(0.5f, 0.5f);
            var t = new int[seg * (aros * 6 + 3) - seg * 6];
            int k = 0;
            for (int r = 0; r < aros - 1; r++)
            {
                for (int s = 0; s < seg; s++)
                {
                    int a = r * seg + s;
                    int b = r * seg + (s + 1) % seg;
                    int c = (r + 1) * seg + s;
                    int d = (r + 1) * seg + (s + 1) % seg;
                    t[k++] = a; t[k++] = c; t[k++] = b;
                    t[k++] = b; t[k++] = c; t[k++] = d;
                }
            }
            for (int s = 0; s < seg; s++)
            {
                int a = (aros - 1) * seg + s;
                int b = (aros - 1) * seg + (s + 1) % seg;
                t[k++] = a; t[k++] = top; t[k++] = b;
            }
            var m = new Mesh { name = "hongo_sombrero" };
            m.vertices = v;
            m.uv = uv;
            m.triangles = t;
            m.RecalculateNormals();
            _capHongo = m;
            return m;
        }

        /// Láminas: cono muy chato bajo el sombrero, con el centro hundido.
        private static Mesh LaminasHongoMesh()
        {
            if (_laminasHongo != null) return _laminasHongo;
            const int seg = 12;
            var v = new Vector3[seg + 1];
            var uv = new Vector2[seg + 1];
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                v[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                uv[i] = new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f);
            }
            v[seg] = new Vector3(0f, -1f, 0f);
            uv[seg] = new Vector2(0.5f, 0.5f);
            var t = new int[seg * 3];
            for (int i = 0; i < seg; i++)
            {
                t[i * 3] = seg;
                t[i * 3 + 1] = i;
                t[i * 3 + 2] = (i + 1) % seg;
            }
            var m = new Mesh { name = "hongo_laminas" };
            m.vertices = v;
            m.uv = uv;
            m.triangles = t;
            m.RecalculateNormals();
            _laminasHongo = m;
            return m;
        }

        static Texture2D _hongoTex;

        /// Base clara con manchas luminosas: se usa como albedo y como emisión, así el
        /// brillo del sombrero tambien tiene patron en vez de ser un color plano.
        private static Texture2D TexturaHongoCap()
        {
            if (_hongoTex != null) return _hongoTex;
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "cueva_hongo";
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = 0.62f + (Terrain3D.Valor(x * 0.09f + 5f, y * 0.09f + 9f) * 0.5f + 0.5f) * 0.18f;
                    for (int s = 0; s < 8; s++)
                    {
                        float cx = (s * 47 + 23) % n;
                        float cy = (s * 83 + 11) % n;
                        float dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cy);
                        dx = Mathf.Min(dx, n - dx);
                        dy = Mathf.Min(dy, n - dy);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float radio = 6f + (s % 3) * 4f;
                        if (d > radio) continue;
                        float borde = Mathf.Clamp01((radio - d) / 3.5f);
                        v = Mathf.Lerp(v, 1f, borde * borde * (3f - 2f * borde));
                    }
                    px[y * n + x] = new Color(v, v, v, 1f);
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            _hongoTex = t;
            return t;
        }

        private void Cristal(Transform padre, Vector3 pos, float radio, float alto, Material mat)
        {
            int seg = 8;
            Vector3[] v = new Vector3[seg + 1];
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                v[i] = new Vector3(Mathf.Cos(a) * radio, 0f, Mathf.Sin(a) * radio);
            }
            v[seg] = new Vector3(0f, alto, 0f);
            int[] t = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                t[i * 3] = i;
                t[i * 3 + 1] = (i + 1) % seg;
                t[i * 3 + 2] = seg;
                t[seg * 3 + i * 3] = (i + 1) % seg;
                t[seg * 3 + i * 3 + 1] = i;
                t[seg * 3 + i * 3 + 2] = seg;
            }
            Mesh m = new Mesh();
            m.vertices = v;
            m.triangles = t;
            m.RecalculateNormals();
            GameObject go = new GameObject("Cristal");
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-8f, 8f));
            go.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static void QuitarCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        private static void Vestir(GameObject go, Material mat)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
