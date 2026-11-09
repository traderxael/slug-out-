using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SlugOut.World
{
    /// Panel de la historia (IMGUI, sin prefabs ni Canvas que cablear).
    /// Muestra el fragmento ganado, y con TAB abre "Ruta de la cueva" con lo ya vivido.
    public class StoryPanel : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private int fuente = 32;
        [SerializeField] private int nivelDesbloqueado = 1;
        [SerializeField] private int vistosCount = 0;
        [SerializeField] private bool rutaAbierta = false;
        [SerializeField] private string ultimoMostrado = "-";
        [SerializeField] private bool mostrarAlIniciar = true;
        [Tooltip("Segundos reales que queda cada fragmento antes de irse solo")]
        [SerializeField] private float duracionFragmento = 7f;
        [Tooltip("Espacio tambien dispara: no cerrar el fragmento antes de este tiempo")]
        [SerializeField] private float minimoAntesDeCerrar = 0.8f;

        private readonly List<SlugStory.Fragmento> _cola = new List<SlugStory.Fragmento>();
        private readonly HashSet<int> _vistos = new HashSet<int>();
        private SlugArenaGame _arena;
        private int _olaVista;
        private bool _epilogoEnCola;
        private VozPlayer _voz;
        private string _claveVoz;
        private float _duracionActual;
        private bool _pausaPorHistoria;
        private float _cd;
        private float _cdTab;
        // tiempo visible acumulado con dt acotado: unscaledTime salta durante la carga de la escena
        // y cerraba el primer fragmento antes de que se viera
        private float _visto;
        private bool _tabAntes;

        // Ilustracion por nivel en Resources/Story/campana/NN.png y Story/infinito/NN.png.
        // Se carga de a una, al mostrar el fragmento: cargar las 36 juntas llena la memoria
        // con texturas que el jugador nunca ve. La cache guarda tambien el null para no reintentar.
        // Tope explicito de 2: sin tope, a lo largo de una campana quedaban residentes las 36
        // laminas (cada una con sus mipmaps) aunque se mostraran de a una, en una GT 640 de 2 GB.
        // Se descarta la mas antigua, que nunca es la que esta en pantalla: la que se muestra es
        // siempre la ultima cargada.
        private const int MaxLamas = 2;
        private static readonly Dictionary<string, Texture2D> _lamas = new Dictionary<string, Texture2D>();
        private static readonly List<string> _ordenLamas = new List<string>();

        private static Texture2D Ilustracion(int nivel, bool infinito)
        {
            string carpeta = infinito ? "infinito" : "campana";
            string clave = carpeta + "/" + nivel;
            Texture2D t;
            if (_lamas.TryGetValue(clave, out t)) return t;
            t = Resources.Load<Texture2D>("Story/" + clave);
            if (t == null && infinito) t = Resources.Load<Texture2D>("Story/campana/" + nivel);
            _lamas[clave] = t;
            if (t == null) { Debug.LogWarning("[StoryPanel] falta ilustracion Story/" + clave); return null; }

            _ordenLamas.Add(clave);
            while (_ordenLamas.Count > MaxLamas)
            {
                string vieja = _ordenLamas[0];
                _ordenLamas.RemoveAt(0);
                Texture2D descartada;
                if (_lamas.TryGetValue(vieja, out descartada))
                {
                    _lamas.Remove(vieja);
                    if (descartada != null) Resources.UnloadAsset(descartada);
                }
            }
            return t;
        }

        public int Vistos { get { return _vistos.Count; } }

        /// Pausa que este panel puso por la narracion (no la de la ruta ni la del menu).
        public bool PausaNarrativaActiva { get { return _pausaPorHistoria; } }
        public float DuracionFragmentoActual { get { return _duracionActual; } }

        /// Progreso de campana: `_vistos` tambien guarda claves 1000+ola y el epilogo (9000).
        public int VistosCampana
        {
            get
            {
                int n = 0;
                foreach (int clave in _vistos)
                    if (clave >= 1 && clave <= SlugStory.TotalCampana) n++;
                return n;
            }
        }

        /// Nivel que resalta la RUTA: el que se esta jugando, no un contador propio.
        public int NivelEnRuta
        {
            get
            {
                if (GameFlow.Activo != null && !GameFlow.Activo.Infinito)
                    return Mathf.Clamp(GameFlow.Activo.Nivel, 1, SlugStory.TotalCampana);
                return Mathf.Clamp(nivelDesbloqueado, 1, SlugStory.TotalCampana);
            }
        }

        /// El HUD de combate no tiene nada que decir debajo de la ruta modal.
        public static bool RutaVisible { get; private set; }
        /// Con una lamina narrativa abierta, los nameplates de enemigos quedarian cortados
        /// por el borde de la tarjeta; se ocultan solo mientras dura el fragmento.
        public static bool FragmentoVisible { get; private set; }

        // Mismo arreglo que en GameHUD: seis GUIStyle nuevos por cuadro de OnGUI.
        private GUIStyle _stTitulo, _stCuerpo, _stPie, _stPieRuta, _stH1, _stFila;

        private GUIStyle Etiqueta(ref GUIStyle campo, int tam, FontStyle estilo, bool ajuste)
        {
            if (campo == null)
            {
                campo = new GUIStyle(GUI.skin.label);
                campo.fontSize = tam;
                campo.fontStyle = estilo;
                campo.wordWrap = ajuste;
                campo.clipping = TextClipping.Clip;
            }
            if (campo.font == null && HudTheme.Fuente != null) campo.font = HudTheme.Fuente;
            return campo;
        }

        private void Awake()
        {
            _arena = GetComponent<SlugArenaGame>();
            _voz = GetComponent<VozPlayer>();
            _duracionActual = duracionFragmento;
        }

        /// Lo llama la arena al arrancar el nivel: el fragmento pertenece al nivel que se juega,
        /// no a la primera captura. En infinito no encola (las olas traen el suyo) pero conserva
        /// el epilogo si esta en pantalla, que es el puente narrativo del nivel 28.
        public void MostrarNivelActual(int nivel, bool infinito)
        {
            if (!mostrarAlIniciar) return;
            VolverAVivir();
            bool conservarEpilogo = infinito && _epilogoEnCola;
            SoltarPausaHistoria();
            if (!conservarEpilogo)
            {
                _cola.Clear();
                _epilogoEnCola = false;
                DetenerVoz();
            }
            _visto = 0f;
            _olaVista = 0;
            if (infinito) return;

            // El nivel sale del NivelConfig que se esta jugando, no de un estado global que
            // puede haberse quedado en otro nivel al entrar por CONTINUAR/EXPLORAR.
            int n = Mathf.Clamp(nivel, 1, SlugStory.TotalCampana);
            nivelDesbloqueado = n;
            // Reintentar o volver a EXPLORAR un nivel ya leido no repite el fragmento: queda en la RUTA.
            if (!_vistos.Contains(n)) Encolar(SlugStory.DeCampana(n), n);
        }

        /// Cierre de campana: se encola aparte (clave 9000) para no contarlo en el progreso de 28.
        public void MostrarEpilogo()
        {
            if (_vistos.Contains(9000)) return;
            VolverAVivir();
            _epilogoEnCola = true;
            Encolar(SlugStory.Epilogo(), 9000);
        }

        /// ArenaGame es un unico GameObject persistente y las pruebas de medicion apagan este
        /// panel para que la tarjeta no tape la toma. Si quedaba apagado, el nivel siguiente
        /// encolaba su fragmento en un Update que nunca corria y la narracion desaparecia el
        /// resto de la sesion. Pedirle al panel el fragmento de un nivel es la senal de que
        /// tiene que volver a estar vivo.
        private void VolverAVivir()
        {
            if (!enabled)
            {
                enabled = true;
                _visto = 0f;
            }
        }

        /// Nueva partida: el progreso de fragmentos es de la partida, no de la sesion de la app.
        /// ArenaGame es un unico objeto persistente (GameFlow hace DontDestroyOnLoad sobre el
        /// mismo GameObject que StoryPanel), asi que sin esto quien reinicia la aventura no
        /// vuelve a ver ni el fragmento 1.
        public void ReiniciarCampana()
        {
            OlvidarClaves(clave => clave < 1000 || clave >= 9000);
        }

        /// Lo mismo para las olas del infinito, sin tocar el progreso de campana: la RUTA lo
        /// muestra y no tiene por que borrarse por jugar una partida infinita.
        public void ReiniciarInfinito()
        {
            OlvidarClaves(clave => clave >= 1000 && clave < 9000);
        }

        private void OlvidarClaves(System.Func<int, bool> esDelModo)
        {
            List<int> quitar = new List<int>();
            foreach (int clave in _vistos) if (esDelModo(clave)) quitar.Add(clave);
            for (int i = 0; i < quitar.Count; i++) _vistos.Remove(quitar[i]);
            _cola.Clear();
            _epilogoEnCola = false;
            _visto = 0f;
            _olaVista = 0;
            DetenerVoz();
            SoltarPausaHistoria();
            FragmentoVisible = false;
        }

        /// `clave` es lo que se deduplica. En campana coincide con el nivel; en infinito las olas
        /// son 3, 10, 17... y choca de lleno con los niveles de campana, asi que se desplaza 1000
        /// (el panel no se recrea entre Reintentar y cambiar de modo, y si no un 10 visto en
        /// infinito se comeria el fragmento del nivel 10 de la campana).
        public void Encolar(SlugStory.Fragmento f, int clave)
        {
            _cola.Add(f);
            _vistos.Add(clave);
            vistosCount = _vistos.Count;
            ultimoMostrado = f.titulo;
            Debug.Log("[Historia] fragmento " + clave + " (nivel " + f.nivel + "): " + f.titulo);

            // Precarga solo la cabeza: el clip se toca al mostrarse y no queremos cargar la cola entera.
            if (_cola.Count == 1)
            {
                if (_voz == null) _voz = GetComponent<VozPlayer>();
                if (_voz != null)
                {
                    bool infinito = GameFlow.Activo != null && GameFlow.Activo.Infinito;
                    _voz.PrecargarFragmento(f, infinito);
                }
            }
        }

        /// Lo llama la arena al capturar y al ganar: en campana el fragmento ya salio al empezar
        /// el nivel, asi que solo el modo infinito encola por ola.
        public void AvanzarHistoria()
        {
            if (_arena == null) _arena = GetComponent<SlugArenaGame>();
            if (_arena == null || !_arena.Infinito) return;

            int ola = _arena.OlaActual;
            if (!SlugStory.TieneFragmentoDeOla(ola)) return;
            if (!_vistos.Contains(1000 + ola)) Encolar(SlugStory.DeOlaInfinito(ola), 1000 + ola);
        }

        /// GameFlow lo llama al empezar una partida: la ruta abierta no puede sobrevivir al menu.
        public void CerrarRuta()
        {
            rutaAbierta = false;
            // El estatico lo lee el HUD en el mismo cuadro: cerrar solo el campo dejaba el HUD
            // oculto detras de una ruta que ya no existia hasta el proximo TAB.
            RutaVisible = false;
            if (_voz != null) _voz.Pausar(false);
        }

        private static bool Tecla(System.Func<bool> nuevo, System.Func<bool> viejo)
        {
            return nuevo();
        }

        private bool Espacio()
        {
            // ESPACIO/ENTER o A del mando; flanco para que mantener no saltee varios fragmentos.
            return EntradaJuego.Leer().confirmar;
        }

        private bool Tab()
        {
            // TAB o START del mando.
            return EntradaJuego.Leer().pausa;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _cd -= dt;
            _cdTab -= dt;

            // flanco de subida: con isPressed + cooldown, mantener TAB abria y cerraba la ruta en bucle
            bool tab = Tab();
            bool tabNuevo = tab && !_tabAntes;
            _tabAntes = tab;
            bool enMenu = GameFlow.Activo != null && GameFlow.Activo.EnMenu;
            if (tabNuevo && _cdTab <= 0f && !enMenu)
            {
                _cdTab = 0.4f;
                rutaAbierta = !rutaAbierta;
                RutaVisible = rutaAbierta;
                // La ruta pausa la voz y el combate; al cerrar, la narracion sigue si la tarjeta sigue.
                if (_voz != null) _voz.Pausar(rutaAbierta);
                GameFeel.Pausa = rutaAbierta || _pausaPorHistoria;
                Debug.Log("[Historia] ruta " + (rutaAbierta ? "abierta" : "cerrada") + " | vistos=" + VistosCampana + "/" + SlugStory.TotalCampana);
            }

            FragmentoVisible = _cola.Count > 0 && !rutaAbierta;
            if (rutaAbierta) return;

            // En infinito el fragmento pertenece a la ola: se encola al cambiarla, no en la
            // primera captura (que podia llegar varias olas tarde).
            if (GameFlow.Activo != null && !GameFlow.Activo.EnMenu && _arena != null && _arena.Infinito)
            {
                int ola = _arena.OlaActual;
                if (ola != _olaVista)
                {
                    _olaVista = ola;
                    AvanzarHistoria();
                }
            }

            ActualizarVoz();

            if (_cola.Count == 0) { _visto = 0f; return; }
            _visto += Mathf.Min(dt, 0.1f);
            bool cerrar = _visto >= _duracionActual
                || (!enMenu && Espacio() && _cd <= 0f && _visto >= minimoAntesDeCerrar);
            if (cerrar)
            {
                _cd = 0.35f;
                SaltarFragmento();
            }
        }

        /// Cierra el fragmento visible. Lo usan el timer, ESPACIO y los tests de Play.
        public void SaltarFragmento()
        {
            if (_cola.Count == 0) return;

            _cola.RemoveAt(0);
            if (_cola.Count == 0)
            {
                _epilogoEnCola = false;
                SoltarPausaHistoria();
            }
            DetenerVoz();
            _visto = 0f;
        }

        /// Empieza la voz del fragmento que esta en pantalla y estira la tarjeta a su duracion.
        /// En campana, el fragmento congela la arena mientras se narra; el infinito y el epilogo
        /// suenan sobre el combate/resultado.
        private void ActualizarVoz()
        {
            if (_voz == null) _voz = GetComponent<VozPlayer>();

            if (GameFlow.Activo != null && GameFlow.Activo.EnMenu)
            {
                if (_claveVoz != null) DetenerVoz();
                return;
            }

            if (_cola.Count == 0)
            {
                if (_claveVoz != null) { DetenerVoz(); SoltarPausaHistoria(); }
                return;
            }

            SlugStory.Fragmento f = _cola[0];
            bool infinito = GameFlow.Activo != null && GameFlow.Activo.Infinito;
            string clave = CatalogoVoces.RutaDeFragmento(f, infinito)
                + (GameFlow.VocesActivas ? "|v" : "|m");
            if (clave == _claveVoz) return;

            // Sin DetenerVoz() previo: si Encolar precargo el mismo clip, PlayFragmento lo reusa
            // y el cambio de fragmento para/libera el anterior dentro de Cargar.
            _claveVoz = clave;
            float dur = _voz != null ? _voz.PlayFragmento(f, infinito) : 0f;
            _duracionActual = dur > 0f ? Mathf.Max(duracionFragmento, dur + 1f) : duracionFragmento;

            if (!infinito && dur > 0f && !SlugStory.EsEpilogo(f))
            {
                GameFeel.Pausa = true;
                _pausaPorHistoria = true;
            }
        }

        private void DetenerVoz()
        {
            if (_voz != null) _voz.Stop();
            _claveVoz = null;
            _duracionActual = duracionFragmento;
        }

        private void SoltarPausaHistoria()
        {
            if (!_pausaPorHistoria) return;
            _pausaPorHistoria = false;
            if (!rutaAbierta) GameFeel.Pausa = false;
        }

        private void OnDisable()
        {
            // Solo suelta la pausa si la habia puesto este panel: si no, al irse al menu con la
            // ruta abierta este reset descongelaba la arena y el combate segua corriendo debajo
            // del menu. GameFlow adems reafirma la pausa cada cuadro mientras est en menu.
            DetenerVoz();
            if (rutaAbierta || _pausaPorHistoria) GameFeel.Pausa = false;
            _pausaPorHistoria = false;
            rutaAbierta = false;
            RutaVisible = false;
            FragmentoVisible = false;
        }

        private void OnGUI()
        {
            if (!visible) return;
            // el fragmento cuenta lo que recien pasaste: no tiene que tapar el menu
            if (GameFlow.Activo != null && GameFlow.Activo.EnMenu) return;
            // La ruta es modal y el fragmento es una tarjeta narrativa: los dos van por encima
            // del HUD (depth negativo gana en IMGUI). El fragmento, ademas, se angosta para no
            // meterse debajo del minimapa.
            GUI.depth = rutaAbierta ? -5 : -3;
            if (rutaAbierta) { DibujarRuta(); return; }
            if (_cola.Count == 0) return;

            // banner arriba y angosto: el centro de la pantalla es donde pasa el combate
            HudTheme.Asegurar();
            SlugStory.Fragmento f = _cola[0];
            // La tarjeta tiene que entrar los textos nuevos (hasta 380 caracteres) y el pie
            // completo; con 0.6 de ancho y 168 de alto el cuerpo se cortaba en una linea.
            float w = Mathf.Min(Screen.width * 0.78f, 900f);
            float h = 210f;
            float x = (Screen.width - w) * 0.5f;
            float y = 12f;
            Rect box = new Rect(x, y, w, h);
            float resta = 1f - Mathf.Clamp01(_visto / Mathf.Max(0.001f, _duracionActual));

            HudTheme.Panel(box);
            HudTheme.Esquinas(box, new Color(f.color.r, f.color.g, f.color.b, 0.7f), 14f, 1.5f);
            HudTheme.Linea(new Rect(box.x, box.y, box.width, 3f), f.color);

            // lamina del nivel a la izquierda: el banner sigue angosto y el centro queda libre
            Texture2D lamina = Ilustracion(f.nivel, GameFlow.Activo != null && GameFlow.Activo.Infinito);
            float tx = box.x + 22f;
            float tw = box.width - 44f;
            // En pantallas angostas la lamina se saca para que el texto use todo el ancho.
            if (lamina != null && box.width >= 850f)
            {
                float th = h - 58f;
                float twn = th * lamina.width / Mathf.Max(1, lamina.height);
                Rect img = new Rect(box.x + 13f, box.y + 13f, twn, th);
                HudTheme.Chip(new Rect(img.x - 2f, img.y - 2f, img.width + 4f, img.height + 4f));
                GUI.color = Color.white;
                GUI.DrawTexture(img, lamina, ScaleMode.ScaleToFit);
                GUI.color = f.color;
                GUI.DrawTexture(new Rect(img.x, img.y, img.width, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(img.x, img.yMax - 2f, img.width, 2f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                HudTheme.Esquinas(new Rect(img.x - 3f, img.y - 3f, img.width + 6f, img.height + 6f),
                    new Color(f.color.r, f.color.g, f.color.b, 0.8f), 9f, 1.5f);
                tx = img.xMax + 18f;
                tw = box.xMax - 22f - tx;
            }

            // El tiempo restante se lee como una linea que se apaga, sin barra pesada.
            GUI.color = new Color(f.color.r, f.color.g, f.color.b, 0.22f);
            GUI.DrawTexture(new Rect(box.x + 22f, box.y + h - 34f, box.width - 44f, 2f), Texture2D.whiteTexture);
            GUI.color = new Color(f.color.r, f.color.g, f.color.b, 0.9f);
            GUI.DrawTexture(new Rect(box.x + 22f, box.y + h - 34f, (box.width - 44f) * resta, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle tit = Etiqueta(ref _stTitulo, fuente - 8, FontStyle.Bold, true);
            tit.normal.textColor = f.color;
            tit.wordWrap = true;

            GUIStyle cuerpo = Etiqueta(ref _stCuerpo, Mathf.Clamp(fuente - 16, 15, 17), FontStyle.Normal, true);
            cuerpo.normal.textColor = new Color(0.93f, 0.94f, 0.96f);
            // Explicito: si el estilo quedo cacheado de otra llamada, el texto se corta en vez de bajar.
            cuerpo.wordWrap = true;
            cuerpo.clipping = TextClipping.Clip;

            GUIStyle pie = Etiqueta(ref _stPie, 10, FontStyle.Bold, false);
            pie.normal.textColor = new Color(0.7f, 0.78f, 0.86f);
            pie.clipping = TextClipping.Clip;

            string rotulo = SlugStory.EsEpilogo(f) ? "EPÍLOGO" : "FRAGMENTO " + f.nivel;
            GUI.Label(new Rect(tx, box.y + 10f, tw, 30f), rotulo + "  ·  " + f.titulo, tit);
            GUI.Label(new Rect(tx, box.y + 44f, tw, h - 96f), f.texto, cuerpo);
            Rect pieChip = new Rect(tx, box.y + h - 30f, tw, 18f);
            HudTheme.Chip(pieChip);
            string accion = _duracionActual > duracionFragmento ? "ESPACIO SALTA" : "ESPACIO CIERRA";
            GUI.Label(pieChip,
                accion + "   ·   TAB  RUTA DE LA CUEVA   ·   " + VistosCampana + "/" + SlugStory.TotalCampana, pie);
            HudTheme.Linea(new Rect(box.x + 22f, box.y + h - 38f, 52f, 2f),
                new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.75f));
            GUI.color = Color.white;
        }

        private void DibujarRuta()
        {
            HudTheme.Asegurar();
            SlugStory.Fragmento[] c = SlugStory.Campana();
            float sw = Screen.width, sh = Screen.height;

            Color prev = GUI.color;
            GUI.color = new Color(0.006f, 0.012f, 0.022f, 0.96f);
            GUI.DrawTexture(new Rect(0f, 0f, sw, sh), Texture2D.whiteTexture);
            GUI.color = prev;
            HudTheme.Vineta();

            float w = sw * 0.9f;
            float h = sh * 0.86f;
            Rect box = new Rect((sw - w) * 0.5f, (sh - h) * 0.5f, w, h);
            HudTheme.Panel(box);
            HudTheme.Esquinas(box, new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.55f), 18f, 2f);
            HudTheme.Linea(new Rect(box.x, box.y, box.width, 3f),
                new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.9f));

            GUI.Label(new Rect(box.x + 26f, box.y + 12f, box.width - 280f, 34f), "RUTA DE LA CUEVA",
                HudTheme.Estilo(24, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.OroClaro));
            GUI.Label(new Rect(box.x + 26f, box.y + 46f, box.width - 280f, 16f),
                "EXPLORAR   ·   COMPRENDER   ·   COMBATIR",
                HudTheme.Estilo(11, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Cian));

            Rect progreso = new Rect(box.xMax - 216f, box.y + 16f, 190f, 32f);
            HudTheme.Chip(progreso);
            GUI.Label(new Rect(progreso.x + 10f, progreso.y + 3f, progreso.width - 20f, 13f),
                "FRAGMENTOS   " + VistosCampana + " / " + c.Length,
                HudTheme.Estilo(9, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.OroClaro));
            HudTheme.Barra(new Rect(progreso.x + 10f, progreso.y + 20f, progreso.width - 20f, 5f),
                c.Length > 0 ? (float)VistosCampana / c.Length : 0f, HudTheme.Cian);

            HudTheme.Linea(new Rect(box.x + 26f, box.y + 72f, box.width - 52f, 1f),
                new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.18f));

            string[] cabeceras = { "1   OBSERVAR", "2   GANAR CONFIANZA", "3   DUELO" };
            string[] subtitulos = {
                "Cada criatura deja pistas.",
                "La conexión es el primer paso.",
                "Habilidad, reflejos y tu equipo."
            };
            int columnas = 3;
            int baseN = c.Length / columnas;
            int resto = c.Length % columnas;
            int maxFilas = baseN + (resto > 0 ? 1 : 0);
            const float margen = 24f;
            const float gap = 14f;
            float colW = (box.width - 2f * margen - 2f * gap) / columnas;
            float yCab = box.y + 82f;
            float yCartas = yCab + 42f;
            float altoCarta = Mathf.Max(26f, (box.y + box.height - 34f - yCartas) / Mathf.Max(1, maxFilas));

            GUIStyle fila = Etiqueta(ref _stFila, 10, FontStyle.Normal, false);

            for (int col = 0; col < columnas; col++)
            {
                float x = box.x + margen + col * (colW + gap);
                GUI.Label(new Rect(x, yCab, colW - 20f, 18f), cabeceras[col],
                    HudTheme.Estilo(13, FontStyle.Bold, TextAnchor.UpperLeft,
                        col == 2 ? HudTheme.OroClaro : HudTheme.Cian));
                GUI.Label(new Rect(x, yCab + 18f, colW - 10f, 14f), subtitulos[col],
                    HudTheme.Estilo(9, FontStyle.Normal, TextAnchor.UpperLeft, HudTheme.Tenue));

                int inicio = col * baseN + Mathf.Min(col, resto);
                int cuantas = baseN + (col < resto ? 1 : 0);
                for (int row = 0; row < cuantas; row++)
                {
                    int idx = inicio + row;
                    if (idx >= c.Length) break;
                    bool visto = _vistos.Contains(c[idx].nivel);
                    bool actual = c[idx].nivel == NivelEnRuta;
                    Rect carta = new Rect(x, yCartas + row * altoCarta, colW, altoCarta - 3f);
                    HudTheme.Chip(carta);
                    if (visto)
                    {
                        GUI.color = c[idx].color;
                        GUI.DrawTexture(new Rect(carta.x, carta.y + 2f, 3f, carta.height - 4f), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                    if (actual)
                        HudTheme.Esquinas(carta, new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.9f), 8f, 1.5f);

                    fila.fontStyle = visto ? FontStyle.Bold : FontStyle.Normal;
                    fila.normal.textColor = visto ? c[idx].color
                        : (actual ? HudTheme.OroClaro : new Color(0.42f, 0.47f, 0.54f));
                    string txt = visto ? c[idx].nivel + ".  " + c[idx].titulo : c[idx].nivel + ".  ???";
                    GUI.Label(new Rect(carta.x + 12f, carta.y, carta.width - 18f, carta.height), txt, fila);

                    GUI.color = visto ? c[idx].color : new Color(0.28f, 0.32f, 0.38f);
                    GUI.DrawTexture(new Rect(carta.xMax - 12f, carta.y + carta.height * 0.5f - 2.5f, 5f, 5f),
                        Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }

            GUIStyle pie = Etiqueta(ref _stPieRuta, 10, FontStyle.Normal, false);
            pie.normal.textColor = new Color(0.62f, 0.69f, 0.76f);
            GUI.Label(new Rect(box.x + 26f, box.y + box.height - 27f, box.width - 260f, 18f),
                "EN PAUSA   ·   los fragmentos vistos guardan su ilustración", pie);
            Rect volver = new Rect(box.xMax - 222f, box.y + box.height - 31f, 196f, 22f);
            HudTheme.Chip(volver);
            GUI.Label(volver, "TAB   VOLVER AL COMBATE",
                HudTheme.Estilo(9, FontStyle.Bold, TextAnchor.MiddleCenter, HudTheme.OroClaro));
        }
    }
}
