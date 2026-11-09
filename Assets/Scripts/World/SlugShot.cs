using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SlugOut.Core;

namespace SlugOut.World
{
    /// Proyectil: avanza en linea recta y impacta al primer SlugActor rival dentro del radio
    /// (deteccion por distancia, sin Rigidbody ni colliders). Lo usan el jugador y el Escupidor.
    public class SlugShot : MonoBehaviour
    {
        [SerializeField] private float speed = 24f;
        [SerializeField] private float lifeSeconds = 3f;
        [SerializeField] private float radius = 0.8f;
        [SerializeField] private float damage = 20f;
        [SerializeField] private ElementType element = ElementType.Fire;
        [SerializeField] private bool fromPlayer = true;

        private Vector3 _prev;
        private SlugActor _dueno;
        private bool _cargaPlena;
        private int _fase;            // 0 tiro corto, 1 velocimorph (>=100 MPH), 2 megamorph
        private float _probabilidadEfecto = EfectoElemental.ProbabilidadBase;

        public bool DesdeJugador { get { return fromPlayer; } }
        public Vector3 Direccion { get { return transform.forward; } }
        public float Velocidad { get { return speed; } }

        /// La IA tiene que saber que le viene un tiro encima cada frame, y FindObjectsByType por
        /// enemigo y por frame es justamente la GC que el registro de SlugActor vino a matar.
        /// Misma lista estatica y mismos OnEnable/OnDisable que alli.
        static readonly List<SlugShot> _vivas = new List<SlugShot>();
        public static IReadOnlyList<SlugShot> Vivas { get { return _vivas; } }

        /// Materiales compartidos por color: un `new Material` por disparo queda vivo para
        /// siempre en Unity y el Escupidor dispara cada pocos segundos.
        static readonly Dictionary<Color, Material> _materiales = new Dictionary<Color, Material>();

        /// True si no hay muro ni cobertura (capa 8) entre dos puntos. Lo comparten los tiros,
        /// el auto-aim y el aviso del Escupidor: la cobertura es la misma ley para todos.
        public static bool LineaDespejada(Vector3 desde, Vector3 hasta)
        {
            return !Physics.Linecast(desde, hasta, ColisionJugador.Mascara,
                QueryTriggerInteraction.Ignore);
        }

        /// Primer contacto del segmento con una cobertura, como fraccion 0..1. El barrido
        /// horizontal de los actores usa la misma ley para decidir quien llega primero.
        static bool PrimerMuro(Vector3 desde, Vector3 hasta, out float fraccion)
        {
            fraccion = 1f;
            float largo = Vector3.Distance(desde, hasta);
            if (largo <= 0.000001f) return false;
            if (!Physics.Linecast(desde, hasta, out RaycastHit hit, ColisionJugador.Mascara,
                    QueryTriggerInteraction.Ignore)) return false;
            fraccion = Mathf.Clamp01(hit.distance / largo);
            return true;
        }

        /// Crea un proyectil listo para volar. El jugador lo usa a traves de PlayerShooter; el
        /// Escupidor, directo. `delJugador=false` hace que solo pueda golpear al jugador.
        public static SlugShot Lanzar(Vector3 origen, Vector3 direccion, ElementType e, float dano,
            bool delJugador, float velocidad, Color color, float escala = 0.4f)
        {
            if (direccion.sqrMagnitude < 0.0001f) return null;
            direccion.Normalize();

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = (delJugador ? "Tiro" : "Escupitajo");
            Collider col = go.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Destruir(col); }
            go.transform.position = origen;
            go.transform.rotation = Quaternion.LookRotation(direccion, Vector3.up);
            go.transform.localScale = Vector3.one * escala;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = MaterialDe(color);
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            SlugShot shot = go.AddComponent<SlugShot>();
            shot.Configure(e, dano, delJugador);
            shot.SetSpeed(velocidad);
            shot.ConfigurarDisparo(null, 0f, false);
            // Awake no corre fuera de Play: una sonda EditMode que integre Paso a mano
            // arrancaria el barrido desde el origen del mundo y golpearia al azar.
            shot.AnclarOrigen();
            return shot;
        }

        static Material MaterialDe(Color color)
        {
            if (_materiales.TryGetValue(color, out Material m) && m != null) return m;
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            m = new Material(s);
            m.color = color;
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", color * 0.9f);
                m.EnableKeyword("_EMISSION");
            }
            _materiales[color] = m;
            return m;
        }

        private void OnEnable() { if (!_vivas.Contains(this)) _vivas.Add(this); }

        private void OnDisable() { _vivas.Remove(this); }

        public void Configure(ElementType e, float dmg, bool byPlayer)
        {
            element = e;
            damage = dmg;
            fromPlayer = byPlayer;
        }

        /// Quien disparo y con cuanta carga: el dueno importa porque el efecto del elemento cura o
        /// roba vida a su favor, y la carga plena es lo que garantiza el efecto.
        public void ConfigurarDisparo(SlugActor dueño, float tCarga, bool plena)
        {
            _dueno = dueño;
            _cargaPlena = plena;
            // Fase del tiro: 0 corto, 1 velocimorph (>=100 MPH), 2 megamorph.
            // Un volador solo recibe daño con fase >= 1.
            _fase = plena ? 2 : (tCarga >= 0.67f ? 1 : 0);
            // Un tiro a medias aplica el efecto solo a veces; la probabilidad sube con la carga
            // para que la barra no sea todo-o-nada.
            _probabilidadEfecto = Mathf.Lerp(0.15f, EfectoElemental.ProbabilidadBase, Mathf.Clamp01(tCarga));
        }

        public void SetSpeed(float s) { speed = s; }

        /// Fija el punto de partida del barrido. Lo llama `Lanzar` (y Awake en Play); las
        /// pruebas EditMode llaman `Paso` sin ciclo de vida, asi que necesitan este ancla.
        public void AnclarOrigen() { _prev = transform.position; }

        /// `Paso` se integra a mano en EditMode, donde `Destroy` esta prohibido y ensucia el
        /// log de la prueba. En Play manda Destroy (diferido, como cualquier proyectil).
        static void Destruir(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        private void Awake()
        {
            // PlayerShooter fija la posicion de nacimiento ANTES de AddComponent, asi que aqui
            // ya se ve el origen correcto del barrido.
            AnclarOrigen();
        }

        private void Start()
        {
            Destroy(gameObject, lifeSeconds);
        }

        private void Update()
        {
            Paso(Time.deltaTime);
        }

        private bool _consumido;

        /// Paso del proyectil. El segmento se resuelve por primer contacto real, no por el
        /// orden del registro de actores; sin esto dos rivales en el mismo barrido hacian
        /// que el golpe dependiera de quien hubiera nacido ultimo.
        public void Paso(float dt)
        {
            if (_consumido || dt <= 0f) return;
            Vector3 ahora = transform.position + transform.forward * (speed * dt);

            SlugActor elegido = null;
            float contacto = float.MaxValue;
            IReadOnlyList<SlugActor> vivos = SlugActor.Vivos;
            for (int i = 0; i < vivos.Count; i++)
            {
                SlugActor a = vivos[i];
                if (a == null || !a.Alive || a.Capturada) continue;
                if (a.IsPlayer == fromPlayer) continue;
                if (!ContactoBarrida(_prev, ahora, a.transform.position, radius, out float f)) continue;
                // Un enemigo pegado a una roca no se come el tiro que pasa del otro lado.
                if (!LineaDespejada(_prev, a.transform.position)) continue;

                bool antes = f < contacto - 0.00001f;
                bool empate = !antes && Mathf.Abs(f - contacto) <= 0.00001f;
                if (antes || (empate && (elegido == null ||
                        a.GetEntityId().CompareTo(elegido.GetEntityId()) < 0)))
                {
                    elegido = a;
                    contacto = f;
                }
            }

            bool hayMuro = PrimerMuro(_prev, ahora, out float muro);
            if (elegido != null && (!hayMuro || contacto < muro - 0.00001f))
            {
                _consumido = true;
                Impactar(elegido);
                return;
            }
            if (hayMuro)
            {
                _consumido = true;
                Destruir(gameObject);
                return;
            }

            transform.position = ahora;
            _prev = ahora;
        }

        /// Resolucion de un impacto ya elegido: primero se marca el tiro como consumido
        /// (Destroy es diferido en Play) y recien despues se aplican los efectos.
        void Impactar(SlugActor a)
        {
            bool estabaVivo = a.Alive;
            // El dano va PRIMERO: la captura se decide DESPUES, sobre el estado ya golpeado.
            // Si el tiro mata, la babosa cae y no se captura; si la deja bajo el umbral, si.
            // (Revvertir este orden haciacia que un tiro letal capturara a la victima viva.)
            a.TakeDamage(damage, element, _fase);
            if (_dueno != null) _dueno.RegistrarImpacto();   // la racha la sostiene quien tiro
            if (fromPlayer)
            {
                string efecto = EfectoElemental.Aplicar(element, _dueno, a, damage, _cargaPlena, _probabilidadEfecto);
                if (efecto != "neutro" && efecto != "nulo" && !efecto.StartsWith("sin efecto"))
                    Debug.Log("[SlugShot] " + element + " -> " + a.name + ": " + efecto + (_cargaPlena ? " (carga plena, garantizado)" : ""));
                if (!a.IsPlayer && a.ListaParaCapturar) a.Capturar();
            }
            // S5/S6: volador solo cae con tiro a 100 MPH -> logro skill shot.
            if (fromPlayer && !a.Alive && estabaVivo && a.EstaVolando && _fase >= 1)
                Logros.Desbloquear("skill_shot");
            // Élite Espejo: devuelve una fracción del daño al jugador.
            if (fromPlayer && _dueno != null && _dueno.IsPlayer)
            {
                var elite = a.GetComponent<EliteEstado>();
                if (elite != null && elite.ReflejaDanio)
                {
                    float devuelto = damage * elite.ReflejaFraccion;
                    _dueno.TakeDamage(devuelto, element);
                }
            }
            Destruir(gameObject);
        }

        /// Entrada del segmento horizontal en el disco de impacto. Devuelve la fraccion
        /// 0..1 del primer contacto; no usa la proyeccion del centro, que ordena mal dos
        /// rivales descentrados.
        public static bool ContactoBarrida(Vector3 desde, Vector3 hasta, Vector3 objetivo,
            float radio, out float fraccion)
        {
            fraccion = 0f;
            float ax = hasta.x - desde.x, az = hasta.z - desde.z;
            float bx = objetivo.x - desde.x, bz = objetivo.z - desde.z;
            float a = ax * ax + az * az;
            float c = bx * bx + bz * bz - radio * radio;
            if (c <= 0f) return true;              // ya esta dentro del disco al arrancar
            if (a <= 0.00000001f) return false;
            float b = -2f * (bx * ax + bz * az);
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;
            float raiz = Mathf.Sqrt(disc);
            float t = (-b - raiz) / (2f * a);
            if (t < 0f || t > 1f) return false;
            fraccion = t;
            return true;
        }

        /// Antes se movia y despues se probaba punto contra disco: a velocidadMax (62 m/s) un
        /// frame barre mas de una unidad y con el timestep maximo de 1/3 s el tiro cruzaba la
        /// arena entera sin tocar nunca al objetivo, o sea el disparo mas fuerte era el que mas
        /// fallaba. Aqui se prueba el segmento completo del paso. Sin fisica y sin subpasos.
        public static bool AciertaBarrida(Vector3 desde, Vector3 hasta, Vector3 objetivo, float radio)
        {
            return ContactoBarrida(desde, hasta, objetivo, radio, out _);
        }
    }
}
