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
        [SerializeField] private float lado = 46f;
        [SerializeField] private int resolucion = 96;
        [SerializeField] private float radioPlano = 4.5f;
        [SerializeField] private float suavizado = 6f;
        [SerializeField] private float amplitud = 1.35f;

        [Header("Look")]
        [SerializeField] private Color bajo = new Color(0.30f, 0.24f, 0.19f);
        [SerializeField] private Color medio = new Color(0.48f, 0.38f, 0.28f);
        [SerializeField] private Color alto = new Color(0.70f, 0.64f, 0.58f);
        [SerializeField] private bool collider = true;

        private static Terrain3D _instancia;
        public static Terrain3D Activo { get { return _instancia; } }

        /// Parametros de la funcion, publicados para que las pruebas comprueben la misma
        /// funcion que ve el juego en vez de reimplementarla.
        public static float AmplitudGlobal = 1.35f;
        public static float RadioPlanoGlobal = 4.5f;
        public static float SuavizadoGlobal = 6f;

        private Mesh _mesh;

        /// Altura del piso en (x, z). Plano en el centro para que el arranque del combate sea
        /// predecible, y ondulaciones suaves hacia afuera.
        public static float Altura(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            float afuera = Mathf.Clamp01((d - RadioPlanoGlobal) / SuavizadoGlobal);
            afuera *= afuera * (3f - 2f * afuera);        // smoothstep: sin escalon en el borde
            float h = Mathf.Sin(x * 0.23f) * Mathf.Cos(z * 0.19f) * 0.55f
                    + Mathf.Sin((x + z) * 0.11f) * 0.35f
                    + Valor(x * 0.07f, z * 0.07f) * 1.1f;
            return h * amplitudStatic * afuera;
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
            _instancia = this;
            amplitudStatic = amplitud;
            RadioPlanoGlobal = radioPlano;
            SuavizadoGlobal = suavizado;
        }

        private void Start()
        {
            Reconstruir();
        }

        public void Reconstruir()
        {
            amplitudStatic = amplitud;
            RadioPlanoGlobal = radioPlano;
            SuavizadoGlobal = suavizado;
            _mesh = ConstruirMesh(lado, resolucion);
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

        /// Rejilla de (res+1)^2 vertices sobre [-lado/2, lado/2].
        ///
        /// El color por banda sale de SUBMESHES con un material por banda, no de vertex colors:
        /// el Lit de URP no tiene soporte de color por vertice (solo lo tienen los shaders de
        /// Particles y SpeedTree, verificado en el paquete), asi que mesh.SetColors() se ignora
        /// en silencio. Dos submeshes = dos draw calls; en una GT 640 es un costo real pero chico,
        /// y sin esto el relieve se lee solo por las normales.
        public Mesh ConstruirMesh(float medioLado, int res)
        {
            res = Mathf.Max(8, res);
            int n = res + 1;
            var v = new List<Vector3>(n * n);
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = -medioLado + i * pasoDe(res, medioLado);
                    float z = -medioLado + j * pasoDe(res, medioLado);
                    v.Add(new Vector3(x, Altura(x, z), z));
                }
            }

            var bandas = new List<int>[Bandas];
            for (int b = 0; b < Bandas; b++) bandas[b] = new List<int>();
            float paso = pasoDe(res, medioLado);
            for (int j = 0; j < res; j++)
            {
                for (int i = 0; i < res; i++)
                {
                    int a = j * n + i, b2 = a + 1, c = a + n, d = c + 1;
                    float cx = (-medioLado + (i + 0.5f) * paso), cz = (-medioLado + (j + 0.5f) * paso);
                    bandas[BandaDe(cx, cz)].Add(a); bandas[BandaDe(cx, cz)].Add(c); bandas[BandaDe(cx, cz)].Add(b2);
                    bandas[BandaDe(cx, cz)].Add(b2); bandas[BandaDe(cx, cz)].Add(c); bandas[BandaDe(cx, cz)].Add(d);
                }
            }

            var m = new Mesh { name = "TerrenoArena" };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.subMeshCount = Bandas;
            for (int b = 0; b < Bandas; b++) m.SetTriangles(bandas[b], b);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static float pasoDe(int res, float medioLado) { return (medioLado * 2f) / res; }

        public const int Bandas = 3;

        /// 0 = hondo, 1 = medio, 2 = alto.
        private int BandaDe(float x, float z)
        {
            float t = Mathf.Clamp01((Altura(x, z) + amplitudStatic) / (2f * amplitudStatic));
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

        private void OnDisable() { if (_instancia == this) _instancia = null; }
    }
}
