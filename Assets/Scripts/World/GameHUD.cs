using UnityEngine;
using SlugOut.Core;

namespace SlugOut.World
{
    /// HUD: barra de vida del jugador, bajas, enemigos vivos y estado de la partida.
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] private Transform jugador;
        [SerializeField] private SlugArenaGame juego;
        [SerializeField] private int fuente = 30;
        [SerializeField] private bool mostrar = true;

        private SlugActor _actor;
        private PlayerAbilities _hab;
        private PlayerShooter _shooter;

        // ------------------------------------------------------------ geometria del HUD
        // Las bandas de la columna de vida y la fila de abajo se calculan aca y se dibujan con
        // estos numeros. La prueba de layout barresa resoluciones y capacidades de tubos sobre la
        // MISMA fuente que pinta, asi que "si el texto de ayuda se alarga tapa los tubos" deja de
        // ser un comentario al lado del codigo y pasa a ser una guarda que falla.
        // Cambiar un numero aca cambia lo que se dibuja y lo que prueba la guarda: no hay copia.

        public const float AnchoAyuda = 380f;
        public const float TamTubo = 20f;
        public const float SepTubo = 5f;
        public const float AltoVida = 30f;
        public const float MargenInferior = 56f;   // deja lugar a la ayuda y a los tubos de abajo
        public const float AltoPanel = 138f;

        /// El panel se apoya justo arriba de la fila de ayuda/tubos; la vida va arriba del
        /// panel (como en la referencia) y las bandas menores cuelgan debajo de ella.
        public static float YPanel(float h) { return h - 52f - AltoPanel; }
        public static float YVida(float h) { return YPanel(h) + 42f; }

        /// La barra manda el ancho del panel. Con media pantalla el bloque tapaba al heroe
        /// justo en el centro; la referencia usa una tarjeta compacta abajo a la izquierda.
        public static float AnchoBarra(float w) { return Mathf.Min(420f, w * 0.36f); }

        /// La barra de vida arranca despues del retrato cuando el retrato existe.
        public static float XColumna(float w, bool conRetrato) { return 24f + (conRetrato ? 102f : 0f); }

        public static Rect RectRetrato(float w, float h)
        { return new Rect(24f, YVida(h), 90f, 90f); }

        public static Rect RectVida(float w, float h, bool conRetrato)
        { return new Rect(XColumna(w, conRetrato), YVida(h), AnchoBarra(w), AltoVida); }

        /// La carga va ARRIBA de la vida: es un medidor de velocidad y se lee como cabecera
        /// del panel; abajo competia con la vida y el estado y empujaba el resto hacia el borde.
        public static Rect RectCarga(float w, float h, bool conRetrato)
        { Rect v = RectVida(w, h, conRetrato); return new Rect(v.x, v.y - 17f, v.width, 9f); }

        public static Rect RectMPH(float w, float h, bool conRetrato)
        { Rect c = RectCarga(w, h, conRetrato); return new Rect(c.x, c.y - 19f, c.width - 140f, 18f); }

        public static Rect RectEnergia(float w, float h, bool conRetrato)
        { Rect v = RectVida(w, h, conRetrato); return new Rect(v.x + v.width - 104f, v.y + v.height + 25f, 104f, 7f); }

        /// Titulo de la babosa equipada, a todo el ancho: con la barra compacta la energia
        /// pasa a una linea propia y el nombre deja de cortarse.
        public static Rect RectEquipada(float w, float h, bool conRetrato)
        { Rect v = RectVida(w, h, conRetrato); return new Rect(v.x + 8f, v.y + v.height + 4f, v.width - 16f, 18f); }

        /// El estado vive arriba a la derecha del panel, sobre la barra de vida, para no
        /// estirar la tarjeta hacia el centro de la pantalla.
        public static Rect RectEstado(float w, float h, bool conRetrato)
        { Rect v = RectVida(w, h, conRetrato); return new Rect(v.x + v.width - 132f, v.y - 40f, 124f, 20f); }

        public static float AnchoTubos(int cap) { return cap * (TamTubo + SepTubo) - SepTubo; }

        /// Caja de toda la fila de tubos (los cuadraditos, sin la etiqueta de arriba).
        public static Rect RectTubos(float w, float h, int cap)
        { return new Rect((w - AnchoTubos(cap)) * 0.5f, h - 26f, AnchoTubos(cap), 18f); }

        /// El cartel `TUBOS n/cap` va encima de los cuadraditos y es mas ancho que ellos.
        public static Rect RectTubosEtiqueta(float w, float h, int cap)
        { Rect t = RectTubos(w, h, cap); return new Rect(t.x - 80f, t.y - 20f, AnchoTubos(cap) + 165f, 18f); }

        public static Rect RectAyuda(float w, float h)
        { return new Rect(24f, h - 22f, AnchoAyuda, 18f); }

        public static Rect RectRodada(float w, float h)
        { return new Rect(w - 324f, h - 22f, 300f, 18f); }

        private PlayerShooter Shooter()
        {
            SlugActor a = Actor();
            if (_shooter == null && a != null) _shooter = a.GetComponent<PlayerShooter>();
            return _shooter;
        }
        /// SlugActor lo agrega SlugArenaGame solo cuando el jugador elige nivel. Cachearlo en Start()
        /// lo dejaba en null para siempre si el HUD arrancaba desde el menu, y el HUD entero se
        /// saltaba por el guard de OnGUI. Se resuelve al leerlo, como Shooter().
        private SlugActor Actor()
        {
            if (_actor == null && jugador != null) _actor = jugador.GetComponent<SlugActor>();
            return _actor;
        }

        private GUIStyle _estiloMatchup;
        private GUIStyle _estiloCombo;
        private GUIStyle _estiloFrase;
        private float _vidaRetrasada = 1f;

        // OnGUI crea diez estilos por cuadro y GUI se llama varias veces por frame: era la fuente
        // de GC mas grande del HUD. Se crean una sola vez y por frame solo se toca el color, que es
        // lo unico que cambia de verdad. Si alguien mueve `fuente` en el Inspector habria que
        // invalidar: es un valor de diseno, no de partida.
        private GUIStyle _stVida, _stSombra, _stEstadoChip, _stAyuda, _stHabilidad;
        private GUIStyle _stResultado, _stTubos, _stCarga, _stAnillo, _stRodada;
        private PlayerDodge _dodge;
        private RegionCuevaSeca _region;
        int _mVersion = -1;   // de que region son los marcadores cacheados
        private BabosaSalvaje[] _salvajes;

        private int _capturasPrev = -1;
        private float _toastHasta;
        private string _toastTexto = "";
        // Foco del resultado para gamepad: mismo patron de flancos una vez por cuadro.
        private int _focoResultado;
        private int _frameEntradaResultado = -1;
        private bool _confirmarResultado;

        /// Estado observable para las pruebas: el aviso de captura esta en pantalla.
        public bool ToastVisible { get { return Time.unscaledTime < _toastHasta; } }
        public string ToastTexto { get { return _toastTexto; } }

        /// Estado observable para las pruebas: la partida termino y se dibuja el resultado.
        public bool ResultadoVisible
        {
            get
            {
                SlugArenaGame j = juego != null ? juego : GetComponent<SlugArenaGame>();
                return j != null && j.Termino;
            }
        }

        private void Update()
        {
            SlugArenaGame j = juego != null ? juego : GetComponent<SlugArenaGame>();
            if (j == null) return;
            if (_capturasPrev < 0) { _capturasPrev = j.Capturadas; return; }
            if (j.Capturadas > _capturasPrev)
            {
                int delta = j.Capturadas - _capturasPrev;
                string nombre = string.IsNullOrEmpty(j.UltimaCaptura)
                    ? "BABOSA" : j.UltimaCaptura.ToUpperInvariant();
                _toastTexto = delta > 1 ? "¡" + delta + " BABOSAS CAPTURADAS!" : "¡" + nombre + " CAPTURADA!";
                if (j.GoalCapturas > 0) _toastTexto += "   " + j.Capturadas + "/" + j.GoalCapturas;
                _toastHasta = Time.unscaledTime + 1.6f;
                if (!GameFlow.ReducirDestellos) GameFeel.AddTrauma(0.10f);
            }
            _capturasPrev = j.Capturadas;
        }

        private GUIStyle Etiqueta(ref GUIStyle campo, int tam, FontStyle estilo, TextAnchor ancla)
        {
            int uso = HudTheme.Escalar(tam);
            if (campo == null)
            {
                campo = new GUIStyle(GUI.skin.label);
                campo.fontSize = uso;
                campo.fontStyle = estilo;
                campo.alignment = ancla;
            }
            else if (campo.fontSize != uso) campo.fontSize = uso;
            if (campo.font == null && HudTheme.Fuente != null) campo.font = HudTheme.Fuente;
            return campo;
        }

        private void Start()
        {
            if (jugador == null)
            {
                PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
                if (pc != null) jugador = pc.transform;
            }
            if (juego == null) juego = GetComponent<SlugArenaGame>();
        }

        /// Con la carta de la campana en pantalla el combate esta pausado y el HUD solo
        /// compite por la misma franja: cede. En infinito la carta suena mientras se pelea,
        /// ahi el HUD no se va.
        public static bool CartaTapaElHud(bool fragmentoVisible, bool infinito)
        {
            return fragmentoVisible && !infinito;
        }

        private void OnGUI()
        {
            if (!mostrar) return;
            // Con el menu abierto el HUD de partida no tiene nada que decir y se dibujaba
            // encima del menu nuevo: vida, tubos y matchup sobre la portada.
            if (GameFlow.Activo != null && GameFlow.Activo.EnMenu) return;
            // El torneo pausa la partida y dibuja su propia pantalla; el HUD de exploracion
            // no tiene que quedar debajo. La ruta de la cueva tambien es modal.
            if (TorneoUI.Activo) return;
            if (StoryPanel.RutaVisible) return;
            if (Actor() == null) return;
            GUI.depth = 0;   // por encima del banner de historia (depth 10)
            HudTheme.Asegurar();
            HudTheme.Vineta();

            if (CartaTapaElHud(StoryPanel.FragmentoVisible, GameFlow.Activo != null && GameFlow.Activo.Infinito))
                return;   // la vineta queda; el resto es de la carta

            DibujarCabecera();
            DibujarToast();
            if (!StoryPanel.FragmentoVisible)
            {
                DibujarMatchups();
                DibujarAnillo();
            }
            DibujarMinimapa();

            float hp = Actor().Hp;
            float max = Actor().MaxHp;
            float pct = max > 0f ? Mathf.Clamp01(hp / max) : 0f;

            float ancho = AnchoBarra(Screen.width);
            float alto = AltoVida;
            // El retrato define si la columna corre a la derecha: se resuelve una sola vez y de ahi
            // salen TODOS los rectangulos (los helpers y el dibujo comparten la decision).
            Texture2D retratoJugador = Actor().Data != null ? SlugArenaGame.LoadSprite(Actor().Data, true) : null;
            bool conRetrato = retratoJugador != null;
            float x = XColumna(Screen.width, conRetrato);
            float y = YVida(Screen.height);

            Rect fondo = new Rect(x, y, ancho, alto);

            Color prev = GUI.color;
            // Un solo panel cobija retrato, vida, cuenta y estado: sin el, cada banda flotaba
            // sobre la escena por separado y el HUD se leia como textos sueltos.
            Rect panel = new Rect(12f, YPanel(Screen.height), x + ancho + 24f - 12f, AltoPanel);
            HudTheme.Panel(panel);
            HudTheme.Esquinas(panel, new Color(0.32f, 0.84f, 1f, 0.5f), 14f, 2f);
            HudTheme.Linea(new Rect(panel.x + 8f, panel.y + 8f, 3f, 48f), HudTheme.Oro);

            Color acento = Actor().Data != null ? Actor().Data.elementColor : HudTheme.Cian;
            if (conRetrato)
                HudTheme.Retrato(RectRetrato(Screen.width, Screen.height), retratoJugador, acento);

            Color vida = pct > 0.5f ? new Color(0.32f, 0.9f, 0.52f) : (pct > 0.25f ? new Color(0.98f, 0.76f, 0.28f) : new Color(0.96f, 0.34f, 0.3f));

            // barra de dano retrasada: el tramo perdido queda blanco y se encoge despues
            if (Event.current.type == EventType.Repaint)
                _vidaRetrasada = pct > _vidaRetrasada ? pct : Mathf.MoveTowards(_vidaRetrasada, pct, Time.unscaledDeltaTime * 0.6f);
            HudTheme.Barra(fondo, pct, vida, _vidaRetrasada);
            // El destello de invulnerabilidad va ENCIMA del color: pintar la barra entera de
            // blanco hacia que vida llena y vida al 50% se vieran igual.
            if (Actor().Invulnerable && !GameFlow.ReducirDestellos
                && Mathf.Repeat(Time.unscaledTime * 12f, 1f) < 0.5f)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.28f);
                GUI.DrawTexture(new Rect(fondo.x + 2f, fondo.y + 2f, (fondo.width - 4f) * pct, fondo.height - 4f),
                    Texture2D.whiteTexture);
                GUI.color = prev;
            }

            GUIStyle st = Etiqueta(ref _stVida, Mathf.Max(16, fuente - 6), FontStyle.Bold, TextAnchor.MiddleLeft);
            st.normal.textColor = Color.white;
            GUIStyle sombra = Etiqueta(ref _stSombra, Mathf.Max(16, fuente - 6), FontStyle.Bold, TextAnchor.MiddleLeft);
            sombra.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            string textoVida = "VIDA  " + Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(max);
            GUI.Label(new Rect(x + 10f, y + 1f, ancho - 16f, alto), textoVida, sombra);
            GUI.Label(new Rect(x + 8f, y - 1f, ancho - 16f, alto), textoVida, st);

            string estado = "EN CURSO";
            if (juego != null && juego.outcome == "GANASTE") estado = "GANASTE";
            if (juego != null && juego.outcome == "PERDISTE") estado = "PERDISTE";

            Rect rectEstado = RectEstado(Screen.width, Screen.height, conRetrato);
            GUIStyle stEstado = Etiqueta(ref _stEstadoChip, Mathf.Max(12, fuente - 15), FontStyle.Bold, TextAnchor.MiddleCenter);
            stEstado.normal.textColor = estado == "GANASTE" ? HudTheme.Verde
                : (estado == "PERDISTE" ? HudTheme.Rojo : HudTheme.OroClaro);
            HudTheme.Chip(new Rect(rectEstado.x, rectEstado.y - 1f, rectEstado.width, rectEstado.height));
            GUI.Label(rectEstado, estado, stEstado);

            // El registro ya no compite abajo a la izquierda: se consulta en la coleccion.
            GUIStyle st3 = Etiqueta(ref _stAyuda, Mathf.Max(12, fuente - 16), FontStyle.Normal, TextAnchor.UpperLeft);
            st3.normal.textColor = new Color(0.76f, 0.84f, 0.9f);
            if (_hab == null && Actor() != null) _hab = Actor().GetComponent<PlayerAbilities>();
            if (_hab != null && Actor().Data != null)
            {
                float cd = _hab.CooldownRestante;
                float ePct = Mathf.Clamp01(Actor().Energia / Mathf.Max(Actor().EnergiaMax, 1f));
                GUIStyle stH = Etiqueta(ref _stHabilidad, Mathf.Max(13, fuente - 15), FontStyle.Bold, TextAnchor.UpperLeft);
                stH.normal.textColor = cd <= 0f ? HudTheme.Cian : new Color(0.62f, 0.68f, 0.74f);
                string efecto = Duelo.NombreEfecto(Actor().Data.abilityType);
                GUI.Label(RectEquipada(Screen.width, Screen.height, conRetrato),
                    "EQUIPADA  " + Actor().Data.slugName + "  ·  " + efecto
                    + (cd > 0f ? "  " + cd.ToString("F1") + "s" : ""), stH);
                Rect energia = RectEnergia(Screen.width, Screen.height, conRetrato);
                HudTheme.Barra(energia, ePct, new Color(0.28f, 0.82f, 0.98f));
            }

            PlayerShooter sh = Shooter();
            DibujarCarga(sh, conRetrato, prev);
            DibujarTubos(sh, prev);

            // La fila de abajo la comparten tres cosas: ayuda a la izquierda, los tubos centrados
            // y el chip de rodada a la derecha. Que no se toquen lo fija
            // LayoutPantallasChicasTests barriendo resoluciones y capacidades sobre los mismos
            // RectAyuda / RectTubos / RectRodada que se dibujan aca.
            Rect ayuda = RectAyuda(Screen.width, Screen.height);
            HudTheme.Chip(new Rect(ayuda.x - 4f, ayuda.y - 3f, ayuda.width + 8f, ayuda.height + 6f));
            GUI.Label(ayuda, EntradaJuego.TextoAyuda(EntradaJuego.UltimoDispositivo), st3);
            DibujarRodada(prev);

            DibujarRacha(prev);
            DibujarResultado();
        }

        /// Aviso breve de captura: el contador solo no avisa cuando la accion esta en el
        /// centro de la pantalla. Es texto + panel, sin destello; el confort solo apaga
        /// el golpe de camara.
        private void DibujarToast()
        {
            if (!ToastVisible) return;
            SlugArenaGame j = juego != null ? juego : GetComponent<SlugArenaGame>();
            if (j != null && j.Termino) return;
            float restante = _toastHasta - Time.unscaledTime;
            float alfa = Mathf.Clamp01(restante / 0.35f);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alfa);
            Rect r = new Rect(Screen.width * 0.5f - 230f, 96f, 460f, 40f);
            HudTheme.Panel(r);
            HudTheme.Esquinas(r, HudTheme.Oro, 12f, 2f);
            GUI.Label(r, _toastTexto,
                HudTheme.Estilo(16, FontStyle.Bold, TextAnchor.MiddleCenter, HudTheme.OroClaro));
            GUI.color = prev;
        }

        /// El nivel 1 es el unico que explica la regla que sostiene toda la campana: bajar la
        /// babosa del 35 % y dispararle para capturarla. Se apaga con la primera captura del
        /// nivel; los demas niveles ya la muestran en la muesca de la barra.
        public const string TextoAyudaCaptura = "BAJALA DEL 35 % Y DISPARÁ: ASÍ LA CAPTURÁS";
        public static bool MostrarAyudaCaptura(int nivel, int goal, int capturas)
        {
            return nivel == 1 && goal > 0 && capturas == 0;
        }

        /// Aviso unico cuando un Escupidor empieza a cargar: el tell del cuerpo se pierde
        /// entre luces y particulas, y el proyectil no perdona. Se muestra una vez cada 12 s.
        public const string TextoAvisoEscupidor = "ESCUPIDOR · CUBRITE O RODÁ";
        private const float AvisoEscupidorDuracion = 3.5f;
        private const float AvisoEscupidorEnfriamiento = 12f;
        private static float _avisoEscupidorHasta;
        private static float _avisoEscupidorProximo;

        public static bool AvisoEscupidorVisible
        {
            get { return Time.unscaledTime < _avisoEscupidorHasta; }
        }

        public static void MostrarAvisoEscupidor()
        {
            float ahora = Time.unscaledTime;
            if (ahora < _avisoEscupidorProximo) return;
            _avisoEscupidorHasta = ahora + AvisoEscupidorDuracion;
            _avisoEscupidorProximo = ahora + AvisoEscupidorEnfriamiento;
        }

        public static void ReiniciarAvisoEscupidor()
        {
            _avisoEscupidorHasta = 0f;
            _avisoEscupidorProximo = 0f;
        }

        /// Objetivo vivo del nivel. La cuota de capturas no cierra la partida: ademas hay que
        /// despejar la arena. Sin este aviso el HUD seguia pidiendo "CAPTURA N · N/N" y el
        /// jugador no entendia por que el nivel no terminaba. El singular es "1 BABOSA".
        public static string TextoObjetivo(int goal, int capturadas, int vivos, bool infinito, int ola)
        {
            if (infinito) return "SOBREVIVÍ   ·   OLEADA " + ola;
            if (goal <= 0) return "BAJÁ A TODOS LOS RIVALES";
            if (capturadas < goal)
            {
                string cuota = goal == 1 ? "1 BABOSA" : goal + " BABOSAS";
                return "CAPTURÁ " + cuota + "   ·   " + capturadas + "/" + goal;
            }
            if (vivos <= 0) return "OBJETIVO CUMPLIDO";
            return "DESPEJÁ LA ARENA   ·   " + vivos + (vivos == 1 ? " VIVO" : " VIVOS");
        }

        /// Cabecera de ubicacion y logo, como en la referencia de combate: arriba a la izquierda
        /// el lugar, al centro la marca. Son decorativos: ninguno tapa vida, mapa ni acciones.
        private void DibujarCabecera()
        {
            string region = RegionCuevaSeca.NombreRegionActual;
            if (string.IsNullOrEmpty(region)) region = "CUEVA SECA";
            GUIStyle tit = HudTheme.Estilo(18, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Oro);
            GUI.Label(new Rect(24f, 19f, 400f, 24f), region.ToUpperInvariant(), tit);

            string sub = "COMBATE EN TIEMPO REAL";
            if (GameFlow.Activo != null && !GameFlow.Activo.Infinito)
                sub += "   ·   NIVEL " + GameFlow.Activo.Nivel;
            GUIStyle st = HudTheme.Estilo(11, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Tenue);
            GUI.Label(new Rect(24f, 41f, 400f, 16f), sub, st);
            HudTheme.Linea(new Rect(24f, 59f, 196f, 2f), new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.55f));
            HudTheme.Linea(new Rect(224f, 59f, 40f, 2f), new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.85f));

            float w = Screen.width;
            GUIStyle logo = HudTheme.Estilo(24, FontStyle.Bold, TextAnchor.MiddleCenter, HudTheme.Oro);
            GUI.Label(new Rect(w * 0.5f - 120f, 14f, 240f, 30f), "SLUG OUT", logo);
            HudTheme.Linea(new Rect(w * 0.5f - 34f, 44f, 68f, 2f), new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.6f));

            string objetivo;
            if (juego == null) objetivo = "EXPLORÁ LA CUEVA";
            else objetivo = TextoObjetivo(juego.GoalCapturas, juego.Capturadas,
                juego.Alive, juego.Infinito, juego.OlaActual);

            Rect chipObj = new Rect(24f, 64f, 310f, 24f);
            HudTheme.Chip(chipObj);
            float dcx = chipObj.x + 14f, dcy = chipObj.y + chipObj.height * 0.5f;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(dcx, dcy));
            HudTheme.Linea(new Rect(dcx - 3.5f, dcy - 3.5f, 7f, 7f), HudTheme.Oro);
            GUI.matrix = m;
            GUI.Label(new Rect(chipObj.x + 27f, chipObj.y, chipObj.width - 34f, chipObj.height), objetivo,
                HudTheme.Estilo(11, FontStyle.Bold, TextAnchor.MiddleLeft, HudTheme.OroClaro));

            if (GameFlow.Asistencia)
            {
                GUI.Label(new Rect(chipObj.x + chipObj.width + 10f, chipObj.y, 120f, chipObj.height),
                    "ASISTIDO", HudTheme.Estilo(10, FontStyle.Bold, TextAnchor.MiddleLeft,
                        new Color(0.55f, 0.92f, 0.70f)));
            }

            bool ayudaCaptura = juego != null && MostrarAyudaCaptura(
                    GameFlow.Activo != null ? GameFlow.Activo.Nivel : 0,
                    juego.GoalCapturas, juego.Capturas);
            if (ayudaCaptura)
            {
                // Chip oscuro como el objetivo: sobre la cueva el texto suelto se perdia.
                Rect chipAyuda = new Rect(24f, 90f, 340f, 22f);
                HudTheme.Chip(chipAyuda);
                GUI.Label(new Rect(chipAyuda.x + 10f, chipAyuda.y, chipAyuda.width - 14f, chipAyuda.height),
                    TextoAyudaCaptura,
                    HudTheme.Estilo(11, FontStyle.Bold, TextAnchor.MiddleLeft, HudTheme.OroClaro));
            }
            if (AvisoEscupidorVisible)
            {
                Rect chipAviso = new Rect(24f, ayudaCaptura ? 116f : 90f, 340f, 22f);
                HudTheme.Chip(chipAviso);
                GUI.Label(new Rect(chipAviso.x + 10f, chipAviso.y,
                        chipAviso.width - 14f, chipAviso.height),
                    TextoAvisoEscupidor,
                    HudTheme.Estilo(11, FontStyle.Bold, TextAnchor.MiddleLeft, HudTheme.OroClaro));
            }
        }

        /// Estado de la rodada en la misma fila de ayuda, a la derecha del texto de teclas y lejos
        /// del minimapa (que cuelga arriba a la derecha). El rombo de la referencia no existe en
        /// Bahnschrift; el chevron » cumple la misma funcion y no deja un cuadradito vacio.
        private void DibujarRodada(Color prev)
        {
            if (Actor() == null) return;
            if (_dodge == null) _dodge = Actor().GetComponent<PlayerDodge>();
            if (_dodge == null) return;

            string texto;
            Color color;
            if (_dodge.Rodando)
            {
                texto = "» RODANDO";
                color = new Color(0.96f, 0.98f, 1f);
            }
            else if (_dodge.EnfriamientoRestante > 0f)
            {
                texto = "» RODADA " + _dodge.EnfriamientoRestante.ToString("F1") + "s";
                color = new Color(0.64f, 0.7f, 0.77f);
            }
            else
            {
                texto = "» SHIFT RODADA LISTA";
                color = HudTheme.Verde;
            }

            Rect rr = RectRodada(Screen.width, Screen.height);
            HudTheme.Chip(new Rect(rr.x - 8f, rr.y - 3f, rr.width + 8f, rr.height + 6f));
            GUIStyle st = Etiqueta(ref _stRodada, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            st.normal.textColor = color;
            GUI.Label(rr, texto, st);
            GUI.color = prev;
        }

        /// La racha solo ocupa pantalla cuando existe: con combo 0 o 1 no se dibuja nada, para que
        /// el jugador lea "voy en racha" sin tener que escanear un cero fijo.
        private void DibujarRacha(Color prev)
        {
            SlugActor a = Actor();
            if (a == null || a.Combo < 2) return;
            if (_estiloCombo == null)
            {
                _estiloCombo = new GUIStyle(GUI.skin.label);
                _estiloCombo.alignment = TextAnchor.MiddleRight;
                _estiloCombo.fontStyle = FontStyle.Bold;
                _estiloCombo.font = HudTheme.Fuente;
            }
            float restante = a.VentanaCombo * a.ComboRestante;
            _estiloCombo.fontSize = Mathf.Max(17, fuente - 5);
            _estiloCombo.normal.textColor = new Color(1f, 0.84f, 0.42f, 0.55f + 0.45f * a.ComboRestante);
            float bx = Screen.width - 250f, by = Screen.height - 150f;
            GUI.Label(new Rect(bx, by, 234f, 26f), a.Combo + "x  ·  " + restante.ToString("F1") + "s", _estiloCombo);
            HudTheme.Barra(new Rect(bx, by + 28f, 234f, 4f), a.ComboRestante, HudTheme.Oro);
            GUI.color = prev;
        }

        /// Sin esto la partida termina en un texto: Reintentar() y SiguienteNivel() no tenían
        /// ni un llamador y había que reiniciar la escena para volver a jugar.
        private void DibujarResultado()
        {
            SlugArenaGame j = juego != null ? juego : GetComponent<SlugArenaGame>();
            if (j == null || !j.Termino) return;

            Color prev = GUI.color;
            // Velo: el resultado tiene que leerse, no competir con el HUD de combate.
            GUI.color = new Color(0f, 0f, 0f, 0.58f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            bool gano = j.outcome == "GANASTE";
            float w = Mathf.Min(560f, Screen.width * 0.72f);
            float hgt = 196f;
            float x = (Screen.width - w) * 0.5f;
            float y = Mathf.Max(70f, Screen.height * 0.22f);
            Rect caja = new Rect(x, y, w, hgt);

            Color acento = gano ? HudTheme.Verde : HudTheme.Rojo;
            HudTheme.Panel(caja);
            HudTheme.Esquinas(caja, acento, 16f, 2f);
            HudTheme.Linea(new Rect(caja.x, caja.y, caja.width, 3f), acento);

            GUIStyle st = Etiqueta(ref _stResultado, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            st.normal.textColor = gano ? new Color(0.55f, 0.95f, 0.62f) : new Color(0.98f, 0.6f, 0.6f);
            GUI.Label(new Rect(x, y + 10f, w, 30f), j.outcome, st);

            string info = j.Infinito
                ? "OLEADA " + j.OlaActual + "   ·   CAPTURAS " + j.Capturadas
                : (j.GoalCapturas > 0
                    ? "OBJETIVO  CAPTURAR " + j.GoalCapturas + "   ·   " + j.Capturadas + "/" + j.GoalCapturas
                    : "CAPTURAS  " + j.Capturadas);
            GUI.Label(new Rect(x, y + 42f, w, 20f), info,
                HudTheme.Estilo(15, FontStyle.Bold, TextAnchor.MiddleCenter, HudTheme.Texto));

            if (_estiloCombo == null)
            {
                _estiloCombo = new GUIStyle(GUI.skin.label);
                _estiloCombo.fontStyle = FontStyle.Bold;
                _estiloCombo.font = HudTheme.Fuente;
                _estiloCombo.fontSize = 15;
            }
            _estiloCombo.alignment = TextAnchor.MiddleCenter;
            SlugActor actorResultado = Actor();
            if (actorResultado != null)
            {
                _estiloCombo.normal.textColor = new Color(1f, 0.84f, 0.42f);
                GUI.Label(new Rect(x, y + 62f, w, 20f),
                    "RACHA MAXIMA  " + actorResultado.ComboMaximo + "x", _estiloCombo);
            }

            GameFlow gf = GameFlow.Activo;
            if (gf == null) return;
            float bw = (w - 40f) * 0.5f, bh = 36f;
            float bx = x + 16f, by = y + 88f;
            bool puedeSiguiente = gano && !gf.Infinito && gf.Nivel < SlugStory.TotalCampana;
            // El 28 no tiene siguiente: el hueco que quedaba se usa para el puente al infinito.
            bool finalCampana = GameFlow.EsFinalDeCampana(gano, gf.Infinito, gf.Nivel);
            int filas = 3 + ((puedeSiguiente || finalCampana) ? 1 : 0);

            // La tarjeta de historia se come el A/ESPACIO para cerrarse; el resultado no debe
            // confirmar su boton en el mismo flanco.
            bool historiaTapa = StoryPanel.RutaVisible || StoryPanel.FragmentoVisible;
            EntradaFrame en = EntradaJuego.Leer();
            if (!historiaTapa && _frameEntradaResultado != Time.frameCount)
            {
                _frameEntradaResultado = Time.frameCount;
                _confirmarResultado = en.confirmar;
                if (en.menuArriba || en.menuIzquierda)
                    _focoResultado = NavegacionFoco.Mover(_focoResultado, -1, filas);
                if (en.menuAbajo || en.menuDerecha)
                    _focoResultado = NavegacionFoco.Mover(_focoResultado, 1, filas);
                _focoResultado = Mathf.Clamp(_focoResultado, 0, filas - 1);
            }

            if (BotonResultado(new Rect(bx, by, bw, bh), "REINTENTAR", 0)) gf.Reintentar();
            if (BotonResultado(new Rect(bx + bw + 8f, by, bw, bh), "MENU", 1)) gf.VolverAlMenu();

            float by2 = by + 42f;
            if (BotonResultado(new Rect(bx, by2, bw, bh), "REVISAR EQUIPO", 2)) CollectionUI.Abrir();
            if (puedeSiguiente && BotonResultado(new Rect(bx + bw + 8f, by2, bw, bh), "SIGUIENTE NIVEL", 3))
                gf.SiguienteNivel();
            if (finalCampana && BotonResultado(new Rect(bx + bw + 8f, by2, bw, bh), "MODO INFINITO", 3))
                gf.IniciarInfinito();
        }

        private bool BotonResultado(Rect r, string texto, int indice)
        {
            bool clic = GameFlow.BotonImgui(r, texto);
            if (_focoResultado == indice)
            {
                HudTheme.Esquinas(r, HudTheme.Oro, 8f, 2f);
                if (_confirmarResultado) { _confirmarResultado = false; clic = true; }
            }
            return clic;
        }

        /// Minimapa fijo de la arena: habitats, coberturas, bocas con destino y jugador. Al no
        /// recentrarse en el jugador, las tres salidas y las paredes se ven siempre; el rumbo
        /// sigue en la flecha. Panel arriba a la derecha, fuera de la vida y las acciones.
        private void DibujarMinimapa()
        {
            CacheMapa();
            if (_region == null || _region.Root == null) return;
            Transform p = jugador != null ? jugador : (Actor() != null ? Actor().transform : null);
            if (p == null) return;

            float lado = 176f;
            float x = Screen.width - lado - 24f;
            GUIStyle titulo = HudTheme.Estilo(9, FontStyle.Bold, TextAnchor.MiddleCenter, HudTheme.Oro);
            GUI.Label(new Rect(x, 18f, lado, 16f), RegionCuevaSeca.NombreRegionActual, titulo);

            float diam = 156f;
            Rect disco = new Rect(x + (lado - diam) * 0.5f, 38f, diam, diam);
            HudTheme.Circulo(disco);
            HudTheme.Esquinas(new Rect(disco.x - 3f, disco.y - 3f, disco.width + 6f, disco.height + 6f),
                new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.35f), 11f, 1.5f);

            float cx = disco.center.x;
            float cy = disco.center.y;
            float radioMapa = 67f;
            GUIStyle mini = HudTheme.Estilo(10, FontStyle.Bold, TextAnchor.MiddleCenter, HudTheme.Oro);
            GUI.Label(new Rect(cx - 10f, disco.y + 7f, 20f, 14f), "N", mini);

            // Anillo de paredes: el disco ya trae anillos, pero este marca el limite jugable.
            Color prev = GUI.color;
            GUI.color = new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.45f);
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                float px = cx + Mathf.Cos(a) * (radioMapa - 6f);
                float py = cy + Mathf.Sin(a) * (radioMapa - 6f);
                GUI.DrawTexture(new Rect(px - 1f, py - 1f, 2f, 2f), Texture2D.whiteTexture);
            }
            GUI.color = prev;

            Transform[] marcadores = _region.MarcadoresRaiz;
            string[] letras = _region.MarcadoresLetra;
            Color[] colores = _region.MarcadoresColor;
            if (marcadores != null)
            {
                for (int i = 0; i < marcadores.Length; i++)
                    MarcarMapa(marcadores[i], letras[i], colores[i], cx, cy, radioMapa);
            }

            RegionDefinicion def = Regiones.De(RegionCuevaSeca.Actual);
            CoberturaDef[] coberturas = def.coberturas;
            if (coberturas != null)
            {
                for (int i = 0; i < coberturas.Length; i++)
                {
                    Vector2 q = MapaRadar.CoberturaADisco(
                        coberturas[i].angulo + def.anguloBase, coberturas[i].radio,
                        cx, cy, radioMapa);
                    GUI.color = new Color(0f, 0f, 0f, 0.55f);
                    GUI.DrawTexture(new Rect(q.x - 4f, q.y - 4f, 8f, 8f), Texture2D.whiteTexture);
                    GUI.color = new Color(0.62f, 0.70f, 0.78f, 0.9f);
                    GUI.DrawTexture(new Rect(q.x - 3f, q.y - 3f, 6f, 6f), Texture2D.whiteTexture);
                }
                GUI.color = prev;
            }

            Transform[] bocas = _region.TunelesRaiz;
            int[] destinos = _region.DestinosTuneles;
            if (bocas != null)
            {
                for (int i = 0; i < bocas.Length; i++)
                {
                    int destino = destinos != null && i < destinos.Length ? destinos[i] : -1;
                    MarcarBoca(bocas[i], destino, cx, cy, radioMapa);
                }
            }

            if (_salvajes != null)
            {
                for (int i = 0; i < _salvajes.Length; i++)
                {
                    if (_salvajes[i] == null) continue;
                    PuntoMapa(_salvajes[i].transform.position, new Color(0.4f, 0.95f, 0.95f),
                        cx, cy, radioMapa, 4f);
                }
            }

            System.Collections.Generic.IReadOnlyList<SlugActor> vivos = SlugActor.Vivos;
            for (int i = 0; i < vivos.Count; i++)
            {
                SlugActor a = vivos[i];
                if (a == null || a.IsPlayer || !a.Alive || a.Capturada) continue;
                PuntoMapa(a.transform.position, new Color(0.95f, 0.35f, 0.3f),
                    cx, cy, radioMapa, 4f);
            }

            Vector2 jugadorMapa = MapaRadar.MundoADisco(p.position, cx, cy, radioMapa);
            float angulo = Mathf.Atan2(p.forward.x, p.forward.z) * Mathf.Rad2Deg;
            HudTheme.Flecha(new Rect(jugadorMapa.x - 9f, jugadorMapa.y - 13f, 18f, 26f), angulo);

            HudTheme.Chip(new Rect(x, disco.yMax + 2f, lado, 38f));
            GUIStyle leyenda = HudTheme.Estilo(8, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.66f, 0.74f, 0.82f));
            string textoHabitats = "";
            if (marcadores != null)
            {
                for (int i = 0; i < marcadores.Length; i++)
                {
                    if (i > 0) textoHabitats += "  ";
                    textoHabitats += letras[i] + " " + _region.MarcadoresNombre[i];
                }
            }
            GUI.Label(new Rect(x, disco.yMax + 4f, lado, 11f), textoHabitats, leyenda);

            string textoTuneles = "T → ";
            if (destinos != null)
            {
                for (int i = 0; i < destinos.Length; i++)
                {
                    if (destinos[i] < 0) continue;
                    if (textoTuneles.Length > 4) textoTuneles += "  ";
                    textoTuneles += MapaRadar.CodigoRegion(destinos[i]) + " "
                        + MapaRadar.NombreCorto(destinos[i]);
                }
            }
            GUI.Label(new Rect(x, disco.yMax + 16f, lado, 11f), textoTuneles, leyenda);
            GUI.Label(new Rect(x, disco.yMax + 28f, lado, 11f), "[] cobertura · T tunel", leyenda);

            string aviso = TextoTunelCercano(p.position);
            if (!string.IsNullOrEmpty(aviso))
            {
                Rect chip = new Rect(x, disco.yMax + 42f, lado, 20f);
                HudTheme.Chip(chip);
                GUI.Label(new Rect(chip.x + 6f, chip.y, chip.width - 10f, chip.height), aviso,
                    HudTheme.Estilo(8, FontStyle.Bold, TextAnchor.MiddleLeft, HudTheme.OroClaro));
            }
        }

        private string TextoTunelCercano(Vector3 posicion)
        {
            Transform[] bocas = _region != null ? _region.TunelesRaiz : null;
            int[] destinos = _region != null ? _region.DestinosTuneles : null;
            if (bocas == null || destinos == null) return null;
            float mejor = 4.5f * 4.5f;
            int elegido = -1;
            for (int i = 0; i < bocas.Length && i < destinos.Length; i++)
            {
                if (bocas[i] == null || destinos[i] < 0) continue;
                Vector3 d = bocas[i].position - posicion;
                d.y = 0f;
                if (d.sqrMagnitude < mejor) { mejor = d.sqrMagnitude; elegido = destinos[i]; }
            }
            if (elegido < 0) return null;
            return "TÚNEL → " + Regiones.De(elegido).nombre + " · CRUZÁ";
        }

        private void CacheMapa()
        {
            if (_region == null) _region = Object.FindAnyObjectByType<RegionCuevaSeca>();
            if (_region == null || _region.Root == null) return;
            // Al cruzar un tunel la region se reconstruye: los Transform viejos quedan destruidos
            // y hay que volver a tomar los nuevos. `Version` sube justo en ese momento.
            if (_mVersion != RegionCuevaSeca.Version)
            {
                _mVersion = RegionCuevaSeca.Version;
                _salvajes = null;
            }
            if (_salvajes == null) _salvajes = Object.FindObjectsByType<BabosaSalvaje>();
        }

        private void MarcarMapa(Transform t, string letra, Color color, float cx, float cy,
                                float radio)
        {
            if (t == null) return;
            Vector2 q = MapaRadar.MundoADisco(t.position, cx, cy, radio);
            Rect r = new Rect(q.x - 7f, q.y - 7f, 14f, 14f);
            Color prev = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, 0.2f);
            GUI.DrawTexture(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
            GUIStyle letraEstilo = HudTheme.Estilo(8, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.03f, 0.05f, 0.07f));
            GUI.Label(r, letra, letraEstilo);
        }

        private void MarcarBoca(Transform t, int destino, float cx, float cy, float radio)
        {
            if (t == null) return;
            Vector2 q = MapaRadar.MundoADisco(t.position, cx, cy, radio);
            Color prev = GUI.color;
            Color color = new Color(1f, 0.78f, 0.35f);
            GUI.color = new Color(color.r, color.g, color.b, 0.2f);
            GUI.DrawTexture(new Rect(q.x - 8f, q.y - 8f, 16f, 16f), Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(q.x - 5f, q.y - 5f, 10f, 10f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(q.x - 4f, q.y - 4f, 8f, 8f), Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(new Rect(q.x + 6f, q.y - 7f, 28f, 12f), MapaRadar.CodigoRegion(destino),
                HudTheme.Estilo(8, FontStyle.Bold, TextAnchor.MiddleLeft, color));
        }

        private void PuntoMapa(Vector3 mundo, Color color, float cx, float cy, float radio,
                               float tam)
        {
            Vector2 q = MapaRadar.MundoADisco(mundo, cx, cy, radio);
            Color prev = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, 0.18f);
            GUI.DrawTexture(new Rect(q.x - tam * 0.5f - 3f, q.y - tam * 0.5f - 3f, tam + 6f, tam + 6f),
                Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(q.x - tam * 0.5f, q.y - tam * 0.5f, tam, tam),
                Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// Los tubos del blaster, abajo al centro: son el ritmo del combate.
        private void DibujarTubos(PlayerShooter s, Color prev)
        {
            if (s == null) return;
            int cap = s.TubosCapacidad;
            float tam = TamTubo, sep = SepTubo;
            Rect fila = RectTubos(Screen.width, Screen.height, cap);
            float x0 = fila.x, y0 = fila.y;
            HudTheme.Linea(new Rect(fila.x - 9f, fila.y - 3f, 2f, fila.height + 6f),
                new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.5f));
            HudTheme.Linea(new Rect(fila.xMax + 7f, fila.y - 3f, 2f, fila.height + 6f),
                new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.5f));
            for (int i = 0; i < cap; i++)
            {
                Rect r = new Rect(x0 + i * (tam + sep), y0, tam, 18f);
                bool lleno = i < s.Tubos;
                float recarga = !lleno && i == s.Tubos ? s.RecargaProgreso : 0f;
                HudTheme.SlotTubo(r, lleno, recarga, new Color(0.36f, 0.9f, 1f));
            }
            GUI.color = prev;
            GUIStyle stT = Etiqueta(ref _stTubos, 13, FontStyle.Bold, TextAnchor.UpperCenter);
            stT.normal.textColor = s.SinTubos ? HudTheme.Rojo : HudTheme.OroClaro;
            GUI.Label(RectTubosEtiqueta(Screen.width, Screen.height, cap),
                s.SinTubos ? "SIN TUBOS - recargando" : "TUBOS " + s.Tubos + "/" + cap, stT);
        }

        /// Barra de carga con la muesca del umbral: sin marca no hay forma de saber
        /// cuando el tiro va a transformarse.
        private void DibujarCarga(PlayerShooter s, bool conRetrato, Color prev)
        {
            if (s == null) return;
            float carga = s.CargaProgreso;
            float muesca = s.MuescaActual();
            Rect barra = RectCarga(Screen.width, Screen.height, conRetrato);

            HudTheme.Barra(barra, carga, carga >= muesca ? HudTheme.Oro : new Color(0.42f, 0.68f, 0.8f));
            HudTheme.Linea(new Rect(barra.x + barra.width * muesca - 1.5f, barra.y - 4f, 3f, barra.height + 8f),
                HudTheme.OroClaro);
            GUI.color = prev;

            GUIStyle stV = Etiqueta(ref _stCarga, Mathf.Max(11, fuente - 15), FontStyle.Bold, TextAnchor.UpperLeft);
            stV.normal.textColor = new Color(0.86f, 0.93f, 0.98f);
            GUI.Label(RectMPH(Screen.width, Screen.height, conRetrato),
                Mathf.RoundToInt(s.VelocidadActual * 2.23694f) + " MPH   ·   umbral " + Mathf.RoundToInt(s.UmbralMPH * 2.23694f) + " MPH", stV);
        }

        /// Sobre cada enemigo: cuanto le pega el jugador (tabla elemental) y un ! rojo si el
        /// enemigo le pega fuerte al jugador. El objetivo del auto-aim va resaltado.
        private void DibujarMatchups()
        {
            Camera cam = Camera.main;
            if (cam == null || !Actor().Alive) return;
            PlayerShooter sh = Shooter();
            // El corchete sigue al objetivo real del disparo: mientras se carga es el fijado, si no
            // el auto-aim frontal. Asi el jugador ve a quien le va a pegar antes de soltar.
            SlugActor objetivo = sh == null ? null
                : (sh.Apuntando && sh.ObjetivoActual != null ? sh.ObjetivoActual : sh.NearestEnemy());
            if (_estiloMatchup == null)
            {
                _estiloMatchup = new GUIStyle(GUI.skin.label);
                _estiloMatchup.fontStyle = FontStyle.Bold;
                _estiloMatchup.alignment = TextAnchor.MiddleCenter;
                _estiloMatchup.richText = true;
                _estiloMatchup.font = HudTheme.Fuente;
            }

            System.Collections.Generic.IReadOnlyList<SlugActor> vivos = SlugActor.Vivos;
            for (int vi = 0, vn = vivos.Count; vi < vn; vi++)
            {
                SlugActor a = vivos[vi];
                if (a == null || a.IsPlayer || !a.Alive || a.Capturada) continue;
                Vector3 sp = cam.WorldToScreenPoint(a.transform.position + Vector3.up * 2.1f);
                if (sp.z <= 0f) continue;

                float ataque = ElementalChart.GetMultiplier(Actor().Element, a.Element, SlugActor.AnilloInvertido);
                float defensa = ElementalChart.GetMultiplier(a.Element, Actor().Element, SlugActor.AnilloInvertido);
                string color = ataque > 1f ? "#5CFF7A" : (ataque < 1f ? "#9AA4B0" : "#FFFFFF");
                string signo = ataque > 1f ? "▲" : (ataque < 1f ? "▼" : "");
                // Los duelistas del mazo conservan su SlugData (pelean montados), asi que el
                // nameplate diria el nombre de la babosa: la marca runtime devuelve el nombre propio.
                DuelistaMarca marca = a.GetComponent<DuelistaMarca>();
                string nombre = marca != null ? marca.Nombre
                    : (a.Data != null ? a.Data.slugName.ToUpperInvariant() : "RIVAL");
                string txt = "<color=#DCEBFF>" + nombre + "</color>  <color=" + color + ">"
                    + signo + "x" + ataque.ToString("0.##") + "</color>";
                if (defensa > 1f) txt += " <color=#FF5C5C>!</color>";
                bool esObjetivo = a == objetivo;
                if (esObjetivo) txt = "<color=#FFD54A>[</color> " + txt + " <color=#FFD54A>]</color>";

                _estiloMatchup.fontSize = esObjetivo ? 20 : 15;
                float y = Screen.height - sp.y;
                Rect r = new Rect(sp.x - 115f, y - 24f, 230f, 42f);
                Rect caja = new Rect(r.x + 16f, r.y, r.width - 32f, 36f);
                Color prev = GUI.color;
                GUI.color = esObjetivo ? new Color(1f, 0.92f, 0.75f) : Color.white;
                HudTheme.Chip(caja);
                if (esObjetivo)
                    HudTheme.Esquinas(new Rect(caja.x - 4f, caja.y - 4f, caja.width + 8f, caja.height + 8f),
                        new Color(1f, 0.78f, 0.34f, 0.9f), 8f, 1.5f);
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
                GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, 24f), StripColor(txt), _estiloMatchup);
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x, r.y, r.width, 24f), txt, _estiloMatchup);
                GUI.color = prev;

                float hpPct = a.MaxHp > 0f ? Mathf.Clamp01(a.Hp / a.MaxHp) : 0f;
                Rect barraVida = new Rect(r.center.x - 70f, r.y + 27f, 140f, 5f);
                Color colorElemento = a.Data != null ? a.Data.elementColor : new Color(0.95f, 0.4f, 0.35f);
                // La cuota del nivel es capturar: la linea del umbral es la informacion que
                // decide si el tiro suma a la coleccion o solo da de baja. Antes no estaba en
                // pantalla y el jugador tenia que adivinarla.
                float umbral = Mathf.Clamp01(a.UmbralCaptura);
                Color colorVida = hpPct <= umbral
                    ? Color.Lerp(HudTheme.Oro, colorElemento, 0.25f)
                    : Color.Lerp(new Color(0.92f, 0.26f, 0.26f), colorElemento, 0.35f);
                HudTheme.Barra(barraVida, hpPct, colorVida);
                HudTheme.Linea(new Rect(barraVida.x + barraVida.width * umbral - 1f,
                    barraVida.y - 2f, 2f, barraVida.height + 4f),
                    new Color(1f, 0.86f, 0.45f, a.ListaParaCapturar ? 1f : 0.75f));

                if (a.ListaParaCapturar)
                {
                    Color prevC = GUI.color;
                    GUI.color = new Color(1f, 0.86f, 0.45f);
                    _estiloMatchup.fontSize = 11;
                    GUI.Label(new Rect(r.center.x - 70f, barraVida.y + 7f, 140f, 16f), "CAPTURA",
                        _estiloMatchup);
                    GUI.color = prevC;
                }

                // Frase de presentacion: sale al aparecer el duelista y se apaga sola. Nunca se
                // dibuja junto al fragmento de historia porque DibujarMatchups no corre con el.
                if (marca != null && !string.IsNullOrEmpty(marca.Frase))
                {
                    float edad = Time.time - marca.Nace;
                    // La frase queda en pantalla lo que dura su voz (mínimo 4 s si no hay audio).
                    float duracionFrase = marca.DuracionFrase > 0f
                        ? Mathf.Max(4f, marca.DuracionFrase + 0.5f) : 4f;
                    if (edad >= 0f && edad < duracionFrase)
                    {
                        if (_estiloFrase == null)
                        {
                            _estiloFrase = new GUIStyle(GUI.skin.label);
                            _estiloFrase.fontStyle = FontStyle.Bold;
                            _estiloFrase.alignment = TextAnchor.MiddleCenter;
                            _estiloFrase.wordWrap = true;
                            _estiloFrase.font = HudTheme.Fuente;
                        }
                        _estiloFrase.fontSize = 12;
                        _estiloFrase.normal.textColor = new Color(1f, 0.9f, 0.7f);
                        float alpha = Mathf.Clamp01((duracionFrase - edad) / 0.6f);
                        Rect rf = new Rect(r.x - 20f, r.y + 36f, r.width + 40f, 30f);
                        Color prevFrase = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, alpha);
                        HudTheme.Chip(rf);
                        GUI.color = new Color(0f, 0f, 0f, 0.85f * alpha);
                        GUI.Label(new Rect(rf.x + 1.5f, rf.y + 1.5f, rf.width, rf.height), marca.Frase, _estiloFrase);
                        GUI.color = new Color(1f, 1f, 1f, alpha);
                        GUI.Label(rf, marca.Frase, _estiloFrase);
                        GUI.color = prevFrase;
                    }
                }
            }
        }

        /// Cuando el anillo esta al reves, las flechas ▲▼ del matchup significan lo contrario
        /// y nada en pantalla lo dice. Este cartel es la unica forma que tiene el jugador de
        /// saber que sus veinte niveles de costumbre no valen nada aca.
        /// True solo si el camino de dibujo se ejecuto este cuadro: si replicara
        /// SlugActor.AnilloInvertido, la prueba no probaria nada distinto.
        public static bool AnilloVisible { get; private set; }

        private void DibujarAnillo()
        {
            if (!SlugActor.AnilloInvertido) { AnilloVisible = false; return; }
            GUIStyle st = Etiqueta(ref _stAnillo, 19, FontStyle.Bold, TextAnchor.MiddleCenter);
            float w = Screen.width, y = 118f;
            string txt = "ANILLO AL REVES   ·   lo que vencía, pierde";
            Color prev = GUI.color;
            Rect caja = new Rect(w * 0.5f - 268f, y - 4f, 536f, 32f);
            HudTheme.Chip(caja);
            HudTheme.Linea(new Rect(caja.x, caja.y, 4f, caja.height), new Color(1f, 0.42f, 0.28f, 1f));
            HudTheme.Esquinas(caja, new Color(1f, 0.45f, 0.3f, 0.85f), 10f, 1.5f);
            GUI.color = new Color(1f, 0.66f, 0.46f);
            GUI.Label(new Rect(w * 0.5f - 260f, y, 520f, 26f), txt, st);
            GUI.color = prev;
            AnilloVisible = true;
        }

        /// Compilada una vez: Regex.Replace(string, string, string) recompila el patron en cada
        /// llamado, y esto corrre por enemigo y por cuadro.
        static readonly System.Text.RegularExpressions.Regex _tagColor =
            new System.Text.RegularExpressions.Regex("</?color[^>]*>", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string StripColor(string s)
        {
            return _tagColor.Replace(s, "");
        }

        public float VidaPorcen { get { return Actor() != null ? Actor().Hp / Mathf.Max(Actor().MaxHp, 1f) : -1f; } }
        public string Estado { get { return juego != null ? juego.outcome : "-"; } }
    }
}
