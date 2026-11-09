using UnityEngine;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.World
{
    /// Estado de combate de una babosa (jugador o enemigo), alimentado por SlugData.
    /// El dano se resuelve con la tabla elemental REAL del repo (ElementalChart).
    public class SlugActor : MonoBehaviour
    {
        [SerializeField] private SlugData data;
        [SerializeField] private bool isPlayer = false;
        [SerializeField] private float maxHp = 100f;

        /// Balance A tactico: el jugador tiene una vida fija de 120. Antes salia de la
        /// tabla del slug (80 + choque*0,6), asi que cambiar de babosa equipada cambiaba
        /// la vida en combate. Los enemigos conservan esa formula.
        public const float VidaJugadorTactica = 120f;

        public SlugData Data { get { return data; } }
        public bool IsPlayer { get { return isPlayer; } }
        public float Hp { get; private set; }
        public float MaxHp { get { return maxHp; } }
        public bool Alive { get { return Hp > 0f; } }
        public ElementType Element { get { return data != null ? data.element : ElementType.Fire; } }

        /// Cada golpe aplicado: (quien lo recibio, dano final, multiplicador elemental).
        public static event System.Action<SlugActor, float, float> Damaged;

        /// SlugShot, PlayerShooter, PlayerAbilities y GameHUD buscaban actores con
        /// FindObjectsByType cada frame (y cada llamada aloca un array nuevo): era la mayor
        /// fuente de GC del proyecto. Mismo estilo de los otros estaticos de este archivo.
        static readonly System.Collections.Generic.List<SlugActor> _vivos = new System.Collections.Generic.List<SlugActor>();
        public static System.Collections.Generic.IReadOnlyList<SlugActor> Vivos { get { return _vivos; } }

        [Header("Invulnerabilidad del jugador tras un golpe")]
        /// Los i-frames son el techo del dano entrante: aunque lo rodeen seis enemigos, solo
        /// entra un golpe cada medio segundo. El modelo de supervivencia de la campana usa
        /// este numero, asi que vive aqui y no copiado en un test.
        public const float InvulnerabilidadTrasGolpe = 0.5f;
        [SerializeField] private float invulnerableTrasGolpe = InvulnerabilidadTrasGolpe;
        private float _invulnerableHasta = -1f;

        public bool Invulnerable { get { return isPlayer && Time.time < _invulnerableHasta; } }
        public float LastMultiplier { get; private set; } = 1f;

        [Header("Estados (escudo, quemadura, congelado, aturdido)")]
        [SerializeField] private float escudo = 0f;
        [SerializeField] private float quemaduraPorSegundo = 0f;
        [SerializeField] private float energia = 100f;

        private float _quemaHasta, _quemaProximo, _congeladoHasta, _aturdidoHasta, _energiaMax = 100f;
        private const float RegeneracionEnergiaPorSegundo = 12f;

        public float Escudo { get { return escudo; } }
        /// Da la cara el dano por segundo que dejo una quemadura o un envenenamiento. No estaba
        /// expuesto y era la unica forma de verificar desde una sonda que el efecto ataco.
        public float QuemaduraPorSegundo { get { return quemaduraPorSegundo; } }
        public bool EstaCongelado { get { return Time.time < _congeladoHasta; } }
        public bool EstaAturdido { get { return Time.time < _aturdidoHasta; } }
        public bool EstaAfectado { get { return EstaCongelado || EstaAturdido; } }
        public float Energia { get { return energia; } }
        public float EnergiaMax { get { return _energiaMax; } }

        public void DarEscudo(float cantidad) { escudo += cantidad; }
        public void Congelar(float segundos) { _congeladoHasta = Mathf.Max(_congeladoHasta, Time.time + segundos); }
        public void Aturdir(float segundos) { _aturdidoHasta = Mathf.Max(_aturdidoHasta, Time.time + segundos); }
        public void Quemar(float porSegundo, float segundos)
        {
            quemaduraPorSegundo = Mathf.Max(quemaduraPorSegundo, porSegundo);
            _quemaHasta = Mathf.Max(_quemaHasta, Time.time + segundos);
            if (_quemaProximo <= 0f) _quemaProximo = Time.time + 1f;
        }

        /// Solo suma: nunca deja al actor por encima del maximo. Es la contraparte de
        /// GastaEnergia, y Air la necesita para que su premio de cadencia sea real.
        public void DevolverEnergia(float cantidad)
        {
            energia = Mathf.Min(_energiaMax, energia + Mathf.Max(0f, cantidad));
        }

        /// Devuelve false si no alcanza: el costo de la habilidad es la energia del personaje.
        public bool GastaEnergia(float cantidad)
        {
            if (energia < cantidad) return false;
            energia -= cantidad;
            return true;
        }

        private void Update()
        {
            if (!Alive) return;
            // La racha se cae por reloj de partida, no por frames: pausado (timeScale 0) Time.time
            // no avanza y el combo no se pierde, que es el comportamiento que el jugador espera.
            if (combo > 0 && Time.time > _comboHasta) { combo = 0; _comboHasta = -1f; }
            if (energia < _energiaMax) energia = Mathf.Min(_energiaMax, energia + RegeneracionEnergiaPorSegundo * Time.deltaTime);
            if (quemaduraPorSegundo > 0f && Time.time < _quemaHasta && Time.time >= _quemaProximo)
            {
                _quemaProximo = Time.time + 1f;
                Hp = Mathf.Max(0f, Hp - quemaduraPorSegundo);
                Damaged?.Invoke(this, quemaduraPorSegundo, 1f);
            }
            if (Time.time >= _quemaHasta) { quemaduraPorSegundo = 0f; _quemaProximo = 0f; }
        }

        private void Awake()
        {
            RecalcMax();
            Hp = maxHp;
        }

        private void OnEnable() { if (!_vivos.Contains(this)) _vivos.Add(this); }

        private void OnDisable() { _vivos.Remove(this); }

        private void RecalcMax()
        {
            maxHp = isPlayer
                ? VidaJugadorTactica
                : (data != null ? 80f + data.clashPower * 0.6f : 100f);
            _energiaMax = data != null ? Mathf.Max(40f, data.requiredEnergy) : 100f;
            energia = _energiaMax;
        }

        public void Configure(SlugData d, bool player)
        {
            data = d;
            isPlayer = player;
            RecalcMax();
            Hp = maxHp;
            name = (d != null ? d.slugName : "Babosa") + (player ? " (jugador)" : " (enemigo)");
        }

        /// Lo pone SlugArenaGame al armar el nivel. En campana tardia el anillo funciona al
        /// reves y eso cambia el dano de todos los golpes, del HUD al impacto.
        public static bool AnilloInvertido;

        /// Solo para pruebas y herramientas: deja la vida a un valor concreto sin matar al personaje.
        public void FijarVidaPrueba(float valor)
        {
            Hp = Mathf.Clamp(valor, 0.01f, maxHp);
        }

        public void Heal(float amount)
        {
            Hp = Mathf.Min(maxHp, Hp + amount);
        }

        /// Refuerzo de vida (velocimorphs de la segunda mitad de la campania).
        /// Llamar DESPUES de Configure: deja la vida llena al nuevo maximo.
        public void MultiplicarHp(float m)
        {
            maxHp *= m;
            Hp = maxHp;
        }

        /// Vida maxima del jugador para un nivel (la progresion de la campana vive en
        /// SlugCampaign). El enemigo NO pasa por aqui: conserva la formula de la tabla.
        /// Deja la vida llena, como hace Configure.
        public void FijarVidaMaxima(float valor)
        {
            if (!isPlayer) return;
            maxHp = Mathf.Max(1f, valor);
            Hp = maxHp;
        }

        /// Suma vida maxima SIN llenar la barra: el jugador recibe la parte que le sobra,
        /// asi que un cambio de nivel no cura al personaje por sorpresa.
        public void AumentarVidaMaxima(float delta)
        {
            if (!isPlayer || delta <= 0f) return;
            maxHp += delta;
            Hp = Mathf.Min(maxHp, Hp + delta);
        }

        [Header("Racha de impactos (ventana de 5 s)")]
        [SerializeField] private float ventanaCombo = 5f;
        [SerializeField] private int combo = 0;
        [SerializeField] private int comboMaximo = 0;
        private float _comboHasta = -1f;

        public int Combo { get { return combo; } }
        public int ComboMaximo { get { return comboMaximo; } }
        public float VentanaCombo { get { return ventanaCombo; } }
        /// Lo que dibuja el HUD: 1 justo despues de cerrar un golpe y 0 cuando vence. El contador
        /// solo no sirve en pantalla porque no dice cuanto falta para perderlo.
        public float ComboRestante { get { return _comboHasta < 0f ? 0f : Mathf.Clamp01((_comboHasta - Time.time) / Mathf.Max(ventanaCombo, 0.001f)); } }

        /// La racha es solo lectura en pantalla: no multiplica el dano a proposito. Atarla al dano
        /// reequilibraria la campana entera, y eso es decision aparte, no un arreglo de bugs.
        public void RegistrarImpacto()
        {
            combo++;
            if (combo > comboMaximo) comboMaximo = combo;
            _comboHasta = Time.time + ventanaCombo;
        }

        [Header("Captura")]
        [SerializeField] private bool capturable = true;
        [SerializeField] private float capturaUmbral = 0.35f;

        public bool Capturada { get; private set; }
        /// El jugador nunca es capturable: el disparo del Escupidor no puede meterlo a la
        /// coleccion ni disparar la regla de captura sobre el.
        public bool ListaParaCapturar { get { return capturable && !isPlayer && !Capturada && Alive && Hp <= MaxHp * capturaUmbral; } }
        /// El HUD dibuja la marca de captura en la barra de vida con este mismo valor: la regla
        /// y la marca no pueden separarse.
        public bool Capturable { get { return capturable; } }
        public float UmbralCaptura { get { return capturaUmbral; } }

        /// Baja el umbral de captura (eco de la cueva: "captura fácil").
        public void FijarUmbralCaptura(float valor) { capturaUmbral = Mathf.Clamp01(valor); }

        /// Un enemigo puede no ser capturable (el sífilo del tejedor): si lo fuera, contaria para
        /// la cuota de capturas del nivel y el tejedor "ganaria" niveles sin que el jugador
        /// capture nada.
        public void FijarCapturable(bool c) { capturable = c; }

        // ------------------------------------------------------------------ estado de enterrado / volador (S3)
        [SerializeField] private bool enterrado = false;
        [SerializeField] private bool volador = false;
        private Renderer[] _rend;

        /// Un enterrado es invulnerable y no avanza hasta que sale a la superficie.
        public bool EstaEnterrado { get { return enterrado; } }

        /// Enterrarse es esconderse: lo que esta bajo tierra no se ve (el jugador ve por que el
        /// tiro no le hace nada) y deja de pintar su malla.
        public void SetEnterrado(bool e)
        {
            enterrado = e;
            if (_rend == null || _rend.Length == 0) _rend = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < _rend.Length; i++)
                if (_rend[i] != null) _rend[i].enabled = !e;
        }

        /// Un volador solo recibe daño con tiro a 100 MPH (fase >= 1).
        public bool EstaVolando { get { return volador; } }
        public void SetVolando(bool v) { volador = v; }

        /// Daño real ya con el multiplicador elemental. Un volador ignora tiros por debajo de 100 MPH.
        public float TakeDamage(float raw, ElementType attacker)
        {
            return TakeDamage(raw, attacker, faseDelUltimoGolpe: 0);
        }

        /// Sobrecarga que recibe la fase del tiro que lo impactó (0 = tiro corto, 1 = velocimorph, 2 = megamorph).
        public float TakeDamage(float raw, ElementType attacker, int faseDelUltimoGolpe)
        {
            if (!Alive || Invulnerable) return 0f;
            // Un volador solo se daña con tiro a 100 MPH (fase >= 1).
            if (volador && faseDelUltimoGolpe < 1) return 0f;
            // Un enterrado no recibe daño hasta que sale a la superficie.
            if (enterrado) return 0f;

            float mult = ElementalChart.GetMultiplier(attacker, Element, AnilloInvertido);
            float final = raw * mult;
            if (isPlayer && GameFlow.Asistencia) final *= 0.75f;
            LastMultiplier = mult;
            if (escudo > 0f)
            {
                float absorbido = Mathf.Min(escudo, final);
                escudo -= absorbido;
                final -= absorbido;
                if (final <= 0f) { Damaged?.Invoke(this, 0f, mult); return 0f; }
            }
            Hp = Mathf.Max(0f, Hp - final);
            if (isPlayer) _invulnerableHasta = Time.time + (GameFlow.Asistencia ? 0.8f : invulnerableTrasGolpe);
            Debug.Log("[SlugActor] " + name + " recibio " + final.ToString("F1") + " (x" + mult.ToString("F2") + " de " + attacker + ") hp=" + Hp.ToString("F1"));
            if (Hp <= 0f) Debug.Log("[SlugActor] " + name + " quedo fuera de combate");
            Damaged?.Invoke(this, final, mult);
            return final;
        }

        /// Sube la energía máxima (eco de la cueva: "energía plena").
        public void AumentarEnergiaMax(float delta)
        {
            if (delta <= 0f) return;
            _energiaMax += delta;
            energia = Mathf.Min(_energiaMax, energia + delta);
        }

        /// Atrapada por el jugador: deja de pelear y entra a la coleccion.
        public void Capturar()
        {
            if (Capturada) return;
            Capturada = true;

            // Momento de captura: hitstop + camara lenta. Hitstop es de instancia (vive en el
            // componente GameFeel de la escena), AddTrauma es estatico.
            var feel = Object.FindAnyObjectByType<GameFeel>();
            if (feel != null) feel.Hitstop(0.12f);
            GameFeel.AddTrauma(1.2f);

            // FX por elemento: chime y particulas del color.
            var fx = Object.FindAnyObjectByType<SlugFx>();
            if (fx != null) fx.Capture(Data != null ? Data.elementColor : Color.cyan);

            Debug.Log("[SlugActor] " + name + " CAPTURADA");
        }
    }
}
