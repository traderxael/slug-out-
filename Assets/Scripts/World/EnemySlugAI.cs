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
        private enum Estado { Acecho, Carga, Embestida, Recuperacion, AvisoSalto, Aire, Aterrizaje, AvisoEsquiva, Esquiva, EsquivaRecup }

        [SerializeField] private Transform target;
        [SerializeField] private float speed = 3.2f;
        [SerializeField] private float attackRange = 1.8f;
        [SerializeField] private float attackCooldown = 1.4f;
        [SerializeField] private float attackDamage = 8f;
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

        [Header("Estado (solo lectura)")]
        [SerializeField] private Estado estado = Estado.Acecho;
        [SerializeField] private float progresoAviso = 0f;
        [SerializeField] private int embestidas = 0;
        [SerializeField] private int saltos = 0;
        [SerializeField] private int aterrizajesConGolpe = 0;
        [SerializeField] private float alturaActual = 0f;
        [SerializeField] private int esquivas = 0;
        [SerializeField] private int esquivasCanceladas = 0;

        private float _cd;
        private float _t;
        private float _cdEmbestida;
        private float _cdSalto;
        private float _cdEsquiva;
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

        public enum Arquetipo { Embestidor, Saltarin, Frenetico }

        /// 0 = sin aviso; crece a 1 durante el windup del embestidor, la agachada del saltarin o la
        /// ventanada de la esquiva. SlugAnimator lo lee para pintar el tell, asi que los tres
        /// arquetipos y la esquiva avisan igual: no hay un sistema de telegrafia paralelo.
        public float ProgresoAviso { get { return progresoAviso; } }
        /// Queda al descubierto despues de embestir, de aterrizar y de esquivar: el castigo de su
        /// propia mecanica, para que esquivar no sea gratis.
        public bool EstaExpuesto { get { return estado == Estado.Recuperacion || estado == Estado.Aterrizaje || estado == Estado.EsquivaRecup; } }
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
            if (d == null) return Arquetipo.Frenetico;
            if (d.clashPower >= 60f) return Arquetipo.Embestidor;
            if (d.clashPower < 45f && d.accelerationRate >= 90f) return Arquetipo.Saltarin;
            return Arquetipo.Frenetico;
        }

        public void SetTarget(Transform t) { target = t; }
        public void SetSpeed(float s) { speed = s; }
        public void SetDamage(float d) { attackDamage = d; }
        public void SetArquetipo(Arquetipo a)
        {
            arquetipo = a;
            if (a == Arquetipo.Saltarin) { speed *= 1.25f; attackCooldown *= 0.8f; }
            else if (a == Arquetipo.Frenetico) { attackCooldown *= 0.6f; attackDamage *= 0.7f; }
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
            // el primer salto cae durante la aproximacion (llega a los ~3u), no despues de
            // plantarse en tu cara; cada saltarin despegua en un momento distinto, pero el MISMO
            // momento distinto si el run tiene la misma semilla
            System.Random rng = new System.Random(_semillaRun + ++_nacimientos * 7919);
            _cdSalto = 0.6f + (float)rng.NextDouble() * 0.9f;
            _fase = (float)rng.NextDouble() * 6.2831855f;
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

            switch (arquetipo)
            {
                case Arquetipo.Saltarin: PeleaSaltarin(dist, delta); return;
                case Arquetipo.Frenetico: progresoAviso = 0f; PeleaBasica(dist, delta); return;
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
                    if (_cdEmbestida <= 0f && dist > distanciaMinimaEmbestida) { EmpezarCarga(delta); return; }
                    _cd -= Time.deltaTime;
                    if (_cd <= 0f) { _cd = attackCooldown; Golpear(1f); }
                    return;

                case Estado.Carga:
                    _t += Time.deltaTime;
                    float k = Mathf.Clamp01(_t / cargaDuracion);
                    progresoAviso = k;
                    if (k < cargaAtrasRatio) transform.position -= _rumbo * speed * cargaAtrasVelocidad * Time.deltaTime;
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
                    transform.position += _rumbo * speed * embestidaMultiplicador * acelera * Time.deltaTime;
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
                transform.position += _rumboEsquiva * (speed * pasoVelocidadRatio * dt);
                ApoyarEnPiso();
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
            transform.position += dir * s * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 6f * Time.deltaTime);
            ApoyarEnPiso();
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
                    if (_cdSalto <= 0f && dist > saltoDistanciaMinima && dist < saltoRango)
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
            transform.position = new Vector3(transform.position.x, _yDestino, transform.position.z);
            _yBase = _yDestino;

            Vector3 p = target.position;
            p.y = _yBase;
            if ((p - _destino).sqrMagnitude <= saltoRadio * saltoRadio)
            {
                aterrizajesConGolpe++;
                Golpear(saltoMultiplicador);
            }
            GameFeel.AddTrauma((p - _destino).sqrMagnitude <= saltoRadio * saltoRadio ? 0.34f : 0.12f);
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
            _cd -= Time.deltaTime;
            if (_cd <= 0f) { _cd = attackCooldown; Golpear(1f); }
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
            if (_foe == null || !_foe.Alive) return;
            SlugAnimator animMordida = Anim();
            if (animMordida != null) animMordida.PlayShot();   // mordisco: mismo estiron que el disparo
            ElementType e = _self != null ? _self.Element : ElementType.Earth;
            _foe.TakeDamage(attackDamage * multiplicador, e);
        }

        public void RegisterKill() { killsRegistered++; }
    }
}
