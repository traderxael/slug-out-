using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SlugOut.World
{
    /// Rodada del jugador: la unica respuesta activa contra un tiro en vuelo durante la
    /// exploracion. No da invulnerabilidad, solo saca el cuerpo del corredor del proyectil, y
    /// por eso usa la MISMA ley geometrica que la esquiva de la IA (EnemySlugAI.VentanaDeEsquiva):
    /// si el jugador y el enemigo se medían con reglas distintas, esquivar seria gratis contra
    /// el bicho equivocado.
    ///
    /// Coste para que no resuelva todo: al terminar hay una ventana corta donde no se puede
    /// volver a rodar, y si el tiro llega despues de la rodada pega igual.
    public class PlayerDodge : MonoBehaviour
    {
        [Header("Rodada")]
        [SerializeField] private float velocidadRodada = 12f;
        [SerializeField] private float duracionRodada = 0.3f;
        [SerializeField] private float recuperacion = 0.2f;
        [SerializeField] private float enfriamiento = 1.1f;
        [Tooltip("Igual que el margen de SlugShot: lo que hay que sacar del corredor.")]
        [SerializeField] private float margenCuerpo = 0.9f;

        [Header("Contadores (solo lectura)")]
        [SerializeField] private int rodadas = 0;
        [SerializeField] private int intentosBloqueados = 0;

        private enum Fase { Quiet, Rodando, Recuperando }

        private PlayerController _ctrl;
        private SlugFx _fx;
        private Fase _fase = Fase.Quiet;
        private float _t;
        private float _cd;
        private Vector3 _rumbo;
        private bool _teclaPrevia;

        public bool Rodando { get { return _fase == Fase.Rodando; } }
        public bool Expuesto { get { return _fase == Fase.Recuperando; } }
        public int Rodadas { get { return rodadas; } }
        public int IntentosBloqueados { get { return intentosBloqueados; } }
        public float EnfriamientoRestante { get { return Mathf.Max(0f, _cd); } }
        public float ProgresoRodada { get { return _fase == Fase.Rodando ? Mathf.Clamp01(_t / duracionRodada) : 0f; } }
        public float ProgresoRecuperacion { get { return _fase == Fase.Recuperando ? Mathf.Clamp01(_t / recuperacion) : 0f; } }

        /// Rumbo fijado al iniciar la rodada. El animador lo necesita para girar el
        /// cuerpo sobre el eje correcto; no altera el desplazamiento.
        public Vector3 RumboRodada { get { return _rumbo; } }

        /// Desplazamiento lateral maximo de una rodada completa: el dato que la ley necesita.
        public float PasoLateral { get { return velocidadRodada * duracionRodada; } }

        /// Distancia maxima a la que un tiro de `velocidadTiro` todavía se puede cubrir con la
        /// rodada, y minimo por debajo del cual ya esta encima. Reutiliza la ley de la IA en vez
        /// de duplicar la formula.
        public bool CubreTiro(float distancia, float velocidadTiro)
        {
            return EnemySlugAI.VentanaDeEsquiva(distancia, velocidadTiro, PasoLateral, duracionRodada, margenCuerpo);
        }

        private void Awake()
        {
            _ctrl = GetComponent<PlayerController>();
            _fx = GetComponent<SlugFx>();
        }

        private void Update()
        {
            if (GameFeel.Pausa) return;
            if (SlugArenaGame.CombateCongelado) return;

            bool tecla = TeclaRodada();
            bool inicio = tecla && !_teclaPrevia;
            _teclaPrevia = tecla;
            if (inicio) IntentarRodada();

            Paso(Time.deltaTime);
        }

        /// Se lee con isPressed y borde propio, no con wasPressedThisFrame: las teclas simuladas
        /// del CLI llegan como down+up en el mismo cuadro y el borde se pierde.
        private bool TeclaRodada()
        {
            return EntradaJuego.Leer().rodada;
        }

        public bool IntentarRodada()
        {
            if (_fase != Fase.Quiet || _cd > 0f)
            {
                intentosBloqueados++;
                return false;
            }

            Vector3 dir = _ctrl != null ? _ctrl.MoveInput : Vector3.zero;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.y = 0f;
            _rumbo = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

            _fase = Fase.Rodando;
            _t = 0f;
            if (_fx == null) _fx = GetComponent<SlugFx>();
            if (_fx != null) _fx.Rodada();
            return true;
        }

        /// Vuelve a Quiet: al reaparecer no puede quedar una rodada a medias dejando
        /// el modelo girado y los pies bajo el piso.
        public void Reiniciar()
        {
            _fase = Fase.Quiet;
            _t = 0f;
            _teclaPrevia = true;
        }

        /// dt como argumento, como en SlugShot.Paso y EnemySlugAI.PeleaEsquiva: fuera del game
        /// loop Time.deltaTime vale cero y la maquina no se puede probar integrando frames.
        public void Paso(float dt)
        {
            if (_cd > 0f) _cd = Mathf.Max(0f, _cd - dt);

            if (_fase == Fase.Rodando)
            {
                // La rodada va a 12 m/s: mismo barrido que al caminar para que no
                // atraviese un pilar ni el anillo en un cuadro largo.
                Vector3 paso = _rumbo * (velocidadRodada * dt);
                if (_ctrl != null) _ctrl.Mover(paso);
                else
                {
                    transform.position = ColisionJugador.Deslizar(transform.position, paso,
                        ColisionJugador.RadioDefecto, ColisionJugador.AltoDefecto,
                        out _, out _);
                }
                if (_ctrl != null) _ctrl.ApoyarEnPiso();
                _t += dt;
                if (_t >= duracionRodada)
                {
                    rodadas++;
                    _fase = Fase.Recuperando;
                    _t = 0f;
                }
                return;
            }

            if (_fase == Fase.Recuperando)
            {
                _t += dt;
                if (_t >= recuperacion)
                {
                    _fase = Fase.Quiet;
                    _t = 0f;
                    _cd = enfriamiento;
                }
            }
        }
    }
}
