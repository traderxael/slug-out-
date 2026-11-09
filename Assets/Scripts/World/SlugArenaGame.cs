using System.Collections.Generic;
using UnityEngine;
using SlugOut.Slugs;
using SlugOut.Core;

namespace SlugOut.World
{
    /// Modo arena: el jugador (una babosa del roster) aguanta oleadas de otras babosas.
    /// Carga los modelos 3D desde Resources/Slugs/<slugId>; si no existen, usa una capsula.
    /// Toda la logica es por distancia (sin fisica) para que sea reproducible.
    public class SlugArenaGame : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private SlugData[] roster;
        [SerializeField] private SlugData playerSlug;

        [Header("Heroe")]
        [Tooltip("El jugador es Eli Shane con su blaster; si se apaga vuelve a la babosa clasica")]
        [SerializeField] private bool usarShane = true;
        [SerializeField] private bool usarRelieve = true;
        [SerializeField] private bool usarRocas = true;

        [Header("Escenario")]
        [SerializeField] private Transform player;
        [SerializeField] private float spawnRadius = 18f;
        [SerializeField] private float arenaLimit = 24f;

        [Header("Oleadas")]
        [SerializeField] private int totalEnemies = 8;
        [SerializeField] private int maxAlive = 4;
        [SerializeField] private float spawnInterval = 2.5f;
        [SerializeField] private float enemySpeed = 3.0f;
        [SerializeField] private float enemyDamage = 7f;

        /// Balance A tactico: la curva de la campana se diseno sobre una base de 8 y el
        /// objetivo es 7. Se escala la curva entera (no los saltos ni el resto de valores)
        /// para conservar la progresion de los 28 niveles.
        public const float DanoEnemigoReferencia = 8f;
        public const float DanoEnemigoObjetivo = 7f;
        public static float AjusteDanoEnemigoTactico(float danoDeCurva)
        {
            return danoDeCurva * (DanoEnemigoObjetivo / DanoEnemigoReferencia);
        }

        [Header("Estado (solo lectura)")]
        [SerializeField] private int score = 0;
        [SerializeField] private int spawned = 0;
        [SerializeField] private int aliveCount = 0;
        [SerializeField] private int playerHpRounded = 0;
        [SerializeField] private int killsThisWave = 0;
        [SerializeField] private int capturadas = 0;
        [SerializeField] private int refuerzos = 0;
        [SerializeField] private System.Collections.Generic.List<string> coleccion = new System.Collections.Generic.List<string>();
        [SerializeField] public string outcome = "en curso";

        private readonly List<GameObject> _alive = new List<GameObject>();
        /// Los sífilos que invoca el tejedor (S3): no son enemigos del nivel (no cuentan para
        /// las apariciones ni para la cuota de capturas), asi que van en su propia lista.
        private readonly List<GameObject> _sifilos = new List<GameObject>();
        private SlugData[] _orden;
        private float _timer;
        private int _nextIndex;
        private SlugActor _playerActor;
        private bool _finished;
        private bool _avisoLimite;
        private int goalCapturas;
        private bool _infinito;
        /// Numero de nivel de campana del nivel en curso (0 en infinito). Vive como campo
        /// porque el final de partida se evalua en Update, fuera del metodo que recibio cfg.
        private int _nivelActual;
        /// Oleada para la que ya se aplico la vida del jugador en el modo infinito.
        private int _oleadaVida = 1;

        public int GoalCapturas { get { return goalCapturas; } }
        public bool Termino { get { return _finished; } }
        public bool Infinito { get { return _infinito; } }
        /// Una oleada = una tanda de maxAlive apariciones. Es el numero que leen los fragmentos de
        /// infinito (ola 24, 31, 38, 45 y 52), que con total=20 nunca se alcanzaban.
        public int OlaActual { get { return 1 + spawned / Mathf.Max(1, maxAlive); } }

        /// Espejo estatico de Termino: la IA enemiga y el tirador tienen que saber que la partida
        /// ya cerró sin buscar la arena cada cuadro. No es GameFeel.Pausa a proposito: esa baja
        /// timeScale y se lleva por delante la animacion de victoria y el reloj del hitstop.
        public static bool CombateCongelado;

        public int Score { get { return score; } }
        public int Capturadas { get { return capturadas; } }
        /// Ultima especie capturada: el aviso del HUD la nombra en vez de decir solo "babosa".
        public string UltimaCaptura { get; private set; } = "";
        public Color UltimaCapturaColor { get; private set; } = Color.cyan;
        public string ColeccionTexto { get { return string.Join(", ", coleccion.ToArray()); } }
        public int Spawned { get { return spawned; } }
        public int TotalEnemies { get { return totalEnemies; } }
        public int Alive { get { return aliveCount; } }
        public int Capturas { get { return capturadas; } }
        /// Apariciones extra cuando la cuota de capturas ya no era alcanzable con lo que quedaba.
        public int Refuerzos { get { return refuerzos; } }

        /// El nivel solo puede ganarse capturando `objetivo` babosas. Si el jugador bajo (mato)
        /// demasiadas, `total - spawned + vivos` puede quedar por debajo de lo que falta: ahi el
        /// nivel se quedaba sin enemigos, sin final y sin forma de ganar. Refuerzo = una aparicion
        /// extra para que la cuota vuelva a estar al alcance. Funcion pura para probarla en EditMode.
        public static bool NecesitaRefuerzo(int total, int spawned, int vivos, int objetivo, int capturadas)
        {
            if (objetivo <= 0) return false;          // infinito: no hay cuota que sostener
            int faltan = objetivo - capturadas;
            if (faltan <= 0) return false;            // cuota ya cumplida
            int posibles = Mathf.Max(0, total - spawned) + Mathf.Max(0, vivos);
            return posibles < faltan;
        }

        /// S3: reparto de roles nuevos dentro de la campana. Los arquetipos (volador, tejedor
        /// y excavador) existian en el codigo pero ningun asset del roster los traia: eran
        /// codigo muerto. En vez de tocar los 64 .asset del roster (que son de otra sesion), el
        /// rol se reparte por nivel y numero de aparicion: determinista (la campana es
        /// reproducible) y con la frecuencia acotada para que el nivel siga siendo jugable.
        /// El excavador manda sobre el tejedor y el tejedor sobre el volador: una aparicion es
        /// UN rol, nunca tres a la vez.
        public static SlugData.RolEnemigo RolDeCampana(int nivel, int indice)
        {
            if (nivel >= 16 && indice % 13 == 7) return SlugData.RolEnemigo.Excavador;
            if (nivel >= 12 && indice % 11 == 5) return SlugData.RolEnemigo.Tejedor;
            if (nivel >= 8 && indice % 7 == 2) return SlugData.RolEnemigo.Volador;
            return SlugData.RolEnemigo.Normal;
        }

        /// Arranque directo para pruebas: mismo camino que GameFlow, y se baja el menu
        /// para que GameFlow no vuelva a congelar al cuadro siguiente.
        public void IniciarPrueba(int total, int capturas)
        {
            if (GameFlow.Activo != null) GameFlow.Activo.IniciarConfig(new NivelConfig(total, 2, 1.4f, 2.6f, 4f, capturas));
            else Iniciar(new NivelConfig(total, 2, 1.4f, 2.6f, 4f, capturas), null);
        }

        /// Solo pruebas: exige N capturas sin cambiar nada mas de la partida.
        public void FijarGoalPrueba(int n) { goalCapturas = n; }

        private void Awake()
        {
            // GameFeel tiene que existir antes de que el menu congele: si se creara en Start,
            // con el menu abierto Start sale temprano y nadie aplica la pausa.
            if (GetComponent<GameFeel>() == null) gameObject.AddComponent<GameFeel>();

            // el relieve reemplaza la malla del "Terreno" que ya esta en la escena, en vez de
            // sumar un piso nuevo encima (z-fighting con el plano raso)
            if (usarRelieve)
            {
                GameObject suelo = GameObject.Find("Terreno");
                if (suelo == null) suelo = gameObject;
                if (suelo.GetComponent<Terrain3D>() == null) suelo.AddComponent<Terrain3D>();
                // La region se cuelga del mismo objeto que el terreno: usa Terrain3D.Altura
                // para asentar cada prop y no reemplaza nada de la escena.
                if (suelo.GetComponent<RegionCuevaSeca>() == null)
                    suelo.AddComponent<RegionCuevaSeca>();
                if (suelo.GetComponent<LookCuevaPixar>() == null)
                    suelo.AddComponent<LookCuevaPixar>();
            }
            if (usarRocas && GetComponent<RockShower>() == null) gameObject.AddComponent<RockShower>();
            // La voz vive en la arena desde el arranque: StoryPanel la busca en su primer Update
            // y los duelistas la usan al aparecer. Un AudioSource propio, no el de ArenaMusic.
            if (GetComponent<VozPlayer>() == null) gameObject.AddComponent<VozPlayer>();
            if (GetComponent<EcosCuevaManager>() == null) gameObject.AddComponent<EcosCuevaManager>();
        }

        private void Start()
        {
            // con el menu abierto la arena espera: Iniciar() llega desde GameFlow al elegir nivel
            if (GameFlow.Activo != null && GameFlow.Activo.EnMenu) return;
            Iniciar(NivelConfig.Campana(1), null);
        }

        /// Un solo punto de arranque: lo usa Start (escenas sin menu) y GameFlow (menu/campana).
        public void Iniciar(NivelConfig cfg, SlugData slugElegido)
        {
            if (cfg.total == 0) cfg = NivelConfig.Campana(1);
            if (player == null)
            {
                PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
                if (pc != null) player = pc.transform;
            }
            roster = SlugRoster.Todos;
            if (roster == null || roster.Length == 0)
            {
                Debug.LogWarning("[SlugArena] roster vacio: no se puede jugar.");
                outcome = "sin roster";
                return;
            }

            totalEnemies = cfg.total;
            maxAlive = cfg.maxVivos;
            spawnInterval = cfg.intervalo;
            enemySpeed = cfg.velocidad;
            enemyDamage = AjusteDanoEnemigoTactico(cfg.dano);
            goalCapturas = cfg.capturas;
            _infinito = cfg.infinito;
            _nivelActual = cfg.nivel;
            // La historia pertenece al nivel que arranca: la arena se lo dice al panel, que ya
            // no adivina por capturas. Cubre NUEVA AVENTURA, CONTINUAR, SIGUIENTE, EXPLORAR y REINTENTAR.
            StoryPanel historia = GetComponent<StoryPanel>();
            if (historia != null) historia.MostrarNivelActual(cfg.nivel, _infinito);
            if (slugElegido != null) playerSlug = slugElegido;

            ReiniciarPartida();
            _oleadaVida = 1;
            // Semilla por run: se sortea UNA vez y se loguea. Con la misma semilla el orden de
            // nacimiento, el momento del primer salto y la fase del zigzag son los mismos; antes
            // dependian del Random global de Unity, que mueve cualquier otro lanzamiento del proyecto.
            int semilla = Random.Range(1, int.MaxValue);
            EnemySlugAI.NuevaSemillaDeRun(semilla);
            Debug.Log("[SlugArena] semilla de run " + semilla + " (infinito=" + _infinito + ")");
            SlugActor.AnilloInvertido = cfg.anilloInvertido;
            RockShower rs = GetComponent<RockShower>();
            if (rs != null) rs.Activar(usarRocas && cfg.rocas);
            _orden = IntercalarPorElemento(SinElJugador(roster, playerSlug), cfg.tema);
            if (_orden.Length == 0) _orden = IntercalarPorElemento(roster, cfg.tema);

            if (player != null && playerSlug != null)
            {
                _playerActor = player.GetComponent<SlugActor>();
                if (_playerActor == null) _playerActor = player.gameObject.AddComponent<SlugActor>();
                _playerActor.Configure(playerSlug, true);

                PlayerShooter shooter = player.GetComponent<PlayerShooter>();
                if (shooter == null) shooter = player.gameObject.AddComponent<PlayerShooter>();
                shooter.SetData(playerSlug);

                // Progresion del jugador: la vida y el cargador mejoran con la campana. Antes
                // la vida era 120 fijos contra un dano que subia 4 -> 17 y un cargador que no
                // daba los disparos que el nivel pedia: medido en Play, la campana se trababa
                // en el nivel 9. Ver SlugCampaign para los numeros y para el modelo.
                int nivelDeReferencia = _infinito ? NivelConfig.TotalNiveles : cfg.nivel;
                _playerActor.FijarVidaMaxima(_infinito
                    ? NivelConfig.VidaDelJugadorEnInfinito(1)
                    : NivelConfig.VidaDelJugador(nivelDeReferencia));
                shooter.SetRecargaPorTubo(NivelConfig.RecargaPorTuboParaNivel(nivelDeReferencia));
                shooter.SetPenalizacionDisparoPesado(
                    NivelConfig.PenalizacionDisparoPesadoParaNivel(nivelDeReferencia));
                // OJO: la escena ya puede traer su propio SlugAnimator — no duplicar,
                // dos animators se pelean por escala/posicion/rotacion del transform.
                if (player.GetComponent<SlugAnimator>() == null)
                    player.gameObject.AddComponent<SlugAnimator>();

                PlayerAbilities hab = player.GetComponent<PlayerAbilities>();
                if (hab == null) hab = player.gameObject.AddComponent<PlayerAbilities>();
                hab.Configurar(playerSlug, _playerActor);

                PlayerDodge dodge = player.GetComponent<PlayerDodge>();
                if (dodge == null) dodge = player.gameObject.AddComponent<PlayerDodge>();

                if (usarShane) AttachShane(player.gameObject, playerSlug);
                else AttachModel(player.gameObject, playerSlug, new Vector3(0f, -1f, 0f), true);   // el heroe siempre evolucionado
                AdjuntarCompanero(player.gameObject, playerSlug);
                // S6: mejoras permanentes del escondite se aplican al iniciar cada nivel.
                Escondite.AplicarAlJugador(_playerActor, shooter);
                Debug.Log("[SlugArena] jugador = " + playerSlug.slugName + " (" + playerSlug.element + ") hp=" + _playerActor.MaxHp.ToString("F1") + " objetivo=" + (goalCapturas > 0 ? "capturar " + goalCapturas : "bajar a todos"));
            }

            Debug.Log("[SlugArena] inicio: " + totalEnemies + " enemigos, max " + maxAlive + " vivos, intervalo " + spawnInterval.ToString("F1") + "s, velocidad " + enemySpeed + ", dano " + enemyDamage);

            if (GetComponent<ArenaMusic>() == null)
                gameObject.AddComponent<ArenaMusic>();   // loop procedural Am-F-C-G
        }

        private void Update()
        {
            // con el menu abierto Iniciar() todavia no corrio: sin partida armada hay que salir
            // antes de tocar _orden, si no SpawnEnemy lanza NRE en cada cuadro
            if (_orden == null) return;
            if (_finished || roster == null || roster.Length == 0) return;

            // Ecos de la cueva: avanza el temporizador y ofrece mejoras dentro del nivel.
            if (EcosCuevaManager.Instancia != null) EcosCuevaManager.Instancia.Tick(Time.deltaTime);

            // OJO: contar la baja y sacarla del listado en la MISMA pasada.
            // (Antes se limpiaban arriba y el bucle que sumaba puntos ya no las veia: score quedaba en 0.)
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                GameObject g = _alive[i];
                SlugActor a = g != null ? g.GetComponent<SlugActor>() : null;
                bool fuera = g == null || a == null || !a.Alive || a.Capturada;
                if (fuera)
                {
                    _alive.RemoveAt(i);
                    if (a != null && a.Capturada)
                    {
                        capturadas++;
                        UltimaCaptura = a.Data != null ? a.Data.slugName : "Babosa";
                        UltimaCapturaColor = a.Data != null ? a.Data.elementColor : Color.cyan;
                        string id = a.Data != null ? a.Data.slugId : "?";
                        if (!coleccion.Contains(id)) coleccion.Add(id);
                        StoryPanel historia = GetComponent<StoryPanel>();
                        if (historia != null) historia.AvanzarHistoria();
                        if (EcosCuevaManager.Instancia != null) EcosCuevaManager.Instancia.NotificarCaptura();
                        if (_playerActor != null && _playerActor.Data != null)
                            SlugEvolucion.GanarXP(_playerActor.Data.slugId, SlugEvolucion.XpDeCaptura);
                        // S6: cristales y logros por captura.
                        Escondite.GanarCristales(5);
                        Logros.Desbloquear("primera_captura");
                        if (capturadas >= 10) Logros.Desbloquear("coleccionista");
                        Debug.Log("[SlugArena] CAPTURADA " + (a.Data != null ? a.Data.slugName : "(sin datos)") +
                            " total=" + capturadas + " coleccion=" + coleccion.Count);
                    }
                    else
                    {
                        score++;
                        killsThisWave++;
                        Debug.Log("[SlugArena] baja! score=" + score + " vivos=" + _alive.Count + " spawn=" + spawned);
                    }
                }
            }

            // S3: los sífilos del tejedor se limpian aparte (no son enemigos del nivel).
            for (int i = _sifilos.Count - 1; i >= 0; i--)
            {
                GameObject g = _sifilos[i];
                SlugActor a = g != null ? g.GetComponent<SlugActor>() : null;
                if (g == null || a == null || !a.Alive)
                {
                    _sifilos.RemoveAt(i);
                    if (g != null) Destroy(g);
                }
            }

            aliveCount = _alive.Count;
            playerHpRounded = _playerActor != null ? Mathf.RoundToInt(_playerActor.Hp) : 0;

            // En infinito la vida sigue creciendo con la oleada (misma progresion que la
            // campana): el jugador entra con la vida del final de la campaña y cada oleada le
            // suma la suya. Sin esto, el infinito empezaba con un escalón de 260 de vida.
            if (_infinito && _playerActor != null && OlaActual != _oleadaVida)
            {
                float antes = NivelConfig.VidaDelJugadorEnInfinito(_oleadaVida);
                float ahora = NivelConfig.VidaDelJugadorEnInfinito(OlaActual);
                _oleadaVida = OlaActual;
                _playerActor.AumentarVidaMaxima(ahora - antes);
                Debug.Log("[SlugArena] oleada " + OlaActual + ": vida maxima " +
                    _playerActor.MaxHp.ToString("F0"));
            }

            // La arena tiene limites: sin esto el jugador se iba caminando del escenario.
            if (player != null && _playerActor != null && _playerActor.Alive)
            {
                Vector3 pp = player.position;
                float cx = Mathf.Clamp(pp.x, -arenaLimit, arenaLimit);
                float cz = Mathf.Clamp(pp.z, -arenaLimit, arenaLimit);
                if ((cx != pp.x || cz != pp.z) && !_avisoLimite)
                {
                    _avisoLimite = true;
                    Debug.LogWarning("[SlugArena] el clamp de arena recorto al jugador en "
                        + pp.ToString("F1") + ": el anillo de muros no lo freno.");
                }
                pp.x = cx;
                pp.z = cz;
                player.position = pp;
            }

            if (_playerActor != null && !_playerActor.Alive)
            {
                outcome = "PERDISTE";
                _finished = true;
                CombateCongelado = true;
                Debug.Log("[SlugArena] PERDISTE. score=" + score + " | enemigos aparecidos=" + spawned);
                return;
            }

            if (ObjetivoCumplido() && spawned >= totalEnemies && _alive.Count == 0)
            {
                outcome = "GANASTE";
                _finished = true;
                CombateCongelado = true;
                Debug.Log("[SlugArena] GANASTE. score=" + score + " capturadas=" + capturadas + " de " + totalEnemies);
                // S6: cristales, estrellas y logros al ganar.
                Escondite.GanarCristales(20);
                int numEstrellas = Estrellas.Calcular(true, capturadas, goalCapturas,
                    _playerActor != null ? _playerActor.Hp : 0f,
                    _playerActor != null ? _playerActor.MaxHp : 1f);
                Estrellas.Guardar(_nivelActual, numEstrellas);
                if (_nivelActual >= NivelConfig.TotalNiveles)
                {
                    Logros.Desbloquear("campana_completa");
                    Logros.Desbloquear("cinco_estrellas"); // se verifica con estrellas totales
                }
                if (GameFlow.Activo != null) GameFlow.Activo.NivelSuperado(GameFlow.Activo.Nivel);
                StoryPanel historia = GetComponent<StoryPanel>();
                if (historia != null)
                {
                    historia.AvanzarHistoria();
                    // El 28 es el unico nivel con cierre: epilogo + boton al infinito en el HUD.
                    if (GameFlow.Activo != null
                        && GameFlow.EsFinalDeCampana(true, _infinito, GameFlow.Activo.Nivel))
                        historia.MostrarEpilogo();
                }
                if (player != null)
                {
                    SlugAnimator an = player.GetComponent<SlugAnimator>();
                    if (an != null) an.PlayVictory();
                }
                return;
            }

            // El refuerzo solo se decide despues de purgar bajas: _alive.Count es el numero real
            // de vivos y con el se calcula si la cuota de capturas sigue siendo alcanzable.
            bool refuerzo = NecesitaRefuerzo(totalEnemies, spawned, _alive.Count, goalCapturas, capturadas);
            if ((spawned >= totalEnemies && !refuerzo) || _alive.Count >= maxAlive) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = spawnInterval;
            if (refuerzo)
            {
                refuerzos++;
                Debug.Log("[SlugArena] refuerzo " + refuerzos + ": quedaban " + _alive.Count +
                    " vivos y " + (totalEnemies - spawned) + " por aparecer para " +
                    (goalCapturas - capturadas) + " capturas pendientes");
            }
            SpawnEnemy();
        }

        /// El estado de la partida es [SerializeField] para verlo en el Inspector, asi que puede
        /// quedar guardado en la escena: sin esto una partida arranca con contadores de otra.
        private void ReiniciarPartida()
        {
            score = 0;
            spawned = 0;
            aliveCount = 0;
            playerHpRounded = 0;
            killsThisWave = 0;
            capturadas = 0;
            refuerzos = 0;
            UltimaCaptura = "";
            UltimaCapturaColor = Color.cyan;
            coleccion.Clear();
            outcome = "en curso";
            // hay que DESTRUIR los enemigos del arranque anterior: solo sacarlos del listado
            // deja babosas vivas persiguiendo al jugador sin que nadie las cuente
            foreach (GameObject g in _alive) if (g != null) Destroy(g);
            _alive.Clear();
            foreach (GameObject g in _sifilos) if (g != null) Destroy(g);
            _sifilos.Clear();
            _nextIndex = 0;
            _timer = 0f;
            _finished = false;
            CombateCongelado = false;
            if (EcosCuevaManager.Instancia != null) EcosCuevaManager.Instancia.ReiniciarNivel();
            // el anillo invertido es estado del nivel, no del actor: si Iniciar() no lo vuelve
            // a poner, el modo infinito heredaria la ley del ultimo nivel de campana
            SlugActor.AnilloInvertido = false;
            _avisoLimite = false;
            PlayerController pc = player != null
                ? player.GetComponent<PlayerController>()
                : Object.FindAnyObjectByType<PlayerController>();
            if (pc != null) pc.Colocar(Vector3.zero);
        }

        /// Orden de aparicion round-robin por elemento (estable, reproducible): cada tanda trae
        /// elementos distintos, asi la tabla elemental pesa desde la primera oleada. Con tema,
        /// ese elemento abre la tanda y el nivel se siente distinto mas alla de los numeros.
        public static SlugData[] IntercalarPorElemento(SlugData[] fuente)
        {
            return IntercalarPorElemento(fuente, null);
        }

        public static SlugData[] IntercalarPorElemento(SlugData[] fuente, ElementType? tema)
        {
            var grupos = new List<List<SlugData>>();
            var indice = new Dictionary<ElementType, int>();
            foreach (SlugData d in fuente)
            {
                if (d == null) continue;
                if (!indice.TryGetValue(d.element, out int g))
                {
                    g = grupos.Count;
                    indice[d.element] = g;
                    grupos.Add(new List<SlugData>());
                }
                grupos[g].Add(d);
            }
            if (tema.HasValue)
            {
                var reordenados = new List<List<SlugData>>();
                for (int g = 0; g < grupos.Count; g++)
                    if (grupos[g].Count > 0 && grupos[g][0].element == tema.Value) reordenados.Add(grupos[g]);
                for (int g = 0; g < grupos.Count; g++)
                    if (grupos[g].Count > 0 && grupos[g][0].element != tema.Value) reordenados.Add(grupos[g]);
                grupos = reordenados;
            }
            var orden = new List<SlugData>();
            int total = 0;
            foreach (List<SlugData> g in grupos) total += g.Count;
            for (int ronda = 0; orden.Count < total; ronda++)
                foreach (List<SlugData> g in grupos)
                    if (ronda < g.Count) orden.Add(g[ronda]);
            return orden.ToArray();
        }

        /// En campana no alcanza con bajar a todos: hay que capturar (es la coleccion de la historia).
        private bool ObjetivoCumplido()
        {
            return goalCapturas <= 0 || capturadas >= goalCapturas;
        }

        /// El roster del menu incluye al jugador: sin esto pelearia contra su propia copia.
        private static SlugData[] SinElJugador(SlugData[] fuente, SlugData jugador)
        {
            var lista = new System.Collections.Generic.List<SlugData>();
            foreach (SlugData d in fuente)
                if (d != null && d != jugador) lista.Add(d);
            return lista.ToArray();
        }

        private static bool IsAlive(GameObject g)
        {
            SlugActor a = g.GetComponent<SlugActor>();
            return a != null && a.Alive;
        }

        private void SpawnEnemy()
        {
            SlugData d = _orden[_nextIndex % _orden.Length];
            if (d == null) return;
            int indice = _nextIndex;
            _nextIndex++;

            float ang = _nextIndex * 2.39996f;
            Vector3 pos = new Vector3(Mathf.Cos(ang) * spawnRadius, 0f, Mathf.Sin(ang) * spawnRadius);

            // El enemigo NO puede nacer dentro de un pilar o de una roca. Medido en Play:
            // seis de las 28 posiciones de aparicion caian dentro de un solido (spawn 03,
            // 13, 14, 16, 24 y 26). Ahi el enemigo queda atrapado, la IA choca contra el
            // proxy, el jugador tampoco lo alcanza con el tiro y el nivel no se termina
            // NUNCA. Se busca el punto libre antes de crear el bicho.
            pos = ColisionJugador.PuntoLibre(pos, -pos,
                ColisionJugador.RadioDefecto, ColisionJugador.AltoDefecto);

            GameObject go = new GameObject("Enemigo_" + d.slugId);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation((Vector3.zero - pos).normalized, Vector3.up);

            // Historia: las primeras oleadas son protoforms; desde la 10 aparecen los
            // velocimorphs (la evolucion de la segunda parte de la campania).
            bool evolucionado = spawned + 1 >= 10;
            // Rivales y jefe del mazo: un humano del roster de diseno en lugar de la babosa.
            string duelista = DuelistaMazo.Elegir(spawned, totalEnemies, _infinito, _nextIndex);
            bool montado = duelista != null && AttachDuelista(go, duelista, d);
            bool usedModel = montado || AttachModel(go, d, Vector3.zero, evolucionado);
            SlugActor actor = go.AddComponent<SlugActor>();
            actor.Configure(d, false);
            // S3: el rol lo fija el asset; si el asset no lo trae, lo reparte la campana.
            SlugData.RolEnemigo rol = d.rolEnemigo != SlugData.RolEnemigo.Normal
                ? d.rolEnemigo
                : RolDeCampana(_nivelActual, indice);
            EnemySlugAI.Arquetipo arq = EnemySlugAI.ArquetipoDe(d, rol);
            // S3: estado inicial de los enemigos nuevos. El volador vuela desde el principio;
            // el excavador sale a la superficie (la IA lo entierra en su ciclo) porque nacer
            // enterrado son 6 s de invulnerabilidad y de tiro perdido.
            if (arq == EnemySlugAI.Arquetipo.Volador) actor.SetVolando(true);
            if (arq == EnemySlugAI.Arquetipo.Excavador) actor.SetEnterrado(false);
            EnemySlugAI ai = go.AddComponent<EnemySlugAI>();
            ai.SetArquetipo(arq);

            ai.SetTarget(player);
            ai.SetDamage(enemyDamage);
            if (evolucionado)
            {
                // segunda mitad de la campana: los velocimorphs pegan y corren mas, y aguantan.
                // La rampa va ACOTADA: sin tope sumaba +8 m/s sobre una base de 3.2 hacia el
                // enemigo 50, y en infinito no paraba nunca. En campana el techo es el doble de la
                // base; en infinito lo sube la oleada (si no, el modo se aplana en la ola 12).
                float tope = _infinito ? Mathf.Min(enemySpeed * 3f, enemySpeed * (1f + 0.06f * OlaActual))
                                       : enemySpeed * 2f;
                ai.SetSpeed(Mathf.Min(tope, enemySpeed + _nextIndex * 0.16f));
                ai.SetDamage(enemyDamage * 1.4f * (_infinito ? Mathf.Min(2.5f, 1f + 0.03f * OlaActual) : 1f));
                actor.MultiplicarHp(1.25f);
            }
            else
            {
                ai.SetSpeed(Mathf.Min(enemySpeed * 1.6f, enemySpeed + _nextIndex * 0.08f));
            }

            // Élite: probabilidad que crece con la oleada. Va DESPUES de la rampa de daño y
            // velocidad: antes la elite multiplicaba los valores de la IA y enseguida los
            // sobrescribian con SetDamage/SetSpeed, asi que "Furia" y "Prisa" no hacian nada.
            // El jefe (Dr. Blakk, el ultimo enemigo de cada nivel) jamas sale elite: con
            // "Vigor + Sangre" (x2 vida + regeneracion) el nivel 1 pasaba de ganarse en 27 s a
            // quedarse en TOPE 90 s, y ya tiene sus propias 3 fases (DrBlakkFases).
            if (duelista != DuelistaMazo.Jefe && ProbabilidadElite(OlaActual) < Random.value)
            {
                var rng = new System.Random();
                int n = rng.Next(1, 3); // 1 o 2 modificadores
                var mods = new EliteModificador[n];
                var usados = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < n; i++)
                {
                    int idx;
                    do { idx = rng.Next(_elites.Length); } while (usados.Contains(idx));
                    usados.Add(idx);
                    mods[i] = _elites[idx];
                }
                var elite = go.AddComponent<EliteEstado>();
                elite.modificadores = mods;
                elite.Aplicar(actor, ai);
                Debug.Log("[SlugArena] ELITE " + elite.Nombres() + " en " + d.slugName);
            }

            go.AddComponent<SlugAnimator>();

            _alive.Add(go);
            spawned++;
            aliveCount = _alive.Count;
            if (montado)
            {
                actor.MultiplicarHp(duelista == DuelistaMazo.Jefe ? 2.5f : 1.5f);
                int indiceFrase;
                string frase = DuelistaVoces.Siguiente(duelista, out indiceFrase);
                DuelistaMarca marca = go.AddComponent<DuelistaMarca>();
                marca.Configurar(duelista, frase, indiceFrase);
                if (VozPlayer.Instancia != null)
                    marca.FijarDuracionFrase(VozPlayer.Instancia.PlayDuelista(duelista, indiceFrase));
                // S4: Dr. Blakk a 3 fases (fase 2 = anillo al revés).
                if (duelista == DuelistaMazo.Jefe)
                    go.AddComponent<DrBlakkFases>();
            }
            Debug.Log("[SlugArena] spawn " + d.slugName + " (" + d.element + ") modelo="
                + (montado ? "duelista:" + duelista : (usedModel ? "malla" : "capsula")));
        }

        // ------------------------------------------------------------------ élites (S3)

        static readonly EliteModificador[] _elites =
        {
            new EliteModificador { id = "elite_vigor", nombre = "Vigor", hpMultiplicador = 2f, badgeColor = new Color(0.4f, 0.9f, 0.4f) },
            new EliteModificador { id = "elite_furia", nombre = "Furia", danoMultiplicador = 1.5f, badgeColor = new Color(0.95f, 0.4f, 0.35f) },
            new EliteModificador { id = "elite_prisa", nombre = "Prisa", velocidadMultiplicador = 1.4f, badgeColor = new Color(0.9f, 0.9f, 0.35f) },
            new EliteModificador { id = "elite_espejo", nombre = "Espejo", reflejaDanio = true, reflejaFraccion = 0.25f, badgeColor = new Color(0.6f, 0.5f, 0.9f) },
            new EliteModificador { id = "elite_sangre", nombre = "Sangre", regeneracion = true, regenPorSegundo = 2f, badgeColor = new Color(0.8f, 0.3f, 0.5f) },
        };

        /// Probabilidad de que un enemigo nazca élite: baja al principio, crece con nivel/oleada.
        static float ProbabilidadElite(int nivelOla)
        {
            return Mathf.Clamp01(0.05f + (nivelOla - 1) * 0.02f);
        }

        /// Sífilo del tejedor: enemigo diminuto de 1 HP, no capturable, suma presión.
        public void SpawnSifilo(Vector3 pos, Transform target)
        {
            pos = ColisionJugador.PuntoLibre(pos, Vector3.zero,
                ColisionJugador.RadioDefecto, ColisionJugador.AltoDefecto);

            GameObject go = new GameObject("Sifilo");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation((Vector3.zero - pos).normalized, Vector3.up);

            SlugActor actor = go.AddComponent<SlugActor>();
            ConfigurarSifilo(actor);

            EnemySlugAI ai = go.AddComponent<EnemySlugAI>();
            ai.SetArquetipo(EnemySlugAI.Arquetipo.Frenetico);
            ai.SetTarget(target);
            ai.SetSpeed(5f);
            ai.SetDamage(2f);

            go.AddComponent<SlugAnimator>();
            // El sífilo NO cuenta como enemigo del nivel: no entra en _alive ni en `spawned`.
            // Si contara, se comería una aparición del roster (el nivel perdería un enemigo de
            // verdad) y la cuota de capturas se calcularía con un enemigo que no se puede
            // capturar. Se lleva en su propia lista y se limpia en el mismo barrido.
            _sifilos.Add(go);
            Debug.Log("[SlugArena] spawn sifilo");
        }

        /// Un sífilo es de 1 HP y NUNCA capturable: si lo fuera, contaría para la cuota de
        /// capturas (la colección y la historia) y el tejedor "ganaría" niveles solo.
        public static void ConfigurarSifilo(SlugActor actor)
        {
            if (actor == null) return;
            var dato = new SlugData { slugId = "sifilo", slugName = "Sífilo", element = ElementType.Toxic,
                baseDamage = 3f, clashPower = 10f, elementColor = Color.green };
            actor.Configure(dato, false);
            actor.FijarVidaMaxima(1f);
            actor.FijarUmbralCaptura(1f); // a vida completa ya cumplira el umbral
            actor.FijarCapturable(false);
        }

        /// Monta un duelistas del mazo en lugar de la babosa. Conserva la paleta que salio de
        /// Blender (un submesh por material del MTL) y solo tine los tubos con el color del
        /// elemento. Devuelve false si falta el modelo: el spawn cae a AttachModel sin romperse.
        private static bool AttachDuelista(GameObject actor, string id, SlugData d)
        {
            GameObject modelo = DuelistaMazo.Cargar(id);
            if (modelo == null)
            {
                Debug.LogWarning("[SlugArena] falta Resources/Duelists/" + id + ": va babosa normal");
                return false;
            }
            GameObject inst = UnityEngine.Object.Instantiate(modelo, actor.transform);
            inst.name = "Cuerpo_" + id;
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            Color colorTubo = d != null ? d.elementColor : Color.yellow;
            foreach (Renderer mr in inst.GetComponentsInChildren<Renderer>(true))
            {
                Material[] originales = mr.sharedMaterials;
                if (originales == null) continue;
                Material[] copia = new Material[originales.Length];
                for (int i = 0; i < originales.Length; i++)
                {
                    Material m = originales[i];
                    // new Material(...) a proposito: escribir .color sobre el material compartido
                    // pintaria el color del elemento en el asset del OBJ y se filtraria a todo
                    // personaje que lo use, incluso entre partidas.
                    if (m != null && m.name.EndsWith("_tubos"))
                    {
                        Material tinte = new Material(m);
                        tinte.color = colorTubo;
                        // Mismo emissive que el heroe (MaterialesPixar.PrepararHeroe): los
                        // ribetes del equipo son lo que separa al duelista del fondo de la
                        // cueva, y tenidos pero no emisivos se leen como un color plano mas.
                        MaterialesPixar.SetEmision(tinte, colorTubo * 1.1f);
                        m = tinte;
                    }
                    else if (m != null)
                    {
                        // Autobrillo de la propia albedo: el mismo criterio que el heroe y
                        // las babosas. Sin esto el duelista se lee como una silueta oscura
                        // contra el piso azul de la cueva, sobre todo de noche.
                        Material t = new Material(m);
                        MaterialesPixar.SetEmision(t, MaterialesPixar.ColorBase(t) * 0.22f);
                        m = t;
                    }
                    copia[i] = m;
                }
                mr.sharedMaterials = copia;
            }

            // Sin cartel-retrato: un duelista 3D ya se identifica por el modelo y el
            // nameplate del HUD. El billboard tapaba media pantalla sobre la accion.
            // El cuerpo lo mueve DuelistaAnimator por huesos; SlugAnimator sigue a cargo
            // del raiz (squash, flash, lunge), asi que los dos no se pisan.
            DuelistaAnimator.Obtener(actor, DuelistaAnimator.Modo.Duelista);
            return true;
        }

        /// El heroe: Eli Shane con el Slug Blaster y los tubos. La babosa elegida sigue valiendo
        /// como el elemento de la municion y del retrato flotante.
        private static void AttachShane(GameObject actor, SlugData cargada)
        {
            GameObject modelo = Resources.Load<GameObject>("Shane/shane_pixar");
            bool pixar = modelo != null;
            bool fbx = false;
            if (modelo == null)
            {
                modelo = Resources.Load<GameObject>("Shane/shane_pixar_fbx");
                pixar = modelo != null;
                fbx = pixar;
            }
            if (modelo == null) modelo = Resources.Load<GameObject>("Shane/shane");
            if (modelo == null)
            {
                Debug.LogWarning("[SlugArena] falta Resources/Shane/shane_pixar, shane_pixar_fbx y shane: cae a la babosa");
                AttachModel(actor, cargada, new Vector3(0f, -1f, 0f), true);
                return;
            }
            // La escena trae un `Cuerpo_Shane` authoring con el OBJ viejo de 7 prendas.
            // Si no se saca, el heroe se ve doble: la figura rigida atras de la nueva con
            // esqueleto. Y Reiniciar/Reintentar vuelve a pasar por aqui, asi que esto
            // tambien corta la acumulacion de cuerpos y retratos repetidos.
            DescartarHijos(actor, "Cuerpo_Shane");
            DescartarHijos(actor, "Retrato");
            DescartarHeroesViejos(actor);

            GameObject inst = UnityEngine.Object.Instantiate(modelo, actor.transform);
            inst.name = "Cuerpo_Shane";
            inst.transform.localPosition = pixar ? new Vector3(0f, -1f, 0f) : new Vector3(0f, -0.9f, 0f);
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one * (pixar ? 1f : 1.15f);

            MeshRenderer propio = actor.GetComponent<MeshRenderer>();
            if (propio != null) propio.enabled = false;

            Color colorTubo = cargada != null ? cargada.elementColor : Color.yellow;
            if (pixar)
            {
                MaterialesPixar.QuitarAnimadores(inst);
                if (!fbx && MaterialesPixar.EsModeloTexturizado(inst))
                    MaterialesPixar.PrepararHeroe(inst, colorTubo);
                else
                    MaterialesPixar.PrepararHeroeFbx(inst, colorTubo);
                MaterialesPixar.AlinearPies(inst.transform, -1f);
            }
            else
            {
                foreach (Renderer mr in inst.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] originales = mr.sharedMaterials;
                    if (originales == null || originales.Length == 0) continue;
                    Material[] tintados = new Material[originales.Length];
                    for (int i = 0; i < originales.Length; i++)
                    {
                        string prenda = PrendaDeShane(mr.gameObject.name,
                            originales[i] != null ? originales[i].name : null);
                        Color c = ColorDeShane(prenda, colorTubo);
                        Material m = new Material(SlugOut.World.Shaders.Lit());
                        m.color = c;
                        if (prenda == "Tubos")
                        {
                            m.SetColor("_EmissionColor", c * 1.1f);
                            m.EnableKeyword("_EMISSION");
                        }
                        tintados[i] = m;
                    }
                    mr.sharedMaterials = tintados;
                }
            }
            // El retrato flotante era la identidad del heroe-objeto; con el modelo Pixar
            // texturizado es un cartel gigante sobre la cabeza que tapa la accion.
            // El heroe 3D no lleva cartel-retrato: el HUD ya muestra su retrato.
            // Con esqueleto, el heroe deja de ser una figura rigida: mantiene el brazo del
            // blaster levantado y el tiro empuja hacia atras.
            DuelistaAnimator.Obtener(actor, DuelistaAnimator.Modo.Duelista, true);

            Debug.Log("[SlugArena] heroe = " + (pixar ? "Shane Pixar texturizado" : "Shane legacy") +
                " (blaster + tubos), carga elemental=" + (cargada != null ? cargada.element.ToString() : "?"));
        }

        /// Que prenda es un material de Shane. El OBJ viejo tenia un GameObject por prenda
        /// y esa era la clave; el FBX riggeado es UN solo SkinnedMeshRenderer con un submesh
        /// por material, asi que la prenda se lee del sufijo del nombre ("shane_campera").
        private static void DescartarHeroesViejos(GameObject actor)
        {
            GameObject[] todos = UnityEngine.Object.FindObjectsByType<GameObject>();
            for (int i = 0; i < todos.Length; i++)
            {
                GameObject go = todos[i];
                if (go == null || go == actor) continue;
                if (go.name == "shane_malla" || go.name == "shane_rig" ||
                    (go.name == "Cuerpo_Shane" && go.transform.parent != actor.transform))
                    go.SetActive(false);
            }
        }

        private static void DescartarHijos(GameObject padre, string nombre)
        {
            Transform[] hijos = padre.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < hijos.Length; i++)
                if (hijos[i] != null && hijos[i] != padre.transform && hijos[i].name == nombre)
                    UnityEngine.Object.Destroy(hijos[i].gameObject);
        }

        private static string PrendaDeShane(string nombreObjeto, string nombreMaterial)
        {
            switch (nombreObjeto)
            {
                case "Pelo": case "Piel": case "Campera": case "Pantalon":
                case "Botas": case "Tubos": case "Blaster":
                    return nombreObjeto;
            }
            if (string.IsNullOrEmpty(nombreMaterial)) return null;
            int i = nombreMaterial.LastIndexOf('_');
            if (i < 0 || i == nombreMaterial.Length - 1) return null;
            string suf = nombreMaterial.Substring(i + 1);
            return char.ToUpperInvariant(suf[0]) + suf.Substring(1);
        }

        private static Color ColorDeShane(string prenda, Color colorTubo)
        {
            switch (prenda)
            {
                case "Pelo": return new Color(0.87f, 0.28f, 0.07f);
                case "Piel": return new Color(0.93f, 0.74f, 0.60f);
                case "Campera": return new Color(0.10f, 0.16f, 0.34f);
                case "Pantalon": return new Color(0.13f, 0.15f, 0.20f);
                case "Botas": return new Color(0.22f, 0.15f, 0.09f);
                case "Tubos": return colorTubo;
                // La cara del generador parametrico: sin estos casos los ojos caen al
                // default metalico y el heroe legacy queda con la cara gris.
                case "Ojos": return new Color(0.96f, 0.96f, 0.96f);
                case "Pupila": return new Color(0.08f, 0.08f, 0.10f);
                case "Boca": return new Color(0.48f, 0.23f, 0.18f);
                default: return new Color(0.32f, 0.35f, 0.40f);   // Blaster y lo demas: metal
            }
        }

        /// Cuelga el modelo 3D del roster como hijo. Devuelve true si encontro el OBJ.
        /// Ademas viste el cuerpo con el sprite OFICIAL del personaje (recursos/Sprites)
        /// y cuelga un retrato billboard encima, para que cada babosa se reconozca.
        private static void AdjuntarCompanero(GameObject actor, SlugData d)
        {
            if (actor == null || d == null) return;
            GameObject viejo = GameObject.Find("Companero");
            if (viejo != null) Object.Destroy(viejo);
            GameObject modelo = Resources.Load<GameObject>("Slugs/" + d.slugId + "_pixar");
            bool texturado = modelo != null;
            if (modelo == null) modelo = Resources.Load<GameObject>("Slugs/" + d.slugId);
            if (modelo == null) return;

            GameObject root = new GameObject("Companero");
            GameObject inst = Object.Instantiate(modelo, root.transform);
            inst.name = "Cuerpo_Companero";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            if (texturado) MaterialesPixar.PrepararBabosa(inst, d.slugId, d.elementColor);
            else
            {
                ApplyColor(inst, d.elementColor);
                Texture2D tex = LoadSprite(d, false);
                if (tex != null) AplicarTextura(inst, tex, d.elementColor);
            }

            SlugCompanion comp = root.AddComponent<SlugCompanion>();
            comp.objetivo = actor.transform;
            Vector3 p = actor.transform.position + actor.transform.rotation * comp.offset;
            p.y = Terrain3D.Altura(p.x, p.z) + 0.02f;
            root.transform.position = p;
            root.transform.rotation = Quaternion.Euler(0f, actor.transform.eulerAngles.y, 0f);
        }

        private static bool AttachModel(GameObject actor, SlugData d, Vector3 localOffset, bool evolucionado)
        {
            GameObject model = Resources.Load<GameObject>("Slugs/" + d.slugId + "_pixar");
            bool pixar = model != null;
            if (model == null) model = Resources.Load<GameObject>("Slugs/" + d.slugId);
            Debug.Log("[SlugArena] modelo " + d.slugId + " = "
                + (pixar ? "pixar" : (model != null ? "legacy" : "capsula")));
            GameObject cuerpo;
            if (model == null)
            {
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cap.name = "CuerpoCapsula";
                Collider c = cap.GetComponent<Collider>();
                if (c != null) Destroy(c);
                cap.transform.SetParent(actor.transform, false);
                cap.transform.localPosition = localOffset;
                ApplyColor(cap, d.elementColor);
                cuerpo = cap;
            }
            else
            {
                GameObject inst = Instantiate(model, actor.transform);
                inst.name = "Cuerpo_" + d.slugId;
                inst.transform.localPosition = localOffset;
                inst.transform.localRotation = Quaternion.identity;
                bool texturizado = MaterialesPixar.EsModeloTexturizado(inst);
                if (texturizado) MaterialesPixar.PrepararBabosa(inst, d.slugId, d.elementColor);
                else ApplyColor(inst, d.elementColor);
                cuerpo = inst;

                MeshRenderer own = actor.GetComponent<MeshRenderer>();
                if (own != null) own.enabled = false;

                // Ondula la columna nada mas si el modelo trajo esqueleto: los demas
                // siguen con la animacion procedural de SlugAnimator, que les queda bien.
                // Las babosas Pixar son estaticas a proposito: las anima SlugAnimator.
                if (!texturizado && inst.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                {
                    DuelistaAnimator.Obtener(actor, DuelistaAnimator.Modo.Babosa);
                }
            }

            // identidad visual: el sprite oficial viste a los modelos legacy y siempre
            // alimenta el retrato billboard. En un modelo texturizado no se pisa la textura 3D.
            Texture2D tex = LoadSprite(d, evolucionado);
            if (tex != null)
            {
                if (!MaterialesPixar.EsModeloTexturizado(cuerpo))
                    AplicarTextura(cuerpo, tex, d.elementColor);
            }
            return model != null;
        }

        /// Busca el sprite del personaje en Resources/Sprites (png convertidos desde
        /// los webp oficiales del repo). El orden depende de la evolucion de la historia.
        /// Publico: el HUD tambien usa el retrato del jugador.
        ///
        /// Dos cosas que antes salian caras: se resolvian las candidates y se preguntaba a
        /// Resources en CADA lectura (el HUD llama esto dentro de OnGUI, o sea varias veces por
        /// cuadro), y si faltaba el retrato no habia nada. Ahora hay cache por babosa y un
        /// fallback del color del elemento, que es arte que SI existe (viene en el .asset).
        static readonly System.Collections.Generic.Dictionary<SlugData, Texture2D[]> _retratos =
            new System.Collections.Generic.Dictionary<SlugData, Texture2D[]>();
        static readonly System.Collections.Generic.Dictionary<ElementType, Texture2D> _deElemento =
            new System.Collections.Generic.Dictionary<ElementType, Texture2D>();
        static readonly System.Collections.Generic.HashSet<string> _avisados = new System.Collections.Generic.HashSet<string>();

        public static Texture2D LoadSprite(SlugData d, bool evolucionado)
        {
            if (d == null) return null;
            Texture2D[] par;
            if (!_retratos.TryGetValue(d, out par))
            {
                par = new Texture2D[2];
                par[0] = BuscarRetrato(d, false);
                par[1] = BuscarRetrato(d, true);
                _retratos[d] = par;
            }
            Texture2D t = par[evolucionado ? 1 : 0];
            return t != null ? t : RetratoDeElemento(d);
        }

        static Texture2D BuscarRetrato(SlugData d, bool evolucionado)
        {
            string normal = Normalizado(d.slugName);
            string conEspacios = normal.Replace('-', ' ');
            string[] bases = new[] { d.slugId, d.slugName, normal, conEspacios };
            string[] carpetas = evolucionado
                ? new[] { "Velocimorphs/", "Protoforms/" }
                : new[] { "Protoforms/", "Velocimorphs/" };

            for (int c = 0; c < carpetas.Length; c++)
                for (int i = 0; i < bases.Length; i++)
                {
                    if (string.IsNullOrEmpty(bases[i])) continue;
                    Texture2D t = Resources.Load<Texture2D>("Sprites/" + carpetas[c] + bases[i]);
                    if (t != null) return t;
                }

            if (_avisados.Add(d.slugId + (evolucionado ? "/v" : "/p")))
                Debug.Log("[SlugArena] sin retrato para " + d.slugId + " (evolucionado=" + evolucionado +
                    "): usa el color del elemento. Faltan " + (_avisados.Count) + " nombres en Resources/Sprites.");
            return null;
        }

        /// Minusculas, sin tildes y con guion en vez de espacio: los 40 ids nuevos son
        /// "esquirla-helada" y el png legacy esta como "Esquirla Helada" (y "fosforo" contra
        /// "Fósforo"). Sin normalizar esos dos caían al vacío aunque el dibujo existiera.
        static string Normalizado(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            char[] v = s.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < v.Length; i++)
            {
                switch (v[i])
                {
                    case 'á': v[i] = 'a'; break;
                    case 'é': v[i] = 'e'; break;
                    case 'í': v[i] = 'i'; break;
                    case 'ó': v[i] = 'o'; break;
                    case 'ú': v[i] = 'u'; break;
                    case 'ü': v[i] = 'u'; break;
                    case 'ñ': v[i] = 'n'; break;
                    case ' ': v[i] = '-'; break;
                }
            }
            return new string(v);
        }

        static Texture2D RetratoDeElemento(SlugData d)
        {
            Texture2D t;
            if (_deElemento.TryGetValue(d.element, out t)) return t;
            Color c = d.elementColor;
            t = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    bool borde = x == 0 || y == 0 || x == 7 || y == 7;
                    t.SetPixel(x, y, borde ? c * 0.45f : c);
                }
            t.Apply(false);
            t.name = "retrato_" + d.element;
            _deElemento[d.element] = t;
            return t;
        }

        /// Viste el cuerpo con la textura: base = sprite, glow elemental suave detras.
        private static void AplicarTextura(GameObject cuerpo, Texture2D tex, Color elemento)
        {
            if (MaterialesPixar.EsModeloTexturizado(cuerpo)) return;
            // Renderer, no MeshRenderer: las babosas nuevas del mazo vienen FBX con
            // esqueleto y Unity las importa como SkinnedMeshRenderer. Con MeshRenderer
            // el sprite oficial no se les aplicaba y el cuerpo quedaba liso.
            Renderer[] rs = cuerpo.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                Material m = rs[i].material;   // instancia por babosa
                if (!m.HasProperty("_BaseMap") && !m.HasProperty("_MainTex")) continue;
                m.mainTexture = tex;
                m.color = Color.white;
                if (m.HasProperty("_EmissionColor"))
                {
                    m.SetColor("_EmissionColor", elemento * 0.25f);
                    m.EnableKeyword("_EMISSION");
                }
                RecortarAlfa(m);   // el sprite oficial tiene fondo transparente
            }
        }

        /// En el cuerpo 3D no se puede mezclar (se veria el relleno por detras): se recorta el alfa.
        private static void RecortarAlfa(Material m)
        {
            m.EnableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f);
            m.renderQueue = 2450;
        }

        private static void ApplyColor(GameObject root, Color color)
        {
            if (MaterialesPixar.EsModeloTexturizado(root))
            {
                Debug.LogWarning("[SlugArena] ApplyColor ignorado: el modelo ya trae textura");
                return;
            }
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            Material m = new Material(s);
            m.color = color;
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", color * 0.6f);
                m.EnableKeyword("_EMISSION");
            }   // doble cara: el winding no puede romper el look

            // Renderer, no MeshRenderer: un modelo riggeado llega como SkinnedMeshRenderer,
            // que NO deriva de MeshRenderer. Con MeshRenderer el tinte no se le aplicaba a
            // nadie y el cuerpo quedaba sin color del elemento, sin un solo aviso.
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++) rs[i].sharedMaterial = m;
        }
    }
}