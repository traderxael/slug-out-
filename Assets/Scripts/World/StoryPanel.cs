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
            }
            return campo;
        }

        private void Start()
        {
            if (mostrarAlIniciar)
            {
                SlugStory.Fragmento f = SlugStory.DeCampana(GameFlow.Activo != null ? GameFlow.Activo.Nivel : 1);
                _cola.Add(f);
                _vistos.Add(f.nivel);
                vistosCount = _vistos.Count;
            }
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
        }

        /// Lo llama la arena al capturar y al ganar.
        public void AvanzarHistoria()
        {
            SlugArenaGame arena = GetComponent<SlugArenaGame>();
            if (arena != null && arena.Infinito)
            {
                int ola = arena.OlaActual;
                if (!SlugStory.TieneFragmentoDeOla(ola)) return;
                SlugStory.Fragmento fi = SlugStory.DeOlaInfinito(ola);
                if (!_vistos.Contains(1000 + ola)) Encolar(fi, 1000 + ola);
                return;
            }

            int siguiente = Mathf.Clamp(nivelDesbloqueado + 1, 1, SlugStory.TotalCampana);
            nivelDesbloqueado = siguiente;
            SlugStory.Fragmento f = SlugStory.DeCampana(siguiente);
            // La rama `if (_cola.Count == 0)` descartaba el fragmento si ya habia uno mostrando:
            // con dos capturas seguidas el contador "vistos N/28" quedaba corto PARA SIEMPRE.
            // Ahora se encola siempre, deduplicado por nivel (que avanza por captura y por victoria,
            // y los dos disparos pueden pedir el mismo fragmento).
            if (!_vistos.Contains(f.nivel)) Encolar(f, f.nivel);
        }

        /// GameFlow lo llama al empezar una partida: la ruta abierta no puede sobrevivir al menu.
        public void CerrarRuta()
        {
            rutaAbierta = false;
        }

        private static bool Tecla(System.Func<bool> nuevo, System.Func<bool> viejo)
        {
            return nuevo();
        }

        private bool Espacio()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.spaceKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.Space);
#else
            return false;
#endif
        }

        private bool Tab()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.tabKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.Tab);
#else
            return false;
#endif
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
            if (tabNuevo && _cdTab <= 0f)
            {
                _cdTab = 0.4f;
                rutaAbierta = !rutaAbierta;
                GameFeel.Pausa = rutaAbierta;
                Debug.Log("[Historia] ruta " + (rutaAbierta ? "abierta" : "cerrada") + " | vistos=" + _vistos.Count + "/" + SlugStory.TotalCampana);
            }

            if (rutaAbierta) return;
            if (_cola.Count == 0) { _visto = 0f; return; }
            _visto += Mathf.Min(dt, 0.1f);
            bool cerrar = _visto >= duracionFragmento || (Espacio() && _cd <= 0f && _visto >= minimoAntesDeCerrar);
            if (cerrar)
            {
                _cd = 0.35f;
                _cola.RemoveAt(0);
                _visto = 0f;
            }
        }

        private void OnDisable()
        {
            // Solo suelta la pausa si la habia puesto este panel: si no, al irse al menu con la
            // ruta abierta este reset descongelaba la arena y el combate segua corriendo debajo
            // del menu. GameFlow adems reafirma la pausa cada cuadro mientras est en menu.
            if (rutaAbierta) GameFeel.Pausa = false;
            rutaAbierta = false;
        }

        private void OnGUI()
        {
            if (!visible) return;
            // el fragmento cuenta lo que recien pasaste: no tiene que tapar el menu
            if (GameFlow.Activo != null && GameFlow.Activo.EnMenu) return;
            GUI.depth = 10;
            if (rutaAbierta) { DibujarRuta(); return; }
            if (_cola.Count == 0) return;

            // banner arriba y angosto: el centro de la pantalla es donde pasa el combate
            SlugStory.Fragmento f = _cola[0];
            float w = Mathf.Min(Screen.width * 0.62f, 920f);
            float h = 132f;
            float x = (Screen.width - w) * 0.5f;
            float y = 12f;
            Rect box = new Rect(x, y, w, h);
            float resta = 1f - Mathf.Clamp01(_visto / Mathf.Max(0.001f, duracionFragmento));

            Color prev = GUI.color;
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.93f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);

            // lamina del nivel a la izquierda: el banner sigue angosto y el centro queda libre
            Texture2D lamina = Ilustracion(f.nivel, GameFlow.Activo != null && GameFlow.Activo.Infinito);
            float tx = box.x + 20f;
            float tw = box.width - 40f;
            if (lamina != null && box.width >= 560f)
            {
                float th = h - 54f;
                float twn = th * lamina.width / Mathf.Max(1, lamina.height);
                Rect img = new Rect(box.x + 13f, box.y + 13f, twn, th);
                GUI.color = Color.white;
                GUI.DrawTexture(img, lamina, ScaleMode.ScaleToFit);
                GUI.color = f.color;
                GUI.DrawTexture(new Rect(img.x, img.y, img.width, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(img.x, img.yMax - 2f, img.width, 2f), Texture2D.whiteTexture);
                tx = img.xMax + 16f;
                tw = box.xMax - 20f - tx;
            }

            GUI.color = f.color;
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 4f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(box.x, box.y + h - 3f, box.width * resta, 3f), Texture2D.whiteTexture);
            GUI.color = prev;

            GUIStyle tit = Etiqueta(ref _stTitulo, fuente - 8, FontStyle.Bold, true);
            tit.normal.textColor = f.color;

            GUIStyle cuerpo = Etiqueta(ref _stCuerpo, fuente - 13, FontStyle.Normal, true);
            cuerpo.normal.textColor = new Color(0.93f, 0.94f, 0.96f);

            GUIStyle pie = Etiqueta(ref _stPie, Mathf.Max(12, fuente - 13), FontStyle.Normal, false);
            pie.normal.textColor = new Color(0.62f, 0.65f, 0.70f);

            GUI.Label(new Rect(tx, box.y + 10f, tw, 32f), "NIVEL " + f.nivel + "  ·  " + f.titulo, tit);
            GUI.Label(new Rect(tx, box.y + 44f, tw, h - 76f), f.texto, cuerpo);
            GUI.Label(new Rect(tx, box.y + h - 28f, tw, 22f),
                "ESPACIO cierra   ·   TAB: Ruta de la cueva (pausa)   ·   vistos " + _vistos.Count + "/" + SlugStory.TotalCampana, pie);
        }

        private void DibujarRuta()
        {
            SlugStory.Fragmento[] c = SlugStory.Campana();
            float w = Screen.width * 0.82f;
            float h = Screen.height * 0.80f;
            Rect box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            Color prev = GUI.color;
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.95f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = prev;

            GUIStyle h1 = Etiqueta(ref _stH1, fuente, FontStyle.Bold, false);
            h1.normal.textColor = new Color(0.95f, 0.95f, 0.97f);
            GUI.Label(new Rect(box.x + 24f, box.y + 16f, box.width - 48f, 40f), "RUTA DE LA CUEVA  —  " + _vistos.Count + "/" + c.Length, h1);

            GUIStyle fila = Etiqueta(ref _stFila, Mathf.Max(13, fuente - 8), FontStyle.Normal, false);

            int filas = Mathf.CeilToInt(c.Length / 2f);
            float alto = (box.height - 80f) / filas;
            for (int i = 0; i < c.Length; i++)
            {
                int col = i % 2;
                int fil = i / 2;
                Rect r = new Rect(box.x + 24f + col * (box.width - 48f) * 0.5f, box.y + 64f + fil * alto, (box.width - 48f) * 0.48f, alto);
                bool visto = _vistos.Contains(c[i].nivel);
                fila.normal.textColor = visto ? c[i].color : new Color(0.35f, 0.37f, 0.42f);
                string txt = (visto ? c[i].nivel + ". " + c[i].titulo : c[i].nivel + ". ???");
                GUI.Label(r, txt, fila);
            }

            GUIStyle pie = Etiqueta(ref _stPieRuta, Mathf.Max(12, fuente - 13), FontStyle.Normal, false);
            pie.normal.textColor = new Color(0.62f, 0.65f, 0.70f);
            GUI.Label(new Rect(box.x + 24f, box.y + h - 32f, box.width - 48f, 24f), "EN PAUSA   ·   TAB para volver al combate", pie);
        }
    }
}
