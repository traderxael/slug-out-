using System.Collections.Generic;
using UnityEngine;

namespace SlugOut.World
{
    /// Animacion procedural sobre huesos, en la misma linea que SlugAnimator (sin
    /// AnimationClips ni AnimatorController en el proyecto).
    ///
    /// Regla que sostiene todo esto: cada hueso llega de Blender con una rotacion de
    /// bind pose no identidad. Escribirle `localRotation` directo lo teletransporta a
    /// identidad y el personaje queda en cruz. Todo lo que se aplica aca es
    /// `bind * delta`, con el bind capturado una sola vez al armar.
    public class DuelistaAnimator : MonoBehaviour
    {
        public enum Modo { Duelista, Babosa }

        [Header("Modo")]
        [SerializeField] private Modo modo = Modo.Duelista;

        [Header("Apuntar")]
        [Tooltip("Postura de Shane: el brazo del blaster queda levantado siempre.")]
        [SerializeField] private bool apuntar = false;
        public bool Apuntar { get { return apuntar; } set { apuntar = value; } }

        [Header("Caminata")]
        [Tooltip("Grados de barrido del muslo hacia adelante y atras")]
        [SerializeField] private float pasoMuslo = 34f;
        [Tooltip("La rodilla solo dobla hacia atras: el maximo es el doblez, no el rango")]
        [SerializeField] private float doblaRodilla = 52f;
        [SerializeField] private float balanceoBrazo = 26f;
        [SerializeField] private float codoFijo = 24f;
        [Tooltip("Ciclos por segundo a 1 unidad de velocidad, con tope")]
        [SerializeField] private float frecuenciaPorVelocidad = 0.62f;
        [SerializeField] private float frecuenciaMax = 3.1f;
        [SerializeField] private float reboteCadera = 0.035f;

        [Header("Idle")]
        [SerializeField] private float respirar = 2.2f;
        [SerializeField] private float giroCabeza = 6f;
        [SerializeField] private float derivaBrazos = 3.5f;

        [Header("Ataque")]
        [SerializeField] private float durAtaque = 0.34f;
        [SerializeField] private float levantarBrazo = 62f;
        [SerializeField] private float giroTorso = 16f;

        [Header("Impacto")]
        [SerializeField] private float durImpacto = 0.26f;
        [SerializeField] private float retrocesoCabeza = 16f;

        [Header("Muerte")]
        [SerializeField] private float durMuerte = 0.75f;
        [SerializeField] private float caidaCadera = 0.62f;

        class Hueso
        {
            public Transform t;
            public Quaternion bind;
        }

        private readonly Dictionary<string, Hueso> _huesos = new Dictionary<string, Hueso>();
        private Transform _cadera;
        private Vector3 _caderaBind;
        private Transform _rig;
        private SlugActor _actor;
        private Transform _raizActor;
        private Vector3 _ultimaPos;
        private float _velocidadSuave;
        private float _fase;
        private float _tAtaque = -1f;
        private float _fuerzaAtaque = 1f;
        private float _tImpacto = -1f;
        private float _tMuerte = -1f;
        private float _t;
        private bool _listo;
        private bool _avisoSinHuesos;

        // Contador propio en vez de GetInstanceID(): en Unity 6 esa API esta marcada
        // obsolete-como-error y rompe la compilacion del ensamble entero.
        private static int _siguiente = 1;
        private readonly int _numero = _siguiente++;

        private void Awake()
        {
            Recolectar();
        }

        /// Separado de Awake porque AttachShane corre desde el flujo del menu, y alli Unity
        /// no llega a invocar Awake: el componente quedaba activo, sin errores, con cero
        /// huesos y el heroe rigido para siempre. Update lo reintentar si hace falta.
        /// Falso cuando el cuerpo que se linkeo ya no existe: AttachShane puede correr mas
        /// de una vez y la segunda pasada destruye el rig anterior, dejando los punteros
        /// cacheados en null. Ahi hay que volver a recolectar, no quedarse con el cache.
        private bool HuesosVivos()
        {
            if (_huesos.Count == 0) return false;
            foreach (KeyValuePair<string, Hueso> kv in _huesos)
                if (kv.Value.t == null) return false;
            return true;
        }

        private void Recolectar()
        {
            if (_listo && HuesosVivos()) return;
            _huesos.Clear();
            _listo = false;
            _actor = GetComponent<SlugActor>();
            _raizActor = transform;
            _ultimaPos = _raizActor.position;
            // Fase distinta por instancia: sin esto, una oleada camina en comparsa exacta
            // y se lee como un fondo, no como personajes.
            _fase = (_numero % 977) * (Mathf.PI * 2f / 977f);

            foreach (Transform h in GetComponentsInChildren<Transform>(true))
            {
                if (EsHueso(h.name))
                    _huesos[h.name] = new Hueso { t = h, bind = h.localRotation };
            }
            _rig = null;
            foreach (KeyValuePair<string, Hueso> kv in _huesos)
            {
                if (kv.Key == "raiz") { _rig = kv.Value.t; break; }
            }
            if (_huesos.TryGetValue("cadera", out Hueso c))
            {
                _cadera = c.t;
                _caderaBind = _cadera != null ? _cadera.localPosition : Vector3.zero;
            }
            _listo = _huesos.Count > 0;
        }

        private static bool EsHueso(string n)
        {
            switch (n)
            {
                case "raiz": case "cadera": case "columna": case "cuello": case "cabeza":
                case "brazo_izq": case "brazo_der":
                case "antebrazo_izq": case "antebrazo_der":
                case "mano_izq": case "mano_der":
                case "muslo_izq": case "muslo_der":
                case "pantorrilla_izq": case "pantorrilla_der":
                case "pie_izq": case "pie_der":
                case "cola": case "cuerpo": case "pecho":
                case "antena_0": case "antena_1":
                    return true;
            }
            return false;
        }

        /// Rota un hueso respecto de su bind pose. Angulos en grados locales.
        private void Girar(string hueso, float x, float y, float z)
        {
            if (!_huesos.TryGetValue(hueso, out Hueso h) || h.t == null) return;
            h.t.localRotation = h.bind * Quaternion.Euler(x, y, z);
        }

        private void Resetear(string hueso)
        {
            if (!_huesos.TryGetValue(hueso, out Hueso h) || h.t == null) return;
            h.t.localRotation = h.bind;
        }

        private void ResetearTodo()
        {
            foreach (KeyValuePair<string, Hueso> kv in _huesos)
                if (kv.Value.t != null) kv.Value.t.localRotation = kv.Value.bind;
            if (_cadera != null) _cadera.localPosition = _caderaBind;
        }

        /// Publico: la arena lo llama cuando el duelistar/dispara para que el brazo
        /// del arma acompañe el tiro.
        /// Quien monta el modelo decide el esqueleto que va a mover: un duelistar
        /// usa las extremidades, una babosa la columna ondulada.
        public Modo ModoActual { get { return modo; } set { modo = value; } }

        /// `fuerza` 0..1: un tiro de 20 m/s no puede sacudir el hombro igual que uno de 62.
        public void AnimarAtaque(float fuerza = 1f)
        {
            _tAtaque = 0f;
            _fuerzaAtaque = Mathf.Clamp01(fuerza);
        }
        public void AnimarImpacto() { _tImpacto = 0f; }

        private void OnEnable()
        {
            // SlugActor ya publica un evento estatico por golpe recibido: suscribirse aca
            // evita tener que tocar SlugActor ni la IA para que el impacto se vea.
            SlugActor.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            SlugActor.Damaged -= OnDamaged;
        }

        private void OnDamaged(SlugActor golpeado, float final, float mult)
        {
            if (golpeado == _actor) AnimarImpacto();
        }

        /// Get-or-add. AttachShane corre cada vez que arranca un nivel y Reintentar vuelve
        /// a pasar por aqui: con AddComponent a secas el jugador acumulaba dos animadores y
        /// NotificarAtaque, que usa GetComponent, terminaba moviendo uno mientras el otro
        /// (el ultimo agregado) se quedaba quieto.
        public static DuelistaAnimator Obtener(GameObject actor, Modo modo, bool apuntar = false)
        {
            if (actor == null) return null;
            DuelistaAnimator a = actor.GetComponent<DuelistaAnimator>();
            if (a == null) a = actor.AddComponent<DuelistaAnimator>();
            a.ModoActual = modo;
            a.Apuntar = apuntar;
            // Reusar el componente sin limpiar el estado dejaba al heroe colapsado para
            // siempre: _tMuerte es una trampa en Update y nunca se levanta solo, asi que
            // tras morir en el nivel 3 llegaba al 4 ya en el suelo.
            a.Recolectar();      // por si el cuerpo de esta pasada no es el del cache
            a.ReiniciarEstado();
            return a;
        }

        private void ReiniciarEstado()
        {
            _tMuerte = -1f;
            _tAtaque = -1f;
            _tImpacto = -1f;
            _velocidadSuave = 0f;
            ResetearTodo();
        }

        /// Llamado desde la IA en el telegrafiado: el brazo del arma tiene que levantarse
        /// cuando el enemigo avisa, no cuando ya llego.
        public static void NotificarAtaque(GameObject actor, float fuerza = 1f)
        {
            if (actor == null) return;
            DuelistaAnimator a = actor.GetComponent<DuelistaAnimator>();
            if (a != null) a.AnimarAtaque(fuerza);
        }

        private void Update()
        {
            if (!_listo || !HuesosVivos()) Recolectar();
            if (!_listo)
            {
                // Una sola vez por personaje: si el modelo no trae armature hay que decirlo,
                // pero no una linea por cuadro.
                if (!_avisoSinHuesos)
                {
                    _avisoSinHuesos = true;
                    Debug.LogWarning("[DuelistaAnimator] en " + name + " no hay ningun hueso: "
                        + "el modelo no viene riggeado (falta el FBX) y queda rigido.");
                }
                return;
            }
            // SlugActor se agrega DESPUES de este componente en SpawnEnemy. Resolverlo
            // solo en Awake lo deja en null para siempre: ni el impacto (que compara el
            // golpeado contra _actor) ni la muerte (que lee _actor.Alive) se enteraban.
            if (_actor == null) _actor = GetComponent<SlugActor>();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _t += dt;

            MedirVelocidad(dt);

            bool muerto = _actor != null && !_actor.Alive;
            if (muerto && _tMuerte < 0f) _tMuerte = 0f;
            if (_tMuerte >= 0f)
            {
                _tMuerte += dt;
                AplicarMuerte();
                return;
            }

            if (_tAtaque >= 0f) { _tAtaque += dt; if (_tAtaque > durAtaque) _tAtaque = -1f; }
            if (_tImpacto >= 0f) { _tImpacto += dt; if (_tImpacto > durImpacto) _tImpacto = -1f; }

            ResetearTodo();
            if (modo == Modo.Babosa) AplicarBabosa();
            else AplicarHumanoide();
            if (apuntar) AplicarApuntar();
            if (_tAtaque >= 0f) AplicarAtaque();
            if (_tImpacto >= 0f) AplicarImpacto();
        }

        /// Pose de apuntar sostenida. Va DESPUES del idle/caminata a proposito: el brazo
        /// del arma no puede balancearse como el otro, y si se aplicara antes el paso
        /// lo pisaria y el personaje quedaria con el blaster colgando.
        private void AplicarApuntar()
        {
            Girar("brazo_der", -74f, 0f, -8f);
            Girar("antebrazo_der", -12f, 0f, 0f);
            Girar("mano_der", 0f, 8f, 0f);
            Girar("columna", -2f, 9f, 0f);
            Girar("cabeza", -5f, 7f, 0f);
        }

        private void MedirVelocidad(float dt)
        {
            Vector3 pos = _raizActor.position;
            float v = dt > 0f ? Vector3.Distance(pos, _ultimaPos) / dt : 0f;
            _ultimaPos = pos;
            // Suavizado: la posicion la fija Terrain3D.Altura cada cuadro y sale a saltos.
            _velocidadSuave = Mathf.Lerp(_velocidadSuave, v, Mathf.Clamp01(dt * 9f));
        }

        // ------------------------------------------------------------------ humanoide

        private void AplicarHumanoide()
        {
            float f = Mathf.Min(frecuenciaMax, _velocidadSuave * frecuenciaPorVelocidad);
            float w = _t * f * Mathf.PI * 2f + _fase;
            // Mezcla continua en vez de un umbral seco: antes el paso aparecia de golpe al
            // pasar 0,35 y al frenar en contacto melee las piernas quedaban congeladas.
            float paso = Mathf.Clamp01((f - 0.15f) / 0.5f);
            float quieto = 1f - paso;

            float s = Mathf.Sin(w);
            float c = Mathf.Cos(w);
            float r = Mathf.Sin(_t * 1.9f + _fase);

            // --- piernas: la rodilla dobla mucho mas en la fase de vuelo (el pie viaja de
            // atras hacia adelante) que en el apoyo. Un seno simetrico no distingue las dos.
            float vueloI = Mathf.Clamp01(Mathf.Cos(w) * 0.5f + 0.5f);
            float vueloD = Mathf.Clamp01(-Mathf.Cos(w) * 0.5f + 0.5f);
            Girar("muslo_izq", pasoMuslo * s * paso, 0f, 0f);
            Girar("muslo_der", -pasoMuslo * s * paso, 0f, 0f);
            float rodI = -(paso * doblaRodilla * (0.12f + 0.88f * vueloI) + quieto * 7f);
            float rodD = -(paso * doblaRodilla * (0.12f + 0.88f * vueloD) + quieto * 7f);
            Girar("pantorrilla_izq", rodI, 0f, 0f);
            Girar("pantorrilla_der", rodD, 0f, 0f);
            // el pie compensa parte del doblez de la pantorrilla para no mirar al piso
            Girar("pie_izq", -rodI * 0.55f - paso * 10f * s, 0f, 0f);
            Girar("pie_der", -rodD * 0.55f + paso * 10f * s, 0f, 0f);

            // --- brazos en contrafase con la pierna del mismo lado
            Girar("brazo_izq", -balanceoBrazo * s * paso + derivaBrazos * r * quieto, 0f, 0f);
            Girar("brazo_der", balanceoBrazo * s * paso - derivaBrazos * r * quieto, 0f, 0f);
            Girar("antebrazo_izq",
                -codoFijo * (0.35f + 0.65f * paso) - balanceoBrazo * 0.5f * s * paso, 0f, 0f);
            Girar("antebrazo_der",
                -codoFijo * (0.35f + 0.65f * paso) + balanceoBrazo * 0.5f * s * paso, 0f, 0f);

            // --- cadera y columna giran al reves. Eso es lo que hace que se lea como paso
            // y no como un muñeco deslizandose; el peso ademas se corre de lado.
            if (_cadera != null)
            {
                Vector3 p = _caderaBind;
                p.y += Mathf.Abs(c) * reboteCadera * paso
                     + Mathf.Sin(_t * 0.9f + _fase) * 0.008f * quieto;
                p.x += s * 0.012f * paso;
                _cadera.localPosition = p;
            }
            Girar("cadera", 0f, 7f * s * paso, 0f);
            Girar("columna", 3.5f * paso + respirar * r * quieto, -9f * s * paso, 2.6f * c * paso);
            // La cabeza contra-rota para que la mirada no baile con el torso.
            Girar("cabeza", -2f * paso,
                4.5f * s * paso + giroCabeza * Mathf.Sin(_t * 0.62f + _fase) * quieto,
                -1.5f * c * paso);
        }

        private void AplicarAtaque()
        {
            float k = Mathf.Clamp01(_tAtaque / durAtaque);
            // Sube rapido y vuelve con ease: si fuera lineal el brazo se ve empujado, no lanzado.
            float env = k < 0.32f ? k / 0.32f : 1f - (k - 0.32f) / 0.68f;
            env = Mathf.SmoothStep(0f, 1f, env);
            // El tiro cargado sacude de verdad; el corto apenas mueve el hombro.
            env *= Mathf.Lerp(0.42f, 1f, _fuerzaAtaque);
            if (apuntar)
            {
                // Retroceso de un tipo que ya apunta: el arma empuja hacia atras y el
                // hombro patina. Levantar el brazo desde aca se veria como un saludo.
                Girar("brazo_der", -74f + 14f * env, 0f, -8f);
                Girar("antebrazo_der", -12f - 20f * env, 0f, 0f);
                Girar("columna", -2f + 5f * env, 9f - 7f * env, 0f);
                Girar("cabeza", -5f + 6f * env, 7f, 0f);
                return;
            }
            Girar("brazo_der", -levantarBrazo * env, 0f, 0f);
            Girar("antebrazo_der", -codoFijo * 0.5f * env, 0f, 0f);
            Girar("mano_der", -12f * env, 0f, 0f);
            Girar("columna", -4f * env, -giroTorso * env, 0f);
            Girar("cabeza", -3f * env, giroTorso * 0.6f * env, 0f);
        }

        private void AplicarImpacto()
        {
            float k = Mathf.Clamp01(_tImpacto / durImpacto);
            float env = Mathf.Sin(k * Mathf.PI);
            Girar("cabeza", retrocesoCabeza * env, 0f, 0f);
            Girar("columna", retrocesoCabeza * 0.45f * env, 0f, 0f);
        }

        private void AplicarMuerte()
        {
            float k = Mathf.Clamp01(_tMuerte / durMuerte);
            float env = Mathf.SmoothStep(0f, 1f, k);
            Girar("columna", 34f * env, 8f * env, 0f);
            Girar("cabeza", 22f * env, 0f, 6f * env);
            Girar("muslo_izq", -46f * env, 0f, 0f);
            Girar("muslo_der", -38f * env, 0f, 0f);
            Girar("pantorrilla_izq", -62f * env, 0f, 0f);
            Girar("pantorrilla_der", -54f * env, 0f, 0f);
            Girar("brazo_izq", 26f * env, 0f, 18f * env);
            Girar("brazo_der", 20f * env, 0f, -22f * env);
            if (_cadera != null)
            {
                // Cae sobre su propia altura de cadera: si el modelo mide 1,9 la pierna
                // no tiene a donde ir, asi que la sentada es proporcional.
                Vector3 p = _caderaBind;
                p.y -= _caderaBind.y * caidaCadera * env;
                _cadera.localPosition = p;
            }
        }

        // ------------------------------------------------------------------ babosa

        private void AplicarBabosa()
        {
            float f = Mathf.Min(2.6f, 0.9f + _velocidadSuave * 0.8f);
            float w = _t * f + _fase;
            // Onda que viaja de la cola a la cabeza: cada tramo va retrasado del anterior.
            Girar("cola", Mathf.Sin(w) * 7f, Mathf.Cos(w) * 5f, 0f);
            Girar("cuerpo", Mathf.Sin(w - 0.9f) * 8f, Mathf.Cos(w - 0.9f) * 6f, 0f);
            Girar("pecho", Mathf.Sin(w - 1.8f) * 9f, Mathf.Cos(w - 1.8f) * 6f, 0f);
            Girar("cabeza", Mathf.Sin(w - 2.6f) * 11f, Mathf.Cos(w - 2.6f) * 8f, 0f);
            Girar("antena_0", Mathf.Sin(w * 1.7f) * 12f, 0f, Mathf.Cos(w * 1.3f) * 9f);
            Girar("antena_1", Mathf.Sin(w * 1.7f + 1.1f) * 12f, 0f, Mathf.Cos(w * 1.3f + 1.1f) * 9f);
        }
    }
}
