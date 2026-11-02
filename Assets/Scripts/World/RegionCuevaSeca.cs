using UnityEngine;

namespace SlugOut.World
{
    /// Primera region de la Cueva Seca, construida por codigo sobre el terreno existente.
    ///
    /// No reemplaza nada: agrega props bajo un root propio y se apoya en Terrain3D.Altura
    /// para que todo quede asentado en el relieve. Tres microhabitats con silueta y luz
    /// propias (fumarolas, pozo humedo, cristales), caminos desde la plaza y un tunel en
    /// el muro que sugiere la region siguiente. Todo low-poly y sin sombras proyectadas
    /// en los props chicos: la maquina objetivo es una GT 640 de 2 GB.
    public class RegionCuevaSeca : MonoBehaviour
    {
        public const string NombreRegion = "LA CUEVA SECA";
        public const string DestinoTunel = "GALERIAS DEL ECO";

        /// Que region esta de pie ahora. El tunel la cambia y el mundo se reconstruye en el
        /// mismo sitio (comparten el relieve), asi que "viajar" no mueve al jugador por un mapa
        /// gigante: entra en otra cueva.
        public static int Actual { get; private set; }
        /// Sube cada vez que se reconstruye. El HUD cachea los marcadores de la region, y sin
        /// esto seguiria apuntando a los Transform destruidos de la region anterior.
        public static int Version { get; private set; }

        public static string NombreRegionActual { get { return Regiones.De(Actual).nombre; } }

        /// Nombre de una region por indice, para logs y para el cartel del minimapa.
        public static string NombreDe(int i) { return Regiones.De(i).nombre; }

        RegionDefinicion Def { get { return Regiones.De(Actual); } }

        /// Marcadores para el minimapa: el HUD no tiene que saber que regiones existen.
        public Transform[] MarcadoresRaiz { get; private set; }
        public string[] MarcadoresLetra { get; private set; }
        public Color[] MarcadoresColor { get; private set; }
        public string[] MarcadoresNombre { get; private set; }
        /// Bocas de tunel de la cueva actual, en el mismo orden que `Def.destinosTunel`.
        /// `TunelRaiz` es la primera, que es la que estaba verificada en Play y en el build.
        public Transform TunelRaiz { get; private set; }
        public Transform[] TunelesRaiz { get; private set; }
        /// Region de destino de cada boca, en el mismo orden que `TunelesRaiz`. El HUD
        /// necesita el nombre para el minimapa y para el aviso de "cruzа para viajar".
        public int[] DestinosTuneles { get; private set; }

        [SerializeField] private bool construir = true;
        [SerializeField] private float radio = 20f;
        [SerializeField] private float radioTunel = 23.2f;

        private Transform _root;
        private Material _roca, _rocaClara, _rocaPared, _camino, _brasa, _agua, _cristal, _madera;

        public int Microhabitats { get; private set; }
        public bool TieneTunel { get; private set; }
        public Transform Root { get { return _root; } }

        void OnDestroy()
        {
            DestruirMateriales();
            if (_instanciaActiva == this) _instanciaActiva = null;
        }

        private void Awake()
        {
            if (!construir) return;
            // Idempotente: recargar la escena o volver al menu no debe duplicar la region. Se busca
            // cualquier hijo "Region_..." en vez de una lista de nombres: con la tercera cueva, una
            // lista fija de nombres hubiera construido dos cuevas encima de la que ya estaba.
            if (_root != null) return;
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).name.StartsWith("Region_")) return;
            Construir();
        }

        /// Desarma la cueva actual y arma la que diga `destino`. Devuelve el punto por donde el
        /// jugador sale de la nueva region: la boca del tunel que apunta a la region de donde
        /// viene (`origen`), para que el viaje se lea como un pasaje y no como un teletransporte
        /// al azar. Sin origen se usa la primera boca, que es la que estaba verificada.
        public Vector3 Reconstruir(int destino, out Transform tunelDeSalida, int origen = -1)
        {
            Actual = Mathf.Clamp(destino, 0, Regiones.Todas.Length - 1);
            if (_root != null) Destroy(_root.gameObject);
            // Los materiales se crean con `new Material(...)` y Unity no los libera al perder la
            // referencia: sin esto, cada ida y vuelta dejaba ocho materiales huespedes vivos.
            DestruirMateriales();
            _materialesDelPaso.Clear();
            Microhabitats = 0;
            TieneTunel = false;
            Construir();
            Version++;
            Transform boca = origen >= 0 ? BuscarTunelHacia(origen) : null;
            if (boca == null) boca = TunelRaiz;
            tunelDeSalida = boca;
            return tunelDeSalida != null ? tunelDeSalida.position : EnPiso(0f, 0f);
        }

        Transform BuscarTunelHacia(int indiceRegion)
        {
            if (_root == null || indiceRegion < 0) return null;
            string nombre = "Tunel_" + Regiones.De(indiceRegion).nombre.Replace(' ', '_');
            return _root.Find(nombre);
        }

        /// Los materiales que creo esta componente en tiempo de ejecucion. Se guardan aca para
        /// poder destruirlos de verdad al cambiar de region.
        readonly System.Collections.Generic.List<Material> _materialesDelPaso =
            new System.Collections.Generic.List<Material>();

        void DestruirMateriales()
        {
            for (int i = 0; i < _materialesDelPaso.Count; i++)
                if (_materialesDelPaso[i] != null) Destroy(_materialesDelPaso[i]);
            _materialesDelPaso.Clear();
        }

        private void Materiales()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");

            RegionDefinicion d = Def;
            _roca = Nuevo(s, "roca", d.roca, 0f, 0.85f);
            _rocaClara = Nuevo(s, "roca_clara", d.rocaClara, 0f, 0.85f);
            _rocaPared = Nuevo(s, "roca_pared", d.rocaPared, 0f, 0.95f);
            _camino = Nuevo(s, "camino", d.camino, 0f, 0.95f);
            _madera = Nuevo(s, "madera", d.madera, 0f, 0.9f);
            _brasa = Nuevo(s, "brasa", d.acento, 0f, 0.4f, d.emisionAcento);
            _agua = Nuevo(s, "agua", d.agua, 0.1f, 0.15f, d.emisionAgua);
            _cristal = Nuevo(s, "cristal", d.cristal, 0.2f, 0.25f, d.emisionCristal);
            AplicarTexturaPiedra(LookCuevaPixar.PiedraCueva);
        }

        /// La misma piedra procedural en las rocas, paredes y caminos de la region: los
        /// tuneles y microhabitats dejan de ser primitivas de color plano.
        public void AplicarTexturaPiedra(Texture2D tex)
        {
            if (tex == null) return;
            LookCuevaPixar.AplicarPiedra(_roca, tex, 2.2f);
            LookCuevaPixar.AplicarPiedra(_rocaClara, tex, 2.2f);
            LookCuevaPixar.AplicarPiedra(_rocaPared, tex, 3f);
            LookCuevaPixar.AplicarPiedra(_camino, tex, 3f);
        }

        static RegionCuevaSeca _instanciaActiva;

        private static Material Nuevo(Shader s, string nombre, Color c, float metal, float rug,
                                      Color? emision = null)
        {
            var m = new Material(s) { name = "region_" + nombre, color = c };
            if (_instanciaActiva != null) _instanciaActiva._materialesDelPaso.Add(m);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f - rug);
            if (emision.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emision.Value * 1.4f);
            }
            return m;
        }

        // ------------------------------------------------------------------ construccion

        /// El relieve es parte de la region, no decoracion: se aplica antes de apoyar nada y el
        /// mesh del piso se regenera, asi la silueta que se ve es la misma que pisan el jugador,
        /// las salvajes, las rocas y la boca del tunel. Sin el corte de "ya esta igual", volver a
        /// la Cueva Seca reconstruiria el terreno en cada viaje por nada.
        private static void AplicarRelieve(RegionDefinicion d)
        {
            if (Terrain3D.DesfaseRegion == d.relieveDesfase &&
                Mathf.Approximately(Terrain3D.AmplitudRegion, d.relieveAmplitud)) return;
            Terrain3D.DesfaseRegion = d.relieveDesfase;
            Terrain3D.AmplitudRegion = d.relieveAmplitud;
            Terrain3D t = Terrain3D.Activo != null
                ? Terrain3D.Activo : Object.FindFirstObjectByType<Terrain3D>();
            if (t != null) t.Reconstruir();
            Debug.Log("[Region] relieve " + d.nombre + " desfase=" + d.relieveDesfase
                + " amplitud=" + d.relieveAmplitud.ToString("F2"));
        }

        /// La piedra del piso sale de la misma definicion que las rocas y los caminos. El
        /// tiling se calcula con el medio lado para conservar la densidad de veta si cambia
        /// el tamano del mesh, en vez de quedar con una textura estirada.
        private static void AplicarPaletaTerreno(RegionDefinicion d, Terrain3D t)
        {
            if (t == null) return;
            float tiling = Mathf.Max(4f, (t.MedioLado * 2f) / 4f);
            Color alto = Color.Lerp(d.rocaClara, Color.white, 0.10f);
            t.AplicarPaleta(d.roca, d.rocaClara, alto, LookCuevaPixar.PiedraCueva, tiling);
        }

        private void Construir()
        {
            _instanciaActiva = this;
            RegionDefinicion d = Def;
            Terrain3D terreno = Terrain3D.Activo != null
                ? Terrain3D.Activo : GetComponent<Terrain3D>();
            if (terreno == null) terreno = Object.FindFirstObjectByType<Terrain3D>();
            // El Terreno viene escalado 5x en la escena. Normalizar antes de leer lossyScale
            // evita que el root de la region quede compensando una escala que ya no existe.
            if (terreno != null) terreno.NormalizarEscala();
            radio = d.radioHabitat > 0.001f ? d.radioHabitat : radio;
            Materiales();
            AplicarRelieve(d);
            AplicarPaletaTerreno(d, terreno);
            var go = new GameObject("Region_" + Clave(d.nombre));
            _root = go.transform;
            _root.SetParent(transform, false);
            // La region vive bajo el "Terreno"; si el padre conserva escala, se cancela para
            // que los props se midan en metros. Con el terreno normalizado esto queda en 1.
            Vector3 escalaPadre = transform.lossyScale;
            _root.localScale = new Vector3(
                Mathf.Abs(escalaPadre.x) > 0.0001f ? 1f / escalaPadre.x : 1f,
                Mathf.Abs(escalaPadre.y) > 0.0001f ? 1f / escalaPadre.y : 1f,
                Mathf.Abs(escalaPadre.z) > 0.0001f ? 1f / escalaPadre.z : 1f);

            HabitatAt[] marcadores = new HabitatAt[d.habitats.Length];
            // Tres microhabitats alrededor de la plaza, cada uno en un angulo distinto para
            // que desde la camara se lean como zonas separadas y no como un solo decorado.
            Color[] luces = { d.luzHabitat1, d.luzHabitat2, d.luzHabitat3 };
            MarcadoresNombre = new string[d.habitats.Length];
            for (int i = 0; i < d.habitats.Length; i++)
            {
                HabitatDef h = d.habitats[i];
                Transform raiz = ConstruirHabitat(h, h.angulo + d.anguloBase,
                    i < luces.Length ? luces[i] : d.acento);
                marcadores[i] = new HabitatAt
                {
                    raiz = raiz,
                    letra = h.letra,
                    color = ColorHabitat(h.tipo, d)
                };
            }
            MarcadoresRaiz = new Transform[d.habitats.Length];
            MarcadoresLetra = new string[d.habitats.Length];
            MarcadoresColor = new Color[d.habitats.Length];
            for (int i = 0; i < marcadores.Length; i++)
            {
                MarcadoresRaiz[i] = marcadores[i].raiz;
                MarcadoresLetra[i] = marcadores[i].letra;
                MarcadoresColor[i] = marcadores[i].color;
                MarcadoresNombre[i] = d.habitats[i].legible;
            }

            Caminos(d);
            // Una boca por region vecina. El angulo 200 sigue siendo la primera boca de cada cueva,
            // que es la que esta verificada caminando en el build (Cueva Seca -> Galerias y vuelta).
            TunelRaiz = null;
            int[] destinos = d.destinosTunel ?? new int[0];
            float[] angulos = d.angulosTunel ?? new float[0];
            TunelesRaiz = new Transform[Mathf.Min(destinos.Length, angulos.Length)];
            DestinosTuneles = new int[TunelesRaiz.Length];
            for (int i = 0; i < DestinosTuneles.Length; i++) DestinosTuneles[i] = -1;
            for (int i = 0; i < TunelesRaiz.Length; i++)
            {
                if (destinos[i] < 0 || destinos[i] >= Regiones.Todas.Length || destinos[i] == Actual)
                {
                    Debug.LogError("[Region] " + d.nombre + ": boca de tunel " + i
                        + " con destino invalido (" + destinos[i] + ")");
                    continue;
                }
                Transform boca = Tunel(angulos[i] + d.anguloBase, destinos[i]);
                TunelesRaiz[i] = boca;
                DestinosTuneles[i] = destinos[i];
                if (TunelRaiz == null) TunelRaiz = boca;
            }
            Coberturas(d);
            BabosasSalvajes(d);
            Ambiente(d);
            // Ambientación mágica (motas, brillos, niebla móvil, pulso de luces). Vive en el
            // terreno, no bajo Region_*, así sobrevive a los viajes entre cuevas.
            AmbienteMagico amb = GetComponent<AmbienteMagico>();
            if (amb == null) amb = gameObject.AddComponent<AmbienteMagico>();
            amb.Aplicar(d, _root);
            BordeCueva();
            Plaza();
            Balizas(d);

            // Si Look ya existe (viaje entre cuevas) aplica su grupo/paleta ahora; en el
            // primer arranque se agrega despues de esta region y su Start aplica la actual.
            LookCuevaPixar look = GetComponent<LookCuevaPixar>();
            if (look != null) look.Aplicar(d);

            Debug.Log("[Region] " + d.nombre + " lista: " + Microhabitats
                + " microhabitats, tunel=" + TieneTunel);
        }

        struct HabitatAt { public Transform raiz; public string letra; public Color color; }

        static string Clave(string nombre)
        {
            if (nombre == "LA CUEVA SECA") return "CuevaSeca";
            if (nombre == "GALERIAS DEL ECO") return "GaleriasEco";
            return nombre.Replace(" ", "_");
        }

        static Color ColorHabitat(TipoHabitat t, RegionDefinicion d)
        {
            switch (t)
            {
                case TipoHabitat.Fumarolas: return new Color(1f, 0.55f, 0.2f);
                case TipoHabitat.PozoHumedo: return new Color(0.3f, 0.8f, 0.9f);
                case TipoHabitat.Cristales: return new Color(0.6f, 0.5f, 1f);
                case TipoHabitat.Estalactitas: return new Color(0.78f, 0.62f, 0.42f);
                case TipoHabitat.CharcoEco: return new Color(0.32f, 0.66f, 0.92f);
                case TipoHabitat.ChimeneaCeniza: return new Color(1f, 0.42f, 0.12f);
                case TipoHabitat.PuenteRoto: return new Color(0.82f, 0.70f, 0.45f);
                case TipoHabitat.LagunaAzufre: return new Color(0.78f, 0.85f, 0.22f);
                case TipoHabitat.Pagoda: return new Color(0.85f, 0.15f, 0.12f);
                case TipoHabitat.EstanqueKoi: return new Color(0.20f, 0.65f, 0.55f);
                case TipoHabitat.BosqueBambu: return new Color(0.45f, 0.75f, 0.30f);
                default: return d.acento;
            }
        }

        Transform ConstruirHabitat(HabitatDef h, float angulo, Color luz)
        {
            switch (h.tipo)
            {
                case TipoHabitat.Fumarolas: return Fumarolas(angulo, luz);
                case TipoHabitat.PozoHumedo: return PozoHumedo(angulo, luz);
                case TipoHabitat.Cristales: return Cristales(angulo, luz);
                case TipoHabitat.Estalactitas: return Estalactitas(angulo, luz);
                case TipoHabitat.CharcoEco: return CharcoEco(angulo, luz);
                case TipoHabitat.ColumnasRunas: return ColumnasRunas(angulo, luz);
                case TipoHabitat.ArcoRocoso: return ArcoRocoso(angulo, luz);
                case TipoHabitat.RaicesAnudadas: return RaicesAnudadas(angulo, luz);
                case TipoHabitat.ColadaEscalonada: return ColadaEscalonada(angulo, luz);
                case TipoHabitat.ChimeneaCeniza: return ChimeneaCeniza(angulo, luz);
                case TipoHabitat.PuenteRoto: return PuenteRoto(angulo, luz);
                case TipoHabitat.LagunaAzufre: return LagunaAzufre(angulo, luz);
                case TipoHabitat.Pagoda: return Pagoda(angulo, luz);
                case TipoHabitat.EstanqueKoi: return EstanqueKoi(angulo, luz);
                case TipoHabitat.BosqueBambu: return BosqueBambu(angulo, luz);
            }
            // Sin `default` a proposito: un valor nuevo de TipoHabitat sin silueta propia se cae
            // aca y se ve en consola, en vez de construir una ColumnasRunas disfrazada.
            Debug.LogError("[Region] silueta " + h.tipo + " no tiene constructor: se ve otra zona");
            return ColumnasRunas(angulo, luz);
        }

        private Vector3 EnPiso(float anguloGrados, float r, float alto = 0f)
        {
            float rad = anguloGrados * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad) * r, z = Mathf.Sin(rad) * r;
            return new Vector3(x, Terrain3D.Altura(x, z) + alto, z);
        }

        /// Cinta de ancho constante que sigue el relieve: cada par de vertices se apoya
        /// en Terrain3D.Altura, asi que el camino nunca queda por encima ni por debajo
        /// del suelo donde camina el personaje.
        private GameObject Cinta(Transform padre, string nombre, float angulo, float ancho,
                                 float alto, Material mat, bool sombras)
        {
            const float r0 = 5.0f;
            float r1 = radio - 0.5f;
            int pasos = Mathf.Max(2, Mathf.CeilToInt((r1 - r0) / 0.75f));
            var verts = new System.Collections.Generic.List<Vector3>((pasos + 1) * 2);
            var tris = new System.Collections.Generic.List<int>(pasos * 6);
            // El grupo de la region puede tener escala/rotacion: los vertices se pasan
            // a su espacio local para que la cinta quede exactamente sobre el terreno.
            Matrix4x4 aLocal = padre != null ? padre.worldToLocalMatrix : Matrix4x4.identity;
            for (int i = 0; i <= pasos; i++)
            {
                float r = Mathf.Lerp(r0, r1, i / (float)pasos);
                float a = (angulo + Mathf.Sin(r * 0.4f) * 4f) * Mathf.Deg2Rad;
                float cx = Mathf.Cos(a) * r, cz = Mathf.Sin(a) * r;
                float lx = -Mathf.Sin(a) * ancho * 0.5f;
                float lz = Mathf.Cos(a) * ancho * 0.5f;
                for (int lado = -1; lado <= 1; lado += 2)
                {
                    float x = cx + lx * lado, z = cz + lz * lado;
                    verts.Add(aLocal.MultiplyPoint3x4(
                        new Vector3(x, Terrain3D.Altura(x, z) + alto, z)));
                }
                if (i > 0)
                {
                    int b = (i - 1) * 2;
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                }
            }
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var malla = new Mesh();
            malla.SetVertices(verts);
            malla.SetTriangles(tris, 0);
            malla.RecalculateNormals();
            malla.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            go.AddComponent<MeshRenderer>();
            Vestir(go, mat, sombras);
            return go;
        }

        private GameObject Caja(Transform padre, string nombre, Vector3 tam, Vector3 pos,
                          Material mat, Vector3? rot = null, bool sombras = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            go.transform.localScale = tam;
            if (rot.HasValue) go.transform.rotation = Quaternion.Euler(rot.Value);
            Vestir(go, mat, sombras);
            return go;
        }

        static readonly System.Collections.Generic.Dictionary<string, Mesh> _mallasRoca =
            new System.Collections.Generic.Dictionary<string, Mesh>();

        /// Malla de roca facetada: anillos irregulares, sin colision propia. El proxy de
        /// cobertura sigue siendo la caja de capa 8, asi que el barrido y la piedra visible
        /// conservan la misma huella jugable sin encarecer la fisica.
        private static Mesh MallaRoca(string clave, Vector3 tam, int lados, int bandas, int semilla)
        {
            if (_mallasRoca.TryGetValue(clave, out Mesh guardada) && guardada != null) return guardada;
            var rnd = new System.Random(semilla);
            var anillos = new Vector3[(bandas + 1) * lados];
            var uvAnillos = new Vector2[anillos.Length];
            for (int a = 0; a <= bandas; a++)
            {
                float t = a / (float)bandas;
                float y = t * tam.y;
                float radio = Mathf.Lerp(0.5f, 0.42f, t);
                for (int i = 0; i < lados; i++)
                {
                    float ang = i / (float)lados * Mathf.PI * 2f;
                    float r = (radio + ((float)rnd.NextDouble() * 2f - 1f) * 0.08f) * tam.x;
                    float yJitter = a == 0 || a == bandas
                        ? 0f : ((float)rnd.NextDouble() * 2f - 1f) * 0.06f;
                    anillos[a * lados + i] = new Vector3(Mathf.Cos(ang) * r, y + yJitter,
                        Mathf.Sin(ang) * r * (tam.z / Mathf.Max(0.0001f, tam.x)));
                    uvAnillos[a * lados + i] = new Vector2(i / (float)lados, t);
                }
            }

            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            void Tri(Vector3 p0, Vector3 p1, Vector3 p2,
                     Vector2 u0, Vector2 u1, Vector2 u2)
            {
                int n = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                uvs.Add(u0); uvs.Add(u1); uvs.Add(u2);
                tris.Add(n); tris.Add(n + 1); tris.Add(n + 2);
            }

            for (int a = 0; a < bandas; a++)
            {
                for (int i = 0; i < lados; i++)
                {
                    int b = (i + 1) % lados;
                    int i0 = a * lados + i, i1 = a * lados + b;
                    int i2 = (a + 1) * lados + b, i3 = (a + 1) * lados + i;
                    // Triangulos con vertices propios: la roca queda facetada, no una columna lisa.
                    Tri(anillos[i0], anillos[i2], anillos[i1],
                        uvAnillos[i0], uvAnillos[i2], uvAnillos[i1]);
                    Tri(anillos[i0], anillos[i3], anillos[i2],
                        uvAnillos[i0], uvAnillos[i3], uvAnillos[i2]);
                }
            }
            int baseArriba = bandas * lados;
            for (int i = 0; i < lados; i++)
            {
                int b = (i + 1) % lados;
                Tri(Vector3.zero, anillos[i], anillos[b],
                    new Vector2(0.5f, 0.5f), uvAnillos[i], uvAnillos[b]);
                Tri(new Vector3(0f, tam.y, 0f), anillos[baseArriba + b],
                    anillos[baseArriba + i], new Vector2(0.5f, 0.5f),
                    uvAnillos[baseArriba + b], uvAnillos[baseArriba + i]);
            }

            var malla = new Mesh { name = clave };
            malla.SetVertices(verts);
            malla.SetUVs(0, uvs);
            malla.SetTriangles(tris, 0);
            malla.RecalculateNormals();
            malla.RecalculateBounds();
            _mallasRoca[clave] = malla;
            return malla;
        }

        private GameObject Roca(Transform padre, string nombre, Vector3 tam, Vector3 pos,
            Material mat, int lados, int bandas, int semilla)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            go.transform.rotation = padre.rotation;
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = MallaRoca(nombre + "_" + semilla + "_" +
                tam.x.ToString("F2") + "_" + tam.y.ToString("F2") + "_" + tam.z.ToString("F2"),
                tam, lados, bandas, semilla);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private GameObject Cilindro(Transform padre, string nombre, float r, float alto,
                                    Vector3 pos, Material mat, Vector3? rot = null,
                                    bool sombras = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = nombre;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(r, alto * 0.5f, r);
            if (rot.HasValue) go.transform.rotation = Quaternion.Euler(rot.Value);
            Vestir(go, mat, sombras);
            return go;
        }

        private void Esfera(Transform padre, string nombre, float r, Vector3 pos, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = nombre;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * r;
            Vestir(go, mat, false);
        }

        private void Cono(Transform padre, string nombre, float r, float alto, Vector3 pos,
                          Material mat, Vector3? rot = null)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            if (rot.HasValue) go.transform.rotation = Quaternion.Euler(rot.Value);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = ConoMesh();
            go.transform.localScale = new Vector3(r * 2f, alto, r * 2f);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Mesh _cono;

        private static Mesh ConoMesh()
        {
            if (_cono != null) return _cono;
            var m = new Mesh { name = "cono_lowpoly" };
            const int lados = 7;
            var v = new Vector3[lados + 2];
            v[0] = new Vector3(0f, 0f, 0f);
            v[lados + 1] = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < lados; i++)
            {
                float a = i / (float)lados * Mathf.PI * 2f;
                v[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f);
            }
            var tris = new int[lados * 2 * 3];
            int k = 0;
            for (int i = 0; i < lados; i++)
            {
                int a = i + 1, b = (i + 1) % lados + 1;
                tris[k++] = a; tris[k++] = b; tris[k++] = lados + 1;
                tris[k++] = b; tris[k++] = a; tris[k++] = 0;
            }
            m.vertices = v;
            m.triangles = tris;
            m.RecalculateNormals();
            _cono = m;
            return m;
        }

        private void Vestir(GameObject go, Material mat, bool sombras)
        {
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = sombras
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// La escena original solo tenia una luz puntual blanca y el cielo azul de la skybox:
        /// el piso se leia plano y azul. Una direccional calida y un ambiente de roca mantienen
        /// legibles caminos y siluetas, sin sombras dinamicas (GT 640).
        private void Ambiente(RegionDefinicion d)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = d.ambiente;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = d.nieblaInicio;
            RenderSettings.fogEndDistance = d.nieblaFin;
            RenderSettings.fogColor = d.niebla;

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = d.fondoCamara;
            }

            var go = new GameObject("Luz_Cueva");
            go.transform.SetParent(_root, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = d.luzDireccional;
            l.intensity = d.intensidadLuz;
            l.shadows = LightShadows.None;   // GT 640: el contraste viene de materiales, no de sombras
            l.transform.rotation = Quaternion.Euler(52f, -34f, 0f);
        }

        /// Las cuatro paredes de la escena eran cajas grises lisas. Se visten de roca en runtime
        /// y se corona el borde con bloques irregulares: leen como caverna sin tocar la escena.
        private void BordeCueva()
        {
            string[] paredes = { "Pared_Oeste", "Pared_Este", "Pared_Norte", "Pared_Sur" };
            for (int i = 0; i < paredes.Length; i++)
            {
                GameObject p = GameObject.Find(paredes[i]);
                if (p == null) continue;
                p.layer = ColisionJugador.CapaMuros;
                // La caja de la escena mide 3 m y el relieve llega a ~2,7 m en el borde: montada
                // la capsula de 2 m sobre una loma, el barrido pasaba por encima del muro. Se
                // entierra 1 m y se levanta hasta 7, mas alto que cualquier loma de la arena.
                Transform t = p.transform;
                Vector3 espesor = t.localScale;
                t.position = new Vector3(t.position.x, 3f, t.position.z);
                espesor.y = 8f;
                t.localScale = espesor;
                Renderer r = p.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = _rocaPared;
            }

            for (int s = 0; s < 4; s++)
            {
                float baseAng = s * 90f;
                for (int i = 0; i < 6; i++)
                {
                    float t = (i + 0.5f) / 6f;
                    float x, z;
                    if (s == 0) { x = Mathf.Lerp(-23f, 23f, t); z = 24.6f; }
                    else if (s == 2) { x = Mathf.Lerp(23f, -23f, t); z = -24.6f; }
                    else if (s == 1) { x = 24.6f; z = Mathf.Lerp(-23f, 23f, t); }
                    else { x = -24.6f; z = Mathf.Lerp(23f, -23f, t); }
                    float alto = 0.9f + (i % 3) * 0.45f;
                    // El padre Terreno esta escalado 5x: estas lajas quedaban enormes
                    // y el jugador las atravesaba. El muro visible del anillo las releva.
                    GameObject borde = Caja(_root, "borde_" + s + "_" + i,
                         new Vector3(2.2f + (i % 2) * 1.1f, alto, 1.8f + (i % 2) * 0.8f),
                         new Vector3(x, 3.05f + alto * 0.5f, z),
                         i % 2 == 0 ? _roca : _rocaClara,
                         new Vector3(0f, baseAng + (i - 2) * 6f, (i % 2 == 0 ? 4f : -3f)), true);
                    borde.SetActive(false);
                }
            }

            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * 360f;
                Vector3 p = EnPiso(a, 22f);
                GameObject roca = Caja(_root, "roca_pared_" + i, new Vector3(2.4f, 1.4f, 1.8f),
                     p + Vector3.up * 0.55f, _roca,
                     new Vector3(0f, a + 18f, 8f), true);
                roca.SetActive(false);
            }
        }

        /// Plaza de piedra clara en el centro: da un punto de partida legible y une los caminos.
        private void Plaza()
        {
            Transform g = Grupo("Plaza", EnPiso(0f, 0f, 0.02f));
            // El personaje se apoya en Terrain3D.Altura; una losa con la cara superior
            // a 6 u 8 cm lo hacia caminar hundido hasta la rodilla del pie. Se entierra
            // la piedra dejando solo 1,5 cm de labio.
            Cilindro(g, "disco", 3.2f, 0.06f, EnPiso(0f, 0f, -0.015f), _camino);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * 360f;
                Vector3 p = EnPiso(a, 4.2f, -0.035f);
                Caja(g, "borde_plaza_" + i, new Vector3(1.8f, 0.10f, 0.8f), p, _rocaClara,
                     new Vector3(0f, a + 90f, 0f), true);
            }
        }

        /// Balizas frente a cada camino: una piedra con una brasa del color del bioma. Una por
        /// microhabitat y una por boca de tunel, con el angulo real de cada una.
        private void Balizas(RegionDefinicion d)
        {
            int[] destinos = d.destinosTunel ?? new int[0];
            float[] angulos = d.angulosTunel ?? new float[0];
            int bocas = Mathf.Min(destinos.Length, angulos.Length);
            int n = d.habitats.Length + bocas;
            for (int i = 0; i < n; i++)
            {
                float ang; Material mat;
                if (i < d.habitats.Length)
                {
                    ang = d.habitats[i].angulo + d.anguloBase;
                    mat = MaterialHabitat(d.habitats[i].tipo);
                }
                else { ang = angulos[i - d.habitats.Length] + d.anguloBase; mat = _brasa; }
                Vector3 p = EnPiso(ang, 6.2f);
                Transform g = Grupo("Baliza_" + i, p);
                Cilindro(g, "poste", 0.12f, 1.3f, p + Vector3.up * 0.65f, _rocaClara);
                Esfera(g, "brasa", 0.16f, p + Vector3.up * 1.4f, mat);
            }
        }

        private Transform Grupo(string nombre, Vector3 pos)
        {
            var g = new GameObject(nombre);
            g.transform.SetParent(_root, false);
            g.transform.position = pos;
            return g.transform;
        }

        /// Zona de fuego: chimeneas de basalto y brasas al pie. Silueta vertical y luz calida.
        private Transform Fumarolas(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_Fumarolas", EnPiso(angulo, radio));
            Caja(g, "plataforma", new Vector3(6.5f, 0.45f, 6.5f), g.position + Vector3.up * 0.18f,
                 _rocaClara, new Vector3(0f, angulo, 0f));
            for (int i = 0; i < 3; i++)
            {
                float dx = (i - 1) * 1.5f;
                Cono(g, "chimenea_" + i, 0.95f - i * 0.18f, 3.2f - i * 0.7f,
                     g.position + new Vector3(dx, 0.25f, (i % 2) * 0.6f), _roca);
                Esfera(g, "brasa_" + i, 0.32f,
                       g.position + new Vector3(dx, 0.35f, (i % 2) * 0.6f + 1.1f), _brasa);
            }
            Luz(g, "luz_habitat", luz, 7f, 5f, g.position + Vector3.up * 2.2f);
            Microhabitats++;
            return g;
        }

        /// Zona de agua: cubeta, juncos y un borde de musgo. Luz fria y reflejo verdoso.
        private Transform PozoHumedo(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_PozoHumedo", EnPiso(angulo, radio));
            Cilindro(g, "cubeta", 2.6f, 0.18f, g.position + Vector3.up * 0.05f, _agua);
            Cilindro(g, "borde", 2.85f, 0.34f, g.position + Vector3.up * 0.10f, _rocaClara);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Vector3 p = g.position + new Vector3(Mathf.Cos(a) * 3.1f, 0.25f, Mathf.Sin(a) * 3.1f);
                Cilindro(g, "junco_" + i, 0.06f, 1.1f + (i % 3) * 0.25f,
                         p + Vector3.up * 0.55f, _madera,
                         new Vector3(0f, a * Mathf.Rad2Deg, (i % 2 == 0 ? 4f : -5f)));
            }
            Luz(g, "luz_habitat", luz, 5f, 4.5f, g.position + Vector3.up * 1.6f);
            Microhabitats++;
            return g;
        }

        /// Zona electrica: racimo de cristales altos. La silueta mas alta de la region
        /// para que sirva de punto de referencia desde cualquier lado de la plaza.
        private Transform Cristales(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_Cristales", EnPiso(angulo, radio));
            Caja(g, "base", new Vector3(5f, 0.4f, 5f), g.position + Vector3.up * 0.15f,
                 _roca, new Vector3(0f, angulo + 20f, 0f));
            for (int i = 0; i < 5; i++)
            {
                float dx = (i - 2) * 0.9f;
                float alto = 2.6f + (i == 2 ? 1.8f : 0f) + (i % 2) * 0.5f;
                Cono(g, "cristal_" + i, 0.42f, alto,
                     g.position + new Vector3(dx, 0.3f, (i % 3 - 1) * 0.7f), _cristal,
                     new Vector3((i % 2 == 0 ? 6f : -7f), 0f, dx * 4f));
            }
            Luz(g, "luz_habitat", luz, 6f, 5f, g.position + Vector3.up * 2.4f);
            Microhabitats++;
            return g;
        }

        /// Techo que gotea: columnas colgando sobre un monticulo de ripio. Es la silueta
        /// invertida de Fumarolas, y esa inversion es lo que hace que el jugador sepa que
        /// cambio de zona sin leer un cartel.
        private Transform Estalactitas(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_Estalactitas", EnPiso(angulo, radio));
            Caja(g, "plataforma", new Vector3(6.5f, 0.45f, 6.5f), g.position + Vector3.up * 0.18f,
                 _rocaClara, new Vector3(0f, angulo, 0f));
            for (int i = 0; i < 5; i++)
            {
                float dx = (i - 2) * 1.15f;
                float alto = 2.4f + (i % 2) * 1.1f;
                // boca abajo: el cono cuelga del techo de piedra que se sugiere arriba
                Cono(g, "estalactita_" + i, 0.5f, alto,
                     g.position + new Vector3(dx, 0.45f + alto * 0.5f, (i % 3 - 1) * 0.8f), _roca,
                     new Vector3(180f, 0f, (i % 2 == 0 ? 5f : -6f)));
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * 360f;
                Caja(g, "ripio_" + i, new Vector3(0.7f, 0.35f, 0.6f),
                     g.position + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 2.4f, 0.35f,
                                              Mathf.Sin(a * Mathf.Deg2Rad) * 2.4f),
                     i % 2 == 0 ? _roca : _rocaClara, new Vector3(0f, a, 6f), true);
            }
            Luz(g, "luz_habitat", luz, 6.5f, 4.5f,
                g.position + Vector3.up * 2.6f);
            Microhabitats++;
            return g;
        }

        /// Charco quieto con anillos concentricos tallados en la piedra: el "eco" se lee como
        /// ondas que salen del centro. Superficie baja y ancha, contraste con la vertical del
        /// resto de la galeria.
        private Transform CharcoEco(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_CharcoEco", EnPiso(angulo, radio));
            Cilindro(g, "cubeta", 3.1f, 0.14f, g.position + Vector3.up * 0.04f, _agua);
            Cilindro(g, "borde", 3.4f, 0.3f, g.position + Vector3.up * 0.10f, _rocaClara);
            for (int i = 0; i < 3; i++)
            {
                float r = 1.0f + i * 0.75f;
                Cilindro(g, "anillo_" + i, r, 0.05f, g.position + Vector3.up * 0.16f, _rocaClara);
            }
            Esfera(g, "nudo", 0.26f, g.position + Vector3.up * 0.3f, _cristal);
            for (int i = 0; i < 5; i++)
            {
                float a = (i / 5f) * 360f + 22f;
                Vector3 p = g.position + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 3.9f, 0f,
                                                     Mathf.Sin(a * Mathf.Deg2Rad) * 3.9f);
                Caja(g, "losa_" + i, new Vector3(1.1f, 0.22f, 0.8f), p + Vector3.up * 0.15f,
                     _camino, new Vector3(0f, a, 0f), true);
            }
            Luz(g, "luz_habitat", luz, 6f, 4.5f,
                g.position + Vector3.up * 1.5f);
            Microhabitats++;
            return g;
        }

        /// Pilares grabados que brillan de a uno: la silueta mas regular de las tres zonas, y
        /// por eso la que mejor se reconoce de lejos como "esta es la galeria".
        private Transform ColumnasRunas(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_ColumnasRunas", EnPiso(angulo, radio));
            Caja(g, "base", new Vector3(5.6f, 0.35f, 5.6f), g.position + Vector3.up * 0.12f,
                 _roca, new Vector3(0f, angulo + 15f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float a = (i / 4f) * 360f + 45f;
                Vector3 p = g.position + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 1.9f, 0f,
                                                     Mathf.Sin(a * Mathf.Deg2Rad) * 1.9f);
                float alto = 3.1f + (i % 2) * 0.9f;
                Cilindro(g, "pilar_" + i, 0.42f, alto * 0.5f, p + Vector3.up * (alto * 0.5f + 0.2f),
                         _rocaClara, new Vector3(0f, a, (i % 2 == 0 ? 2f : -2f)));
                Esfera(g, "runa_" + i, 0.2f, p + Vector3.up * (alto * 0.72f), _brasa);
            }
            Luz(g, "luz_habitat", luz, 6.5f, 5f,
                g.position + Vector3.up * 2.4f);
            Microhabitats++;
            return g;
        }

        /// Arco de piedra natural: dos pies, una clave y luz pasando por debajo. Es la unica
        /// silueta del mundo con un hueco abierto, y por eso se reconoce de lejos.
        private Transform ArcoRocoso(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_ArcoRocoso", EnPiso(angulo, radio));
            g.rotation = Quaternion.Euler(0f, -angulo, 0f);
            Caja(g, "pie_izq", new Vector3(1.1f, 2.6f, 1.4f),
                 g.position + g.right * -1.9f + Vector3.up * 1.3f, _roca, new Vector3(0f, 8f, 3f), true);
            Caja(g, "pie_der", new Vector3(1.1f, 2.6f, 1.4f),
                 g.position + g.right * 1.9f + Vector3.up * 1.3f, _roca, new Vector3(0f, -8f, -3f), true);
            // La clave se apoya en los dos pies con un pequeno vuelco: leida como arco, no como
            // dintel (el del tunel es recto y va sobre pilares mas finos).
            Caja(g, "clave", new Vector3(5.4f, 0.9f, 1.5f), g.position + Vector3.up * 3.0f,
                 _rocaClara, new Vector3(0f, 0f, 1.5f), true);
            Caja(g, "derrumbe", new Vector3(1.3f, 0.5f, 1.1f),
                 g.position + g.right * 2.9f + Vector3.up * 0.3f, _roca, new Vector3(0f, 24f, 0f), true);
            Esfera(g, "brasa_arco", 0.24f, g.position + Vector3.up * 0.4f + g.forward * -1.1f, _cristal);
            for (int i = 0; i < 3; i++)
            {
                float lado = i == 1 ? 0f : (i == 0 ? -3.4f : 3.4f);
                Caja(g, "piedra_" + i, new Vector3(0.9f, 0.35f, 0.9f),
                     g.position + g.right * lado + Vector3.up * 0.2f, _rocaClara,
                     new Vector3(0f, angulo + i * 30f, 0f), true);
            }
            // La luz va DEL OTRO LADO del arco: es lo que hace que el hueco se lea como paso.
            Luz(g, "luz_habitat", luz, 7.5f, 5.5f, g.position + g.forward * -2.6f + Vector3.up * 1.6f);
            Microhabitats++;
            return g;
        }

        /// Raices que cuelgan en arco hasta un piso humedo: la silueta es un domo de curvas, y el
        /// material es madera (unico en el mundo) para que se distinga de la roca.
        private Transform RaicesAnudadas(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_RaicesAnudadas", EnPiso(angulo, radio));
            Cilindro(g, "piso_humedo", 3.3f, 0.09f, g.position + Vector3.up * 0.06f, _agua);
            Esfera(g, "tronco", 1.25f, g.position + Vector3.up * 2.6f, _madera);
            for (int i = 0; i < 6; i++)
            {
                float a = (i / 6f) * 360f + 12f;
                Vector3 dir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f,
                                          Mathf.Sin(a * Mathf.Deg2Rad));
                // Cada raiz es un arco de tres tramos: sale del tronco, se abre y toca piso.
                for (int s = 0; s < 3; s++)
                {
                    float t = (s + 1) / 3f;
                    Vector3 p = g.position + dir * (1.1f + t * 2.5f)
                        + Vector3.up * (2.3f - t * t * 2.2f);
                    Caja(g, "raiz_" + i + "_" + s, new Vector3(0.34f, 0.34f, 1.5f), p, _madera,
                         new Vector3(0f, -a * Mathf.Rad2Deg, -28f * t), true);
                }
                Esfera(g, "nudo_" + i, 0.2f, g.position + dir * 3.7f + Vector3.up * 0.22f, _cristal);
            }
            Luz(g, "luz_habitat", luz, 6.5f, 4.5f, g.position + Vector3.up * 2.2f);
            Microhabitats++;
            return g;
        }

        /// Cascada escalonada: cilindros que se achican y se apilan con laminas de agua entre
        /// escalon y escalon. Vertical y apilada, que es lo que no tiene ninguna otra zona.
        private Transform ColadaEscalonada(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_ColadaEscalonada", EnPiso(angulo, radio));
            float r = 3.5f, y = 0f;
            for (int i = 0; i < 5; i++)
            {
                float alto = 0.5f + i * 0.06f;
                y += alto * 0.5f;
                Cilindro(g, "escalon_" + i, r, alto * 0.5f, g.position + Vector3.up * y, _rocaClara);
                y += alto * 0.5f;
                if (i < 4)
                {
                    // Lamina de agua que desborda el borde del escalon de arriba.
                    Cilindro(g, "lamina_" + i, r * 0.86f, 0.05f, g.position + Vector3.up * (y + 0.06f),
                             _agua);
                }
                r -= 0.62f;
            }
            Esfera(g, "manantial", 0.42f, g.position + Vector3.up * (y + 0.3f), _cristal);
            for (int i = 0; i < 4; i++)
            {
                float a = (i / 4f) * 360f + 45f;
                Vector3 p = g.position + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 4.1f, 0f,
                                                     Mathf.Sin(a * Mathf.Deg2Rad) * 4.1f);
                Caja(g, "poza_" + i, new Vector3(1.2f, 0.22f, 1.2f), p + Vector3.up * 0.1f, _roca,
                     new Vector3(0f, a, 0f), true);
            }
            Luz(g, "luz_habitat", luz, 7f, 5f, g.position + Vector3.up * (y * 0.7f));
            Microhabitats++;
            return g;
        }

        private void Luz(Transform padre, string nombre, Color color, float rango, float intensidad,
                         Vector3 pos)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = rango;
            l.intensity = intensidad;
            l.shadows = LightShadows.None;   // sin sombras dinamicas: GT 640
        }

        /// A que angulos sale un camino de la plaza: uno por microhabitat y uno por boca de tunel,
        /// leidos de la definicion. Es una funcion y no un arreglo literal para que la prueba pueda
        /// exigir que coincide con las zonas reales de ESA cueva (antes estaba hardcodeado con los
        /// angulos de La Cueva Seca y en las otras dos el camino apuntaba al lado equivocado).
        public static float[] AngulosCamino(RegionDefinicion d)
        {
            int[] destinosT = d.destinosTunel ?? new int[0];
            float[] angulosT = d.angulosTunel ?? new float[0];
            int bocas = Mathf.Min(destinosT.Length, angulosT.Length);
            float[] sal = new float[d.habitats.Length + bocas];
            for (int i = 0; i < d.habitats.Length; i++)
                sal[i] = d.habitats[i].angulo + d.anguloBase;
            for (int i = 0; i < bocas; i++)
                sal[d.habitats.Length + i] = angulosT[i] + d.anguloBase;
            return sal;
        }

        /// Placas que salen de la plaza hacia cada microhabitat y hacia cada boca de tunel.
        /// Ojo: antes los angulos estaban hardcodeados con los de La Cueva Seca (12/140/252/200),
        /// o sea que en las demas cuevas el camino apuntaba al lado equivocado. Ahora se leen de
        /// la definicion de la region que esta de pie, y `CaminosApuntanALasZonas` lo fija.
        private void Caminos(RegionDefinicion d)
        {
            Transform g = new GameObject("Caminos").transform;
            g.SetParent(_root, false);
            float[] destinos = AngulosCamino(d);
            for (int dd = 0; dd < destinos.Length; dd++)
            {
                float destino = destinos[dd];
                // Cintas que siguen la altura del terreno vertice a vertice. Las lajas
                // rigidas de 2 m flotaban o se enterraban en las laderas: el personaje
                // apoya en Terrain3D.Altura y la piedra no coincidia con el piso.
                Cinta(g, "cinta_borde", destino, 1.75f, 0.005f, _roca, false);
                Cinta(g, "cinta_camino", destino, 1.25f, 0.015f, _camino, true);
            }
        }

        /// Chimenea de ceniza: una torre partida y desplazada con la boca incandescente. La
        /// vertical no es la de Fumarolas (tres conos juntos) ni la de Cristales (racimo): el
        /// quiebre del fuste es lo que la hace legible de lejos.
        private Transform ChimeneaCeniza(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_ChimeneaCeniza", EnPiso(angulo, radio));
            Caja(g, "crater", new Vector3(5.6f, 0.5f, 5.6f), g.position + Vector3.up * 0.2f,
                 _roca, new Vector3(0f, angulo, 0f));
            Caja(g, "fuste_bajo", new Vector3(1.9f, 2.4f, 1.9f), g.position + Vector3.up * 1.4f,
                 _rocaClara, new Vector3(0f, angulo + 18f, 3f), true);
            Caja(g, "fuste_alto", new Vector3(1.3f, 2.0f, 1.3f),
                 g.position + new Vector3(0.55f, 3.4f, 0.25f), _roca,
                 new Vector3(0f, angulo - 12f, -9f), true);
            Esfera(g, "boca", 0.55f, g.position + new Vector3(0.8f, 4.45f, 0.35f), _brasa);
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * 360f + 30f;
                Caja(g, "escombro_" + i, new Vector3(0.8f, 0.3f, 0.6f),
                     g.position + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 2.6f, 0.25f,
                                              Mathf.Sin(a * Mathf.Deg2Rad) * 2.6f),
                     i % 2 == 0 ? _roca : _rocaClara, new Vector3(0f, a, 7f), true);
            }
            Luz(g, "luz_habitat", luz, 7f, 5f, g.position + Vector3.up * 2.6f);
            Microhabitats++;
            return g;
        }

        /// Puente de piedra quebrado sobre una grieta: dos estribos de distinta altura, una losa
        /// caida y el hueco oscuro en el medio. El unico habitat que se cruza por arriba.
        private Transform PuenteRoto(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_PuenteRoto", EnPiso(angulo, radio));
            g.rotation = Quaternion.Euler(0f, -angulo, 0f);
            Caja(g, "grieta", new Vector3(3.2f, 0.12f, 1.5f), g.position + Vector3.up * 0.02f,
                 _rocaPared);
            Caja(g, "estribo_izq", new Vector3(1.3f, 2.8f, 1.7f),
                 g.position + g.right * -2.6f + Vector3.up * 1.4f, _roca,
                 new Vector3(0f, 0f, 2f), true);
            Caja(g, "estribo_der", new Vector3(1.2f, 2.1f, 1.6f),
                 g.position + g.right * 2.5f + Vector3.up * 1.05f, _rocaClara,
                 new Vector3(0f, 0f, -3f), true);
            Caja(g, "losa_caida", new Vector3(4.4f, 0.35f, 1.3f),
                 g.position + g.right * -0.4f + Vector3.up * 1.15f, _rocaClara,
                 new Vector3(0f, -8f, 14f), true);
            Caja(g, "losa_rota", new Vector3(1.8f, 0.3f, 1.1f),
                 g.position + g.right * 1.5f + Vector3.up * 0.35f, _roca,
                 new Vector3(0f, 22f, -6f), true);
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * 360f + 20f;
                Cilindro(g, "poste_" + i, 0.09f, 1.2f,
                         g.position + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 3.4f, 0.6f,
                                                  Mathf.Sin(a * Mathf.Deg2Rad) * 3.4f),
                         _madera, new Vector3(0f, a, (i % 2 == 0 ? 3f : -3f)));
            }
            Luz(g, "luz_habitat", luz, 6.5f, 4.5f, g.position + Vector3.up * 2.4f);
            Microhabitats++;
            return g;
        }

        /// Laguna de azufre: cubeta elevada, anillos, burbujas y un penacho que sale de una
        /// grieta. La unica zona con el agua por encima del piso.
        private Transform LagunaAzufre(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_LagunaAzufre", EnPiso(angulo, radio));
            Cilindro(g, "borde", 3.8f, 0.5f, g.position + Vector3.up * 0.2f, _rocaClara);
            Cilindro(g, "laguna", 3.35f, 0.2f, g.position + Vector3.up * 0.48f, _agua);
            for (int i = 0; i < 3; i++)
                Cilindro(g, "anillo_" + i, 0.9f + i * 0.85f, 0.04f,
                         g.position + Vector3.up * 0.6f, _cristal);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * 360f + 15f;
                Esfera(g, "burbuja_" + i, 0.12f + (i % 3) * 0.04f,
                       g.position + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * (1.2f + (i % 2) * 1.0f),
                                                0.66f, Mathf.Sin(a * Mathf.Deg2Rad) * (1.2f + (i % 2) * 1.0f)),
                       _cristal);
            }
            Caja(g, "grieta_vapor", new Vector3(0.9f, 1.8f, 0.9f),
                 g.position + Vector3.up * 1.0f, _roca, new Vector3(0f, 0f, 8f), true);
            Esfera(g, "penacho", 0.55f, g.position + Vector3.up * 2.1f, _brasa);
            Luz(g, "luz_habitat", luz, 7f, 5f, g.position + Vector3.up * 1.8f);
            Microhabitats++;
            return g;
        }

        /// Pilares de cobertura de la region (solo las que los declaran). Se ven y frenan al
        /// jugador; el proxy va en capa 8 para que ademas corte la linea de tiro.
        private void Coberturas(RegionDefinicion d)
        {
            if (d.coberturas == null || d.coberturas.Length == 0) return;
            Transform muros = MurosCueva.Raiz(_root);
            for (int i = 0; i < d.coberturas.Length; i++)
            {
                CoberturaDef c = d.coberturas[i];
                float ang = c.angulo + d.anguloBase;
                Vector3 p = EnPiso(ang, c.radio);
                Transform g = Grupo("Cobertura_" + i, p);
                g.rotation = Quaternion.Euler(0f, -ang, 0f);
                int semilla = i * 37 + d.nombre.Length * 11 + Mathf.RoundToInt(ang);
                Roca(g, "base", new Vector3(c.tam.x * 1.3f, 0.35f, c.tam.z * 1.3f),
                     p, _rocaClara, 8, 1, semilla);
                Roca(g, "pilar", c.tam, p, _roca, 7, 3, semilla + 1);
                Roca(g, "tapa", new Vector3(c.tam.x * 1.1f, 0.28f, c.tam.z * 1.1f),
                     p + Vector3.up * c.tam.y, _rocaClara, 8, 1, semilla + 2);
                MurosCueva.Caja(muros, "muro_cobertura_" + i,
                    p + Vector3.up * (c.tam.y * 0.5f),
                    Quaternion.Euler(0f, -ang, 0f), c.tam, 8);
            }
        }

        /// Una salvaje por microhabitat, de la especie que pega con el bioma. No pelean:
        /// el objetivo es observarlas y ganarse su confianza.
        ///
        /// Los ids tienen que existir en el roster Y tener malla en Resources/Slugs: estaban
        /// "fuego"/"agua"/"rayo", que no estan en ninguna de las dos listas, asi que las unicas
        /// criaturas observables del mundo salian como capsula gris (el fallback), se
        /// registraban en la coleccion como especies sin datos y al equiparlas el jugador
        /// seguia jugando con la babosa por defecto, sin avisar.
        private void BabosasSalvajes(RegionDefinicion d)
        {
            for (int i = 0; i < d.habitats.Length; i++)
                Salvaje(d.habitats[i].slugId, d.habitats[i].legible, d.habitats[i].angulo);
        }

        Material MaterialHabitat(TipoHabitat t)
        {
            switch (t)
            {
                case TipoHabitat.Fumarolas: return _brasa;
                case TipoHabitat.PozoHumedo: return _agua;
                case TipoHabitat.Cristales: return _cristal;
                case TipoHabitat.Estalactitas: return _rocaClara;
                case TipoHabitat.CharcoEco: return _agua;
                case TipoHabitat.ColumnasRunas: return _cristal;
                case TipoHabitat.ArcoRocoso: return _rocaClara;
                case TipoHabitat.RaicesAnudadas: return _madera;
                case TipoHabitat.ColadaEscalonada: return _agua;
                default: return _cristal;
            }
        }

        private void Salvaje(string id, string hab, float angulo)
        {
            var go = new GameObject("Salvaje_" + id);
            go.transform.SetParent(_root, false);
            go.transform.position = EnPiso(angulo, radio + 1.5f, 0.01f);
            var s = go.AddComponent<BabosaSalvaje>();
            s.slugId = id;
            s.habitat = hab;
        }

        /// Boca de tunel en el muro: dos pilares, dintel y un fondo oscuro que se lee como
        /// pasaje. Debajo del dintel hay un disparador: entrar cruza al jugador a la region a la
        /// que apunta el cartel, y la region nueva tiene una boca que apunta de vuelta.
        private Transform Tunel(float angulo, int destinoIndice)
        {
            string destino = Regiones.De(destinoIndice).nombre;
            Vector3 basePos = EnPiso(angulo, radioTunel);
            Transform g = Grupo("Tunel_" + destino.Replace(' ', '_'), basePos);
            g.rotation = Quaternion.Euler(0f, -angulo, 0f);

            Caja(g, "pilar_izq", new Vector3(0.8f, 3.4f, 0.9f),
                 basePos + g.right * -1.9f + Vector3.up * 1.7f, _rocaClara, null, true);
            Caja(g, "pilar_der", new Vector3(0.8f, 3.4f, 0.9f),
                 basePos + g.right * 1.9f + Vector3.up * 1.7f, _rocaClara, null, true);
            Caja(g, "dintel", new Vector3(5.2f, 0.8f, 1.0f),
                 basePos + Vector3.up * 3.8f, _rocaClara, null, true);
            Caja(g, "fondo_oscuro", new Vector3(3.2f, 3.4f, 0.3f),
                 basePos + g.forward * 0.5f + Vector3.up * 1.7f,
                 Nuevo(Shader.Find("Universal Render Pipeline/Lit"), "tunel_fondo",
                       new Color(0.02f, 0.02f, 0.03f), 0f, 1f));
            Esfera(g, "marca", 0.22f, basePos + Vector3.up * 1.4f + g.forward * -0.7f, _brasa);
            Luz(g, "luz_tunel", new Color(1f, 0.65f, 0.30f), 4.5f, 3f,
                 basePos + Vector3.up * 2.6f);
            // Disparador: caja sin renderer, apenas mas ancha que el hueco entre pilares.
            var puerta = new GameObject("Pasadizo_" + destino.Replace(' ', '_'));
            puerta.transform.SetParent(g, false);
            puerta.transform.position = basePos + Vector3.up * 1.6f;
            BoxCollider bc = puerta.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(3.4f, 3.2f, 1.6f);
            TunelPasadizo tp = puerta.AddComponent<TunelPasadizo>();
            tp.destino = destinoIndice;

            TieneTunel = true;
            return g;
        }

        // ---------------------------------------------------------- siluetas orientales
        // Tres microhabitats del Templo Olvidado. Baratos para GT 640: solo Caja/Cilindro/Cono/
        // Esfera sobre el grupo del habitat, un PointLight sin sombras y materiales ya cacheados.

        /// Torre de tres pisos con techos de cono achatado (alero oriental), aguja dorada en la
        /// cima y linternas rojas en los cuatro aleros. Cuerpo de laca roja (_brasa), techos de
        /// granito claro girados 45° para que las aristas lean como tejado curvo.
        private Transform Pagoda(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_Pagoda", EnPiso(angulo, radio));
            g.rotation = Quaternion.Euler(0f, -angulo, 0f);
            Caja(g, "basamento", new Vector3(4.6f, 0.5f, 4.6f),
                 g.position + Vector3.up * 0.25f, _roca, null, true);
            float y = 0.5f;
            for (int piso = 0; piso < 3; piso++)
            {
                float cuerpo = 2.6f - piso * 0.5f;
                Caja(g, "piso" + piso, new Vector3(cuerpo, 1.1f, cuerpo),
                     g.position + Vector3.up * (y + 0.55f), _brasa, null, true);
                Cono(g, "techo" + piso, cuerpo * 0.95f, 0.8f,
                     g.position + Vector3.up * (y + 1.25f), _rocaClara, new Vector3(0f, 45f, 0f));
                y += 1.5f;
            }
            Cilindro(g, "aguja", 0.07f, 1.4f, g.position + Vector3.up * (y + 0.5f), _cristal);
            Esfera(g, "orbe", 0.28f, g.position + Vector3.up * (y + 1.1f), _cristal);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                Vector3 dir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad));
                Esfera(g, "linterna_" + i, 0.22f,
                       g.position + dir * 2.1f + Vector3.up * 1.9f, _brasa);
            }
            Luz(g, "luz_habitat", luz, 8f, 5.5f, g.position + Vector3.up * 2.6f + g.forward * -2f);
            Microhabitats++;
            return g;
        }

        /// Estanque de agua jade con borde de rocas, peces koi (esferas achatadas naranja) y un
        /// puente arqueado de laca roja que cruza por encima.
        private Transform EstanqueKoi(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_EstanqueKoi", EnPiso(angulo, radio));
            g.rotation = Quaternion.Euler(0f, -angulo, 0f);
            Cilindro(g, "estanque", 3.4f, 0.12f, g.position + Vector3.up * 0.06f, _agua);
            for (int i = 0; i < 6; i++)
            {
                float a = (i / 6f) * 360f + 15f;
                Vector3 dir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad));
                Esfera(g, "roca_borde_" + i, 0.5f,
                       g.position + dir * 3.5f + Vector3.up * 0.15f, _roca);
            }
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f + 40f;
                Vector3 dir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad));
                var pez = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pez.name = "koi_" + i;
                Object.Destroy(pez.GetComponent<Collider>());
                pez.transform.SetParent(g, false);
                pez.transform.position = g.position + dir * 1.6f + Vector3.up * 0.22f;
                pez.transform.localScale = new Vector3(0.45f, 0.12f, 0.22f);
                Vestir(pez, _cristal, false);
            }
            for (int i = 0; i < 5; i++)
            {
                float t = (i - 2) / 2f;
                Caja(g, "puente_" + i, new Vector3(0.7f, 0.12f, 1.1f),
                     g.position + g.right * (t * 3.6f) + Vector3.up * (0.25f + Mathf.Abs(t) * 0.55f),
                     _brasa, new Vector3(0f, 0f, -t * 12f), false);
            }
            Luz(g, "luz_habitat", luz, 7.5f, 5f, g.position + Vector3.up * 1.8f);
            Microhabitats++;
            return g;
        }

        /// Bambues altos segmentados (tronco + dos nudos + copa achatada de hojas) en circulo.
        /// Material verde generado por codigo, igual que el resto del mundo: nada de assets.
        private Transform BosqueBambu(float angulo, Color luz)
        {
            Transform g = Grupo("Microhabitat_BosqueBambu", EnPiso(angulo, radio));
            g.rotation = Quaternion.Euler(0f, -angulo, 0f);
            Material bambu = Nuevo(Shader.Find("Universal Render Pipeline/Lit"), "bambu",
                                   new Color(0.28f, 0.58f, 0.22f), 0f, 0.6f);
            Material hoja = Nuevo(Shader.Find("Universal Render Pipeline/Lit"), "hoja",
                                  new Color(0.35f, 0.68f, 0.25f), 0f, 0.7f, new Color(0.10f, 0.25f, 0.05f));
            for (int i = 0; i < 7; i++)
            {
                float a = (i / 7f) * 360f + i * 27f;
                float r = 1.2f + (i % 3) * 1.1f;
                Vector3 dir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad));
                Vector3 p = g.position + dir * r;
                float alto = 5.5f + (i % 4) * 0.9f;
                Cilindro(g, "bambu_" + i, 0.13f, alto, p + Vector3.up * (alto * 0.5f), bambu);
                for (int n = 1; n <= 2; n++)
                    Cilindro(g, "nudo_" + i + "_" + n, 0.16f, 0.12f,
                             p + Vector3.up * (alto * n / 3f), bambu);
                var copa = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                copa.name = "copa_" + i;
                Object.Destroy(copa.GetComponent<Collider>());
                copa.transform.SetParent(g, false);
                copa.transform.position = p + Vector3.up * (alto + 0.5f);
                copa.transform.localScale = new Vector3(1.4f, 0.35f, 1.4f);
                Vestir(copa, hoja, false);
            }
            Luz(g, "luz_habitat", luz, 8f, 4.5f, g.position + Vector3.up * 3f);
            Microhabitats++;
            return g;
        }
    }
}
