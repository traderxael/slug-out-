using UnityEngine;
using SlugOut.Slugs;
using SlugOut.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SlugOut.World
{
    /// Disparo del jugador. Apunta solo al enemigo vivo mas cercano (autoAim) y dispara
    /// mientras se mantiene Space o el click. El estado de la tecla se lee con isPressed
    /// (no wasPressedThisFrame) porque las teclas simuladas por el CLI se procesan como
    /// down+up seguidos y wasPressedThisFrame se pierde.
    public class PlayerShooter : MonoBehaviour
    {
        [SerializeField] private SlugData data;
        [SerializeField] private float cooldown = 0.45f;
        [SerializeField] private float shotSpeed = 22f;
        [SerializeField] private float shotDamageScale = 0.8f;
        [SerializeField] private float shotHeight = 0.8f;
        [SerializeField] private bool autoAim = true;
        [SerializeField] private float autoAimRange = 30f;
        [Header("Tubos de babosas (cargador del blaster)")]
        [SerializeField] private int tubosCapacidad = 6;
        [SerializeField] private float recargaPorTubo = 1.6f;
        [SerializeField] private int tubos = -1;
        [SerializeField] private float _recargaT;
        [SerializeField] private int recargasCompletas = 0;

        [Header("Carga a 100 MPH (velocimorph)")]
        [SerializeField] private float cargaMax = 0.6f;
        [SerializeField] private float velocidadMin = 20f;
        [SerializeField] private float velocidadMax = 62f;
        [SerializeField] private float umbralTransformacion = 44.7f;
        [SerializeField] private int transformaciones = 0;
        [SerializeField] private int tirosCortos = 0;
        [SerializeField] private int megamorfosis = 0;
        [SerializeField] private int faseActual = 0;
        // La regla de las 100 MPH ya esta en umbralTransformacion; esta es la segunda franja.
        // Ojo: con velocidadMax=62 el equivalente fisico a 200 MPH (89.4 m/s) es inalcanzable,
        // asi que el nivel 2 es un sustituto de diseno en el ultimo tramo de la barra, no una
        // afirmacion fisica. Subir velocidadMax lo volveria real sin tocar esta funcion.
        // La sonda midio que con 59.5 la franja era t>=0.978 (el 2 % de la barra), y como fase>=2
        // es lo que garantiza el efecto elemental, el premio practicamente nunca salia; 56.0 lo
        // corre al ultimo decil (t>=0.90), que es lo que se queria premiar.
        [SerializeField] private float umbralMegamorph = 56f;
        [SerializeField] private float ultimaVelocidad = 0f;

        [SerializeField] private int shotsFired = 0;
        [SerializeField] private int shotsHit = 0;
        [SerializeField] private string lastTarget = "-";

        private float _cd;
        private float _carga;
        // Duracion real del tramo de recarga en curso: cambia con el peso del ultimo disparo, y
        // la barra del HUD tiene que usar este valor o miente justo en el tiro pesado.
        private float _recargaEfectiva = -1f;
        private bool _veniaApretado;
        private Material _shotMaterial;
        private SlugAnimator _anim;
        private SlugActor _dueno;

        public void SetData(SlugData d) { data = d; }
        public int ShotsFired { get { return shotsFired; } }
        public int ShotsHit { get { return shotsHit; } }
        public int Tubos { get { return tubos; } }
        public int TubosCapacidad { get { return tubosCapacidad; } }
        public float RecargaProgreso
        {
            get
            {
                float porTubo = _recargaEfectiva > 0f ? _recargaEfectiva : recargaPorTubo;
                return porTubo > 0f ? Mathf.Clamp01(_recargaT / porTubo) : 0f;
            }
        }
        public bool SinTubos { get { return tubos <= 0; } }
        public int RecargasCompletas { get { return recargasCompletas; } }
        public int Transformaciones { get { return transformaciones; } }
        public int TirosCortos { get { return tirosCortos; } }
        public float UltimaVelocidad { get { return ultimaVelocidad; } }
        public float UmbralMPH { get { return umbralTransformacion; } }
        public float CargaProgreso { get { return cargaMax > 0f ? Mathf.Clamp01(_carga / cargaMax) : 0f; } }
        public float VelocidadActual { get { return VelocidadPara(_carga, cargaMax, velocidadMin, velocidadMax); } }

        /// Curva del remake de Slugterra: f(t) = 0.4t^2 + 0.6t, velocidad = lerp(min, max, f(t)).
        public static float VelocidadPara(float carga, float cargaMax, float min, float max)
        {
            float t = Mathf.Clamp01(carga / Mathf.Max(cargaMax, 0.0001f));
            return Mathf.Lerp(min, max, 0.4f * t * t + 0.6f * t);
        }

        /// Escala de dano por carga soltada, 1.0 -> 2.0. Antes era un escalon (x1.5 al cruzar el
        /// umbral), asi que del 68 % al 100 % de la barra el dano era el mismo: soltar justo en
        /// la muesca era estrategia dominante y el tercio superior de la barra no servia para
        /// nada. Calibrado para que en la muesca (t=0.676) de 1.566 ~ el 1.5 de hoy: no toca el
        /// balance de la campana, solo le da pendiente al tramo alto.
        public static float DanoEscalaPara(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f + 0.5f * t + 0.5f * t * t;
        }

        /// El otro lado del tradeoff: el tiro pesado cuesta cadencia. Con esto el DPS sigue
        /// creciendo con la carga (1.00 -> 1.25) pero el disparo a tope deja la ventana larga
        /// despues, que es lo que castiga fallarlo.
        public static float RecargaEscala(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f + 0.6f * t;
        }

        /// Fase del tiro segun la MISMA curva de velocidad: 0 normal, 1 velocimorph (>=100 MPH),
        /// 2 megamorph. Antes el velocimorph se decidia por velocidad en el camino vivo y por
        /// accelerationRate en SlugProjectile (dos leyes), y megamorph no existia en ninguna parte.
        public static int FaseDe(float t, float min, float max, float umbral100, float umbral200)
        {
            float v = Mathf.Lerp(min, max, 0.4f * Mathf.Clamp01(t) * Mathf.Clamp01(t) + 0.6f * Mathf.Clamp01(t));
            if (v >= umbral200) return 2;
            if (v >= umbral100) return 1;
            return 0;
        }

        /// SlugActor del propio tirador: se resuelve al leerlo, como en GameHUD.
        private SlugActor ActorPropio()
        {
            if (_dueno == null) _dueno = GetComponent<SlugActor>();
            return _dueno;
        }

        /// Posicion de la muesca en la barra: se resuelve la curva al reves, asi la marca
        /// siempre coincide con la regla real y no con un numero copiado a mano.
        public static float MuescaUmbral(float umbral, float cargaMax, float min, float max)
        {
            float objetivo = (umbral - min) / Mathf.Max(max - min, 0.0001f);
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 32; i++)
            {
                float medio = (lo + hi) * 0.5f;
                if (0.4f * medio * medio + 0.6f * medio < objetivo) lo = medio; else hi = medio;
            }
            return Mathf.Clamp01((lo + hi) * 0.5f);
        }

        public float MuescaActual() { return MuescaUmbral(umbralTransformacion, cargaMax, velocidadMin, velocidadMax); }

        /// Carga y suelta sin depender del foco de la ventana (para pruebas).
        public void SoltarPrueba(float carga) { _carga = Mathf.Clamp(carga, 0f, cargaMax); Soltar(); }

        /// Resolucion PEREZOSA: el SlugAnimator puede agregarse en Start (por la arena),
        /// DESPUES de este Awake — cachear en Awake lo deja en NULL para siempre.
        private SlugAnimator Anim()
        {
            if (_anim == null) _anim = GetComponent<SlugAnimator>();
            return _anim;
        }

        private bool FireHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Mouse m = Mouse.current;
            bool key = kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed);
            bool click = m != null && m.leftButton.isPressed;
            return key || click;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
#else
            return false;
#endif
        }

        public SlugActor NearestEnemy()
        {
            System.Collections.Generic.IReadOnlyList<SlugActor> all = SlugActor.Vivos;
            int n = all.Count;
            SlugActor best = null;
            float bestSq = autoAimRange * autoAimRange;
            for (int i = 0; i < n; i++)
            {
                SlugActor a = all[i];
                if (a == null || a.IsPlayer || !a.Alive || a.Capturada) continue;
                Vector3 d = a.transform.position - transform.position;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = a; }
            }
            return best;
        }

        private void EnsureMaterial()
        {
            if (_shotMaterial != null) return;
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            _shotMaterial = new Material(s);
            _shotMaterial.color = data != null ? data.elementColor : Color.yellow;
        }

        private void Start()
        {
            if (tubos < 0) tubos = tubosCapacidad;
        }

        private void Update()
        {
            // El input NO depende de deltaTime: con timeScale=0 (menu o ruta abierta) los Update
            // siguen corriendo, y mantener ESPACIO conjuraba un tiro encolado y soltarlo disparaba
            // sobre el menu. Congelado el combate pasa lo mismo con el cartel de GANASTE puesto.
            if (GameFeel.Pausa || SlugArenaGame.CombateCongelado) { _veniaApretado = false; return; }
            _cd -= Time.deltaTime;
            if (tubos < tubosCapacidad)
            {
                float porTubo = _recargaEfectiva > 0f ? _recargaEfectiva : recargaPorTubo;
                _recargaT += Time.deltaTime;
                while (_recargaT >= porTubo && tubos < tubosCapacidad)
                {
                    _recargaT -= porTubo;
                    tubos++;
                    if (tubos == tubosCapacidad) recargasCompletas++;
                }
            }
            if (FireHeld())
            {
                _carga = Mathf.Min(cargaMax, _carga + Time.deltaTime);
                _veniaApretado = true;
                return;
            }
            if (_veniaApretado)
            {
                _veniaApretado = false;
                Soltar();
            }
        }

        private void Soltar()
        {
            float velocidad = VelocidadPara(_carga, cargaMax, velocidadMin, velocidadMax);
            float cargaSoltada = _carga;
            if (_cd > 0f || SinTubos) return;   // sin tubos no hay disparo: la recarga manda el ritmo
            // El reset iba ANTES de este guard: soltar con la barra llena durante el enfriamiento o
            // sin tubos borraba la carga sin disparar, sin log y sin efecto. Ahora la carga se
            // conserva y el siguiente intento la aprovecha.
            _carga = 0f;
            _cd = cooldown;
            tubos--;
            Disparar(velocidad, cargaSoltada);
            // El retroceso del hombro es lo que vende que el tiro salio: sin esto el
            // blaster dispara solo, con el brazo quieto.
            // El retroceso escala con la velocidad real del tiro, no con un valor fijo:
            // un dud de 20 m/s y un disparo de 62 m/s no mueven igual el hombro.
            DuelistaAnimator.NotificarAtaque(gameObject,
                Mathf.InverseLerp(velocidadMin, velocidadMax, velocidad));
        }

        private void Disparar(float velocidad, float cargaSoltada)
        {
            Vector3 dir = transform.forward;
            if (autoAim)
            {
                SlugActor en = NearestEnemy();
                if (en != null)
                {
                    lastTarget = en.name;
                    Vector3 d = en.transform.position - transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.0001f)
                    {
                        dir = d.normalized;
                        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                    }
                }
            }

            EnsureMaterial();
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Tiro_" + shotsFired;
            go.transform.localScale = Vector3.one * 0.4f;
            go.transform.position = transform.position + Vector3.up * shotHeight + dir * 0.9f;
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null && _shotMaterial != null) mr.sharedMaterial = _shotMaterial;

            float t = Mathf.Clamp01(cargaSoltada / Mathf.Max(cargaMax, 0.0001f));
            float dmg = (data != null ? data.baseDamage : 30f) * shotDamageScale * DanoEscalaPara(t);
            // El peso del tiro se cobra aqui, en la recarga de despues: el jugador cambia dano
            // por ventana de vulnerabilidad, que es la decision que el diseno promete.
            _recargaEfectiva = recargaPorTubo * RecargaEscala(t);
            SlugShot shot = go.AddComponent<SlugShot>();
            shot.Configure(data != null ? data.element : ElementType.Fire, dmg, true);
            int fase = FaseDe(t, velocidadMin, velocidadMax, umbralTransformacion, umbralMegamorph);
            // El tiro tiene que saber quien lo disparo y con cuanta carga: el efecto del elemento
            // cura o roba vida a favor del dueno, y a carga plena es garantizado.
            shot.ConfigurarDisparo(ActorPropio(), t, fase >= 2);
            shot.SetSpeed(velocidad > 0f ? velocidad : shotSpeed);
            ultimaVelocidad = velocidad;
            faseActual = fase;
            if (fase >= 1) transformaciones++; else tirosCortos++;
            if (fase >= 2) megamorfosis++;

            shotsFired++;
            SlugAnimator animDelCable = Anim();
            if (animDelCable != null) animDelCable.PlayShot();
            Debug.Log("[PlayerShooter] disparo #" + shotsFired + " elemento=" + (data != null ? data.element.ToString() : "?") + " dano=" + dmg.ToString("F1") + " velocidad=" + velocidad.ToString("F1") + "m/s t=" + t.ToString("F2") + " objetivo=" + lastTarget + " fase=" + fase);
        }
    }
}
