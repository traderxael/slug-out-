using UnityEngine;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.World
{
    /// IA de enemigo. Tres arquetipos, cada uno con un counterplay distinto:
    ///  - Embestidor: acecha, AVISA (windup telegrafiado) y embiste en linea recta; queda
    ///    expuesto en la recuperacion. Counterplay: salirse del camino.
    ///  - Saltarin: salta al punto donde estaba el jugador, con aviso y recuperacion.
    ///    Counterplay: no quedarse quieto.
    ///  - Frenetico: cierra haciendo zigzag y muerde seguido pero flojo.
    ///    Counterplay: calcular el tiro, porque en linea recta lo fallas.
    /// Patrones de estado tomados de iDesole/swarmed-worm (MIT). Deteccion por distancia, sin fisica.
    public class EnemySlugAI : MonoBehaviour
    {
        private enum Estado { Acecho, AvisoMordida, AvisoDisparo, DisparoRecup, Carga, Embestida, Recuperacion, AvisoSalto, Aire, Aterrizaje, AvisoEsquiva, Esquiva, EsquivaRecup }

        [SerializeField] private Transform target;
        [SerializeField] private float speed = 3.2f;
        [SerializeField] private float attackRange = 1.8f;
        [SerializeField] private float attackCooldown = 1.4f;
        [SerializeField] private float attackDamage = 7f;
        [SerializeField] private int killsRegistered = 0;

        [Header("Arquetipo")]
        [SerializeField] private Arquetipo arquetipo = Arquetipo.Frenetico;

        [Header("Embestidor (valores de swarmed-worm, MIT)")]
        [SerializeField] private float cargaDuracion = 1.05f;
        [SerializeField] private float cargaAtrasRatio = 0.4f;
        [SerializeField] private float cargaAtrasVelocidad = 0.4f;
        [SerializeField] private float embestidaDuracion = 1.1f;
        [SerializeField] private float embestidaMultiplicador = 2f;
        [SerializeField] private float embestidaAceleracion = 0.45f;
        [SerializeField] private float recuperacionDuracion = 0.6f;
        [SerializeField] private float enfriamientoEmbestida = 4.5f;
        [SerializeField] private float distanciaMinimaEmbestida = 2f;
        [SerializeField] private float rangoEmbestida = 9f;

        [Header("Saltarin")]
        [SerializeField] private float avisoSaltoDuracion = 0.45f;
        [SerializeField] private float saltoDuracion = 0.55f;
        [SerializeField] private float saltoAltura = 1.7f;
        [SerializeField] private float saltoRadio = 2.2f;
        [SerializeField] private float saltoMultiplicador = 1.6f;
        [SerializeField] private float saltoRecuperacion = 0.4f;
        [SerializeField] private float saltoEnfriamiento = 3.2f;
        // minima por DEBAJO del rango de contacto (1.8): si no, el saltarin llega caminando
        // al jugador antes de que le enfríe el salto y su mecanica propia nunca se ve
        [SerializeField] private float saltoDistanciaMinima = 1.2f;
        [SerializeField] private float saltoRango = 10f;

        [Header("Frenetico")]
        [SerializeField] private float zigzagAmplitud = 0.62f;
        [SerializeField] private float zigzagFrecuencia = 4.2f;

        [Header("Mordida basica (los tres arquetipos)")]
        // El mordisco era instantaneo: a rango de contacto no habia forma de leerlo y la rodada
        // no servia contra el ataque mas comun. Ahora avisa con el mismo pulso naranja del
        // embestidor y el golpe se resuelve al final; si el jugador salio del alcance, falla.
        [SerializeField] private float mordidaAviso = 0.22f;
        [Tooltip("Giro lento durante el aviso: permite esquivar de costado, no congelarse.")]
        [SerializeField] private float giroAviso = 180f;
        [SerializeField] private int mordidas = 0;
        [SerializeField] private int mordidasAlAire = 0;

        [Header("Separacion (para que no se apilen)")]
        [Tooltip("Distancia a la que dos babosas se empujan. Sin esto cuatro perseguidores ocupan el mismo punto y tapan la pelea.")]
        [SerializeField] private float separacionRadio = 1.2f;
        [SerializeField] private float separacionFuerza = 0.65f;

        [Header("Escupidor (a distancia)")]
        [SerializeField] private float disparoAviso = 0.5f;
        [SerializeField] private float disparoRecuperacion = 0.4f;
        [SerializeField] private float disparoEnfriamiento = 2.2f;
        [SerializeField] private float disparoRangoMin = 4.5f;
        [SerializeField] private float disparoRangoMax = 13f;
        [SerializeField] private float disparoRetroceso = 4.5f;
        [SerializeField] private float disparoVelocidad = 18f;
        [SerializeField] private float disparoDanoEscala = 1.15f;
        [Tooltip("Fraccion de la velocidad del jugador que se adelanta al apuntar.")]
        [SerializeField] private float disparoPlomo = 0.7f;
        [SerializeField] private int disparos = 0;

        [Header("Esquiva telegrafiada (los tres arquetipos)")]
        // Ventana de aviso: corta a proposito, 200 ms, porque es lo que hace legible la mecanica
        // (el jugador ve el tell y aun asi puede corregir el apuntado).
        [SerializeField] private float agachadaDuracion = 0.2f;
        [SerializeField] private float pasoDuracion = 0.26f;
        [SerializeField] private float pasoVelocidadRatio = 2.2f;
        [SerializeField] private float esquivaRecuperacion = 0.34f;
        [SerializeField] private float enfriamientoEsquiva = 1.8f;
        [SerializeField] private float aturdimientoPorCancelacion = 0.42f;
        // Medio cuerpo: lo que hay que sacar del corredor del tiro para que no toque. Coincide
        // con el radius de SlugShot (0.8), asi que la ley y la colision hablan en los mismos metros.
        [SerializeField] private float margenCuerpo = 0.9f;

        [Tooltip("Cuanto sobresale el pivot del personaje sobre el piso.")]
        [SerializeField] private float alturaSuelo = 0f;

        [Header("Colision")]
        [Tooltip("La cobertura corta tiros Y frena cuerpos: el mismo barrido que usa el jugador.")]
        [SerializeField] private float radioCuerpo = 0.5f;
        [SerializeField] private float altoCuerpo = 2f;

        [Header("Estado (solo lectura)")]
        [SerializeField] private Estado estado = Estado.Acecho;
        [SerializeField] private float progresoAviso = 0f;
        [SerializeField] private int embestidas = 0;
        [SerializeField] private int saltos = 0;
        [SerializeField] private int aterrizajesConGolpe = 0;
        [SerializeField] private int invocaciones = 0;     // tejedor: sífilos invocados
        [SerializeField] private int entierros = 0;        // excavador: veces enterrado
        [SerializeField] private float alturaActual = 0f;
        [SerializeField] private int esquivas = 0;
        [SerializeField] private int esquivasCanceladas = 0;

        private float _cd;
        private float _t;
        private float _cdEmbestida;
        private float _cdSalto;
        private float _cdEsquiva;
        private float _cdDisparo;
        private float _cdTejedor;      // temporizador de invocación/soporte del tejedor
        private float _cdExcavador;    // temporizador de enterrar/salir del excavador
        private Vector3 _rumboEsquiva;
        private float _fase;
        private Vector3 _rumbo;
        private Vector3 _origen;
        private Vector3 _destino;
        private float _yBase;
        private float _yDestino;
        private SlugActor _self;
        private SlugActor _foe;
        private SlugAnimator _anim;
        // Reloj PROPIO de la partida. Time.time es el reloj de la escena: cambia con cuanto tardaste
        // en entrar al nivel, asi que dos intentos iguales no dibujaban el mismo zigzag y la
        // promesa de "reproducible sin fisica" (encabezado de SlugArenaGame) era falsa.
        private float _reloj;

        /// Semilla del run y contador de instancias: cada bicho deriva su offset del par
        /// (semilla, orden de nacimiento), no del Random global de Unity, que se mueve con
        /// cualquier otro Random.Range del proyecto (hitstop, sacudida, retratos).
        static int _semillaRun = 1;
        static int _nacimientos;

        public static void NuevaSemillaDeRun(int semilla)
        {
            _semillaRun = semilla;
            _nacimientos = 0;
        }

        public static int SemillaActual { get { return _semillaRun; } }
        public float Reloj { get { return _reloj; } }

        public enum Arquetipo { Embestidor, Saltarin, Frenetico, Escupidor, Volador, Tejedor, Excavador }

        /// 0 = sin aviso; crece a 1 durante el windup del embestidor, la agachada del saltarin o la
        /// ventanada de la esquiva. SlugAnimator lo lee para pintar el tell, asi que los tres
        /// arquetipos y la esquiva avisan igual: no hay un sistema de telegrafia paralelo.
        public float ProgresoAviso { get { return progresoAviso; } }
        /// Contadores de la mordida: la que llega y la que el jugador esquivo saliendo del alcance.
        public int Mordidas { get { return mordidas; } }
        public int MordidasAlAire { get { return mordidasAlAire; } }
        public bool Mordiendo { get { return estado == Estado.AvisoMordida; } }
        public int Disparos { get { return disparos; } }
        public bool Disparando { get { return estado == Estado.AvisoDisparo || estado == Estado.DisparoRecup; } }
        /// Queda al descubierto despues de embestir, de aterrizar y de esquivar: el castigo de su
        /// propia mecanica, para que esquivar no sea gratis.
        public bool EstaExpuesto { get { return estado == Estado.Recuperacion || estado == Estado.Aterrizaje || estado == Estado.EsquivaRecup || estado == Estado.DisparoRecup; } }
        public Arquetipo ArquetipoActual { get { return arquetipo; } }
        public int Embestidas { get { return embestidas; } }
        public int Saltos { get { return saltos; } }
        public int AterrizajesConGolpe { get { return aterrizajesConGolpe; } }
        public float Altura { get { return alturaActual; } }
        public bool EnAire { get { return estado == Estado.Aire; } }
        public bool EnEsquiva { get { return estado == Estado.AvisoEsquiva || estado == Estado.Esquiva; } }
        public int Esquivas { get { return esquivas; } }
        public int EsquivasCanceladas { get { return esquivasCanceladas; } }
        /// El lado que va a tomar: se elige por geometria, nunca por azar, para que la lectura del
        /// jugador y las sondas puedan reproducirlo.
        public Vector3 RumboEsquiva { get { return _rumboEsquiva; } }

        /// El arquetipo se deriva de los stats que ya tiene cada personaje: no hace falta
        /// tocar los 22 .asset para repartir roles.
        public static Arquetipo ArquetipoDe(SlugData d)
        {
            return ArquetipoDe(d, d != null ? d.rolEnemigo : SlugData.RolEnemigo.Normal);
        }

        /// Igual, pero con el rol ya decidido: lo fija el asset o lo reparte la campana
        /// (ver SlugArenaGame.RolDeCampana). El rol siempre manda sobre los stats.
        public static Arquetipo ArquetipoDe(SlugData d, SlugData.RolEnemigo rol)
        {
            if (rol == SlugData.RolEnemigo.Volador) return Arquetipo.Volador;
            if (rol == SlugData.RolEnemigo.Tejedor) return Arquetipo.Tejedor;
            if (rol == SlugData.RolEnemigo.Excavador) return Arquetipo.Excavador;
            if (d == null) return Arquetipo.Frenetico;
            if (d.clashPower >= 60f) return Arquetipo.Embestidor;
            if (d.clashPower < 45f && d.accelerationRate >= 90f) return Arquetipo.Saltarin;
            // Cuarto rol: el unico stat que nadie usaba. Va despues de los otros dos para no
            // robarles sus siluetas; con el roster actual quedan 30/5/6/23.
            if (d.launchSpeedMph >= 55f) return Arquetipo.Escupidor;
            return Arquetipo.Frenetico;
        }

        public void SetTarget(Transform t) { target = t; }
        public void SetSpeed(float s) { speed = s; }
        public float GetSpeed() { return speed; }
        public void SetDamage(float d) { attackDamage = d; }
        public float GetDamage() { return attackDamage; }
        public void SetArquetipo(Arquetipo a)
        {
            arquetipo = a;
            if (a == Arquetipo.Saltarin) { speed *= 1.25f; attackCooldown *= 0.8f; }
            else if (a == Arquetipo.Frenetico) { attackCooldown *= 0.6f; attackDamage *= 0.7f; }
            else if (a == Arquetipo.Escupidor) { speed *= 0.9f; attackCooldown *= 1.2f; attackDamage *= 1.1f; }
            else if (a == Arquetipo.Volador) { speed *= 1.1f; attackCooldown *= 1.1f; }
            else if (a == Arquetipo.Tejedor) { speed *= 0.85f; attackCooldown *= 1.4f; attackDamage *= 0.8f; }
            else if (a == Arquetipo.Excavador) { speed *= 0.75f; attackCooldown *= 1.6f; attackDamage *= 1.2f; }
        }

        /// Resolucion PEREZOSA: en SpawnEnemy el animator se agrega DESPUES de este
        /// componente — cachear en Awake lo deja en NULL para siempre.
        private SlugAnimator Anim()
        {
            if (_anim == null) _anim = GetComponent<SlugAnimator>();
            return _anim;
        }

        private void Awake()
        {
            _self = GetComponent<SlugActor>();
        }

        /// El dodge-cancel necesita saber si lo golpearon, y SlugActor ya emite Damaged para el
        /// HUD: se engancha ahi en vez de mirar la vida por frame (que no distinguiria un golpe
        /// de una cura, y menos todavia en que estado estaba la esquiva).
        private void OnEnable() { SlugActor.Damaged += OnDamaged; }

        private void OnDisable() { SlugActor.Damaged -= OnDamaged; }

        private void Start()
        {
            if (target == null)
            {
                PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
                if (pc != null) target = pc.transform;
            }
            if (target != null) _foe = target.GetComponent<SlugActor>();
            _cd = attackCooldown * 0.6f;
            _cdEmbestida = 1.2f;
            _cdTejedor = 8f;
            _cdExcavador = 6f;
            // el primer salto cae durante la aproximacion (llega a los ~3u), no despues de
            // plantarse en tu cara; cada saltarin despegua en un momento distinto, pero el MISMO
            // momento distinto si el run tiene la misma semilla
            System.Random rng = new System.Random(_semillaRun + ++_nacimientos * 7919);
            _cdSalto = 0.6f + (float)rng.NextDouble() * 0.9f;
            _fase = (float)rng.NextDouble() * 6.2831855f;
            _cdDisparo = 1.0f + (float)rng.NextDouble() * 0.8f;
            _yBase = Terrain3D.Altura(transform.position.x, transform.position.z) + alturaSuelo;
        }

        private void Update()
        {
            // El cartel de GANASTE/PERDISTE no detenia a nadie: los enemigos seguian mordiendo
            // despues de terminada la partida (y el player ya no mueve, asi que era daño gratis).
            if (SlugArenaGame.CombateCongelado) return;
            _reloj += Time.deltaTime;
            if (_self != null && !_self.Alive) return;
            if (_self != null && _self.Capturada) return;
            if (_self != null && _self.EstaAfectado) return;   // congelado o aturdido: no se mueve ni pega
            if (target == null) return;

            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;

            // La esquiva manda sobre los tres arquetipos y se resuelve aqui: si viviera dentro de
            // cada Pelea*, el embestidor tendria que conocerla en Carga, el saltarin en Aire y el
            // frenetico en su rama, y tres copias de la misma ley se divergen.
            _cdEsquiva -= Time.deltaTime;
            if (estado == Estado.AvisoEsquiva || estado == Estado.Esquiva || estado == Estado.EsquivaRecup)
            {
                PeleaEsquiva(Time.deltaTime);
                return;
            }
            if (estado == Estado.Acecho && _cdEsquiva <= 0f && TiroEntrante())
            {
                EmpezarEsquiva();
                return;
            }

            // La mordida avisada manda sobre el arquetipo: mientras corre el aviso el bicho no
            // cambia de idea ni salta, y el golpe se decide al final del aviso.
            if (estado == Estado.AvisoMordida)
            {
                PeleaMordida(Time.deltaTime);
                return;
            }

            if (estado == Estado.AvisoDisparo || estado == Estado.DisparoRecup)
            {
                PeleaEscupidor(Time.deltaTime);
                return;
            }

            switch (arquetipo)
            {
                case Arquetipo.Saltarin: PeleaSaltarin(dist, delta); return;
                case Arquetipo.Frenetico: progresoAviso = 0f; PeleaBasica(dist, delta); return;
                case Arquetipo.Escupidor: PeleaEscupidor(Time.deltaTime); return;
                case Arquetipo.Volador: PeleaVolador(dist, delta); return;
                case Arquetipo.Tejedor: PeleaTejedor(Time.deltaTime); return;
                case Arquetipo.Excavador: PeleaExcavador(dist, delta, Time.deltaTime); return;
            }

            _cdEmbestida -= Time.deltaTime;

            switch (estado)
            {
                case Estado.Acecho:
                    progresoAviso = 0f;
                    if (dist > rangoEmbestida)
                    {
                        Mover(delta / Mathf.Max(dist, 0.001f), speed);
                        return;
                    }
                    // el aviso tiene prioridad sobre el mordisco: si no, el enemigo entra a rango de
                    // contacto, muerde cada 0.84s y el windup no llega a arrancar nunca
                    if (_cdEmbestida <= 0f && dist > distanciaMinimaEmbestida && TengoLinea()) { EmpezarCarga(delta); return; }
                    Separar(Time.deltaTime);
                    _cd -= Time.deltaTime;
                    if (_cd <= 0f && TengoLinea()) EmpezarMordida();
                    return;

                case Estado.Carga:
                    _t += Time.deltaTime;
                    float k = Mathf.Clamp01(_t / cargaDuracion);
                    progresoAviso = k;
                    if (k < cargaAtrasRatio) MoverConColision(-_rumbo * speed * cargaAtrasVelocidad * Time.deltaTime);
                    if (k >= 1f)
                    {
                        estado = Estado.Embestida; _t = 0f; embestidas++;
                        SlugAnimator a = Anim();
                        if (a != null) a.PlayShot();
                    }
                    return;

                case Estado.Embestida:
                    _t += Time.deltaTime;
                    float e = Mathf.Clamp01(_t / embestidaDuracion);
                    float acelera = Mathf.Clamp01(_t / embestidaAceleracion);
                    MoverConColision(_rumbo * speed * embestidaMultiplicador * acelera * Time.deltaTime);
                    GolpearEnContacto(1.5f);
                    if (_t >= embestidaDuracion)
                    {
                        estado = Estado.Recuperacion; _t = 0f;
                        progresoAviso = 0f;
                        _cdEmbestida = enfriamientoEmbestida;
                    }
                    return;

                case Estado.Recuperacion:
                    _t += Time.deltaTime;
                    if (_t >= recuperacionDuracion) { estado = Estado.Acecho; _t = 0f; }
                    return;
            }
        }

        private void EmpezarCarga(Vector3 delta)
        {
            estado = Estado.Carga;
            _t = 0f;
            _rumbo = delta.sqrMagnitude > 0.0001f ? delta.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(_rumbo, Vector3.up);
            DuelistaAnimator.NotificarAtaque(gameObject);
        }

        /// La ley sola, sin escena: un tiro a `distancia` metros que llega en `distancia/velocidad`
        /// segundos se esquiva si el cuerpo puede salir del corredor (pasoLateral > margen) y el
        /// tiro no esta ni pegado ni fuera de alcance. Publicarla en un estatico es lo que permite
        /// probarla en EditMode: PeleaEsquiva depende de Time.deltaTime y ese no avanza sin Play.
        public static bool VentanaDeEsquiva(float distancia, float velocidadTiro, float pasoLateral, float tiempoVentana, float margen)
        {
            if (pasoLateral < margen) return false;                     // lento: no saca el cuerpo
            if (distancia < margen) return false;                       // ya encima: avisar regala el tell
            return distancia <= velocidadTiro * tiempoVentana + margen; // alcanza a tiempo
        }

        /// Empuje horizontal para que dos perseguidores no ocupen el mismo punto. Es una regla
        /// pura y determinista (sin Random ni fisica): cuando dos cuerpos coinciden exactamente,
        /// el lado lo decide el nombre, asi uno empuja a +X y el otro a -X en el mismo cuadro.
        public static Vector3 Separacion(Vector3 propio,
            System.Collections.Generic.IReadOnlyList<SlugActor> actores,
            SlugActor yo, float radio)
        {
            Vector3 empuje = Vector3.zero;
            if (actores == null || radio <= 0.0001f) return empuje;

            // El orden de la lista es el orden de nacimiento (OnEnable): sirve de desempate
            // estable cuando dos cuerpos coinciden exactamente.
            int indiceYo = -1;
            for (int i = 0, n = actores.Count; i < n; i++)
                if (actores[i] == yo) { indiceYo = i; break; }

            for (int i = 0, n = actores.Count; i < n; i++)
            {
                SlugActor a = actores[i];
                if (a == null || a == yo || a.IsPlayer || !a.Alive || a.Capturada) continue;
                Vector3 d = propio - a.transform.position;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq >= radio * radio) continue;

                if (sq < 0.0001f)
                {
                    // Dos bichos en el mismo punto: el que nacio antes empuja a +X y el otro a
                    // -X. Sin esto los dos elegirian el mismo lado y seguirian pegados.
                    float signo = indiceYo >= 0 && indiceYo > i ? -1f : 1f;
                    empuje += new Vector3(signo, 0f, 0f);
                    continue;
                }

                float dist = Mathf.Sqrt(sq);
                empuje += d / dist * (1f - dist / radio);
            }
            return empuje;
        }

        /// Esquiva solo si (a) el corredor del tiro le barre el cuerpo, (b) el paso lateral que le
        /// permite su velocidad alcanza para sacar medio cuerpo del corredor, y (c) la ventana da
        /// tiempo. Un bicho lento jamas esquiva y un tiro ya encima tampoco: no hay azar en la ley.
        private bool TiroEntrante()
        {
            float pasoLateral = speed * pasoVelocidadRatio * pasoDuracion;
            if (pasoLateral < margenCuerpo) return false;

            System.Collections.Generic.IReadOnlyList<SlugShot> tiros = SlugShot.Vivas;
            for (int i = tiros.Count - 1; i >= 0; i--)
            {
                SlugShot s = tiros[i];
                if (s == null || !s.DesdeJugador) continue;

                Vector3 rumbo = s.Direccion; rumbo.y = 0f;
                if (rumbo.sqrMagnitude < 0.0001f) continue;
                rumbo.Normalize();

                Vector3 origen = s.transform.position; origen.y = 0f;
                Vector3 yo = transform.position; yo.y = 0f;
                Vector3 haciaMi = yo - origen;
                float distancia = haciaMi.magnitude;
                float velocidad = Mathf.Max(s.Velocidad, 0.001f);
                if (!VentanaDeEsquiva(distancia, velocidad, pasoLateral, agachadaDuracion + pasoDuracion, margenCuerpo)) continue;

                // Mismo barrido que usa la colision: si el segmento que recorre en su ventana de
                // esquiva no pasa a menos de margenCuerpo, no habia nada que esquivar.
                if (!SlugShot.AciertaBarrida(origen, origen + rumbo * (distancia + 1f), yo, margenCuerpo)) continue;

                Vector3 perp = Vector3.Cross(Vector3.up, rumbo);
                // El lado lo elige la geometria: el flanco que lo deja del lado del tirador, para
                // que esquivar no sea tambien alejarse del combate.
                _rumboEsquiva = Vector3.Dot(perp, -haciaMi) >= 0f ? perp : -perp;
                return true;
            }
            return false;
        }

        private void EmpezarEsquiva()
        {
            estado = Estado.AvisoEsquiva;
            _t = 0f;
            progresoAviso = 0f;
        }

        /// dt como argumento por el mismo motivo que SlugShot.Paso: permite integrar la maquina en
        /// una sonda EditMode, donde Time.deltaTime no avanza.
        public void PeleaEsquiva(float dt)
        {
            _t += dt;

            if (estado == Estado.AvisoEsquiva)
            {
                float k = Mathf.Clamp01(_t / agachadaDuracion);
                progresoAviso = k;              // el anillo cierra con el mismo tell del windup
                if (k >= 1f) { estado = Estado.Esquiva; _t = 0f; }
                return;
            }

            if (estado == Estado.Esquiva)
            {
                progresoAviso = 1f;             // agachado: el aviso queda pleno mientras se mueve
                MoverConColision(_rumboEsquiva * (speed * pasoVelocidadRatio * dt));
                if (_t >= pasoDuracion)
                {
                    esquivas++;
                    estado = Estado.EsquivaRecup;
                    _t = 0f;
                    progresoAviso = 0f;
                }
                return;
            }

            if (_t >= esquivaRecuperacion)
            {
                estado = Estado.Acecho;
                _t = 0f;
                _cdEsquiva = enfriamientoEsquiva;
            }
        }

        /// Maquina del Escupidor. Aviso y recuperacion se gatean por dt para poder integrarlas
        /// en EditMode; el movimiento tambien usa dt, no Time.deltaTime.
        public void PeleaEscupidor(float dt)
        {
            if (target == null) return;
            Vector3 delta = target.position - transform.position; delta.y = 0f;
            float dist = delta.magnitude;

            if (estado == Estado.AvisoDisparo)
            {
                _t += dt;
                progresoAviso = Mathf.Clamp01(_t / Mathf.Max(0.0001f, disparoAviso));
                MirarHacia(delta, 120f, dt);
                if (_t < disparoAviso) return;
                progresoAviso = 0f;
                if (TengoLinea())
                {
                    DispararAlJugador();
                    estado = Estado.DisparoRecup;
                    _t = 0f;
                }
                else
                {
                    // Perdio la linea durante el aviso: la cobertura gana esta ronda.
                    estado = Estado.Acecho;
                    _t = 0f;
                    _cdDisparo = 0.4f;
                }
                return;
            }

            if (estado == Estado.DisparoRecup)
            {
                _t += dt;
                if (_t >= disparoRecuperacion)
                {
                    estado = Estado.Acecho;
                    _t = 0f;
                    _cdDisparo = disparoEnfriamiento;
                }
                return;
            }

            if (estado == Estado.AvisoMordida) { PeleaMordida(dt); return; }

            _cdDisparo -= dt;
            _cd -= dt;
            MirarHacia(delta, 360f, dt);

            if (dist < disparoRetroceso)
            {
                if (dist > 0.001f)
                {
                    bool retrocedio = AvanzarUtil(-delta / dist, speed * 0.9f, dt);
                    if (retrocedio) return;
                    Vector3 d = target.position - transform.position;
                    d.y = 0f;
                    dist = d.magnitude;
                }
                // Acorralado dentro del alcance: la retirada no lo salva y muerde flojo.
                if (dist <= attackRange && _cd <= 0f && TengoLinea())
                {
                    EmpezarMordida();
                    return;
                }
                if (dist > 0.001f) Avanzar(-(target.position - transform.position) / dist, speed * 0.7f, dt);
                return;
            }
            if (dist > disparoRangoMax)
            {
                Avanzar(delta / dist, speed * 0.9f, dt);
                return;
            }

            // En rango: orbita de costado (determinista) y dispara si hay linea.
            Vector3 perp = Vector3.Cross(Vector3.up, delta / Mathf.Max(dist, 0.001f));
            float lado = Mathf.Sin(_reloj * 1.7f + _fase) >= 0f ? 1f : -1f;
            Avanzar(perp * lado, speed * 0.45f, dt);
            if (_cdDisparo <= 0f && dist >= disparoRangoMin && dist <= disparoRangoMax && TengoLinea())
                EmpezarAvisoDisparo(delta);
        }

        private void EmpezarAvisoDisparo(Vector3 delta)
        {
            estado = Estado.AvisoDisparo;
            _t = 0f;
            progresoAviso = 0f;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            DuelistaAnimator.NotificarAtaque(gameObject);
            // Ensenanza del counterplay en el primer aviso que ve el jugador.
            SlugActor objetivo = target != null ? target.GetComponent<SlugActor>() : null;
            if (objetivo != null && objetivo.IsPlayer) GameHUD.MostrarAvisoEscupidor();
        }

        private void DispararAlJugador()
        {
            if (target == null) return;
            Vector3 origen = transform.position + Vector3.up * 0.8f;
            Vector3 destino = target.position + Vector3.up * 0.8f;
            PlayerController pc = target.GetComponent<PlayerController>();
            Vector3 vel = Vector3.zero;
            if (pc != null)
            {
                Vector3 m = pc.MoveInput; m.y = 0f;
                if (m.sqrMagnitude > 0.0001f) vel = m.normalized * pc.Velocidad;
            }
            float dist = Vector3.Distance(origen, destino);
            float tiempo = dist / Mathf.Max(0.01f, disparoVelocidad);
            destino += vel * (tiempo * disparoPlomo);
            Vector3 dir = destino - origen;
            if (dir.sqrMagnitude < 0.0001f) return;

            ElementType e = _self != null ? _self.Element : ElementType.Earth;
            Color color = _self != null && _self.Data != null ? _self.Data.elementColor : Color.yellow;
            SlugShot.Lanzar(origen, dir, e, attackDamage * disparoDanoEscala, false,
                disparoVelocidad, color, 0.34f);
            SlugAnimator anim = Anim();
            if (anim != null) anim.PlayShot();
            disparos++;
            Debug.Log("[EnemySlugAI] " + name + ": escupitajo #" + disparos
                + " dist=" + dist.ToString("F1") + "m");
        }

        private bool TengoLinea()
        {
            if (target == null) return false;
            return SlugShot.LineaDespejada(transform.position + Vector3.up * 0.8f,
                target.position + Vector3.up * 0.8f);
        }

        private void MirarHacia(Vector3 dir, float gradosPorSegundo, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(dir.normalized, Vector3.up), gradosPorSegundo * dt);
        }

        private void Avanzar(Vector3 dir, float s, float dt)
        {
            Vector3 sep = Separacion(transform.position, SlugActor.Vivos, Self(), separacionRadio) * separacionFuerza;
            dir += sep;
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
            MoverConColision(dir * s * dt);
        }

        /// Igual que Avanzar pero devuelve si el cuerpo avanzo de verdad. La retirada del
        /// Escupidor necesita distinguir una cobertura que lo frena de un deslizamiento util.
        private bool AvanzarUtil(Vector3 dir, float s, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) { ApoyarEnPiso(); return false; }
            dir.Normalize();
            Vector3 sep = Separacion(transform.position, SlugActor.Vivos, Self(), separacionRadio) * separacionFuerza;
            Vector3 final = dir + sep;
            if (final.sqrMagnitude < 0.0001f) final = dir;
            else final.Normalize();

            Vector3 antes = transform.position;
            float pedido = s * dt;
            MoverConColision(final * pedido);
            Vector3 real = transform.position - antes;
            real.y = 0f;
            return Vector3.Dot(real, dir) >= pedido * 0.1f;
        }

        /// Arranca el aviso de mordida. Publico porque las pruebas EditMode integran el aviso
        /// con PeleaMordida(dt), igual que la esquiva; Update lo llama desde Acecho y PeleaBasica.
        public void EmpezarMordida()
        {
            if (estado == Estado.AvisoMordida) return;
            estado = Estado.AvisoMordida;
            _t = 0f;
            progresoAviso = 0f;
            // El enfriamiento corre desde el aviso: el aviso no es tiempo regalado de recarga.
            _cd = attackCooldown;
        }

        /// El golpe se resuelve al FINAL del aviso. Si el jugador salio del alcance, la mordida
        /// falla y se cuenta: con la rodada (3,6 m) o caminando de costado ahora hay contrarrespuesta
        /// contra el ataque cuerpo a cuerpo, no solo contra los tiros.
        public void PeleaMordida(float dt)
        {
            if (estado != Estado.AvisoMordida) return;   // idempotente para las pruebas y para Update
            _t += dt;
            progresoAviso = Mathf.Clamp01(_t / Mathf.Max(0.0001f, mordidaAviso));

            // Sigue girando lento hacia el objetivo: quedarse quieto no alcanza, hay que moverse.
            if (target != null)
            {
                Vector3 d = target.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(d.normalized, Vector3.up), giroAviso * dt);
            }

            if (_t < mordidaAviso) return;

            progresoAviso = 0f;
            estado = Estado.Acecho;
            if (target == null) return;
            Vector3 dif = target.position - transform.position;
            dif.y = 0f;
            if (dif.sqrMagnitude <= attackRange * attackRange)
            {
                mordidas++;
                // El Escupidor muerde flojo: su amenaza es el proyectil; acorralarlo no debe
                // castigar como un cuerpo a cuerpo completo.
                Golpear(arquetipo == Arquetipo.Escupidor ? 0.6f : 1f);
            }
            else
            {
                mordidasAlAire++;
                Debug.Log("[EnemySlugAI] " + name + ": mordida al aire (el objetivo salio del alcance)");
            }
        }

        /// Golpearlo dentro de la ventanada de aviso cancela la esquiva y lo aturde: es el
        /// counterplay del dodge, y por eso el aviso tiene que ser visible.
        private void OnDamaged(SlugActor actor, float dano, float multiplicador)
        {
            if (actor != _self || estado != Estado.AvisoEsquiva) return;
            esquivasCanceladas++;
            estado = Estado.Acecho;
            _t = 0f;
            progresoAviso = 0f;
            _cdEsquiva = enfriamientoEsquiva;
            if (_self != null) _self.Aturdir(aturdimientoPorCancelacion);
            Debug.Log("[EnemySlugAI] " + name + ": esquiva cancelada por el golpe, aturdido " +
                      aturdimientoPorCancelacion.ToString("F2") + "s (esquivas=" + esquivas +
                      ", canceladas=" + esquivasCanceladas + ")");
        }

        private void Mover(Vector3 dir, float s)
        {
            // La separacion se suma a la direccion de avance: no frena la persecucion, la abre
            // en abanico cuando dos bichos eligen el mismo camino.
            dir += Separacion(transform.position, SlugActor.Vivos, Self(), separacionRadio) * separacionFuerza;
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up), 6f * Time.deltaTime);
            }
            MoverConColision(dir * s * Time.deltaTime);
        }

        /// Mueve en horizontal con el mismo barrido del jugador. Sin esto la cobertura corta
        /// los tiros pero el embestidor la atraviesa, y el pilar deja de leerse como solido.
        private void MoverConColision(Vector3 delta)
        {
            delta.y = 0f;
            Vector3 centro = transform.position + Vector3.up * (altoCuerpo * 0.5f);
            if (ColisionJugador.SobreMuro(centro, radioCuerpo, altoCuerpo))
            {
                // Nacido o aterrizado dentro de un proxy: el barrido no resuelve solape inicial.
                Vector3 libre = ColisionJugador.PuntoLibre(centro, Vector3.zero,
                    radioCuerpo, altoCuerpo);
                transform.position = new Vector3(libre.x, transform.position.y, libre.z);
                Physics.SyncTransforms();
                centro = transform.position + Vector3.up * (altoCuerpo * 0.5f);
            }
            if (delta.sqrMagnitude < 0.0000001f) { ApoyarEnPiso(); return; }
            Vector3 fin = ColisionJugador.Deslizar(centro, delta, radioCuerpo, altoCuerpo,
                out _, out _);
            transform.position = new Vector3(fin.x, transform.position.y, fin.z);
            ApoyarEnPiso();
        }

        /// Resolucion perezosa, como Anim(): el SlugActor puede agregarse antes o despues.
        private SlugActor Self()
        {
            if (_self == null) _self = GetComponent<SlugActor>();
            return _self;
        }

        /// Empuje suave cuando el bicho ya esta en rango y no camina: sin esto los cuatro se
        /// quedan plantados uno encima del otro alrededor del jugador.
        private void Separar(float dt)
        {
            Vector3 sep = Separacion(transform.position, SlugActor.Vivos, Self(), separacionRadio);
            if (sep.sqrMagnitude <= 0.0001f) return;
            MoverConColision(sep.normalized * speed * separacionFuerza * 0.5f * dt);
        }

        /// Mismo terreno que pisa el jugador: la altura sale de la misma funcion estatica.
        private void ApoyarEnPiso()
        {
            Vector3 p = transform.position;
            _yBase = Terrain3D.Altura(p.x, p.z) + alturaSuelo;
            if (!Mathf.Approximately(p.y, _yBase)) transform.position = new Vector3(p.x, _yBase, p.z);
        }

        /// Maquina del saltarin: agachada con aviso -> arco -> onda de aterrizaje -> queda
        /// expuesto. El destino se fija AL DESPEGAR, asi que quedarse quieto durante el aviso
        /// es exactamente lo que te hace quedar adentro del circulo.
        private void PeleaSaltarin(float dist, Vector3 delta)
        {
            _cdSalto -= Time.deltaTime;
            switch (estado)
            {
                case Estado.AvisoSalto:
                    _t += Time.deltaTime;
                    float ka = Mathf.Clamp01(_t / avisoSaltoDuracion);
                    progresoAviso = ka;
                    if (ka >= 1f) Despegar(delta);
                    return;

                case Estado.Aire:
                    _t += Time.deltaTime;
                    float k = Mathf.Clamp01(_t / saltoDuracion);
                    Vector3 piso = Vector3.Lerp(_origen, _destino, Mathf.SmoothStep(0f, 1f, k));
                    alturaActual = 4f * saltoAltura * k * (1f - k);
                    // el piso tambien sube o baja entre los dos puntos: si no, un salto cuesta
                    // arriba termina clavado en la ladera
                    float yPiso = Mathf.Lerp(_yBase, _yDestino, k);
                    transform.position = new Vector3(piso.x, yPiso + alturaActual, piso.z);
                    if (k >= 1f) Aterrizar();
                    return;

                case Estado.Aterrizaje:
                    _t += Time.deltaTime;
                    progresoAviso = 0f;
                    if (_t >= saltoRecuperacion) { estado = Estado.Acecho; _t = 0f; }
                    return;

                default:
                    progresoAviso = 0f;
                    // Tampoco salta a traves de una cobertura: el arco mide 1,7 m y un pilar
                    // de 2,8 m lo corta, asi que el salto se cancelaria en el aire sin leerse.
                    if (_cdSalto <= 0f && dist > saltoDistanciaMinima && dist < saltoRango
                        && SlugShot.LineaDespejada(transform.position + Vector3.up,
                            target.position + Vector3.up))
                    {
                        estado = Estado.AvisoSalto;
                        _t = 0f;
                        _rumbo = delta.sqrMagnitude > 0.0001f ? delta.normalized : transform.forward;
                        transform.rotation = Quaternion.LookRotation(_rumbo, Vector3.up);
                        return;
                    }
                    PeleaBasica(dist, delta);
                    return;
            }
        }

        private void Despegar(Vector3 delta)
        {
            estado = Estado.Aire;
            _t = 0f;
            saltos++;
            progresoAviso = 0f;
            _origen = new Vector3(transform.position.x, _yBase, transform.position.z);
            Vector3 hacia = target.position;
            hacia.y = _yBase;
            Vector3 v = hacia - _origen;
            float d = v.magnitude;
            if (d > saltoRango) v = v / Mathf.Max(d, 0.0001f) * saltoRango;
            _destino = _origen + v;
            _yBase = Terrain3D.Altura(_origen.x, _origen.z) + alturaSuelo;
            _yDestino = Terrain3D.Altura(_destino.x, _destino.z) + alturaSuelo;
            if (d > 0.0001f) transform.rotation = Quaternion.LookRotation(v.normalized, Vector3.up);
        }

        private void Aterrizar()
        {
            estado = Estado.Aterrizaje;
            _t = 0f;
            _cdSalto = saltoEnfriamiento;
            alturaActual = 0f;
            _yDestino = Terrain3D.Altura(transform.position.x, transform.position.z) + alturaSuelo;
            // Si el arco cae dentro de un pilar, el cuerpo sale antes de resolver el golpe.
            Vector3 libre = ColisionJugador.PuntoLibre(
                new Vector3(transform.position.x, _yDestino + 1f, transform.position.z),
                Vector3.zero, radioCuerpo, altoCuerpo);
            _yDestino = Terrain3D.Altura(libre.x, libre.z) + alturaSuelo;
            transform.position = new Vector3(libre.x, _yDestino, libre.z);
            _yBase = _yDestino;

            SlugActor victima = target != null ? target.GetComponent<SlugActor>() : null;
            float hpAntes = victima != null ? victima.Hp : 0f;
            Vector3 p = target != null ? target.position : transform.position;
            p.y = _yBase;
            Vector3 pos = transform.position; pos.y = _yBase;
            bool enRadio = target != null && (p - pos).sqrMagnitude <= saltoRadio * saltoRadio;
            if (enRadio) Golpear(saltoMultiplicador);
            if (enRadio && victima != null && victima.Hp < hpAntes) aterrizajesConGolpe++;
            GameFeel.AddTrauma(enRadio && victima != null && victima.Hp < hpAntes ? 0.34f : 0.12f);
        }

        /// Volador: vuela sobre obstáculos (ignora cobertura al moverse) y solo recibe daño
        /// con tiro a 100 MPH (fase >= 1). Su contra juego es cargar el disparo.
        private void PeleaVolador(float dist, Vector3 delta)
        {
            if (dist > attackRange)
            {
                Vector3 dir = dist > 0.001f ? delta / dist : Vector3.forward;
                // Vuela en línea recta sin respetar cobertura: el pilar no lo detiene.
                transform.position += dir * speed * Time.deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up), 6f * Time.deltaTime);
                ApoyarEnPiso();
                return;
            }
            Separar(Time.deltaTime);
            _cd -= Time.deltaTime;
            if (_cd <= 0f && TengoLinea()) EmpezarMordida();
        }

        /// Tejedor / soporte: no ataca al jugador directamente. Cada ~8 s invoca un sífilo
        /// (enemigo de 1 HP) o aplica un modificador de velocidad a los aliados cercanos.
        private void PeleaTejedor(float dt)
        {
            _cdTejedor -= dt;
            if (_cdTejedor <= 0f)
            {
                _cdTejedor = 8f;
                InvocarSifilo();
            }
            // Se mantiene a distancia media del jugador, sin morder.
            if (target != null)
            {
                Vector3 d = target.position - transform.position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < 6f)
                {
                    // Se retira un poco: es soporte, no cuerpo a cuerpo.
                    Vector3 lejos = -d / Mathf.Max(dist, 0.001f);
                    MoverConColision(lejos * speed * 0.6f * dt);
                }
                else if (dist > 10f)
                {
                    MoverConColision(d / Mathf.Max(dist, 0.001f) * speed * 0.6f * dt);
                }
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(target.position - transform.position, Vector3.up), 4f * dt);
            }
        }

        /// Excavador: entierra (invulnerable, invisible, no avanza), luego sale, ataca y vuelve a enterrarse.
        private void PeleaExcavador(float dist, Vector3 delta, float dt)
        {
            var self = Self();
            if (self != null && self.EstaEnterrado)
            {
                _cdExcavador -= dt;
                if (_cdExcavador <= 0f)
                {
                    self.SetEnterrado(false);
                    _cdExcavador = 5f;
                    entierros++;
                    Debug.Log("[EnemySlugAI] " + name + " sale de la tierra");
                }
                return; // enterrado: no se mueve ni ataca
            }

            _cdExcavador -= dt;
            if (_cdExcavador <= 0f && target != null)
            {
                // Entierra: invulnerable por ~6 s.
                var actor = Self();
                if (actor != null) actor.SetEnterrado(true);
                _cdExcavador = 6f;
                entierros++;
                Debug.Log("[EnemySlugAI] " + name + " se entierra");
                return;
            }

            if (target != null && dist > attackRange)
            {
                Vector3 dir = dist > 0.001f ? delta / dist : Vector3.forward;
                MoverConColision(dir * speed * Time.deltaTime);
                return;
            }
            Separar(Time.deltaTime);
            _cd -= Time.deltaTime;
            if (_cd <= 0f && TengoLinea()) EmpezarMordida();
        }

        /// Invoca un sífilo: enemigo diminuto de 1 HP que muere con un tiro, pero suma presión.
        private void InvocarSifilo()
        {
            if (target == null) return;
            var arena = Object.FindFirstObjectByType<SlugArenaGame>();
            if (arena == null) return;
            arena.SpawnSifilo(transform.position + transform.forward * 2f, target);
            invocaciones++;
            Debug.Log("[EnemySlugAI] " + name + " invocó sífilo #" + invocaciones);
        }

        private void PeleaBasica(float dist, Vector3 delta)
        {
            if (dist > attackRange)
            {
                Vector3 dir = dist > 0.001f ? delta / dist : Vector3.forward;
                if (arquetipo == Arquetipo.Frenetico)
                {
                    // teje perpendicular al avance: a velocidad de carga el tiro recto lo pasa
                    // por arriba y hay que calcularlo, que es el counterplay de este bicho
                    Vector3 perp = Vector3.Cross(Vector3.up, dir);
                    float lado = Mathf.Sin(_reloj * zigzagFrecuencia + _fase) * zigzagAmplitud;
                    dir = (dir + perp * lado).normalized;
                }
                Mover(dir, speed);
                return;
            }
            // Al plantarse a morder los cuerpos se empujan apenas, para no quedar uno dentro del otro.
            Separar(Time.deltaTime);
            _cd -= Time.deltaTime;
            if (_cd <= 0f && TengoLinea()) EmpezarMordida();
        }

        private void GolpearEnContacto(float multiplicador)
        {
            if (target == null) return;
            Vector3 d = target.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > attackRange * attackRange) return;
            Golpear(multiplicador);
        }

        private void Golpear(float multiplicador)
        {
            // Resolucion perezosa: en las pruebas EditMode Start no corre y _foe quedaria null
            // aunque el target si tenga SlugActor. Mismo motivo que Anim().
            if (_foe == null && target != null) _foe = target.GetComponent<SlugActor>();
            if (_foe == null || !_foe.Alive) return;
            // Una esquina de piedra tapa tanto como una pared: el golpe no atraviesa cobertura.
            if (!TengoLinea()) return;
            SlugAnimator animMordida = Anim();
            if (animMordida != null) animMordida.PlayShot();   // mordisco: mismo estiron que el disparo
            ElementType e = _self != null ? _self.Element : ElementType.Earth;
            _foe.TakeDamage(attackDamage * multiplicador, e);
        }

        public void RegisterKill() { killsRegistered++; }
    }
}
