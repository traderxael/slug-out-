using UnityEngine;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.World
{
    /// Menu principal y progreso de campana, en IMGUI (misma logica que el HUD: sin Canvas que cablear).
    /// Vive en el mismo Mapa3D: mientras la pantalla de menu esta abierta la arena no arranca,
    /// y el tiempo se congela por GameFeel.Pausa.
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Activo { get; private set; }

        private enum Pantalla { Principal, Niveles, Babosas, Ajustes }
        private Pantalla _pantalla;

        [SerializeField] private bool enMenu = true;
        [SerializeField] private int nivelActual = 1;
        [SerializeField] private bool infinito;
        [SerializeField] private int desbloqueado = 1;
        [SerializeField] private string slugElegido = "";
        [SerializeField] private int partidasIniciadas = 0;

        private Vector2 _scroll;
        // Segunda pulsacion de NUEVA AVENTURA: borrar el progreso no puede pasar de un clic.
        private float _confirmarNuevaHasta = -1f;
        // Equipo activo del torneo: exactamente 3. El slot 0 es tambien la carga del
        // blaster en campana, asi el modo de siempre no se rompe.
        private readonly System.Collections.Generic.List<string> _equipo =
            new System.Collections.Generic.List<string>();
        private string _seleccion = "";
        private int _filtroBabosas = FiltroTodas;
        private string _busquedaBabosas = "";
        private GUIStyle _stBusquedaBabosas;
        private int _nivelSeleccionado = -1;
        private readonly System.Collections.Generic.List<SlugData> _babosasFiltradas =
            new System.Collections.Generic.List<SlugData>();
        private const int TamEquipo = 3;
        public const int FiltroTodas = -1;
        public const int FiltroEquipadas = -2;
        private const string CLAVE_EQUIPO = "slugout.equipo";
        private const string CLAVE_DESBLOQUEO = "slugout.desbloqueado";
        private const string CLAVE_SLUG = "slugout.slug";
        private const string CLAVE_COLECCION = "slugout.coleccion";
        private const string CLAVE_DESTELLOS = "slugout.reducirDestellos";
        private const string CLAVE_VOCES = "slugout.voces";
        private const string CLAVE_ASISTENCIA = "slugout.asistencia";
        private const string CLAVE_AIM_AMPLIO = "slugout.aimAmplio";
        private const string CLAVE_TEXTO_GRANDE = "slugout.textoGrande";
        private const int FilasAjustes = 5;

        // Foco de menu para gamepad/teclado: el indice es el orden de las filas dibujadas.
        private int _focoMenu;
        private int _indiceFila;
        private bool _confirmarFoco;
        private int _frameEntradaMenu = -1;

        public bool EnMenu { get { return enMenu; } }
        /// Modo confort: baja el flash de los golpes, el parpadeo del HUD y el temblor de camara.
        public static bool ReducirDestellos { get; private set; }
        /// Narracion de la historia y frases de duelistas. Encendida por defecto.
        public static bool VocesActivas { get; private set; } = true;
        /// Modo asistencia: menos dano recibido y mas i-frames. Apagado por defecto.
        public static bool Asistencia { get; private set; }
        /// Auto-aim amplio (85 grados) para quien necesita mas ayuda; por defecto 65.
        public static bool AimAmplio { get; private set; }
        /// Texto del HUD y los menus un 15 % mas grande.
        public static bool TextoGrande { get; private set; }
        public int Nivel { get { return nivelActual; } }
        public bool Infinito { get { return infinito; } }
        public int Desbloqueado { get { return desbloqueado; } }
        public string SlugElegido { get { return slugElegido; } }

        public enum EstadoNivel { Completado, Disponible, Bloqueado }

        /// La campana es lineal: el nivel desbloqueado es el proximo jugable, los anteriores
        /// estan completados y los posteriores bloqueados.
        public static EstadoNivel EstadoDeNivel(int nivel, int desbloqueado)
        {
            if (nivel < desbloqueado) return EstadoNivel.Completado;
            if (nivel == desbloqueado) return EstadoNivel.Disponible;
            return EstadoNivel.Bloqueado;
        }

        public static string CondicionDeNivel(int nivel)
        {
            return nivel <= 1 ? "" : "Completa el nivel " + (nivel - 1) + " para desbloquearlo.";
        }

        /// Filtro de elemento; TODAS y EQUIPADAS se resuelven fuera porque la equipada no es
        /// una propiedad de SlugData. No toca IDs ni datos: solo decide que tarjetas se ven.
        public static bool CoincideElemento(SlugData d, int filtro)
        {
            if (d == null) return false;
            if (filtro == FiltroTodas || filtro == FiltroEquipadas) return true;
            return (int)d.element == filtro;
        }

        public static bool CoincideBusqueda(SlugData d, string texto)
        {
            if (d == null) return false;
            if (string.IsNullOrWhiteSpace(texto)) return true;
            string q = texto.Trim().ToLowerInvariant();
            string nombre = (d.slugName ?? "").ToLowerInvariant();
            string elemento = SlugTexto.NombreElemento(d.element).ToLowerInvariant();
            string habitat = (d.habitat ?? "").ToLowerInvariant();
            return nombre.Contains(q) || elemento.Contains(q) || habitat.Contains(q);
        }

        private void Awake()
        {
            if (Activo != null && Activo != this) { Destroy(gameObject); return; }
            Activo = this;
            DontDestroyOnLoad(gameObject);
            desbloqueado = Mathf.Clamp(PlayerPrefs.GetInt(CLAVE_DESBLOQUEO, 1), 1, SlugStory.TotalCampana);
            slugElegido = PlayerPrefs.GetString(CLAVE_SLUG, "");
            ReducirDestellos = PlayerPrefs.GetInt(CLAVE_DESTELLOS, 0) == 1;
            VocesActivas = PlayerPrefs.GetInt(CLAVE_VOCES, 1) == 1;
            Asistencia = PlayerPrefs.GetInt(CLAVE_ASISTENCIA, 0) == 1;
            AimAmplio = PlayerPrefs.GetInt(CLAVE_AIM_AMPLIO, 0) == 1;
            TextoGrande = PlayerPrefs.GetInt(CLAVE_TEXTO_GRANDE, 0) == 1;
            HudTheme.EscalaTexto = TextoGrande ? 1.15f : 1f;
            CargarEquipo();
            VolverAlMenu();
        }

        private void OnDisable()
        {
            if (Activo == this) Activo = null;
        }

        public static SlugData BabosaElegida()
        {
            string id = Activo != null ? Activo.slugElegido : "";
            return SlugRoster.Buscar(id) ?? SlugRoster.Buscar("infierno");
        }

        public static string ColeccionGuardada()
        {
            return PlayerPrefs.GetString(CLAVE_COLECCION, "");
        }

        public static void GuardarColeccion(string csv)
        {
            PlayerPrefs.SetString(CLAVE_COLECCION, csv ?? "");
            PlayerPrefs.Save();
        }

        public void VolverAlMenu()
        {
            enMenu = true;
            _pantalla = Pantalla.Principal;
            _focoMenu = 0;
            GameFeel.Pausa = true;
        }

        public void IniciarCampana(int nivel)
        {
            nivelActual = Mathf.Clamp(nivel, 1, SlugStory.TotalCampana);
            infinito = false;
            // Empezar aventura reinicia las frases; seguir de nivel no, asi Blakk y los rivales
            // rotan sus lineas durante la campana en vez de repetir la primera.
            if (nivelActual <= 1)
            {
                DuelistaVoces.ReiniciarPartida();
                GameHUD.ReiniciarAvisoEscupidor();
                StoryPanel historia = GetComponent<StoryPanel>();
                if (historia != null) historia.ReiniciarCampana();
            }
            PrepararRegion(RegionParaNivel(nivelActual));
            Comenzar(NivelConfig.Campana(nivelActual));
        }

        /// La campana son 28 niveles en cinco arcos (6+6+6+6+4): cada cueva tiene su tramo y
        /// el epilogo cae en el Templo Olvidado. La region no puede depender de por donde
        /// camino el jugador la ultima vez (Actual es estatico y persiste entre partidas).
        public static int RegionParaNivel(int nivel)
        {
            return CampanaMapa.RegionDeNivel(nivel);
        }

        /// Reconstruye la cueva del tramo antes de que la arena arme el nivel. Si ya esta en
        /// la region correcta no toca nada; sin RegionCuevaSeca (tests con arena aislada) sale.
        private static void PrepararRegion(int destino)
        {
            RegionCuevaSeca region = Object.FindAnyObjectByType<RegionCuevaSeca>();
            if (region == null) return;
            if (RegionCuevaSeca.Actual == destino && region.Root != null) return;
            region.Reconstruir(destino, out _, -1);
        }

        public void IniciarInfinito()
        {
            infinito = true;
            DuelistaVoces.ReiniciarPartida();
            StoryPanel historiaInfinito = GetComponent<StoryPanel>();
            if (historiaInfinito != null) historiaInfinito.ReiniciarInfinito();
            Comenzar(NivelConfig.Infinito());
        }

        public void Reintentar()
        {
            if (!infinito) PrepararRegion(RegionParaNivel(nivelActual));
            Comenzar(infinito ? NivelConfig.Infinito() : NivelConfig.Campana(nivelActual));
        }

        public void SiguienteNivel()
        {
            IniciarCampana(Mathf.Min(nivelActual + 1, SlugStory.TotalCampana));
        }

        /// Cierre de campana compartido por el HUD y la arena: ganar el ultimo nivel de campana
        /// (no en infinito) habilita el epilogo y el puente al modo sin techo.
        public static bool EsFinalDeCampana(bool gano, bool infinito, int nivel)
        {
            return gano && !infinito && nivel >= SlugStory.TotalCampana;
        }

        public void IniciarConfig(NivelConfig cfg)
        {
            if (!infinito && !cfg.infinito)
            {
                nivelActual = Mathf.Clamp(nivelActual, 1, SlugStory.TotalCampana);
                PrepararRegion(RegionParaNivel(nivelActual));
            }
            Comenzar(cfg);
        }

        private void Comenzar(NivelConfig cfg)
        {
            // GameFlow, SlugArenaGame y StoryPanel estan en el MISMO GameObject (ArenaGame,
            // que es persistente). Buscar la arena por tipo devuelve a veces la copia recien
            // cargada que Awake ya destruyo -- Destroy se difiere al fin del cuadro -- y el
            // nivel arranca en un objeto que desaparece: la historia se encola ahi y la
            // tarjeta nunca se ve.
            SlugArenaGame arena = GetComponent<SlugArenaGame>();
            if (arena == null) { Debug.LogWarning("[GameFlow] ArenaGame no tiene SlugArenaGame."); return; }
            enMenu = false;
            partidasIniciadas++;
            GameFeel.Pausa = false;
            // La ruta (TAB) vive en StoryPanel y solo la cierra su propio Toggle: si el jugador se
            // iba al menu con la ruta abierta, Comenzar dejaba Pausa=false pero rutaAbierta=true y
            // el overlay "EN PAUSA" se pintaba sobre un combate vivo.
            StoryPanel panel = GetComponent<StoryPanel>();
            if (panel != null) panel.CerrarRuta();
            arena.Iniciar(cfg, BabosaElegida());
        }

        /// Lo llama la arena al GANASTE. Desbloquea el siguiente nivel de campana.
        public void NivelSuperado(int nivel)
        {
            // La coleccion se guarda PRIMERO y para los dos modos: antes el return del infinito
            // dejaba las capturas del modo infinito fuera del guardado, y en campana el guardado
            // se sobrescribia con la coleccion de ESTA partida (la arena la vacia al reiniciar),
            // asi que terminar un nivel podia hacer que el conteo del menu BAJARA.
            GuardarColeccionDeArena();
            if (infinito) return;
            int siguiente = Mathf.Min(nivel + 1, SlugStory.TotalCampana);
            if (siguiente > desbloqueado)
            {
                desbloqueado = siguiente;
                PlayerPrefs.SetInt(CLAVE_DESBLOQUEO, desbloqueado);
                PlayerPrefs.Save();
                Debug.Log("[GameFlow] nivel " + siguiente + " desbloqueado");
            }
        }

        /// La coleccion de la partida pasa al guardado del menu (la arena la tiene en memoria).
        public void GuardarColeccionDeArena()
        {
            SlugArenaGame arena = GetComponent<SlugArenaGame>();
            if (arena == null) return;
            string csv = arena.ColeccionTexto;
            if (string.IsNullOrEmpty(csv)) return;

            char[] separador = new char[] { ',' };
            System.Collections.Generic.HashSet<string> ids = new System.Collections.Generic.HashSet<string>();
            foreach (string s in csv.Split(separador, System.StringSplitOptions.RemoveEmptyEntries))
                if (!string.IsNullOrWhiteSpace(s)) ids.Add(s.Trim());
            foreach (string s in ColeccionGuardada().Split(separador, System.StringSplitOptions.RemoveEmptyEntries))
                if (!string.IsNullOrWhiteSpace(s)) ids.Add(s.Trim());

            System.Collections.Generic.List<string> ordenadas = new System.Collections.Generic.List<string>(ids);
            ordenadas.Sort(System.StringComparer.Ordinal);
            GuardarColeccion(string.Join(",", ordenadas));
            Debug.Log("[GameFlow] coleccion fusionada: " + ordenadas.Count + " ids guardados, " +
                arena.Capturadas + " capturadas en esta partida");
        }

        private void Update()
        {
            if (!enMenu) return;
            // El guard anterior era `if (!enMenu || !GameFeel.Pausa) return;`, o sea solo re-afirmaba
            // la pausa si ya estaba puesta: nunca la recuperaba cuando algo la habia soltado, y la
            // arena seguia corriendo debajo del menu.
            if (!GameFeel.Pausa) GameFeel.Pausa = true;
        }

        private void OnGUI()
        {
            if (!enMenu) return;
            // El torneo tapa el menu pero no bloquea sus botones: sin este corte, un clic
            // sobre su pantalla podia activar la fila del menu que quedaba debajo.
            if (TorneoUI.Activo) return;
            if (CollectionUI.Abierto) return;
            HudTheme.Asegurar();
            GUI.depth = -1;   // menor depth se dibuja despues = mas arriba: 0 es el HUD, 10 el banner
            TeclasMenu();

            float w = Screen.width, h = Screen.height;
            // Ya no se tapa la pantalla con un rectangulo negro: la caverna del juego queda
            // de fondo, como en la referencia, y solo se oscurece la franja del menu.
            DibujarVelo(w, h);

            switch (_pantalla)
            {
                case Pantalla.Principal: DibujarPrincipal(w, h); break;
                case Pantalla.Niveles: DibujarNiveles(w, h); break;
                case Pantalla.Babosas: DibujarBabosas(w, h); break;
                case Pantalla.Ajustes: DibujarAjustes(w, h); break;
            }
        }

        /// La Game View entrega las teclas al IMGUI; el Input System no siempre ve las
        /// pulsaciones cuando el menu esta pausado con timeScale=0.
        private void TeclasMenu()
        {
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.F2) { _pantalla = Pantalla.Ajustes; _focoMenu = 0; e.Use(); }
                else if (_pantalla == Pantalla.Ajustes && e.keyCode == KeyCode.F3)
                { AlternarDestellos(); e.Use(); }
                else if (_pantalla != Pantalla.Principal && e.keyCode == KeyCode.Escape)
                { _pantalla = Pantalla.Principal; _focoMenu = 0; e.Use(); }
            }
            TeclasFoco();
        }

        private int FilasPrincipales()
        {
            bool hayProgreso = desbloqueado > 1 || !string.IsNullOrEmpty(ColeccionGuardada());
            return 8 + (hayProgreso ? 1 : 0);
        }

        /// Navegacion por flancos con mando o flechas. Se procesa UNA vez por cuadro: IMGUI
        /// llama a OnGUI en Layout y Repaint y un flanco no puede mover el foco dos veces.
        private void TeclasFoco()
        {
            if (_frameEntradaMenu == Time.frameCount) return;
            _frameEntradaMenu = Time.frameCount;
            _confirmarFoco = false;

            EntradaFrame en = EntradaJuego.Leer();
            int filas = _pantalla == Pantalla.Principal ? FilasPrincipales()
                      : (_pantalla == Pantalla.Ajustes ? FilasAjustes : 0);
            if (filas > 0)
            {
                _focoMenu = Mathf.Clamp(_focoMenu, 0, filas - 1);
                if (en.menuArriba || en.menuIzquierda)
                    _focoMenu = NavegacionFoco.Mover(_focoMenu, -1, filas);
                if (en.menuAbajo || en.menuDerecha)
                    _focoMenu = NavegacionFoco.Mover(_focoMenu, 1, filas);
                if (en.confirmar) _confirmarFoco = true;
            }
            if (en.cancelar && _pantalla != Pantalla.Principal)
            {
                _pantalla = Pantalla.Principal;
                _focoMenu = 0;
            }
        }

        /// Bandas de opacidad decreciente de izquierda a derecha: el texto se lee y la
        /// escena sigue visible. Mas barato y mas flexible que una textura de degradado.
        private void DibujarVelo(float w, float h)
        {
            float ancho = Mathf.Min(620f, w * 0.46f);
            int bandas = 30;
            float paso = ancho / bandas;
            Color prev = GUI.color;
            for (int i = 0; i < bandas; i++)
            {
                float t = i / (float)bandas;
                GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.93f * (1f - t * t));
                GUI.DrawTexture(new Rect(paso * i, 0f, paso + 1f, h), Texture2D.whiteTexture);
            }
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.62f);
            GUI.DrawTexture(new Rect(0f, h - 52f, w, 52f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// Geometria del menu principal, separada del dibujo para que una prueba pueda exigir que
        /// las ocho filas no pisen el pie de pantalla a ninguna altura (a 1024x576 lo pisaban).
        public static float ArranqueMenu(float h) { return Mathf.Max(158f, h * 0.17f); }

        public static float PasoFilaMenu(float h, int filas)
        {
            int n = Mathf.Max(1, filas);
            return Mathf.Clamp((h - 70f - ArranqueMenu(h)) / n - 6f, 34f, 54f);
        }

        /// La fila de EQUIPO de la coleccion tiene que terminar antes de donde empieza la ficha
        /// de la derecha; con paso fijo de 176 el tercer slot llegaba a x=640 y la ficha arrancaba
        /// en 614 a 1024 px de ancho.
        public static float PasoSlotEquipo(float w)
        {
            float bordeFicha = w * 0.60f - 24f;
            float disponible = Mathf.Max(240f, bordeFicha - 120f);
            return disponible / TamEquipo;
        }

        public static float AnchoSlotEquipo(float w) { return Mathf.Min(168f, PasoSlotEquipo(w) - 8f); }

        private void DibujarPrincipal(float w, float h)
        {
            float x = Mathf.Max(56f, w * 0.055f);
            float ancho = Mathf.Min(470f, w * 0.40f);
            // Con 576 px de alto, h*0.19 dejaba el titulo fuera de pantalla (se veia cortado).
            // El arranque del bloque y la altura de fila se derivan del alto real.
            float y = ArranqueMenu(h);

            GUI.Label(new Rect(w - 220f, 20f, 196f, 18f), "F2  AJUSTES",
                EstiloDerecha(11, FontStyle.Bold, new Color(0.55f, 0.60f, 0.68f)));
            GUI.Label(new Rect(x, y - 132f, ancho, 60f), "SLUG OUT",
                EstiloIzq(44, FontStyle.Bold, new Color(1f, 0.72f, 0.3f)));
            GUI.Label(new Rect(x + 2f, y - 70f, ancho, 22f), "4 CUEVAS, UNA ARENA",
                EstiloIzq(14, FontStyle.Bold, new Color(0.70f, 0.77f, 0.86f)));
            HudTheme.Linea(new Rect(x + 2f, y - 64f, 190f, 2f),
                new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.65f));
            HudTheme.Linea(new Rect(x + 198f, y - 64f, 44f, 2f),
                new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.85f));

            bool hayProgreso = desbloqueado > 1 || !string.IsNullOrEmpty(ColeccionGuardada());
            // Antes: fila fija derivada del alto (clamp 42..54). A 1024x576 las ocho filas
            // terminaban en y=551 y el pie de pantalla (h-44=532) se dibujaba encima de SALIR.
            // Ahora el paso se reparte entre el arranque y el piso que deja el pie, y solo se
            // aprieta cuando de verdad hace falta: a 768 sigue dando el tope de 54 de siempre.
            // Ocho filas sin CONTINUAR (NUEVA, COLECCION, REGISTRO, EXPLORAR, INFINITO, DUELO,
            // AJUSTES, SALIR) y nueve con el. El numero tiene que ser el de filas que se dibujan:
            // con la fila REGISTRO nueva y el 7 de antes, a 1024x576 SALIR terminaba en 549,5 y el
            // pie empieza en 532. `LayoutPantallasChicasTests` barre 8 y 9.
            int filas = 8 + (hayProgreso ? 1 : 0);
            float fila = PasoFilaMenu(h, filas);
            _indiceFila = 0;

            if (hayProgreso &&
                FilaMenu(x, ref y, ancho, fila, "CONTINUAR",
                         "Campana · nivel " + desbloqueado, new Color(1f, 0.72f, 0.3f), true, true))
                IniciarCampana(desbloqueado);

            bool confirmando = Time.unscaledTime < _confirmarNuevaHasta;
            if (FilaMenu(x, ref y, ancho, fila, "NUEVA AVENTURA",
                    confirmando ? "Otra vez para confirmar: se borra el progreso"
                                : "Empezar desde el principio",
                    new Color(0.55f, 0.85f, 0.72f), true))
            {
                if (confirmando) NuevaAventura();
                else _confirmarNuevaHasta = Time.unscaledTime + 4f;
            }

            SlugData eq = BabosaElegida();
            if (FilaMenu(x, ref y, ancho, fila, "COLECCION",
                    eq != null ? "Equipada: " + eq.slugName + " · " + eq.element
                               : "Elegir que babosa llevas",
                    new Color(0.55f, 0.80f, 1f), true))
            {
                _pantalla = Pantalla.Babosas;
                _focoMenu = 0;
            }

            if (FilaMenu(x, ref y, ancho, fila, "REGISTRO",
                    CollectionUI.ContadorDescubiertas() + " babosas en el cuaderno",
                    new Color(0.62f, 0.86f, 0.86f), true))
                CollectionUI.Abrir();

            if (FilaMenu(x, ref y, ancho, fila, "EXPLORAR",
                    "Ruta de la cueva · " + SlugStory.TotalCampana + " niveles",
                    new Color(0.78f, 0.72f, 1f), true))
            {
                _pantalla = Pantalla.Niveles;
                _focoMenu = 0;
            }

            if (FilaMenu(x, ref y, ancho, fila, "MODO INFINITO",
                    "Oleadas sin techo", new Color(0.92f, 0.70f, 0.45f), true))
                IniciarInfinito();

            if (FilaMenu(x, ref y, ancho, fila, "MODO DUELO",
                    "Torneo por turnos · 3 vs IA",
                    new Color(0.95f, 0.62f, 0.55f), true))
                TorneoUI.Abrir();

            if (FilaMenu(x, ref y, ancho, fila, "AJUSTES",
                    ReducirDestellos ? "Destellos y sacudida reducidos" : "Destellos, sacudida y confort",
                    new Color(0.70f, 0.78f, 0.92f), true))
            {
                _pantalla = Pantalla.Ajustes;
                _focoMenu = 0;
            }

            if (FilaMenu(x, ref y, ancho, fila, "SALIR", null,
                    new Color(0.55f, 0.57f, 0.62f), true))
                Application.Quit();

            string coleccion = ColeccionGuardada();
            int n = string.IsNullOrEmpty(coleccion) ? 0 : coleccion.Split(',').Length;
            // "1 babosas descubiertas" se leia mal en la primera partida, que es justamente
            // la que todo el mundo ve.
            string cuantas = n == 1 ? "1 babosa descubierta" : n + " babosas descubiertas";
            GUI.Label(new Rect(x + 2f, h - 44f, w * 0.62f, 24f),
                "v0.4 prototipo  ·  " + cuantas + "  ·  " + EntradaJuego.TextoAyuda(EntradaJuego.UltimoDispositivo),
                EstiloIzq(12, FontStyle.Normal, new Color(0.55f, 0.59f, 0.66f)));

            if (eq != null)
                GUI.Label(new Rect(w - 420f, h - 44f, 400f, 24f),
                    eq.slugName.ToUpperInvariant() + "  ·  " + eq.element.ToString().ToUpperInvariant(),
                    EstiloDerecha(12, FontStyle.Bold, new Color(0.72f, 0.78f, 0.86f)));

            DibujarVitrina(w, h);
        }

        /// Vitrina de las tres babosas emblema a la derecha del menu: ocupa el espacio que
        /// antes era solo caverna y le da al arranque la identidad de la referencia.
        private void DibujarVitrina(float w, float h)
        {
            if (w < 760f) return;
            float ancho = Mathf.Min(440f, w * 0.4f);
            float x = w - 24f - ancho;
            float y = Mathf.Max(148f, h * 0.24f);
            float alto = Mathf.Min(330f, h - y - 64f);
            if (alto < 230f) return;

            Rect panel = new Rect(x, y, ancho, alto);
            HudTheme.Panel(panel);
            HudTheme.Esquinas(panel, new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.55f), 14f, 1.5f);
            HudTheme.Linea(new Rect(panel.x, panel.y, panel.width, 2f),
                new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.85f));
            GUI.Label(new Rect(x + 16f, y + 10f, ancho - 32f, 20f), "BABOSAS DE LA CUEVA",
                EstiloIzq(13, FontStyle.Bold, HudTheme.OroClaro));
            GUI.Label(new Rect(x + 16f, y + 28f, ancho - 32f, 14f),
                "TRES EMBLEMAS   ·   COMPAÑÍA DE EXPLORACIÓN",
                EstiloIzq(8, FontStyle.Bold, HudTheme.Cian));

            string[] ids = { "electroshock", "ascua", "copo" };
            SlugData eq = BabosaElegida();
            float yCarta = y + 48f;
            float altoCarta = 69f;
            for (int i = 0; i < ids.Length; i++)
            {
                SlugData d = SlugRoster.Buscar(ids[i]);
                if (d == null) continue;
                Rect carta = new Rect(x + 14f, yCarta + i * (altoCarta + 6f), ancho - 28f, altoCarta);
                HudTheme.Chip(carta);
                if (eq == d)
                    HudTheme.Esquinas(carta, new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.9f), 9f, 1.5f);
                HudTheme.Linea(new Rect(carta.x, carta.y + 1f, carta.width, 2f),
                    new Color(d.elementColor.r, d.elementColor.g, d.elementColor.b, 0.7f));
                MiniRetrato(new Rect(carta.x + 8f, carta.y + 7f, 54f, 54f), d);

                GUI.Label(new Rect(carta.x + 72f, carta.y + 7f, carta.width - 82f, 18f), d.slugName,
                    EstiloIzq(15, FontStyle.Bold, HudTheme.Texto));
                GUI.Label(new Rect(carta.x + 72f, carta.y + 27f, carta.width - 82f, 13f),
                    d.element.ToString().ToUpperInvariant(),
                    EstiloIzq(9, FontStyle.Bold, d.elementColor));
                GUI.Label(new Rect(carta.x + 72f, carta.y + 42f, carta.width - 82f, 13f),
                    Duelo.NombreEfecto(d.abilityType),
                    EstiloIzq(8, FontStyle.Normal, HudTheme.Tenue));
                if (eq == d)
                    GUI.Label(new Rect(carta.xMax - 82f, carta.y + 6f, 74f, 12f), "EQUIPADA",
                        EstiloDerecha(8, FontStyle.Bold, HudTheme.OroClaro));
            }

            int descubiertas = CollectionUI.ContadorDescubiertas();
            int total = SlugRoster.Todos != null ? SlugRoster.Todos.Length : 0;
            float yPie = y + alto - 32f;
            HudTheme.Linea(new Rect(x + 16f, yPie - 6f, ancho - 32f, 1f),
                new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.2f));
            GUI.Label(new Rect(x + 16f, yPie, ancho - 32f, 13f),
                "NIVEL " + desbloqueado + "   ·   " + descubiertas + " / " + total + " DESCUBIERTAS",
                EstiloIzq(8, FontStyle.Bold, HudTheme.Tenue));
            HudTheme.Barra(new Rect(x + 16f, yPie + 16f, ancho - 32f, 5f),
                total > 0 ? (float)descubiertas / total : 0f, HudTheme.Cian);
        }

        private static void MiniRetrato(Rect r, SlugData d)
        {
            Texture2D tex = SlugArenaGame.LoadSprite(d, true);
            if (tex != null && !tex.name.StartsWith("retrato_"))
            {
                HudTheme.Retrato(r, tex, d.elementColor);
                return;
            }
            HudTheme.Chip(r);
            Color prev = GUI.color;
            GUI.color = new Color(d.elementColor.r, d.elementColor.g, d.elementColor.b, 0.2f);
            GUI.DrawTexture(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), Texture2D.whiteTexture);
            GUI.color = prev;
            HudTheme.Esquinas(r, new Color(d.elementColor.r, d.elementColor.g, d.elementColor.b, 0.85f), 9f, 1.5f);
            Texture2D silueta = HudTheme.SiluetaBabosa();
            if (silueta != null)
            {
                Color prevSil = GUI.color;
                GUI.color = new Color(d.elementColor.r, d.elementColor.g, d.elementColor.b, 0.9f);
                GUI.DrawTexture(new Rect(r.x + 5f, r.y + 4f, r.width - 10f, r.height - 8f),
                    silueta, ScaleMode.ScaleToFit, true);
                GUI.color = prevSil;
            }
            else
            {
                string inicial = !string.IsNullOrEmpty(d.slugName) ? d.slugName.Substring(0, 1).ToUpperInvariant() : "?";
                GUI.Label(r, inicial, HudTheme.Estilo(22, FontStyle.Bold, TextAnchor.MiddleCenter, d.elementColor));
            }
        }

        /// Fila del menu: placa, barra de acento, cuadradito de icono, titulo y subtitulo.
        /// Devuelve true solo si se hizo clic y la fila esta activa.
        private bool FilaMenu(float x, ref float y, float ancho, float alto, string texto,
                              string sub, Color acento, bool activo, bool seleccionado = false)
        {
            int indice = _indiceFila++;
            Rect r = new Rect(x, y, ancho, alto);
            bool foco = _focoMenu == indice;
            bool hover = activo && (r.Contains(Event.current.mousePosition) || foco);

            HudTheme.Chip(r);
            Color colorAcento = activo ? acento : new Color(0.34f, 0.36f, 0.42f);
            HudTheme.Linea(new Rect(r.x, r.y, 3f, r.height), colorAcento);
            if (hover || seleccionado)
            {
                HudTheme.Esquinas(r, new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.9f), 10f, 1.5f);
                GUI.color = new Color(1f, 0.9f, 0.6f, 0.05f);
                GUI.DrawTexture(new Rect(r.x + 3f, r.y + 2f, r.width - 5f, r.height - 4f), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            float icx = r.x + 18f, icy = r.y + r.height * 0.5f;
            Matrix4x4 matriz = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(icx, icy));
            HudTheme.Linea(new Rect(icx - 4f, icy - 4f, 8f, 8f), colorAcento);
            GUI.matrix = matriz;

            Color cTexto = !activo ? new Color(0.44f, 0.46f, 0.51f)
                         : hover ? new Color(1f, 0.87f, 0.58f)
                         : new Color(0.93f, 0.95f, 0.98f);
            bool conSub = !string.IsNullOrEmpty(sub);
            // A 576 el paso baja a 34 px: con el subtitulo en +27 se salia de la placa y se
            // montaba sobre la fila siguiente. En modo compacto entran titulo 14 + sub 9.
            bool compacto = alto < 44f;
            float subY = compacto ? 17f : (alto >= 46f ? 27f : 20f);
            int tamTitulo = compacto ? 14 : 17;
            GUI.Label(new Rect(r.x + 34f, r.y + (conSub ? (compacto ? 1f : 4f) : 0f), r.width - 44f,
                    conSub ? (compacto ? 15f : Mathf.Min(24f, subY - 2f)) : r.height),
                texto, EstiloIzq(tamTitulo, FontStyle.Bold, cTexto));
            if (conSub)
                GUI.Label(new Rect(r.x + 34f, r.y + subY, r.width - 44f, compacto ? 14f : 20f), sub,
                    EstiloIzq(compacto ? 9 : 12, FontStyle.Normal,
                        activo ? new Color(0.62f, 0.67f, 0.75f) : new Color(0.40f, 0.42f, 0.47f)));

            bool clic = activo && GUI.Button(r, GUIContent.none, GUIStyle.none);
            bool confirma = activo && foco && _confirmarFoco;
            if (confirma) _confirmarFoco = false;
            y += alto + 6f;
            return clic || confirma;
        }

        /// Nueva aventura: borra el progreso guardado. Se confirma con una segunda pulsacion.
        private void NuevaAventura()
        {
            desbloqueado = 1;
            PlayerPrefs.SetInt(CLAVE_DESBLOQUEO, 1);
            PlayerPrefs.SetString(CLAVE_COLECCION, "");
            PlayerPrefs.Save();
            _confirmarNuevaHasta = -1f;
            IniciarCampana(1);
        }

        public void AlternarDestellos()
        {
            ReducirDestellos = !ReducirDestellos;
            PlayerPrefs.SetInt(CLAVE_DESTELLOS, ReducirDestellos ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void AlternarVoces()
        {
            VocesActivas = !VocesActivas;
            PlayerPrefs.SetInt(CLAVE_VOCES, VocesActivas ? 1 : 0);
            PlayerPrefs.Save();
            if (!VocesActivas && VozPlayer.Instancia != null) VozPlayer.Instancia.Stop();
        }

        public void AlternarAsistencia()
        {
            Asistencia = !Asistencia;
            PlayerPrefs.SetInt(CLAVE_ASISTENCIA, Asistencia ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void AlternarAimAmplio()
        {
            AimAmplio = !AimAmplio;
            PlayerPrefs.SetInt(CLAVE_AIM_AMPLIO, AimAmplio ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void AlternarTextoGrande()
        {
            TextoGrande = !TextoGrande;
            PlayerPrefs.SetInt(CLAVE_TEXTO_GRANDE, TextoGrande ? 1 : 0);
            PlayerPrefs.Save();
            HudTheme.EscalaTexto = TextoGrande ? 1.15f : 1f;
        }

        /// Solo pruebas: fija el modo sin tocar las preferencias del usuario.
        public static void FijarAsistenciaPrueba(bool v) { Asistencia = v; }
        public static void FijarAimAmplioPrueba(bool v) { AimAmplio = v; }

        private void DibujarAjustes(float w, float h)
        {
            GUI.Label(new Rect(48f, 20f, w - 96f, 40f), "AJUSTES",
                EstiloIzq(30, FontStyle.Bold, new Color(0.93f, 0.95f, 0.98f)));
            if (Boton(new Rect(w - 168f, 20f, 120f, 32f), "< Menu"))
            {
                _pantalla = Pantalla.Principal;
                _focoMenu = 0;
            }

            float ancho = Mathf.Min(700f, w - 96f);
            float x = 48f, y = 88f;
            Rect panel = new Rect(x, y, ancho, 348f);
            HudTheme.Panel(panel);
            HudTheme.Esquinas(panel, new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.45f), 12f, 1.5f);
            HudTheme.Linea(new Rect(x, y, ancho, 2f),
                new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.7f));

            GUI.Label(new Rect(x + 20f, y + 12f, ancho - 40f, 28f),
                "ACCESIBILIDAD Y CONFORT",
                EstiloIzq(20, FontStyle.Bold, new Color(0.93f, 0.95f, 0.98f)));

            float fy = y + 44f;
            if (BotonFoco(new Rect(x + 20f, fy, ancho - 40f, 38f),
                    "CONFORT: " + (ReducirDestellos ? "ACTIVADO" : "APAGADO"), 0)) AlternarDestellos();
            fy += 44f;
            if (BotonFoco(new Rect(x + 20f, fy, ancho - 40f, 38f),
                    "VOCES: " + (VocesActivas ? "ACTIVADAS" : "APAGADAS"), 1)) AlternarVoces();
            fy += 44f;
            if (BotonFoco(new Rect(x + 20f, fy, ancho - 40f, 38f),
                    "ASISTENCIA: " + (Asistencia ? "ACTIVADA" : "APAGADA"), 2)) AlternarAsistencia();
            fy += 44f;
            if (BotonFoco(new Rect(x + 20f, fy, ancho - 40f, 38f),
                    "AIM ASISTIDO: " + (AimAmplio ? "AMPLIO 85°" : "NORMAL 65°"), 3)) AlternarAimAmplio();
            fy += 44f;
            if (BotonFoco(new Rect(x + 20f, fy, ancho - 40f, 38f),
                    "TEXTO: " + (TextoGrande ? "GRANDE" : "NORMAL"), 4)) AlternarTextoGrande();

            GUI.Label(new Rect(x + 20f, fy + 48f, ancho - 40f, 36f),
                "Asistencia: 25 % menos dano y 0,8 s de invulnerabilidad.\n"
                + "Aim amplio: el auto-aim busca en 85° en vez de 65°.",
                EstiloIzq(12, FontStyle.Normal, new Color(0.70f, 0.75f, 0.83f)));
            GUI.Label(new Rect(x + 20f, fy + 88f, ancho - 40f, 20f),
                "▲ pega fuerte   ·   ▼ pega débil   ·   ! el rival te pega fuerte",
                EstiloIzq(12, FontStyle.Bold, new Color(0.78f, 0.84f, 0.92f)));
            GUI.Label(new Rect(x, panel.yMax + 14f, ancho, 20f),
                "MANDO: A confirma · B vuelve · D-PAD/LS elige   ·   TECLADO: ENTER/ESPACIO confirma · ESC vuelve",
                EstiloIzq(11, FontStyle.Bold, new Color(0.62f, 0.68f, 0.76f)));
            GUI.Label(new Rect(x, panel.yMax + 36f, ancho, 20f),
                "ATAJOS: F2 abre Ajustes · F3 alterna confort · ESC vuelve",
                EstiloIzq(11, FontStyle.Normal, new Color(0.55f, 0.60f, 0.68f)));
        }

        /// Boton con foco de gamepad: el borde dorado y el confirmar comparten el mismo estado
        /// que las filas del menu principal.
        private bool BotonFoco(Rect r, string texto, int indice)
        {
            bool clic = Boton(r, texto);
            if (_focoMenu == indice)
            {
                HudTheme.Esquinas(r, HudTheme.Oro, 10f, 2f);
                if (_confirmarFoco) { _confirmarFoco = false; clic = true; }
            }
            return clic;
        }

        private void DibujarNiveles(float w, float h)
        {
            VeloSubpantalla(w, h);
            GUI.Label(new Rect(0, 22f, w, 40f), "RUTA DE LA CUEVA",
                Estilo(30, FontStyle.Bold, HudTheme.OroClaro));
            if (Boton(new Rect(28f, 26f, 130f, 34f), "< Menu")) _pantalla = Pantalla.Principal;

            SlugStory.Fragmento[] c = SlugStory.Campana();
            if (c == null || c.Length == 0) return;
            Rect progreso = new Rect(w - 210f, 28f, 180f, 26f);
            HudTheme.Chip(progreso);
            GUI.Label(progreso, "NIVEL " + desbloqueado + " / " + c.Length,
                Estilo(9, FontStyle.Bold, HudTheme.Cian));
            HudTheme.Linea(new Rect(48f, 76f, w - 96f, 1f),
                new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.2f));

            if (_nivelSeleccionado < 1 || _nivelSeleccionado > c.Length)
                _nivelSeleccionado = Mathf.Clamp(desbloqueado, 1, c.Length);

            // Lista de una columna con encabezados por cueva. El mapa de cuatro nodos va
            // arriba de la lista y solo mueve la seleccion: no desbloquea ningun nivel.
            float listaAncho = Mathf.Min(430f, w * 0.42f);
            Rect lista = new Rect(48f, 112f, listaAncho, h - 176f);
            DibujarMapaCuevas(new Rect(48f, 80f, listaAncho, 26f));
            _scroll = GUI.BeginScrollView(lista, _scroll,
                new Rect(0f, 0f, listaAncho - 22f,
                    CampanaMapa.AlturaContenido(c.Length) + 8f), false, false);
            for (int i = 0; i < c.Length; i++)
            {
                SlugStory.Fragmento f = c[i];
                int region = CampanaMapa.RegionDeNivel(f.nivel);
                if (f.nivel == CampanaMapa.PrimerNivel(region))
                {
                    float hy = CampanaMapa.YDeEncabezado(region);
                    Color colorRegion = Regiones.De(region).acento;
                    GUI.Label(new Rect(2f, hy, listaAncho - 92f, 16f),
                        "CUEVA " + (region + 1) + "/" + Regiones.Todas.Length + " · "
                        + Regiones.De(region).nombre,
                        EstiloIzq(9, FontStyle.Bold, colorRegion));
                    GUI.Label(new Rect(listaAncho - 92f, hy, 82f, 16f),
                        CampanaMapa.Completados(region, desbloqueado) + "/"
                        + CampanaMapa.NivelesPorRegion,
                        EstiloDerecha(9, FontStyle.Bold, colorRegion));
                    HudTheme.Linea(new Rect(2f, hy + 18f, listaAncho - 30f, 1f),
                        new Color(colorRegion.r, colorRegion.g, colorRegion.b, 0.35f));
                }

                EstadoNivel estado = EstadoDeNivel(f.nivel, desbloqueado);
                bool sel = f.nivel == _nivelSeleccionado;
                Rect r = new Rect(0f, CampanaMapa.YDeNivel(f.nivel), listaAncho - 28f, 52f);
                HudTheme.Chip(r);
                if (sel)
                    HudTheme.Esquinas(r, new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.9f), 9f, 1.5f);
                HudTheme.Linea(new Rect(r.x, r.y + 1f, 4f, r.height - 2f),
                    estado == EstadoNivel.Bloqueado ? new Color(0.3f, 0.32f, 0.36f) : f.color);

                Color colorEstado = estado == EstadoNivel.Completado ? HudTheme.Verde
                    : (estado == EstadoNivel.Disponible ? HudTheme.OroClaro : new Color(0.5f, 0.55f, 0.62f));
                string textoEstado = estado == EstadoNivel.Completado ? "COMPLETADO"
                    : (estado == EstadoNivel.Disponible ? "DISPONIBLE" : "BLOQUEADO");
                GUI.Label(new Rect(r.x + 16f, r.y + 4f, r.width - 132f, 22f),
                    f.nivel + ".  " + (estado == EstadoNivel.Bloqueado ? "???" : f.titulo),
                    EstiloIzq(16, FontStyle.Bold,
                        estado == EstadoNivel.Bloqueado ? new Color(0.42f, 0.44f, 0.48f)
                        : (sel ? HudTheme.OroClaro : HudTheme.Texto)));
                GUI.Label(new Rect(r.x + 16f, r.y + 27f, r.width - 132f, 18f),
                    estado == EstadoNivel.Bloqueado ? CondicionDeNivel(f.nivel) : f.elemento,
                    EstiloIzq(10, FontStyle.Normal, new Color(0.6f, 0.66f, 0.74f)));
                GUI.Label(new Rect(r.xMax - 122f, r.y + 15f, 108f, 20f), textoEstado,
                    EstiloDerecha(9, FontStyle.Bold, colorEstado));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _nivelSeleccionado = f.nivel;
            }
            GUI.EndScrollView();

            DibujarDetalleNivel(c, lista);
        }

        private void DibujarMapaCuevas(Rect area)
        {
            int seleccionada = CampanaMapa.RegionDeNivel(_nivelSeleccionado);
            float paso = area.width / Regiones.Todas.Length;
            HudTheme.Linea(new Rect(area.x + paso * 0.5f, area.y + area.height * 0.5f,
                area.width - paso, 1f), new Color(HudTheme.Cian.r, HudTheme.Cian.g,
                HudTheme.Cian.b, 0.25f));
            for (int r = 0; r < Regiones.Todas.Length; r++)
            {
                Rect nodo = new Rect(area.x + paso * r + 3f, area.y, paso - 6f, area.height);
                Color color = Regiones.De(r).acento;
                HudTheme.Chip(nodo);
                if (r == seleccionada)
                    HudTheme.Esquinas(nodo, new Color(HudTheme.Oro.r, HudTheme.Oro.g,
                        HudTheme.Oro.b, 0.9f), 7f, 1.5f);
                if (GUI.Button(nodo, GUIContent.none, GUIStyle.none))
                {
                    _nivelSeleccionado = CampanaMapa.NivelInicial(r, desbloqueado);
                    _scroll.y = Mathf.Max(0f,
                        CampanaMapa.YDeNivel(_nivelSeleccionado) - 60f);
                }
                GUI.Label(nodo,
                    (r + 1) + " " + MapaRadar.NombreCorto(r) + " "
                    + CampanaMapa.Completados(r, desbloqueado) + "/"
                    + CampanaMapa.NivelesPorRegion,
                    HudTheme.Estilo(8, FontStyle.Bold, TextAnchor.MiddleCenter,
                        r == seleccionada ? HudTheme.OroClaro : color));
            }
        }

        private void DibujarDetalleNivel(SlugStory.Fragmento[] c, Rect lista)
        {
            float x = lista.xMax + 20f;
            float ancho = Mathf.Max(300f, Screen.width - x - 48f);
            Rect detalle = new Rect(x, 88f, ancho, Screen.height - 152f);
            SlugStory.Fragmento f = SlugStory.DeCampana(_nivelSeleccionado);
            EstadoNivel estado = EstadoDeNivel(_nivelSeleccionado, desbloqueado);
            Color acento = estado == EstadoNivel.Bloqueado
                ? new Color(0.42f, 0.46f, 0.52f) : f.color;
            HudTheme.Panel(detalle);
            HudTheme.Esquinas(detalle, new Color(acento.r, acento.g, acento.b, 0.65f), 14f, 2f);
            HudTheme.Linea(new Rect(detalle.x, detalle.y, detalle.width, 3f), acento);

            float tx = detalle.x + 24f;
            float tw = detalle.width - 48f;
            int regionDetalle = CampanaMapa.RegionDeNivel(_nivelSeleccionado);
            GUI.Label(new Rect(tx, detalle.y + 16f, tw, 16f),
                "CUEVA " + (regionDetalle + 1) + "/" + Regiones.Todas.Length + " · "
                + Regiones.De(regionDetalle).nombre + "  ·  NIVEL " + _nivelSeleccionado,
                EstiloIzq(9, FontStyle.Bold, new Color(0.6f, 0.72f, 0.82f)));
            GUI.Label(new Rect(tx, detalle.y + 34f, tw, 32f), f.titulo,
                EstiloIzq(24, FontStyle.Bold, estado == EstadoNivel.Bloqueado
                    ? new Color(0.55f, 0.58f, 0.64f) : HudTheme.OroClaro));

            string textoEstado = estado == EstadoNivel.Completado ? "COMPLETADO"
                : (estado == EstadoNivel.Disponible ? "DISPONIBLE" : "BLOQUEADO");
            Color colorEstado = estado == EstadoNivel.Completado ? HudTheme.Verde
                : (estado == EstadoNivel.Disponible ? HudTheme.OroClaro : HudTheme.Rojo);
            Rect chipEstado = new Rect(tx, detalle.y + 72f, 150f, 22f);
            HudTheme.Chip(chipEstado);
            GUI.Label(chipEstado, textoEstado, Estilo(10, FontStyle.Bold, colorEstado));

            GUI.Label(new Rect(tx, detalle.y + 108f, tw, 16f), "OBJETIVO",
                EstiloIzq(10, FontStyle.Bold, new Color(0.55f, 0.70f, 0.80f)));
            GUI.Label(new Rect(tx, detalle.y + 126f, tw, 44f),
                NivelConfig.Campana(_nivelSeleccionado).Objetivo,
                EstiloIzq(14, FontStyle.Normal, new Color(0.86f, 0.90f, 0.94f)));
            GUI.Label(new Rect(tx, detalle.y + 178f, tw, 16f), "ELEMENTO",
                EstiloIzq(10, FontStyle.Bold, new Color(0.55f, 0.70f, 0.80f)));
            GUI.Label(new Rect(tx, detalle.y + 196f, tw, 22f), f.elemento,
                EstiloIzq(14, FontStyle.Bold, f.color));

            if (estado == EstadoNivel.Bloqueado)
            {
                GUI.Label(new Rect(tx, detalle.y + 238f, tw, 44f), CondicionDeNivel(_nivelSeleccionado),
                    EstiloIzq(12, FontStyle.Normal, new Color(0.82f, 0.84f, 0.88f)));
            }
            else
            {
                string textoBoton = estado == EstadoNivel.Completado ? "JUGAR DE NUEVO" : "JUGAR";
                if (Boton(new Rect(tx, detalle.yMax - 58f, tw, 40f), textoBoton))
                    IniciarCampana(_nivelSeleccionado);
            }
        }

        /// Las subpantallas del menú tapan un poco más la cueva para que las tarjetas se lean
        /// sobre el fondo; el menú principal conserva la caverna a la vista.
        private static void VeloSubpantalla(float w, float h)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0.006f, 0.014f, 0.026f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        // ------------------------------------------------------------------ registro

        /// Marca una babosa como descubierta en la coleccion guardada. Es idempotente:
        /// observar dos veces la misma especie no duplica la entrada.
        public static void RegistrarDescubierta(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var set = new System.Collections.Generic.HashSet<string>();
            string csv = ColeccionGuardada();
            if (!string.IsNullOrEmpty(csv))
                foreach (string s in csv.Split(','))
                    if (!string.IsNullOrEmpty(s)) set.Add(s);
            if (!set.Add(id)) return;
            var lista = new System.Collections.Generic.List<string>(set);
            PlayerPrefs.SetString(CLAVE_COLECCION, string.Join(",", lista.ToArray()));
            PlayerPrefs.Save();
        }

        public static bool EstaDescubierta(string id)
        {
            string csv = ColeccionGuardada();
            if (string.IsNullOrEmpty(csv) || string.IsNullOrEmpty(id)) return false;
            foreach (string s in csv.Split(',')) if (s == id) return true;
            return false;
        }

        /// Alta directa (no alterna): la usa el vinculo con una salvaje, que nunca debe
        /// sacar a nadie del equipo por accidente.
        public void SumarAlEquipo(string id)
        {
            if (string.IsNullOrEmpty(id) || _equipo.Contains(id)) return;
            if (_equipo.Count < TamEquipo) _equipo.Add(id);
            else _equipo[TamEquipo - 1] = id;
            GuardarEquipo();
        }

        // ------------------------------------------------------------------ equipo de 3

        private void CargarEquipo()
        {
            _equipo.Clear();
            string csv = PlayerPrefs.GetString(CLAVE_EQUIPO, "");
            if (!string.IsNullOrEmpty(csv))
            {
                string[] partes = csv.Split(',');
                for (int i = 0; i < partes.Length && _equipo.Count < TamEquipo; i++)
                    if (!string.IsNullOrEmpty(partes[i]) && !_equipo.Contains(partes[i]))
                        _equipo.Add(partes[i]);
            }
            if (_equipo.Count == 0)
            {
                string inicial = string.IsNullOrEmpty(slugElegido) ? "infierno" : slugElegido;
                _equipo.Add(inicial);
            }
            if (string.IsNullOrEmpty(_seleccion)) _seleccion = _equipo[0];
            SincronizarCarga();
        }

        private void GuardarEquipo()
        {
            PlayerPrefs.SetString(CLAVE_EQUIPO, string.Join(",", _equipo.ToArray()));
            PlayerPrefs.Save();
            SincronizarCarga();
        }

        /// El slot 0 manda: es la babosa que se carga en el blaster durante la campana.
        private void SincronizarCarga()
        {
            if (_equipo.Count == 0) return;
            slugElegido = _equipo[0];
            PlayerPrefs.SetString(CLAVE_SLUG, slugElegido);
            PlayerPrefs.Save();
        }

        public static string[] Equipo()
        {
            if (Activo == null) return new string[0];
            return Activo._equipo.ToArray();
        }

        /// Si ya esta lo saca (salvo que sea el unico del equipo); si no, entra en el primer
        /// hueco libre o reemplaza el ultimo slot. Nunca deja el equipo vacio.
        private void AlternarEquipo(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_equipo.Contains(id))
            {
                if (_equipo.Count > 1) _equipo.Remove(id);
            }
            else if (_equipo.Count < TamEquipo) _equipo.Add(id);
            else _equipo[TamEquipo - 1] = id;
            GuardarEquipo();
        }

        private static Texture2D Retrato(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Resources.Load<Texture2D>("Sprites/Protoforms/" + id)
                ?? Resources.Load<Texture2D>("Sprites/Velocimorphs/" + id);
        }

        // ------------------------------------------------------------------ registro

        /// Filtros y busqueda del equipo. No tocan `_equipo` ni `_seleccion`: solo deciden
        /// que tarjetas se dibujan. La equipada se resuelve aparte porque no es un campo
        /// de SlugData.
        private void DibujarFiltroBabosas(float x, float y, float ancho)
        {
            string[] nombres = { "TODAS", "FUEGO", "AGUA", "TIERRA", "EQUIPADAS" };
            int[] valores =
            {
                FiltroTodas, (int)ElementType.Fire, (int)ElementType.Water,
                (int)ElementType.Earth, FiltroEquipadas
            };
            float campoAncho = Mathf.Clamp(ancho * 0.27f, 140f, 220f);
            float bw = (ancho - campoAncho - 4f * 6f) / 5f;
            float bx = x;
            for (int i = 0; i < nombres.Length; i++)
            {
                if (HudTheme.BotonChico(new Rect(bx, y, bw, 24f), nombres[i]))
                    _filtroBabosas = valores[i];
                if (_filtroBabosas == valores[i])
                    HudTheme.Linea(new Rect(bx, y + 24f, bw, 2f), HudTheme.Oro);
                bx += bw + 6f;
            }

            Rect campo = new Rect(bx, y, Mathf.Max(120f, x + ancho - bx), 24f);
            if (_stBusquedaBabosas == null)
            {
                _stBusquedaBabosas = new GUIStyle(GUI.skin.textField);
                _stBusquedaBabosas.font = HudTheme.Fuente;
                _stBusquedaBabosas.fontSize = 12;
                _stBusquedaBabosas.normal.textColor = HudTheme.Texto;
                _stBusquedaBabosas.focused.textColor = HudTheme.Texto;
                _stBusquedaBabosas.normal.background = null;
                _stBusquedaBabosas.hover.background = null;
                _stBusquedaBabosas.focused.background = null;
                _stBusquedaBabosas.padding = new RectOffset(8, 8, 4, 4);
            }
            HudTheme.Chip(campo);
            _busquedaBabosas = GUI.TextField(new Rect(campo.x + 4f, campo.y + 1f, campo.width - 8f, campo.height - 2f),
                _busquedaBabosas ?? "", 40, _stBusquedaBabosas);
            if (string.IsNullOrEmpty(_busquedaBabosas))
                GUI.Label(new Rect(campo.x + 10f, campo.y, campo.width - 16f, campo.height),
                    "Buscar...", EstiloIzq(11, FontStyle.Normal, HudTheme.Tenue));
        }

        private void DibujarBabosas(float w, float h)
        {
            VeloSubpantalla(w, h);
            GUI.Label(new Rect(48f, 16f, w - 240f, 40f), "MIS BABOSAS",
                EstiloIzq(30, FontStyle.Bold, HudTheme.OroClaro));
            GUI.Label(new Rect(48f, 50f, w - 240f, 16f),
                "ELIGE TU EQUIPO · 3 CUPOS · LA PRIMERA ES LA CARGA DEL BLASTER",
                EstiloIzq(9, FontStyle.Bold, HudTheme.Cian));
            // Arriba a la derecha: abajo pisaba la fila del EQUIPO, que es lo que la consigna
            // pide justamente evitar.
            if (Boton(new Rect(w - 168f, 20f, 120f, 32f), "< Menu")) _pantalla = Pantalla.Principal;

            SlugData[] t = SlugRoster.Todos;
            if (t == null || t.Length == 0)
            {
                GUI.Label(new Rect(48f, h * 0.4f, w - 96f, 30f),
                    "Roster vacio: ejecuta SlugOut/3. Crear contenedor de babosas.",
                    EstiloIzq(18, FontStyle.Normal, Color.white));
                return;
            }

            float izq = 48f;
            // La grilla tiene que terminar antes de la ficha (x=0.60w-14); con 0.55w la
            // cuarta columna quedaba debajo del panel de detalle.
            float anchoGrid = Mathf.Min(w * 0.55f, w * 0.60f - 14f - izq - 12f);
            float altoGrid = h - 200f;
            DibujarFiltroBabosas(izq, 70f, anchoGrid);

            _babosasFiltradas.Clear();
            for (int i = 0; i < t.Length; i++)
            {
                SlugData d = t[i];
                if (d == null) continue;
                if (!CoincideElemento(d, _filtroBabosas)) continue;
                if (_filtroBabosas == FiltroEquipadas && !_equipo.Contains(d.slugId)) continue;
                if (!CoincideBusqueda(d, _busquedaBabosas)) continue;
                _babosasFiltradas.Add(d);
            }

            int cols = 4;
            float hueco = 10f;
            float contenido = anchoGrid - 18f;   // deja el carril de la barra vertical
            float tw = (contenido - (cols - 1) * hueco) / cols;
            float th = 110f;

            if (_babosasFiltradas.Count == 0)
            {
                GUI.Label(new Rect(izq, 116f, anchoGrid, 44f),
                    "Ninguna babosa coincide con el filtro o la busqueda.\n"
                    + "Prueba con TODAS o borra el texto.",
                    EstiloIzq(12, FontStyle.Normal, new Color(0.74f, 0.8f, 0.86f)));
            }
            else
            {
                int filas = Mathf.CeilToInt(_babosasFiltradas.Count / (float)cols);
                _scroll = GUI.BeginScrollView(new Rect(izq, 104f, anchoGrid, altoGrid), _scroll,
                    new Rect(0, 0, contenido, filas * (th + hueco) + 6f), false, false);

                for (int i = 0; i < _babosasFiltradas.Count; i++)
                {
                    SlugData d = _babosasFiltradas[i];
                    int col = i % cols, fil = i / cols;
                    Rect r = new Rect(col * (tw + hueco), fil * (th + hueco), tw, th);
                    bool sel = d.slugId == _seleccion;
                    bool enEquipo = _equipo.Contains(d.slugId);

                    HudTheme.Chip(r);
                    if (sel)
                        HudTheme.Esquinas(r, new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.9f), 10f, 1.5f);
                    HudTheme.Linea(new Rect(r.x, r.y, r.width, 2f),
                        new Color(d.elementColor.r, d.elementColor.g, d.elementColor.b, 0.8f));

                    Texture2D ret = Retrato(d.slugId);
                    Rect caja = new Rect(r.x + 8f, r.y + 10f, r.width - 16f, 56f);
                    if (ret != null) HudTheme.Retrato(caja, ret, d.elementColor);
                    else
                    {
                        HudTheme.Chip(caja);
                        Color pc = GUI.color;
                        GUI.color = new Color(d.elementColor.r, d.elementColor.g, d.elementColor.b, 0.25f);
                        GUI.DrawTexture(new Rect(caja.x + 3f, caja.y + 3f, caja.width - 6f, caja.height - 6f),
                            Texture2D.whiteTexture);
                        GUI.color = pc;
                        HudTheme.Esquinas(caja, new Color(d.elementColor.r, d.elementColor.g, d.elementColor.b, 0.8f), 8f, 1.5f);
                        Texture2D siluetaRoster = HudTheme.SiluetaBabosa();
                        if (siluetaRoster != null)
                        {
                            Color prevSil = GUI.color;
                            GUI.color = new Color(d.elementColor.r, d.elementColor.g, d.elementColor.b, 0.9f);
                            GUI.DrawTexture(new Rect(caja.x + 4f, caja.y + 3f, caja.width - 8f, caja.height - 6f),
                                siluetaRoster, ScaleMode.ScaleToFit, true);
                            GUI.color = prevSil;
                        }
                        else
                        {
                            GUI.Label(caja, d.slugName.Substring(0, 1),
                                Estilo(24, FontStyle.Bold, d.elementColor));
                        }
                    }

                    GUI.Label(new Rect(r.x + 8f, r.y + 70f, r.width - 16f, 20f), d.slugName,
                        EstiloIzq(13, FontStyle.Bold, HudTheme.Texto));
                    GUI.Label(new Rect(r.x + 8f, r.y + 88f, r.width - 16f, 16f),
                        SlugTexto.NombreElemento(d.element),
                        EstiloIzq(10, FontStyle.Normal, new Color(0.64f, 0.7f, 0.78f)));

                    if (enEquipo)
                    {
                        Rect eq = new Rect(r.x + r.width - 34f, r.y + 5f, 28f, 15f);
                        HudTheme.Chip(eq);
                        GUI.Label(eq, "EQ", Estilo(8, FontStyle.Bold, HudTheme.OroClaro));
                    }

                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _seleccion = d.slugId;
                }
                GUI.EndScrollView();
            }

            DibujarFicha(w, h, t);
        }

        private void DibujarFicha(float w, float h, SlugData[] roster)
        {
            SlugData d = null;
            for (int i = 0; i < roster.Length; i++)
                if (roster[i] != null && roster[i].slugId == _seleccion) { d = roster[i]; break; }
            if (d == null) d = roster[0];

            float x = w * 0.60f;
            float ancho = w * 0.36f;
            float y = 70f;

            Rect ficha = new Rect(x - 14f, y - 8f, ancho + 28f, h - 138f);
            HudTheme.Panel(ficha);
            HudTheme.Esquinas(ficha, new Color(d.elementColor.r, d.elementColor.g, d.elementColor.b, 0.5f), 12f, 1.5f);
            HudTheme.Linea(new Rect(ficha.x, ficha.y, ficha.width, 2f),
                new Color(d.elementColor.r, d.elementColor.g, d.elementColor.b, 0.85f));

            GUI.Label(new Rect(x, y, ancho, 30f), d.slugName,
                EstiloIzq(24, FontStyle.Bold, HudTheme.OroClaro));
            // La descripcion es una frase larga que ocupa dos o tres lineas: con 20 px de alto
            // se cortaba y ademas arrancaba debajo de las descendentes del titulo de 24 px.
            GUI.Label(new Rect(x, y + 34f, ancho, 46f),
                SlugTexto.NombreElemento(d.element).ToUpperInvariant() + "  ·  "
                + SlugTexto.TraducirTexto(d.description ?? ""),
                EstiloIzq(12, FontStyle.Normal, new Color(0.66f, 0.71f, 0.79f)));
            y += 86f;

            y = Bloque(x, y, ancho, "HABITAT", d.habitat);
            y = Bloque(x, y, ancho, "CONDUCTA", d.conducta);
            y = Bloque(x, y, ancho, "HABILIDAD", UsoDe(d));
            y = Bloque(x, y, ancho, "CONTRAJUEGO", ContrajuegoDe(d));
            y = Bloque(x, y, ancho, "LIMITACION", d.limitacion);

            // Barras relativas al roster: sin numeros, solo se lee la diferencia.
            float maxD = 1f, maxV = 1f, maxC = 1f;
            for (int i = 0; i < roster.Length; i++)
            {
                if (roster[i] == null) continue;
                maxD = Mathf.Max(maxD, roster[i].baseDamage);
                maxV = Mathf.Max(maxV, roster[i].launchSpeedMph);
                maxC = Mathf.Max(maxC, roster[i].clashPower);
            }
            y = Barra(x, y, ancho, "Fuerza", d.baseDamage / maxD);
            y = Barra(x, y, ancho, "Velocidad", d.launchSpeedMph / maxV);
            y = Barra(x, y, ancho, "Choque", d.clashPower / maxC);

            bool dentro = _equipo.Contains(d.slugId);
            if (Boton(new Rect(x, h - 96f, ancho, 34f),
                    dentro ? "QUITAR DEL EQUIPO" : "SUMAR AL EQUIPO  (3)"))
                AlternarEquipo(d.slugId);

            DibujarEquipo(w, h);
        }

        /// El contrarrestador sale del dato de la habilidad, no del campo `limitacion`: son dos
        /// cosas distintas y la ficha tiene que poder decir las dos.
        private static string ContrajuegoDe(SlugData d)
        {
            if (!Contrajuego.Tiene(d.abilityType))
                return "Ninguna babosa gana sola: todavia no tiene contrajuego cargado.";
            ContrajuegoDato c = Contrajuego.De(d.abilityType);
            return c.texto + "  (" + Duelo.NombreEfecto(c.contrarresta) + ")";
        }

        private static string UsoDe(SlugData d)        {
            string u = string.IsNullOrEmpty(d.uso) ? "" : d.uso;
            if (string.IsNullOrEmpty(u)) u = "Apoya al explorador en el combate.";
            return u;
        }

        private float Bloque(float x, float y, float ancho, string titulo, string texto)
        {
            GUI.Label(new Rect(x, y, ancho, 16f), titulo,
                EstiloIzq(10, FontStyle.Bold, new Color(0.55f, 0.70f, 0.80f)));
            GUI.Label(new Rect(x, y + 15f, ancho, 34f),
                string.IsNullOrEmpty(texto) ? "—" : texto,
                EstiloIzq(12, FontStyle.Normal, new Color(0.86f, 0.89f, 0.93f)));
            return y + 50f;
        }

        private float Barra(float x, float y, float ancho, string etiqueta, float t)
        {
            t = Mathf.Clamp01(t);
            GUI.Label(new Rect(x, y, 90f, 18f), etiqueta,
                EstiloIzq(10, FontStyle.Bold, new Color(0.70f, 0.78f, 0.86f)));
            HudTheme.Barra(new Rect(x + 96f, y + 5f, ancho - 96f, 9f), t, HudTheme.Cian);
            return y + 22f;
        }

        private void DibujarEquipo(float w, float h)
        {
            float y = h - 58f;
            GUI.Label(new Rect(48f, y - 2f, 90f, 20f), "EQUIPO",
                EstiloIzq(11, FontStyle.Bold, new Color(0.55f, 0.70f, 0.80f)));
            GUI.Label(new Rect(120f, y - 34f, 240f, 12f), "1 · CARGA DEL BLÁSTER",
                EstiloIzq(9, FontStyle.Bold, HudTheme.OroClaro));
            float paso = PasoSlotEquipo(w);
            float anchoSlot = AnchoSlotEquipo(w);
            for (int i = 0; i < TamEquipo; i++)
            {
                Rect slot = new Rect(120f + i * paso, y - 8f, anchoSlot, 40f);
                HudTheme.Chip(slot);
                if (i == 0)
                    HudTheme.Esquinas(slot, new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.9f), 8f, 1.5f);
                if (i < _equipo.Count)
                {
                    SlugData d = SlugRoster.Buscar(_equipo[i]);
                    string nombre = d != null ? d.slugName : _equipo[i];
                    if (d != null)
                        HudTheme.Linea(new Rect(slot.x, slot.y + 2f, 3f, slot.height - 4f), d.elementColor);
                    GUI.Label(new Rect(slot.x + 12f, slot.y, slot.width - 38f, slot.height), nombre,
                        EstiloIzq(12, FontStyle.Bold, HudTheme.Texto));
                    Rect quitar = new Rect(slot.x + slot.width - 26f, slot.y + 8f, 20f, 24f);
                    GUI.Label(quitar, "X", Estilo(12, FontStyle.Bold, HudTheme.Rojo));
                    if (GUI.Button(quitar, GUIContent.none, GUIStyle.none))
                        AlternarEquipo(_equipo[i]);
                }
                else
                {
                    GUI.Label(slot, "VACÍO", Estilo(10, FontStyle.Normal, HudTheme.Tenue));
                }
            }
        }

        /// Botón IMGUI reutilizable. Estaba privado en el menú: el HUD lo necesita para el panel
        /// de fin de partida, y duplicarlo aquí y allá se desincronizaría al primer retoque visual.
        public static bool BotonImgui(Rect r, string texto)
        {
            return HudTheme.Boton(r, texto);
        }

        private bool Boton(Rect r, string texto) { return BotonImgui(r, texto); }

        /// Uno por (tamano, estilo), no uno por boton: este factory se llamaba 3-4 veces por fila
        /// de las 28 del menu, o sea ~100 GUIStyle alojados por cuadro solo en la pantalla de menu.
        static readonly System.Collections.Generic.Dictionary<int, GUIStyle> _estilos =
            new System.Collections.Generic.Dictionary<int, GUIStyle>();

        /// Variante alineada a la izquierda y a la derecha: el menu de referencia no centra
        /// el texto, y reutilizar `Estilo` habria obligado a un segundo factory igual.
        private static GUIStyle EstiloAlineado(int tamano, FontStyle estilo, Color color, TextAnchor alineacion)
        {
            int uso = HudTheme.Escalar(tamano);
            int clave = uso * 8 + (int)estilo + (int)alineacion * 1000 + 100000;
            if (!_estilos.TryGetValue(clave, out GUIStyle s))
            {
                s = new GUIStyle(GUI.skin.label);
                s.font = HudTheme.Fuente;
                s.fontSize = uso;
                s.fontStyle = estilo;
                s.alignment = alineacion;
                s.clipping = TextClipping.Clip;
                s.richText = false;
                _estilos[clave] = s;
            }
            s.normal.textColor = color;
            return s;
        }

        private static GUIStyle EstiloIzq(int tamano, FontStyle estilo, Color color)
        {
            return EstiloAlineado(tamano, estilo, color, TextAnchor.MiddleLeft);
        }

        private static GUIStyle EstiloDerecha(int tamano, FontStyle estilo, Color color)
        {
            return EstiloAlineado(tamano, estilo, color, TextAnchor.MiddleRight);
        }

        private static GUIStyle Estilo(int tamano, FontStyle estilo, Color color)
        {
            int clave = tamano * 8 + (int)estilo;
            GUIStyle s;
            if (!_estilos.TryGetValue(clave, out s))
            {
                s = new GUIStyle(GUI.skin.label);
                s.font = HudTheme.Fuente;
                s.fontSize = tamano;
                s.fontStyle = estilo;
                s.alignment = TextAnchor.MiddleCenter;
                s.richText = false;
                _estilos[clave] = s;
            }
            s.normal.textColor = color;
            return s;
        }
    }
}
