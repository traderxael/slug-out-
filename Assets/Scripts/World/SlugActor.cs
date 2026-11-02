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
        [SerializeField] private float invulnerableTrasGolpe = 0.5f;
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
            maxHp = data != null ? 80f + data.clashPower * 0.6f : 100f;
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

        /// Devuelve el dano real aplicado (ya con el multiplicador elemental).
        public float TakeDamage(float raw, ElementType attacker)
        {
            if (!Alive || Invulnerable) return 0f;
            float mult = ElementalChart.GetMultiplier(attacker, Element, AnilloInvertido);
            float final = raw * mult;
            LastMultiplier = mult;
            if (escudo > 0f)
            {
                float absorbido = Mathf.Min(escudo, final);
                escudo -= absorbido;
                final -= absorbido;
                if (final <= 0f) { Damaged?.Invoke(this, 0f, mult); return 0f; }
            }
            Hp = Mathf.Max(0f, Hp - final);
            if (isPlayer) _invulnerableHasta = Time.time + invulnerableTrasGolpe;
            Debug.Log("[SlugActor] " + name + " recibio " + final.ToString("F1") + " (x" + mult.ToString("F2") + " de " + attacker + ") hp=" + Hp.ToString("F1"));
            if (Hp <= 0f) Debug.Log("[SlugActor] " + name + " quedo fuera de combate");
            Damaged?.Invoke(this, final, mult);
            return final;
        }

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
        public bool ListaParaCapturar { get { return capturable && !Capturada && Alive && Hp <= MaxHp * capturaUmbral; } }

        /// Atrapada por el jugador: deja de pelear y entra a la coleccion.
        public void Capturar()
        {
            if (Capturada) return;
            Capturada = true;
            Debug.Log("[SlugActor] " + name + " CAPTURADA");
        }
    }
}
