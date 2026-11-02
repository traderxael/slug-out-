using System.Collections.Generic;
using UnityEngine;
using SlugOut.Core;

namespace SlugOut.World
{
    /// Peligro del escenario: caen rocas sobre un punto AVISADO antes en el piso.
    /// Mismo lenguaje visual que la onda del saltarin (circulo en el suelo -> algo pasa ahi),
    /// asi el jugador ya sabe que tiene que salir del circulo.
    ///
    /// Cinematico, sin fisica: la caida es y = y0 - 1/2 g t^2 y el impacto se resuelve por
    /// distancia. Son 4-6 rocas de 20 triangulos; con una GT 640 detras, un Rigidbody por roca
    /// y sus colisiones continuas costarian mas de lo que aporta.
    public class RockShower : MonoBehaviour
    {
        [Header("Ciclos")]
        [SerializeField] private bool activo = true;
        [SerializeField] private float intervalo = 7f;
        [SerializeField] private float avisoDuracion = 1.15f;
        [SerializeField] private int maxSimultaneas = 1;

        [Header("Caida")]
        [SerializeField] private float alturaCaida = 15f;
        [SerializeField] private float gravedad = 26f;
        [SerializeField] private int cantidad = 4;
        [SerializeField] private float dispersion = 2.1f;
        [SerializeField] private float radioDanio = 3.1f;
        [SerializeField] private float dano = 18f;

        [Header("Radio de eleccion")]
        [SerializeField] private float radioMinimo = 3f;
        [SerializeField] private float radioMaximo = 9f;

        private class Roca
        {
            public GameObject go;
            public Vector3 origen, destino;
            public float t;
            public bool impactoHecho;
            public float vida;
        }

        private readonly List<Roca> _roc = new List<Roca>();
        private Transform _jugador;
        private SlugActor _foe;
        private float _timer, _avisoT;
        private bool _avisando;
        private Vector3 _punto;
        private GameObject _avisoGo;
        private Material _avisoMat;
        private Mesh _mallaRoca;
        private int _llamadas, _impactos, _esquivas;
        private int _pendientes;
        private bool _huboGolpe;
        private float _hpAlAviso;
        private static readonly Vector3[] IcoVertice = Icosaedro();

        public int Caidas { get { return _llamadas; } }
        public int Impactos { get { return _impactos; } }
        public int Esquivas { get { return _esquivas; } }
        public bool Avisando { get { return _avisando; } }
        public Vector3 Punto { get { return _punto; } }
        public float ProgresoAviso { get { return _avisando ? Mathf.Clamp01(_avisoT / avisoDuracion) : 0f; } }
        public int RocasActivas { get { return _roc.Count; } }
        public int RocasEnSuelo
        {
            get { int n = 0; foreach (Roca r in _roc) if (r.impactoHecho) n++; return n; }
        }

        private void Awake()
        {
            _mallaRoca = ConstruirRoca();
        }

        private void Start()
        {
            PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null) _jugador = pc.transform;
            if (_jugador != null) _foe = _jugador.GetComponent<SlugActor>();
            _timer = intervalo * 0.5f;
        }

        public void Activar(bool on) { activo = on; }
        public bool Activo { get { return activo; } }

        private void Update()
        {
            if (_jugador == null) { Start(); if (_jugador == null) return; }

            if (activo && !_avisando)
            {
                _timer += Time.deltaTime;
                if (_timer >= intervalo) EmpezarAviso(PuntoAleatorioCerca());
            }

            if (_avisando)
            {
                _avisoT += Time.deltaTime;
                float k = Mathf.Clamp01(_avisoT / avisoDuracion);
                if (_avisoGo != null)
                {
                    // el circulo crece hacia su tamano real: ahi se lee cuanto falta
                    float s = Mathf.Lerp(0.35f, 1f, k) * radioDanio * 2f;
                    _avisoGo.transform.localScale = new Vector3(s, 1f, s);
                    Color c = _avisoMat.color;
                    c.a = 0.28f + 0.42f * k;
                    _avisoMat.color = c;
                }
                if (_avisoT >= avisoDuracion) Soltar();
            }

            AvanzarRocas();
        }

        /// Punto del anillo alrededor del jugador, apoyado en el terreno real.
        public Vector3 PuntoAleatorioCerca()
        {
            float ang = Random.value * 6.2831855f;
            float r = Mathf.Lerp(radioMinimo, radioMaximo, Random.value);
            float x = _jugador.position.x + Mathf.Cos(ang) * r;
            float z = _jugador.position.z + Mathf.Sin(ang) * r;
            return new Vector3(x, Terrain3D.Altura(x, z), z);
        }

        /// Entrada forzada: la prueba elige donde cae para poder medir esquiva y golpe por separado.
        public void ForzarAvisoEn(Vector3 p)
        {
            _punto = new Vector3(p.x, Terrain3D.Altura(p.x, p.z), p.z);
            EmpezarAviso(_punto);
        }

        private void EmpezarAviso(Vector3 p)
        {
            _avisando = true;
            _avisoT = 0f;
            _timer = 0f;
            _punto = new Vector3(p.x, Terrain3D.Altura(p.x, p.z), p.z);
            _hpAlAviso = _foe != null ? _foe.Hp : 0f;
            if (_avisoGo == null)
            {
                _avisoGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _avisoGo.name = "AvisoRocas";
                Object.Destroy(_avisoGo.GetComponent<MeshCollider>());
                _avisoMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                _avisoMat.SetFloat("_Surface", 1f);
                _avisoMat.SetFloat("_Blend", 2f);
                // en URP escribir los floats no cambia la variante del shader: hace falta el keyword
                _avisoMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _avisoMat.SetFloat("_ZWrite", 0f);
                _avisoGo.GetComponent<MeshRenderer>().sharedMaterial = _avisoMat;
            }
            _avisoGo.SetActive(true);
            _avisoGo.transform.position = _punto + Vector3.up * 0.06f;
            _avisoGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _avisoMat.color = new Color(1f, 0.32f, 0.18f, 0.28f);
        }

        private void Soltar()
        {
            _avisando = false;
            _llamadas++;
            _pendientes = cantidad;
            _huboGolpe = false;
            if (_avisoGo != null) _avisoGo.SetActive(false);
            for (int i = 0; i < cantidad; i++)
            {
                float ang = i * 2.39996f + _llamadas;
                float r = dispersion * (0.35f + 0.65f * ((i % 3) / 2f));
                float x = _punto.x + Mathf.Cos(ang) * r;
                float z = _punto.z + Mathf.Sin(ang) * r;
                Vector3 destino = new Vector3(x, Terrain3D.Altura(x, z), z);
                GameObject go = new GameObject("Roca");
                go.transform.SetParent(transform, false);
                go.transform.position = destino + Vector3.up * alturaCaida;
                go.transform.localScale = Vector3.one * Random.Range(0.5f, 0.95f);
                go.transform.rotation = Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f);
                MeshFilter mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = _mallaRoca;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MaterialRoca();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 6 sombras mas por GT 640
                _roc.Add(new Roca { go = go, origen = go.transform.position, destino = destino, t = 0f });
            }
        }

        private Material _matRoca;
        private Material MaterialRoca()
        {
            if (_matRoca != null) return _matRoca;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _matRoca = new Material(sh);
            _matRoca.color = new Color(0.42f, 0.37f, 0.33f);
            _matRoca.SetFloat("_Glossiness", 0.03f);
            _matRoca.SetFloat("_Smoothness", 0.04f);
            return _matRoca;
        }

        private void AvanzarRocas()
        {
            for (int i = _roc.Count - 1; i >= 0; i--)
            {
                Roca r = _roc[i];
                if (r.go == null) { _roc.RemoveAt(i); continue; }
                if (!r.impactoHecho)
                {
                    r.t += Time.deltaTime;
                    float y = r.origen.y - 0.5f * gravedad * r.t * r.t;
                    if (y <= r.destino.y)
                    {
                        y = r.destino.y;
                        r.impactoHecho = true;
                        Impacto(r);
                    }
                    r.go.transform.position = new Vector3(r.origen.x, y, r.origen.z);
                }
                else
                {
                    r.vida += Time.deltaTime;
                    if (r.vida > 4.5f) { Destroy(r.go); _roc.RemoveAt(i); }
                }
            }
        }

        private void Impacto(Roca r)
        {
            r.go.transform.localScale *= 1.15f;
            DustEn(r.destino);
            _pendientes--;
            bool adentro = false;
            if (_foe != null && _foe.Alive)
            {
                Vector3 a = _foe.transform.position;
                float dx = a.x - r.destino.x, dz = a.z - r.destino.z;
                if (dx * dx + dz * dz <= radioDanio * radioDanio)
                {
                    adentro = true;
                    _huboGolpe = true;
                    _impactos++;
                    _foe.TakeDamage(dano, ElementType.Earth);
                    GameFeel.AddTrauma(0.5f);
                }
            }
            // la ronda se cuenta una sola vez: si no, "esquivar" saldria 4 por cada lluvia
            if (_pendientes <= 0 && !_huboGolpe) { _esquivas++; _pendientes = 0; }
        }

        /// SlugFx.Hit es por objeto, asi que el polvo necesita un portador en el punto de impacto
        /// (el del jugador estallaria en el origen de la arena).
        private void DustEn(Vector3 p)
        {
            var go = new GameObject("PolvoRoca");
            go.transform.position = p + Vector3.up * 0.3f;
            go.transform.SetParent(transform, false);
            go.AddComponent<SlugFx>().Hit(new Color(0.55f, 0.45f, 0.36f), 1f);
            Destroy(go, 2.6f);
        }

        /// Icosaedro con cada vertice desplazado por hash: 12 vertices / 20 triangulos, roca
        /// irregular sin importar un asset ni pagar una subdivision.
        private static Vector3[] Icosaedro()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new[]
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f),
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
            return v;
        }

        private Mesh ConstruirRoca()
        {
            var verts = new List<Vector3>();
            var idx = new List<int>();
            int[][] caras =
            {
                new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,2},
                new[]{3,9,4}, new[]{3,4,11}, new[]{3,11,2}, new[]{3,2,7}, new[]{3,7,6},
                new[]{4,9,5}, new[]{5,9,1}, new[]{7,6,10}, new[]{1,6,8}, new[]{8,6,10},
                new[]{10,6,2}, new[]{2,6,7}, new[]{8,1,9}, new[]{9,4,5}, new[]{11,4,9},
            };
            foreach (int[] c in caras)
            {
                int baseI = verts.Count;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = IcoVertice[c[k]];
                    float j = 0.78f + 0.44f * Frac(Mathf.Sin(p.x * 12.9898f + p.y * 78.233f + p.z * 37.719f) * 43758.5453f);
                    verts.Add(p * j);
                }
                idx.Add(baseI); idx.Add(baseI + 1); idx.Add(baseI + 2);
            }
            var m = new Mesh { name = "RocaIco" };
            m.SetVertices(verts);
            m.SetTriangles(idx, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static float Frac(float f) { return f - Mathf.Floor(f); }

        private void OnDestroy()
        {
            if (_avisoGo != null) Destroy(_avisoGo);
            foreach (Roca r in _roc) if (r.go != null) Destroy(r.go);
        }
    }
}
