using System.Collections.Generic;
using UnityEngine;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.World
{
    /// Registro de descubrimientos (cuaderno de campo): una ficha por babosa vinculada con
    /// retrato, nombre, tipo, habitat, conducta observada, habilidad, uso y limitacion, en
    /// palabras claras y sin numeros tecnicos.
    ///
    /// Es una pantalla propia y no una subpantalla del menu: se abre con CollectionUI.Abrir()
    /// desde GameFlow y se cierra con su boton o Escape. Mientras esta abierta dibuja por
    /// encima del menu (depth -3) y GameFlow corta su OnGUI para que los clics no atraviesen.
    public class CollectionUI : MonoBehaviour
    {
        public static CollectionUI Instancia { get; private set; }
        public static bool Abierto { get { return Instancia != null; } }

        public static void Abrir()
        {
            if (Instancia != null) return;
            var go = new GameObject("RegistroUI");
            DontDestroyOnLoad(go);
            go.AddComponent<CollectionUI>();
        }

        public static void Cerrar()
        {
            if (Instancia != null) Destroy(Instancia.gameObject);
        }

        /// Cantidad de especies guardadas en el registro. La coleccion vive en PlayerPrefs como
        /// CSV de ids; aqui solo se cuentan entradas no vacias, sin depender del roster.
        public static int ContadorDescubiertas()
        {
            string csv = GameFlow.ColeccionGuardada();
            if (string.IsNullOrEmpty(csv)) return 0;
            int n = 0;
            foreach (string s in csv.Split(','))
                if (!string.IsNullOrWhiteSpace(s)) n++;
            return n;
        }

        private const float ALTO_FICHA = 200f;
        private const int FiltroTodas = -1;
        private const int FiltroEquipadas = -2;

        private Rect _rect;
        private Vector2 _scroll;
        private List<SlugData> _fichas = new List<SlugData>();
        private List<SlugData> _mostradas = new List<SlugData>();
        private int _totalEspecies;
        private int _filtro = FiltroTodas;
        private string _busqueda = "";

        // Estilos creados UNA vez dentro de OnGUI (GUI.skin recien ahi es valido). Crearlos por
        // cuadro era GC cada frame, la misma deuda que ya arreglo GameHUD con Etiqueta().
        private GUIStyle _stVentana, _stHeader, _stNombre, _stLinea, _stNota, _stCerrar, _stBusqueda;
        private bool _estilosListos;

        private void Awake()
        {
            if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
            Instancia = this;
        }

        private void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        private void OnEnable()
        {
            ArmarFichas();
            Ubicar();
        }

        /// La lista se arma al abrir, no por cuadro: el registro no cambia con la ventana abierta.
        private void ArmarFichas()
        {
            _fichas.Clear();
            SlugData[] todas = SlugRoster.Todos;
            _totalEspecies = todas != null ? todas.Length : 0;
            if (todas == null) return;
            for (int i = 0; i < todas.Length; i++)
                if (todas[i] != null && GameFlow.EstaDescubierta(todas[i].slugId))
                    _fichas.Add(todas[i]);
        }

        private void Ubicar()
        {
            float ancho = Mathf.Min(460f, Screen.width * 0.92f);
            float alto = Mathf.Min(560f, Screen.height * 0.92f);
            _rect = new Rect((Screen.width - ancho) * 0.5f, (Screen.height - alto) * 0.5f, ancho, alto);
        }

        private void OnGUI()
        {
            if (_rect.width != Mathf.Min(460f, Screen.width * 0.92f)) Ubicar();

            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                e.Use();
                Cerrar();
                return;
            }
            // B del mando: mismo volver que ESC. Solo con mando para no robarle el ESC al teclado.
            if (EntradaJuego.HayMando && EntradaJuego.Leer().cancelar)
            {
                Cerrar();
                return;
            }

            GUI.depth = -3;   // por encima del menu (-1) y del torneo (-2)
            HudTheme.Asegurar();
            // GUI.color es global entre componentes; si otro OnGUI dejo un tinte oscuro,
            // la ventana entera se dibujaria apagada. Este modal manda color blanco.
            GUI.color = Color.white;
            GUI.color = new Color(0.008f, 0.016f, 0.026f, 0.82f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            HudTheme.Vineta();
            if (!_estilosListos) PrepararEstilos();

            // Marco propio en vez de GUI.Window: la ventana nativa se dibujaba con un velo
            // encima en este orden de profundidades y apagaba todo el contenido.
            HudTheme.Panel(_rect);
            HudTheme.Esquinas(_rect, new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.6f), 16f, 2f);
            GUI.Label(new Rect(_rect.x + 18f, _rect.y + 7f, _rect.width - 36f, 20f),
                "REGISTRO DE BABOSAS",
                HudTheme.Estilo(16, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.OroClaro));
            GUI.BeginGroup(_rect);
            DibujarVentana(4711);
            GUI.EndGroup();
        }

        private void PrepararEstilos()
        {
            _estilosListos = true;

            _stVentana = HudTheme.Ventana();

            _stHeader = new GUIStyle(GUI.skin.label);
            _stHeader.font = HudTheme.Fuente;
            _stHeader.fontSize = 12;
            _stHeader.fontStyle = FontStyle.Bold;
            _stHeader.normal.textColor = new Color(0.66f, 0.86f, 0.96f);

            _stNombre = new GUIStyle(GUI.skin.label);
            _stNombre.font = HudTheme.Fuente;
            _stNombre.fontSize = 16;
            _stNombre.fontStyle = FontStyle.Bold;
            _stNombre.normal.textColor = new Color(0.95f, 0.97f, 1f);
            _stNombre.alignment = TextAnchor.MiddleLeft;

            _stLinea = new GUIStyle(GUI.skin.label);
            _stLinea.font = HudTheme.Fuente;
            _stLinea.fontSize = 12;
            _stLinea.wordWrap = true;
            _stLinea.normal.textColor = new Color(0.82f, 0.88f, 0.94f);

            _stNota = new GUIStyle(_stLinea);
            _stNota.fontStyle = FontStyle.Italic;
            _stNota.normal.textColor = new Color(0.64f, 0.72f, 0.8f);

            _stCerrar = new GUIStyle(GUI.skin.button);
            _stCerrar.font = HudTheme.Fuente;
            _stCerrar.fontSize = 12;
            _stCerrar.fontStyle = FontStyle.Bold;

            // Campo transparente sobre el chip del tema: el textField nativo trae un fondo
            // gris que rompia la paleta.
            _stBusqueda = new GUIStyle(GUI.skin.textField);
            _stBusqueda.font = HudTheme.Fuente;
            _stBusqueda.fontSize = 12;
            _stBusqueda.normal.textColor = new Color(0.9f, 0.95f, 1f);
            _stBusqueda.focused.textColor = new Color(0.9f, 0.95f, 1f);
            _stBusqueda.normal.background = null;
            _stBusqueda.hover.background = null;
            _stBusqueda.focused.background = null;
            _stBusqueda.padding = new RectOffset(8, 8, 4, 4);
        }

        private void DibujarFiltros(float ancho)
        {
            float y = 58f;
            if (HudTheme.BotonChico(new Rect(14f, y, 76f, 24f), "TODAS"))
                _filtro = FiltroTodas;
            if (HudTheme.BotonChico(new Rect(96f, y, 106f, 24f), "EQUIPADAS"))
                _filtro = FiltroEquipadas;
            if (_filtro == FiltroTodas || _filtro == FiltroEquipadas)
            {
                Rect activo = _filtro == FiltroTodas ? new Rect(14f, y + 24f, 76f, 2f) : new Rect(96f, y + 24f, 106f, 2f);
                HudTheme.Linea(activo, HudTheme.Oro);
            }

            Rect campo = new Rect(212f, y, ancho - 226f, 24f);
            HudTheme.Chip(campo);
            _busqueda = GUI.TextField(new Rect(campo.x + 4f, campo.y + 1f, campo.width - 8f, campo.height - 2f),
                _busqueda ?? "", 40, _stBusqueda);
            if (string.IsNullOrEmpty(_busqueda))
                GUI.Label(new Rect(campo.x + 10f, campo.y, campo.width - 16f, campo.height),
                    "Buscar por nombre, tipo o habitat...",
                    HudTheme.Estilo(11, FontStyle.Normal, TextAnchor.MiddleLeft, HudTheme.Tenue));
        }

        private void DibujarVentana(int id)
        {
            float ancho = _rect.width;
            int descubiertas = ContadorDescubiertas();

            GUI.Label(new Rect(14f, 32f, ancho - 210f, 20f),
                descubiertas + " de " + _totalEspecies + " especies registradas", _stHeader);
            HudTheme.Barra(new Rect(ancho - 190f, 38f, 170f, 6f),
                _totalEspecies > 0 ? (float)descubiertas / _totalEspecies : 0f, HudTheme.Cian);
            HudTheme.Linea(new Rect(14f, 54f, ancho - 28f, 1f),
                new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.18f));
            DibujarFiltros(ancho);

            _mostradas.Clear();
            string[] equipo = GameFlow.Equipo();
            for (int i = 0; i < _fichas.Count; i++)
            {
                SlugData d = _fichas[i];
                if (d == null) continue;
                if (_filtro == FiltroEquipadas
                    && System.Array.IndexOf(equipo, d.slugId) < 0) continue;
                if (!GameFlow.CoincideBusqueda(d, _busqueda)) continue;
                _mostradas.Add(d);
            }

            Rect vista = new Rect(14f, 94f, ancho - 28f, _rect.height - 94f - 46f);
            float altoContenido = 10f + _mostradas.Count * ALTO_FICHA + 10f;
            _scroll = GUI.BeginScrollView(vista, _scroll,
                new Rect(0f, 0f, vista.width - 16f, Mathf.Max(altoContenido, vista.height)));

            if (_fichas.Count == 0)
            {
                GUI.Label(new Rect(10f, 8f, vista.width - 36f, 90f),
                    "Todavia no vinculaste babosas.\n\n"
                    + "Explora la region: busca rastros cerca de cada habitat, observa desde lejos "
                    + "sin moverte, y acercate despacio. Si se asusta, no pasa nada: vuelve a su "
                    + "puesto y se puede intentar otra vez.", _stLinea);
            }
            else if (_mostradas.Count == 0)
            {
                GUI.Label(new Rect(10f, 8f, vista.width - 36f, 70f),
                    "Ninguna ficha coincide con el filtro o la busqueda.\n\n"
                    + "Prueba con TODAS o borra el texto de busqueda.", _stLinea);
            }
            else
            {
                for (int i = 0; i < _mostradas.Count; i++)
                    Ficha(_mostradas[i], 10f + i * ALTO_FICHA, vista.width - 20f);
            }

            GUI.EndScrollView();

            if (HudTheme.BotonChico(new Rect(ancho - 100f, _rect.height - 38f, 88f, 28f), "CERRAR"))
                Cerrar();
        }

        private void Ficha(SlugData d, float y, float ancho)
        {
            // Placa de fondo con el color del elemento: la especie se reconoce por color + nombre,
            // nunca solo por un numero de dano (regla del diseno).
            Color c = d.elementColor;
            HudTheme.Panel(new Rect(0f, y, ancho, ALTO_FICHA - 10f));
            HudTheme.Linea(new Rect(0f, y, 3f, ALTO_FICHA - 10f), c);
            HudTheme.Linea(new Rect(3f, y, ancho - 3f, 2f),
                new Color(c.r, c.g, c.b, 0.55f));

            Retrato(new Rect(12f, y + 12f, 84f, 84f), d, c);

            float xT = 106f;
            float wT = ancho - xT - 12f;
            GUI.Label(new Rect(xT, y + 8f, wT, 22f), d.slugName, _stNombre);
            GUI.Label(new Rect(xT, y + 32f, wT, 36f),
                "Tipo: " + SlugTexto.NombreElemento(d.element) + "   ·   Hábitat: " + Vacio(d.habitat), _stLinea);

            GUI.Label(new Rect(12f, y + 104f, ancho - 24f, 24f),
                "Conducta: " + Vacio(d.conducta), _stNota);
            GUI.Label(new Rect(12f, y + 130f, ancho - 24f, 24f),
                "Habilidad: " + Duelo.NombreEfecto(d.abilityType)
                + (string.IsNullOrEmpty(d.uso) ? "" : " · " + d.uso), _stLinea);
            GUI.Label(new Rect(12f, y + 156f, ancho - 24f, 24f),
                "No sirve para: " + Vacio(d.limitacion), _stNota);
        }

        private static void Retrato(Rect r, SlugData d, Color acento)
        {
            Texture2D tex = SlugArenaGame.LoadSprite(d, false);
            bool real = tex != null && !tex.name.StartsWith("retrato_");
            if (real)
            {
                HudTheme.Retrato(r, tex, acento);
                return;
            }

            HudTheme.Chip(r);
            Color prev = GUI.color;
            GUI.color = new Color(acento.r, acento.g, acento.b, 0.2f);
            GUI.DrawTexture(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), Texture2D.whiteTexture);
            GUI.color = prev;
            HudTheme.Esquinas(r, new Color(acento.r, acento.g, acento.b, 0.85f), 10f, 1.5f);
            Texture2D silueta = HudTheme.SiluetaBabosa();
            if (silueta != null)
            {
                Color prevSil = GUI.color;
                GUI.color = new Color(acento.r, acento.g, acento.b, 0.9f);
                GUI.DrawTexture(new Rect(r.x + 6f, r.y + 4f, r.width - 12f, r.height - 22f),
                    silueta, ScaleMode.ScaleToFit, true);
                GUI.color = prevSil;
            }
            else
            {
                string inicial = !string.IsNullOrEmpty(d.slugName)
                    ? d.slugName.Substring(0, 1).ToUpperInvariant() : "?";
                GUI.Label(r, inicial, HudTheme.Estilo(Mathf.RoundToInt(r.height * 0.42f),
                    FontStyle.Bold, TextAnchor.MiddleCenter, acento));
            }
            GUI.Label(new Rect(r.x, r.yMax - 15f, r.width, 12f),
                SlugTexto.NombreElemento(d.element).ToUpperInvariant(),
                HudTheme.Estilo(8, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Color(acento.r, acento.g, acento.b, 0.9f)));
        }

        private static string Vacio(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? "(sin registrar)" : s;
        }
    }
}