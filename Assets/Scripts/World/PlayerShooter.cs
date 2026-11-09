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
        /// Balance A tactico: la tabla de dano base de los slugs se penso con 35 y el
        /// objetivo tactico es 32. El factor se aplica al disparo (no a los assets) para
        /// que todas las especies conserven su diferencia relativa.
        public const float DanoBaseReferencia = 35f;
        public const float DanoBaseObjetivo = 32f;
        public static float AjusteDanoTactico(float baseDamage)
        {
            return baseDamage * (DanoBaseObjetivo / DanoBaseReferencia);
        }

        [SerializeField] private SlugData data;
        [SerializeField] private float cooldown = 0.45f;
        [SerializeField] private float shotSpeed = 22f;
        [SerializeField] private float shotDamageScale = 0.8f;
        private float _bonoDanioMultiplicador = 1f;
        [SerializeField] private float shotHeight = 0.8f;
        [SerializeField] private bool autoAim = true;
        [SerializeField] private float autoAimRange = 30f;
        [Header("Tubos de babosas (cargador del blaster)")]
        [SerializeField] private int tubosCapacidad = 6;
        [SerializeField] private float recargaPorTubo = 1.6f;
        /// Cuanto cuesta el peso del ultimo tiro: la recarga siguiente se multiplica por este
        /// numero. La campana lo modera a medida que el jugador avanza (SlugCampaign).
        /// Es el multiplicador TOTAL (no un extra): se aplica una sola vez.
        [SerializeField] private float penalizacionDisparoPesado = PenalizacionDisparoPesadoPorDefecto;
        [SerializeField] private int tubos = -1;
        [SerializeField] private float _recargaT;
        [SerializeField] private int recargasCompletas = 0;

        [Header("Apuntado")]
        [Tooltip("Medio angulo, en grados, del cono frontal donde el auto-aim busca objetivo. " +
                 "Fuera de el el tiro sale derecho: un enemigo a la espalda ya no gira al heroe.")]
        [SerializeField] private float conoApuntadoGrados = 65f;
        [Tooltip("Giro del cuerpo mientras se carga, para que apuntar se vea antes del disparo.")]
        [SerializeField] private float giroApuntado = 540f;

        /// Ajuste de accesibilidad: el cono normal es 65°; el amplio, 85°.
        public const float ConoAmplioGrados = 85f;
        public static float GradosCono(bool amplio, float normal)
        {
            return amplio ? ConoAmplioGrados : normal;
        }
        private float ConoEfectivo()
        {
            return GradosCono(GameFlow.AimAmplio, conoApuntadoGrados);
        }

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
        private bool _bloqueoHastaSoltar;
        private PlayerDodge _dodge;
        private SlugAnimator _anim;
        private SlugActor _dueno;

        public void SetData(SlugData d) { data = d; }

        /// Bono de daño de un eco de la cueva (se acumula por nivel de eco).
        public void AgregarBonoDanio(float fraccion) { _bonoDanioMultiplicador += fraccion; }

        /// Resetea el multiplicador de daño al empezar cada nivel (los ecos son por nivel).
        public void ReiniciarBonosNivel() { _bonoDanioMultiplicador = 1f; }

        /// Aumenta la capacidad del cargador y llena los tubos nuevos al instante.
        public void AumentarCapacidad(int tubosExtra)
        {
            tubosCapacidad += tubosExtra;
            if (tubos < tubosCapacidad) tubos = tubosCapacidad;
        }

        /// Aumenta la velocidad de vuelo del tiro (m/s).
        public void AgregarVelocidadDisparo(float mps) { shotSpeed += mps; }

        /// El cargador es el techo del dano del jugador: con 6 tubos y 2,56 s por tiro cargado
        /// daba 41 disparos en un nivel de 90 s, y el nivel 28 pide ~48 solo para neutralizar a
        /// sus enemigos. La campana lo mejora nivel a nivel (ver SlugCampaign).
        public void SetRecargaPorTubo(float segundos)
        {
            recargaPorTubo = Mathf.Max(0.05f, segundos);
        }

        /// Multiplicador de la recarga que sigue a un tiro cargado (1,6 = el de antes).
        public void SetPenalizacionDisparoPesado(float valor)
        {
            penalizacionDisparoPesado = Mathf.Max(1f, valor);
        }

        public float RecargaPorTubo { get { return recargaPorTubo; } }
        public float PenalizacionDisparoPesado { get { return penalizacionDisparoPesado; } }
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

        /// True mientras se mantiene el disparo con un objetivo valido: PlayerController deja de
        /// girar hacia el movimiento y el cuerpo se orienta al enemigo, asi se puede strafear.
        public bool Apuntando { get; private set; }
        /// True mientras se mantiene el boton de disparo (haya o no objetivo). PlayerController
        /// lo usa para cobrar la carga con velocidad: cargar es una decision, no gratis.
        public bool Cargando { get; private set; }
        /// Objetivo fijado durante la carga: evita que el auto-aim salte de bicho en bicho
        /// mientras el cuerpo gira hacia el primero.
        public SlugActor ObjetivoActual { get; private set; }

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
        ///
        /// `total` es el multiplicador TOTAL de la penalizacion del tiro pesado (1,6 al
        /// empezar la campana, 1,35 al final). La recarga se interpola entre 1 (tiro sin
        /// cargar) y ese total (tiro a tope): `1 + (total - 1) * t`.
        ///
        /// Antes la penalizacion vivia metida dentro de esta curva Y la campana le sumo
        /// otra por su cuenta, asi que el nivel 1 recargaba a 4,1 s por tubo en vez de
        /// 2,56 s y el jugador disparaba la mitad de veces (22 tiros medidos donde antes
        /// eran 40). El total se cobra UNA vez.
        public static float RecargaEscala(float t, float total)
        {
            t = Mathf.Clamp01(t);
            total = Mathf.Max(1f, total);
            return 1f + (total - 1f) * t;
        }

        /// La penalizacion por defecto (la del nivel 1) para la firma de un argumento.
        public const float PenalizacionDisparoPesadoPorDefecto = 1.6f;

        public static float RecargaEscala(float t)
        {
            return RecargaEscala(t, PenalizacionDisparoPesadoPorDefecto);
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

        /// Deja una carga en curso sin depender del foco (para probar la cancelacion con la rodada).
        public void CargarDePrueba(float carga)
        {
            _carga = Mathf.Clamp(carga, 0f, cargaMax);
            _veniaApretado = true;
            Cargando = true;
        }

        /// La rodada cancela la carga sin disparar: es una respuesta defensiva, no un tiro.
        /// El bloqueo obliga a soltar el boton antes de volver a cargar, para que el tiro
        /// acumulado no salga solo al terminar la rodada.
        public void CancelarCarga()
        {
            _carga = 0f;
            _veniaApretado = false;
            Apuntando = false;
            ObjetivoActual = null;
            Cargando = false;
            _bloqueoHastaSoltar = true;
        }

        public bool CargaBloqueada { get { return _bloqueoHastaSoltar; } }

        /// Resolucion PEREZOSA: el SlugAnimator puede agregarse en Start (por la arena),
        /// DESPUES de este Awake — cachear en Awake lo deja en NULL para siempre.
        private SlugAnimator Anim()
        {
            if (_anim == null) _anim = GetComponent<SlugAnimator>();
            return _anim;
        }

        private bool FireHeld()
        {
            return EntradaJuego.Leer().disparo;
        }

        /// El auto-aim solo apunta hacia adelante. Antes elegia al vivo mas cercano sin mirar la
        /// orientacion: con la camara detras del heroe, un enemigo a la espalda rotaba el cuerpo
        /// 180 grados en el cuadro del disparo y el tiro salia hacia atras sin que se viera.
        public SlugActor NearestEnemy()
        {
            return ElegirObjetivo(SlugActor.Vivos, transform.position, transform.forward,
                autoAimRange, Mathf.Cos(ConoEfectivo() * Mathf.Deg2Rad), TengoLineaAlObjetivo);
        }

        /// El enemigo vivo mas cercano dentro del cono frontal. Funcion pura sobre la lista: la
        /// regla se prueba en EditMode sin escena y el HUD usa exactamente la misma.
        /// `visible` es opcional: sin el (null) elige por cono y distancia; con el, la cobertura
        /// tambien cuenta para el apuntado, igual que cuenta para el tiro y para la IA.
        public static SlugActor ElegirObjetivo(
            System.Collections.Generic.IReadOnlyList<SlugActor> actores,
            Vector3 desde, Vector3 adelante, float alcance, float cosMinimo,
            System.Func<SlugActor, bool> visible = null)
        {
            if (actores == null) return null;
            adelante.y = 0f;
            if (adelante.sqrMagnitude < 0.0001f) return null;
            adelante.Normalize();

            SlugActor mejor = null;
            float mejorSq = Mathf.Max(0.0001f, alcance * alcance);
            for (int i = 0, n = actores.Count; i < n; i++)
            {
                SlugActor a = actores[i];
                // Un enemigo enterrado es invulnerable y esta escondido: apuntarle es tirar a
                // la nada y deja al jugador sin objetivo valido delante.
                if (a == null || a.IsPlayer || !a.Alive || a.Capturada || a.EstaEnterrado) continue;
                Vector3 d = a.transform.position - desde;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq > mejorSq || sq < 0.0001f) continue;
                if (Vector3.Dot(d / Mathf.Sqrt(sq), adelante) < cosMinimo) continue;
                if (visible != null && !visible(a)) continue;
                mejorSq = sq;
                mejor = a;
            }
            return mejor;
        }

        /// Un enemigo detras de una roca no es objetivo del auto-aim: antes el cuerpo giraba
        /// hacia el y el tiro se estrellaba en la cobertura, que se lee como que el disparo
        /// "fallo solo". El barrido se hace a la altura del canon.
        private bool TengoLineaAlObjetivo(SlugActor a)
        {
            if (a == null) return false;
            Vector3 desde = transform.position + Vector3.up * shotHeight;
            Vector3 hasta = a.transform.position + Vector3.up * shotHeight;
            Vector3 d = a.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) desde += d.normalized * 0.9f;
            return SlugShot.LineaDespejada(desde, hasta);
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
            if (GameFeel.Pausa || SlugArenaGame.CombateCongelado)
            {
                _veniaApretado = false;
                Apuntando = false;
                ObjetivoActual = null;
                Cargando = false;
                return;
            }
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
            // La rodada manda sobre la carga: cancelarla evita que el tiro acumulado salga solo
            // al soltar despues de esquivar. El bloqueo dura hasta soltar el boton.
            if (_dodge == null) _dodge = GetComponent<PlayerDodge>();
            if (_dodge != null && _dodge.Rodando && (_carga > 0f || _veniaApretado))
            {
                CancelarCarga();
                return;
            }
            if (_bloqueoHastaSoltar)
            {
                if (!FireHeld() && (_dodge == null || !_dodge.Rodando)) _bloqueoHastaSoltar = false;
                _veniaApretado = false;
                Apuntando = false;
                ObjetivoActual = null;
                Cargando = false;
                return;
            }
            if (FireHeld())
            {
                _carga = Mathf.Min(cargaMax, _carga + Time.deltaTime);
                _veniaApretado = true;
                Cargando = true;
                ApuntarAlObjetivo(Time.deltaTime);
                return;
            }
            Cargando = false;
            if (_veniaApretado)
            {
                _veniaApretado = false;
                Soltar();
            }
            Apuntando = false;
            ObjetivoActual = null;
        }

        /// Mientras se carga, el cuerpo gira hacia el objetivo a la velocidad de apuntado. El
        /// disparo ya no tiene que teletransportar la rotacion: cuando se suelta, el heroe mira
        /// casi exactamente al enemigo y el tiro sale por el canon.
        private void ApuntarAlObjetivo(float dt)
        {
            if (!autoAim) { Apuntando = false; ObjetivoActual = null; return; }
            if (!SigueSiendoValido(ObjetivoActual)) ObjetivoActual = NearestEnemy();
            Apuntando = ObjetivoActual != null;
            if (ObjetivoActual == null) return;

            Vector3 d = ObjetivoActual.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            Quaternion objetivo = Quaternion.LookRotation(d.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, objetivo,
                giroApuntado * dt);
        }

        /// Histéresis del objetivo fijado: se mantiene mientras siga vivo, en alcance y dentro de
        /// un cono apenas mas ancho que el de seleccion. Sin esto, dos enemigos parejos hacian
        /// que el cuerpo saltara entre ellos cuadro a cuadro.
        private bool SigueSiendoValido(SlugActor a)
        {
            if (a == null || a.IsPlayer || !a.Alive || a.Capturada) return false;
            Vector3 d = a.transform.position - transform.position;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq > autoAimRange * autoAimRange) return false;
            if (sq < 0.0001f) return true;
            float cos = Mathf.Cos(ConoEfectivo() * Mathf.Deg2Rad);
            if (Vector3.Dot(d / Mathf.Sqrt(sq), transform.forward) < cos - 0.15f) return false;
            return TengoLineaAlObjetivo(a);
        }

        private SlugActor ObjetivoParaDisparar()
        {
            if (!autoAim) return null;
            if (SigueSiendoValido(ObjetivoActual)) return ObjetivoActual;
            return NearestEnemy();
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
                SlugActor en = ObjetivoParaDisparar();
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

            float t = Mathf.Clamp01(cargaSoltada / Mathf.Max(cargaMax, 0.0001f));
            string slugId = data != null ? data.slugId : "";
            float bonoEvolucionDanio = SlugEvolucion.BonusDanio(slugId);
            float dmg = AjusteDanoTactico(data != null ? data.baseDamage : 30f)
                * shotDamageScale * DanoEscalaPara(t) * _bonoDanioMultiplicador * (1f + bonoEvolucionDanio);
            float velocidadEfectiva = velocidad + SlugEvolucion.BonusVelocidad(slugId);
            // El peso del tiro se cobra aqui, en la recarga de despues: el jugador cambia dano
            // por ventana de vulnerabilidad, que es la decision que el diseno promete.
            // El multiplicador TOTAL se aplica UNA vez: antes se multiplicaba dos veces
            // (la curva de recarga ya traia la penalizacion y encima se le sumaba otra).
            _recargaEfectiva = recargaPorTubo * RecargaEscala(t, penalizacionDisparoPesado);
            int fase = FaseDe(t, velocidadMin, velocidadMax, umbralTransformacion, umbralMegamorph);

            Vector3 pivote = transform.position + Vector3.up * shotHeight;
            Vector3 origen = pivote + dir * 0.9f;
            // Pegado a una cobertura la boca puede quedar dentro de la piedra. En ese caso el
            // tiro nace en el pivote y muere contra el pilar en el primer paso del barrido.
            if (Physics.CheckSphere(origen, 0.18f, ColisionJugador.Mascara)
                || !SlugShot.LineaDespejada(pivote, origen))
                origen = pivote;

            SlugShot shot = SlugShot.Lanzar(origen, dir,
                data != null ? data.element : ElementType.Fire, dmg, true,
                velocidadEfectiva > 0f ? velocidadEfectiva : shotSpeed,
                data != null ? data.elementColor : Color.yellow, 0.4f);
            if (shot == null) return;
            // El tiro tiene que saber quien lo disparo y con cuanta carga: el efecto del elemento
            // cura o roba vida a favor del dueno, y a carga plena es garantizado.
            shot.ConfigurarDisparo(ActorPropio(), t, fase >= 2);
            ultimaVelocidad = velocidad;
            faseActual = fase;
            if (fase >= 1) transformaciones++; else tirosCortos++;
            if (fase >= 2) megamorfosis++;
            // Jingle al transformar el tiro: velocimorph (>=100 MPH) o megamorph.
            var fx = Object.FindFirstObjectByType<SlugFx>();
            if (fx != null) fx.Transformacion(fase);

            shotsFired++;
            SlugEvolucion.GanarXP(slugId, SlugEvolucion.XpDeDisparo);
            SlugAnimator animDelCable = Anim();
            if (animDelCable != null) animDelCable.PlayShot();
            Debug.Log("[PlayerShooter] disparo #" + shotsFired + " elemento=" + (data != null ? data.element.ToString() : "?") + " dano=" + dmg.ToString("F1") + " velocidad=" + velocidadEfectiva.ToString("F1") + "m/s t=" + t.ToString("F2") + " objetivo=" + lastTarget + " fase=" + fase);
        }
    }
}
