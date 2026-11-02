using UnityEngine;
using SlugOut.Core;

namespace SlugOut.World
{
    /// Proyectil: avanza en linea recta y impacta al primer SlugActor rival dentro del radio
    /// (deteccion por distancia, sin Rigidbody ni colliders).
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
        private float _probabilidadEfecto = EfectoElemental.ProbabilidadBase;

        public bool DesdeJugador { get { return fromPlayer; } }
        public Vector3 Direccion { get { return transform.forward; } }
        public float Velocidad { get { return speed; } }

        /// La IA tiene que saber que le viene un tiro encima cada frame, y FindObjectsByType por
        /// enemigo y por frame es justamente la GC que el registro de SlugActor vino a matar.
        /// Misma lista estatica y mismos OnEnable/OnDisable que alli.
        static readonly System.Collections.Generic.List<SlugShot> _vivas = new System.Collections.Generic.List<SlugShot>();
        public static System.Collections.Generic.IReadOnlyList<SlugShot> Vivas { get { return _vivas; } }

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
            // Un tiro a medias aplica el efecto solo a veces; la probabilidad sube con la carga
            // para que la barra no sea todo-o-nada.
            _probabilidadEfecto = Mathf.Lerp(0.15f, EfectoElemental.ProbabilidadBase, Mathf.Clamp01(tCarga));
        }

        public void SetSpeed(float s) { speed = s; }

        private void Awake()
        {
            // PlayerShooter fija la posicion de nacimiento ANTES de AddComponent, asi que aqui
            // ya se ve el origen correcto del barrido.
            _prev = transform.position;
        }

        private void Start()
        {
            Destroy(gameObject, lifeSeconds);
        }

        private void Update()
        {
            Paso(Time.deltaTime);
        }

        /// El paso completo del proyectil: mover y barrer objetivos. El dt va como argumento para
        /// que una sonda EditMode pueda integrarlo frame a frame sin entrar en Play, porque
        /// Time.deltaTime no avanza fuera del game loop y la esquiva hay que verla contra un tiro.
        public void Paso(float dt)
        {
            Vector3 ahora = transform.position + transform.forward * (speed * dt);

            // Se recorre al reves: TakeDamage puede matar y eso saca actores de la lista.
            System.Collections.Generic.IReadOnlyList<SlugActor> vivos = SlugActor.Vivos;
            for (int i = vivos.Count - 1; i >= 0; i--)
            {
                if (i >= vivos.Count) continue;
                SlugActor a = vivos[i];
                if (a == null || !a.Alive || a.Capturada) continue;
                if (a.IsPlayer == fromPlayer) continue;

                if (!AciertaBarrida(_prev, ahora, a.transform.position, radius)) continue;

                a.TakeDamage(damage, element);
                if (_dueno != null) _dueno.RegistrarImpacto();   // la racha la sostiene quien tiro
                string efecto = EfectoElemental.Aplicar(element, _dueno, a, damage, _cargaPlena, _probabilidadEfecto);
                if (efecto != "neutro" && efecto != "nulo" && !efecto.StartsWith("sin efecto"))
                    Debug.Log("[SlugShot] " + element + " -> " + a.name + ": " + efecto + (_cargaPlena ? " (carga plena, garantizado)" : ""));
                if (a.ListaParaCapturar) a.Capturar();
                Destroy(gameObject);
                return;
            }

            transform.position = ahora;
            _prev = ahora;
        }

        /// Antes se movia y despues se probaba punto contra disco: a velocidadMax (62 m/s) un
        /// frame barre mas de una unidad y con el timestep maximo de 1/3 s el tiro cruzaba la
        /// arena entera sin tocar nunca al objetivo, o sea el disparo mas fuerte era el que mas
        /// fallaba. Aqui se prueba el segmento completo del paso. Sin fisica y sin subpasos.
        public static bool AciertaBarrida(Vector3 desde, Vector3 hasta, Vector3 objetivo, float radio)
        {
            Vector3 ab = hasta - desde; ab.y = 0f;
            Vector3 ac = objetivo - desde; ac.y = 0f;
            float l2 = ab.sqrMagnitude;
            float t = l2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(ac, ab) / l2) : 0f;
            Vector3 proyeccion = ab * t;
            return (ac - proyeccion).sqrMagnitude <= radio * radio;
        }
    }
}
