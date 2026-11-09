using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SlugOut.World
{
    /// Movimiento del jugador con WASD / flechas, apoyado en el terreno procedural.
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float speed = 6f;
        /// Paso cuidado: es la unica velocidad que deja entrar en la bola de acercamiento de
        /// una salvaje sin asustarla. El numero sale de la regla del vinculo, no de aqui, para
        /// que control y regla no vuelvan a separarse.
        [SerializeField] private float pasoCuidadoSpeed = Vinculo.VelocidadPasoCuidado;
        [Tooltip("Multiplicador de velocidad mientras se mantiene el disparo. Cargar al maximo " +
                 "deja de ser gratis: se cambia movilidad por dano y transformacion.")]
        [SerializeField] private float factorVelocidadCargando = 0.7f;
        [SerializeField] private float turnSpeed = 720f;
        [Tooltip("Suavizado de aceleracion: 0=instantaneo, 12=fluido")]
        [SerializeField] private float aceleracionMov = 12f;
        private Vector3 _velSuavizada;
        [SerializeField] private bool logMovement = false;
        [SerializeField] private float logEvery = 0.5f;
        [Tooltip("Cuanto sobresale el pivot del personaje sobre el piso.")]
        [SerializeField] private float alturaSuelo = 1f;
        [Header("Colision")]
        [SerializeField] private float radioCuerpo = 0.5f;
        [SerializeField] private float altoCuerpo = 2f;

        private Vector3 _lastPosition;
        private float _logTimer;
        private PlayerShooter _shooter;

        /// Diagnostico para las pruebas de muros: ultimo movimiento toco algo.
        public bool TocoMuro { get; private set; }
        public int ContactosMuro { get; private set; }
        public Vector3 UltimoContacto { get; private set; }

        public Vector3 MoveInput { get; private set; }
        public float DistanceTravelled { get; private set; }

        /// True mientras se mantiene CTRL: lo leen el arnés y las pruebas para verificar
        /// que el paso cuidado existe y se activa con la tecla anunciada.
        public bool PasoCuidado { get; private set; }

        /// Velocidad real de este cuadro (0 quieto, paso cuidado, o carrera).
        public float Velocidad { get; private set; }

        /// Formula pura de la velocidad de marcha: el paso cuidado manda y la carga se aplica
        /// encima. El piso de 0,1 evita que un factor serializado en cero congele al jugador.
        public static float VelocidadDeMarcha(float baseSpeed, float pasoCuidadoSpeed,
            bool pasoCuidado, bool cargando, float factorCargando)
        {
            float v = pasoCuidado ? pasoCuidadoSpeed : baseSpeed;
            return cargando ? v * Mathf.Clamp(factorCargando, 0.1f, 1f) : v;
        }

        /// Apoya el pivot en la altura del terreno. Publico para que una prueba pueda
        /// comprobar que el jugador sigue el relieve y no se queda flotando en y=1.
        /// No hace barrido a proposito: los tests de animacion escriben el transform directo.
        public void ApoyarEnPiso()
        {
            Vector3 p = transform.position;
            float y = Terrain3D.Altura(p.x, p.z) + alturaSuelo;
            if (!Mathf.Approximately(p.y, y)) transform.position = new Vector3(p.x, y, p.z);
        }

        /// Mueve el cuerpo con barrido horizontal. La Y se deja a ApoyarEnPiso y el
        /// empuje se desliza contra la normal de los muros.
        public void Mover(Vector3 delta)
        {
            TocoMuro = false;
            delta.y = 0f;
            ApoyarEnPiso();
            Vector3 p = transform.position;
            if (ColisionJugador.SobreMuro(p, radioCuerpo, altoCuerpo))
            {
                p = ColisionJugador.PuntoLibre(p, new Vector3(-p.x, 0f, -p.z),
                    radioCuerpo, altoCuerpo);
                transform.position = p;
                Physics.SyncTransforms();
                ApoyarEnPiso();
                p = transform.position;
            }
            Vector3 fin = ColisionJugador.Deslizar(p, delta, radioCuerpo, altoCuerpo,
                out bool toco, out _);
            if (toco)
            {
                TocoMuro = true;
                ContactosMuro++;
                UltimoContacto = fin;
            }
            transform.position = fin;
            ApoyarEnPiso();
        }

        /// Teletransporte seguro (spawn, tunel, reintento): resuelve solape y apoya en Y.
        public void Colocar(Vector3 xz)
        {
            Vector3 p = new Vector3(xz.x, 0f, xz.z);
            p = ColisionJugador.PuntoLibre(p, new Vector3(-p.x, 0f, -p.z),
                radioCuerpo, altoCuerpo);
            transform.position = p;
            Physics.SyncTransforms();
            ApoyarEnPiso();
        }

        private void Awake()
        {
            // El piloto del arnes de build (Tools/CruceTunelBuild.ps1) navega leyendo las lineas
            // `pos=` de este script. El log de movimiento se apago por pedido del usuario, asi que
            // queda un encendido por linea de mando: medir sin volver al spam en el juego normal.
            if (!logMovement && System.Array.IndexOf(
                    System.Environment.GetCommandLineArgs(), "-logPos") >= 0) logMovement = true;
        }

        private void Start()
        {
            _lastPosition = transform.position;
            SombraContacto.Agregar(transform, 0.52f);
            if (logMovement) Debug.Log("[PlayerController] inicio en " + transform.position);
            // El modelo del jugador lo carga SlugArenaGame.AttachShane (shane_pixar) o el
            // Cuerpo_Shane ya presente en la escena. Montar PlayerModel ademas apilaba el
            // rogue de Characters/Jugador encima del heroe: el jugador se veia doble.
            if (GetComponent<PlayerModel>() != null) Destroy(GetComponent<PlayerModel>());
        }

        private Vector3 ReadInput()
        {
            // Teclado y mando pasan por la misma puerta; el gamepad conserva el esquema de
            // movimiento del teclado (el apuntado sigue siendo la orientacion de avance).
            EntradaFrame e = EntradaJuego.Leer();
            PasoCuidado = e.pasoCuidado;
            return new Vector3(e.mover.x, 0f, e.mover.y);
        }

        private void Update()
        {
            Vector3 raw = ReadInput();
            if (raw.sqrMagnitude > 1f) raw.Normalize();
            MoveInput = raw;

            if (raw.sqrMagnitude > 0.0001f)
            {
                if (_shooter == null) _shooter = GetComponent<PlayerShooter>();
                bool cargando = _shooter != null && _shooter.Cargando;
                float vel = VelocidadDeMarcha(speed, pasoCuidadoSpeed, PasoCuidado, cargando,
                    factorVelocidadCargando);
                Velocidad = vel;
                Vector3 objetivo = raw * vel;
                float k = aceleracionMov <= 0.01f ? 1f : Mathf.Clamp01(aceleracionMov * Time.deltaTime);
                _velSuavizada = Vector3.Lerp(_velSuavizada, objetivo, k);
                Mover(_velSuavizada * Time.deltaTime);
                // Mientras el tirador apunta (cargando ESPACIO con objetivo), la orientacion la
                // manda el enemigo y no el movimiento: eso permite strafear de costado sin
                // perder el apuntado.
                if (_shooter == null || !_shooter.Apuntando)
                {
                    Quaternion target = Quaternion.LookRotation(raw, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
                }
            }
            else { Velocidad = 0f; _velSuavizada = Vector3.Lerp(_velSuavizada, Vector3.zero, Mathf.Clamp01(14f * Time.deltaTime)); if (_velSuavizada.sqrMagnitude > 0.001f) Mover(_velSuavizada * Time.deltaTime); }

            ApoyarEnPiso();

            DistanceTravelled += Vector3.Distance(transform.position, _lastPosition);
            _lastPosition = transform.position;

            if (logMovement)
            {
                _logTimer += Time.deltaTime;
                if (_logTimer >= logEvery)
                {
                    _logTimer = 0f;
                    Debug.Log("[PlayerController] pos=" + transform.position.ToString("F2") + " input=" + raw + " total=" + DistanceTravelled.ToString("F2"));
                }
            }
        }
    }
}
