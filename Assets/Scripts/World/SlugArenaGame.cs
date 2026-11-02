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

        [Header("Estado (solo lectura)")]
        [SerializeField] private int score = 0;
        [SerializeField] private int spawned = 0;
        [SerializeField] private int aliveCount = 0;
        [SerializeField] private int playerHpRounded = 0;
        [SerializeField] private int killsThisWave = 0;
        [SerializeField] private int capturadas = 0;
        [SerializeField] private System.Collections.Generic.List<string> coleccion = new System.Collections.Generic.List<string>();
        [SerializeField] public string outcome = "en curso";

        private readonly List<GameObject> _alive = new List<GameObject>();
        private SlugData[] _orden;
        private float _timer;
        private int _nextIndex;
        private SlugActor _playerActor;
        private bool _finished;
        private int goalCapturas;
        private bool _infinito;

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
        public string ColeccionTexto { get { return string.Join(", ", coleccion.ToArray()); } }
        public int Spawned { get { return spawned; } }
        public int TotalEnemies { get { return totalEnemies; } }
        public int Alive { get { return aliveCount; } }
        public int Capturas { get { return capturadas; } }

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
            }
            if (usarRocas && GetComponent<RockShower>() == null) gameObject.AddComponent<RockShower>();
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
                PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
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
            enemyDamage = cfg.dano;
            goalCapturas = cfg.capturas;
            _infinito = cfg.infinito;
            if (slugElegido != null) playerSlug = slugElegido;

            ReiniciarPartida();
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
                // OJO: la escena ya puede traer su propio SlugAnimator — no duplicar,
                // dos animators se pelean por escala/posicion/rotacion del transform.
                if (player.GetComponent<SlugAnimator>() == null)
                    player.gameObject.AddComponent<SlugAnimator>();

                PlayerAbilities hab = player.GetComponent<PlayerAbilities>();
                if (hab == null) hab = player.gameObject.AddComponent<PlayerAbilities>();
                hab.Configurar(playerSlug, _playerActor);

                if (usarShane) AttachShane(player.gameObject, playerSlug);
                else AttachModel(player.gameObject, playerSlug, new Vector3(0f, -1f, 0f), true);   // el heroe siempre evolucionado
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
                        string id = a.Data != null ? a.Data.slugId : "?";
                        if (!coleccion.Contains(id)) coleccion.Add(id);
                        StoryPanel historia = GetComponent<StoryPanel>();
                        if (historia != null) historia.AvanzarHistoria();
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

            aliveCount = _alive.Count;
            playerHpRounded = _playerActor != null ? Mathf.RoundToInt(_playerActor.Hp) : 0;

            // La arena tiene limites: sin esto el jugador se iba caminando del escenario.
            if (player != null && _playerActor != null && _playerActor.Alive)
            {
                Vector3 pp = player.position;
                pp.x = Mathf.Clamp(pp.x, -arenaLimit, arenaLimit);
                pp.z = Mathf.Clamp(pp.z, -arenaLimit, arenaLimit);
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
                if (GameFlow.Activo != null) GameFlow.Activo.NivelSuperado(GameFlow.Activo.Nivel);
                StoryPanel historia = GetComponent<StoryPanel>();
                if (historia != null) historia.AvanzarHistoria();
                if (player != null)
                {
                    SlugAnimator an = player.GetComponent<SlugAnimator>();
                    if (an != null) an.PlayVictory();
                }
                return;
            }

            if (spawned >= totalEnemies || _alive.Count >= maxAlive) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = spawnInterval;
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
            coleccion.Clear();
            outcome = "en curso";
            // hay que DESTRUIR los enemigos del arranque anterior: solo sacarlos del listado
            // deja babosas vivas persiguiendo al jugador sin que nadie las cuente
            foreach (GameObject g in _alive) if (g != null) Destroy(g);
            _alive.Clear();
            _nextIndex = 0;
            _timer = 0f;
            _finished = false;
            CombateCongelado = false;
            // el anillo invertido es estado del nivel, no del actor: si Iniciar() no lo vuelve
            // a poner, el modo infinito heredaria la ley del ultimo nivel de campana
            SlugActor.AnilloInvertido = false;
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
            _nextIndex++;

            float ang = _nextIndex * 2.39996f;
            Vector3 pos = new Vector3(Mathf.Cos(ang) * spawnRadius, 0f, Mathf.Sin(ang) * spawnRadius);

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
            EnemySlugAI ai = go.AddComponent<EnemySlugAI>();
            ai.SetArquetipo(EnemySlugAI.ArquetipoDe(d));
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
            go.AddComponent<SlugAnimator>();

            _alive.Add(go);
            spawned++;
            aliveCount = _alive.Count;
            if (montado) actor.MultiplicarHp(duelista == DuelistaMazo.Jefe ? 2.5f : 1.5f);
            Debug.Log("[SlugArena] spawn " + d.slugName + " (" + d.element + ") modelo="
                + (montado ? "duelista:" + duelista : (usedModel ? "OBJ" : "capsula")));
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
                        m = tinte;
                    }
                    copia[i] = m;
                }
                mr.sharedMaterials = copia;
            }

            Texture2D tex = LoadSprite(d, true);
            if (tex != null) PonerRetrato(actor, tex, 2.35f);

            // El cuerpo lo mueve DuelistaAnimator por huesos; SlugAnimator sigue a cargo
            // del raiz (squash, flash, lunge), asi que los dos no se pisan.
            DuelistaAnimator.Obtener(actor, DuelistaAnimator.Modo.Duelista);
            return true;
        }

        /// El heroe: Eli Shane con el Slug Blaster y los tubos. La babosa elegida sigue valiendo
        /// como el elemento de la municion y del retrato flotante.
        private static void AttachShane(GameObject actor, SlugData cargada)
        {
            GameObject modelo = Resources.Load<GameObject>("Shane/shane");
            if (modelo == null)
            {
                Debug.LogWarning("[SlugArena] falta Resources/Shane/shane.obj: cae a la babosa");
                AttachModel(actor, cargada, new Vector3(0f, -1f, 0f), true);
                return;
            }
            // La escena trae un `Cuerpo_Shane` authoring con el OBJ viejo de 7 prendas.
            // Si no se saca, el heroe se ve doble: la figura rigida atras de la nueva con
            // esqueleto. Y Reiniciar/Reintentar vuelve a pasar por aqui, asi que esto
            // tambien corta la acumulacion de cuerpos y retratos repetidos.
            DescartarHijos(actor, "Cuerpo_Shane");
            DescartarHijos(actor, "Retrato");

            GameObject inst = UnityEngine.Object.Instantiate(modelo, actor.transform);
            inst.name = "Cuerpo_Shane";
            inst.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one * 1.15f;

            MeshRenderer propio = actor.GetComponent<MeshRenderer>();
            if (propio != null) propio.enabled = false;

            Color colorTubo = cargada != null ? cargada.elementColor : Color.yellow;
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
                    Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
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
            // Shane mide ~1.9 con el pelo: el retrato de la babosa cargada va por encima de la cabeza
            Texture2D tex = LoadSprite(cargada, false);
            if (tex != null) PonerRetrato(actor, tex, 2.45f);
            // Con esqueleto, el heroe deja de ser una figura rigida: mantiene el brazo del
            // blaster levantado y el tiro empuja hacia atras.
            DuelistaAnimator.Obtener(actor, DuelistaAnimator.Modo.Duelista, true);

            Debug.Log("[SlugArena] heroe = Eli Shane (blaster + tubos), carga elemental=" + (cargada != null ? cargada.element.ToString() : "?"));
        }

        /// Que prenda es un material de Shane. El OBJ viejo tenia un GameObject por prenda
        /// y esa era la clave; el FBX riggeado es UN solo SkinnedMeshRenderer con un submesh
        /// por material, asi que la prenda se lee del sufijo del nombre ("shane_campera").
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
                default: return new Color(0.32f, 0.35f, 0.40f);   // Blaster y lo demas: metal
            }
        }

        /// Cuelga el modelo 3D del roster como hijo. Devuelve true si encontro el OBJ.
        /// Ademas viste el cuerpo con el sprite OFICIAL del personaje (recursos/Sprites)
        /// y cuelga un retrato billboard encima, para que cada babosa se reconozca.
        private static bool AttachModel(GameObject actor, SlugData d, Vector3 localOffset, bool evolucionado)
        {
            GameObject model = Resources.Load<GameObject>("Slugs/" + d.slugId);
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
                ApplyColor(inst, d.elementColor);
                cuerpo = inst;

                MeshRenderer own = actor.GetComponent<MeshRenderer>();
                if (own != null) own.enabled = false;

                // Ondula la columna nada mas si el modelo trajo esqueleto: los demas
                // siguen con la animacion procedural de SlugAnimator, que les queda bien.
                if (inst.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                {
                    DuelistaAnimator.Obtener(actor, DuelistaAnimator.Modo.Babosa);
                }
            }

            // identidad visual: textura oficial + retrato billboard
            Texture2D tex = LoadSprite(d, evolucionado);
            if (tex != null)
            {
                AplicarTextura(cuerpo, tex, d.elementColor);
                PonerRetrato(actor, tex);
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

        /// Cartelito con el sprite oficial girando a la camara: identidad a un vistazo.
        private static void PonerRetrato(GameObject actor, Texture2D tex, float alto = 1.15f)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Retrato";
            Collider col = q.GetComponent<Collider>();
            if (col != null) Destroy(col);   // los tiros no golpean el cartel
            q.transform.SetParent(actor.transform, false);
            q.transform.localPosition = new Vector3(0f, alto, 0f);
            q.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Texture");
            Material m = new Material(s);
            m.mainTexture = tex;
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // visible desde atras
            // el PNG trae alfa real; con el material opaco URP el fondo transparente sale NEGRO
            Transparentar(m);
            MeshRenderer mr = q.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = m;

            q.AddComponent<SpriteBillboard>();
        }

        /// Pone un material URP en modo transparente: sin esto, el alfa del sprite se pinta negro.
        /// En URP no alcanza con escribir los floats: hay que encender la keyword, si no el
        /// shader se queda con la variante opaca y el alfa se pinta negro.
        private static void Transparentar(Material m)
        {
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            if (m.HasProperty("_BlendOp")) m.SetFloat("_BlendOp", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.renderQueue = 3000;
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
