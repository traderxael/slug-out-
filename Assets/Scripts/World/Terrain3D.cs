using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlugOut.World
{
    /// Terreno procedural de la arena. La funcion de altura es ESTATICA y pura: es la unica
    /// fuente de verdad, y el mesh visible y los personajes muestrean la misma. Si cada lado
    /// tuviera su propia altura, el jugador flotaria o se hundiria segun a quien se le pregunte.
    public class Terrain3D : MonoBehaviour
    {
        [Header("Forma")]
        // Medio lado del mesh, en metros de mundo. El nombre viejo (`lado`) se leia como
        // lado completo, pero ConstruirMesh siempre lo uso como ±medioLado.
        [SerializeField] private float medioLado = 32f;
        [SerializeField] private int resolucion = 96;
        [SerializeField] private float radioPlano = 4.5f;
        [SerializeField] private float suavizado = 6f;
        [SerializeField] private float amplitud = 1.35f;
        // Detalle de alta frecuencia que se suma ENCIMA de la macro, en metros. Es una capa
        // aparte a proposito: la macro define donde van los props, los muros y las bocas de
        // tunel, y si el detalle la moviera habria que reubicar el nivel entero.
        [SerializeField] private float amplitudDetalle = 0.15f;

        [Header("Look")]
        [SerializeField] private Color bajo = new Color(0.30f, 0.24f, 0.19f);
        [SerializeField] private Color medio = new Color(0.48f, 0.38f, 0.28f);
        [SerializeField] private Color alto = new Color(0.70f, 0.64f, 0.58f);
        // Ningun script hace raycast contra el terreno: el gameplay apoya en Altura.
        // El MeshCollider authored solo costaba cooking de 9409 vertices al arrancar.
        [SerializeField] private bool collider = false;

        private static Terrain3D _instancia;
        public static Terrain3D Activo { get { return _instancia; } }

        /// Parametros de la funcion, publicados para que las pruebas comprueben la misma
        /// funcion que ve el juego en vez de reimplementarla.
        public static float AmplitudGlobal = 1.35f;
        public static float RadioPlanoGlobal = 4.5f;
        public static float SuavizadoGlobal = 6f;
        /// Cuanto detalle se le suma a la macro, en metros. El detalle nunca reemplaza a la
        /// macro: la suma esta acotada por este numero, y las pruebas lo fijan.
        public static float AmplitudDetalleGlobal = 0.15f;

        /// Relieve de la region que esta de pie. Desplazar la funcion es lo que hace que dos
        /// cuevas no compartan la misma silueta del piso (el contorno se leia identico), y la
        /// amplitud cambia cuanto rompe el terreno. Se aplican antes de reconstruir el mesh y
        /// de apoyar nada: `Altura` es la unica fuente de verdad, asi que props, bocas de tunel,
        /// salvajes y jugador se apoyan en el mismo suelo nuevo.
        public static Vector2 DesfaseRegion = Vector2.zero;
        public static float AmplitudRegion = 1f;

        private Mesh _mesh;

        /// Altura del piso en (x, z): la macro mas el detalle.
        ///
        /// La macro (AlturaMacro) es la que define el nivel: donde van los props, los muros y
        /// las bocas de tunel. El detalle es ruido de alta frecuencia que se suma encima,
        /// acotado por `AmplitudDetalleGlobal`, para que el piso deje de leerse como una
        /// formula. Como el detalle es una capa y no un cambio de la macro, el layout sigue
        /// valiendo y no hay que reubicar nada.
        public static float Altura(float x, float z)
        {
            float detalle = RuidoMundo.Detalle(x, z) * AmplitudDetalleGlobal * AmplitudRegion
                * MascaraPlaza(x, z);
            return AlturaMacro(x, z) + detalle;
        }

        /// La forma sin detalle: la que decide el layout del nivel. Si esto cambia hay que
        /// reubicar props, muros y bocas de tunel; por eso el detalle vive aparte.
        /// Plano en el centro para que el arranque del combate sea predecible, y ondulaciones
        /// suaves hacia afuera.
        public static float AlturaMacro(float x, float z)
        {
            float afuera = MascaraPlaza(x, z);
            // El desfase entra solo en el ruido, no en `d`: la plaza del centro sigue plana en las
            // dos cuevas (ahi aparecen el jugador y los enemigos) y lo que cambia de region es el
            // relieve que la rodea.
            float rx = x + DesfaseRegion.x, rz = z + DesfaseRegion.y;
            float h = Mathf.Sin(rx * 0.23f) * Mathf.Cos(rz * 0.19f) * 0.55f
                    + Mathf.Sin((rx + rz) * 0.11f) * 0.35f
                    + Valor(rx * 0.07f, rz * 0.07f) * 1.1f;
            return h * amplitudStatic * AmplitudRegion * afuera;
        }

        /// 0 en la plaza central, 1 fuera de ella. El mismo smoothstep de antes: sin escalon
        /// en el borde, que es donde se camina.
        private static float MascaraPlaza(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            float afuera = Mathf.Clamp01((d - RadioPlanoGlobal) / SuavizadoGlobal);
            return afuera * afuera * (3f - 2f * afuera);
        }

        private static float amplitudStatic = 1.35f;

        /// Ruido de valor deterministico (hash entero + interpolacion bilineal con smoothstep).
        public static float Valor(float x, float z)
        {
            int xi = Mathf.FloorToInt(x), zi = Mathf.FloorToInt(z);
            float tx = x - xi, tz = z - zi;
            tx = tx * tx * (3f - 2f * tx);
            tz = tz * tz * (3f - 2f * tz);
            float a = Hash(xi, zi), b = Hash(xi + 1, zi), c = Hash(xi, zi + 1), e = Hash(xi + 1, zi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, e, tx), tz) * 2f - 1f;
        }

        private static float Hash(int x, int z)
        {
            unchecked
            {
                uint h = (uint)x * 2246822519u ^ (uint)z * 3266489917u;
                h = (h ^ (h >> 16)) * 668265263u;
                h = h ^ (h >> 16);
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// Pendiente en (x, z), por diferencias centrales. Sirve para pintar las laderas y para
        /// que las rocas rueden hacia abajo.
        public static Vector3 Pendiente(float x, float z, float paso = 0.5f)
        {
            float dx = (Altura(x + paso, z) - Altura(x - paso, z)) / (2f * paso);
            float dz = (Altura(x, z + paso) - Altura(x, z - paso)) / (2f * paso);
            return new Vector3(dx, 0f, dz);
        }

        /// Posicion apoyada en el piso, sumando la altura del cuerpo.
        public static Vector3 EnPiso(float x, float z, float yCuerpo = 0f)
        {
            return new Vector3(x, Altura(x, z) + yCuerpo, z);
        }

        private void Awake()
        {
            // La escena trae el Terreno escalado (5,1,5) para convertir un plano 10x10 en
            // 50x50. Altura se consulta en mundo, asi que el mesh tiene que estar tambien en
            // mundo: con la escala 5 el piso visible en (20,0) era Altura(4,0).
            NormalizarEscala();
            _instancia = this;
            amplitudStatic = amplitud;
            RadioPlanoGlobal = radioPlano;
            SuavizadoGlobal = suavizado;
            AmplitudDetalleGlobal = amplitudDetalle;
        }

        /// Idempotente: RegionCuevaSeca lo llama antes de leer lossyScale, por si la region
        /// se construye antes de que este componente haya despertado.
        public void NormalizarEscala()
        {
            Vector3 s = transform.localScale;
            if (Mathf.Abs(s.x - 1f) > 0.0001f || Mathf.Abs(s.y - 1f) > 0.0001f
                || Mathf.Abs(s.z - 1f) > 0.0001f)
                transform.localScale = Vector3.one;
        }

        public float MedioLado { get { return medioLado; } }

        private void Start()
        {
            Reconstruir();
        }

        public void Reconstruir()
        {
            NormalizarEscala();
            amplitudStatic = amplitud;
            RadioPlanoGlobal = radioPlano;
            SuavizadoGlobal = suavizado;
            AmplitudDetalleGlobal = amplitudDetalle;
            _mesh = ConstruirMesh(medioLado, resolucion);
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = _mesh;

            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = MaterialesBanda();
            if (mr.sharedMaterials.Length != _mesh.subMeshCount)
                Debug.LogWarning("[Terrain3D] materiales " + mr.sharedMaterials.Length + " != submeshes " + _mesh.subMeshCount);

            MeshCollider mc = GetComponent<MeshCollider>();
            if (collider)
            {
                if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = _mesh;
            }
            else if (mc != null) Destroy(mc);

            name = "Terreno";
        }

        public int Vertices { get { return _mesh != null ? _mesh.vertexCount : 0; } }
        public int Triangulos { get { return _mesh != null ? _mesh.triangles.Length / 3 : 0; } }
        public Bounds Limites { get { return _mesh != null ? _mesh.bounds : default(Bounds); } }

        /// Rejilla de (res+1)^2 vertices sobre [-medioLado, medioLado], en metros de mundo.
        ///
        /// El color por banda sale de SUBMESHES con un material por banda, no de vertex colors:
        /// el Lit de URP no tiene soporte de color por vertice (solo lo tienen los shaders de
        /// Particles y SpeedTree, verificado en el paquete), asi que mesh.SetColors() se ignora
        /// en silencio. Tres submeshes = tres draw calls; en una GT 640 es un costo real pero chico,
        /// y sin esto el relieve se lee solo por las normales.
        public Mesh ConstruirMesh(float medioLado, int res)
        {
            res = Mathf.Max(8, res);
            int n = res + 1;
            var v = new List<Vector3>(n * n);
            // UV planar 0..1 sobre la arena: permite tallar la piedra con una textura repetida
            // sin cambiar la altura ni la topologia que comparten jugador y props.
            var uvs = new List<Vector2>(n * n);
            float ancho = medioLado * 2f;
            float paso = pasoDe(res, medioLado);
            // Una sola muestra de Altura por vertice: la banda y la pendiente de cada quad se
            // derivan de esta matriz. Antes se pedia Altura cuatro veces por quad, y la misma
            // BandaDe se calculaba cuatro veces.
            var h = new float[n * n];
            // La macro aparte: la banda de color la decide el relieve de base, no el detalle.
            // Si se calculara sobre la altura final, el detalle pasaria el umbral de pendiente
            // y todo el piso se leeria como ladera, que es un cambio de arte, no de detalle.
            var hMacro = new float[n * n];
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = -medioLado + i * paso;
                    float z = -medioLado + j * paso;
                    h[j * n + i] = Altura(x, z);
                    hMacro[j * n + i] = AlturaMacro(x, z);
                    v.Add(new Vector3(x, h[j * n + i], z));
                    uvs.Add(new Vector2((x + medioLado) / ancho, (z + medioLado) / ancho));
                }
            }

            var bandas = new List<int>[Bandas];
            for (int b = 0; b < Bandas; b++) bandas[b] = new List<int>();
            for (int j = 0; j < res; j++)
            {
                for (int i = 0; i < res; i++)
                {
                    int a = j * n + i, b2 = a + 1, c = a + n, d = c + 1;
                    float altura = (hMacro[a] + hMacro[b2] + hMacro[c] + hMacro[d]) * 0.25f;
                    float dx = (hMacro[b2] - hMacro[a]) / paso;
                    float dz = (hMacro[c] - hMacro[a]) / paso;
                    int banda = BandaDe(altura, Mathf.Sqrt(dx * dx + dz * dz));
                    bandas[banda].Add(a); bandas[banda].Add(c); bandas[banda].Add(b2);
                    bandas[banda].Add(b2); bandas[banda].Add(c); bandas[banda].Add(d);
                }
            }

            var m = new Mesh { name = "TerrenoArena" };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetUVs(0, uvs);
            m.subMeshCount = Bandas;
            for (int b = 0; b < Bandas; b++) m.SetTriangles(bandas[b], b);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static float pasoDe(int res, float medioLado) { return (medioLado * 2f) / res; }

        public const int Bandas = 3;

        /// 0 = hondo, 1 = medio, 2 = alto o ladera. La pendiente es la que hace que las lomas
        /// se lean como relieve y no como un cambio de tono en un piso plano.
        private int BandaDe(float altura, float pendiente)
        {
            if (pendiente > 0.18f) return 2;
            float amp = Mathf.Max(0.0001f, amplitudStatic * AmplitudRegion);
            float t = Mathf.Clamp01((altura + amp) / (2f * amp));
            return t < 0.42f ? 0 : (t < 0.62f ? 1 : 2);
        }

        private Material[] _materiales;

        private Material[] MaterialesBanda()
        {
            if (_materiales != null) return _materiales;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            Color[] colores = { bajo, medio, alto };
            _materiales = new Material[Bandas];
            for (int b = 0; b < Bandas; b++)
            {
                var mat = new Material(sh);
                mat.SetColor("_BaseColor", colores[b]);
                mat.color = colores[b];
                mat.SetFloat("_Glossiness", 0.04f);
                mat.SetFloat("_Smoothness", 0.05f);
                _materiales[b] = mat;
            }
            return _materiales;
        }

        /// La region manda: el terreno deja de tener un azul fijo y comparte la piedra de la
        /// cueva que esta de pie. Se reutilizan los materiales para no filtrar uno por viaje.
        public void AplicarPaleta(Color cBajo, Color cMedio, Color cAlto,
                                  Texture2D piedra, float tiling = 16f)
        {
            Material[] mats = MaterialesBanda();
            Color[] colores = { cBajo, cMedio, cAlto };
            for (int b = 0; b < mats.Length; b++)
            {
                Material m = mats[b];
                if (m == null) continue;
                m.color = colores[b];
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colores[b]);
                if (piedra != null)
                {
                    if (m.HasProperty("_BaseMap"))
                    {
                        m.SetTexture("_BaseMap", piedra);
                        m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
                    }
                    if (m.HasProperty("_MainTex"))
                    {
                        m.SetTexture("_MainTex", piedra);
                        m.SetTextureScale("_MainTex", new Vector2(tiling, tiling));
                    }
                }
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.10f);
            }
        }

        private void OnDisable() { if (_instancia == this) _instancia = null; }
    }
}
