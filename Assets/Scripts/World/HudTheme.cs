using UnityEngine;

namespace SlugOut.World
{
    /// Tema visual del HUD: paneles biselados azul noche con filo cian, acentos dorados y
    /// tipografia condensada. Todo se genera una vez en runtime (texturas de 9 cortes y
    /// fuentes del sistema) para que el costo por cuadro sea solo dibujar.
    public static class HudTheme
    {
        public static readonly Color Cian = new Color(0.32f, 0.84f, 1f);
        public static readonly Color CianTenue = new Color(0.24f, 0.58f, 0.72f);
        public static readonly Color Oro = new Color(1f, 0.78f, 0.34f);
        public static readonly Color OroClaro = new Color(1f, 0.9f, 0.62f);
        public static readonly Color Texto = new Color(0.9f, 0.95f, 1f);
        public static readonly Color Tenue = new Color(0.56f, 0.66f, 0.75f);
        public static readonly Color Rojo = new Color(0.98f, 0.42f, 0.38f);
        public static readonly Color Verde = new Color(0.42f, 0.92f, 0.56f);

        public static Font Fuente { get; private set; }
        public static bool Listo { get; private set; }
        /// Escala de texto de accesibilidad (1 = normal, 1.15 = grande). Afecta los estilos
        /// cacheados y los botones; los rects no cambian.
        public static float EscalaTexto = 1f;
        public static int Escalar(int tam)
        {
            return Mathf.Max(8, Mathf.RoundToInt(tam * EscalaTexto));
        }

        private static Texture2D _panel, _chip, _barra, _marco, _slotVacio, _slotLleno, _circulo,
            _flecha, _vineta, _boton, _botonHover, _botonActivo, _botonOro, _botonOroHover;
        private static GUIStyle _stPanel, _stChip, _stBarra, _stBoton, _stBotonHover, _stBotonActivo,
            _stBotonOro, _stBotonOroHover, _stBotonChico, _stBotonChicoHover;
        private static readonly System.Collections.Generic.Dictionary<int, GUIStyle> _textos =
            new System.Collections.Generic.Dictionary<int, GUIStyle>();
        private static float _escalaBotones = -1f;

        public static void Asegurar()
        {
            if (Listo) return;
            Listo = true;

            try
            {
                Fuente = Font.CreateDynamicFontFromOSFont(
                    new[] { "Bahnschrift", "Segoe UI Semibold", "Segoe UI", "Arial" }, 32);
            }
            catch { Fuente = null; }

            _panel = Panel("hud_panel", 64, 14, new Color(0.07f, 0.16f, 0.23f), new Color(0.02f, 0.05f, 0.09f), 0.92f, Cian);
            _chip = Panel("hud_chip", 48, 10, new Color(0.05f, 0.12f, 0.18f), new Color(0.02f, 0.05f, 0.08f), 0.86f, CianTenue);
            _barra = Panel("hud_barra", 32, 6, new Color(0.02f, 0.05f, 0.09f), new Color(0.01f, 0.03f, 0.06f), 0.96f, new Color(0.2f, 0.4f, 0.5f));
            _slotVacio = Panel("hud_slot_vacio", 32, 7, new Color(0.05f, 0.08f, 0.12f), new Color(0.03f, 0.05f, 0.08f), 0.95f, new Color(0.2f, 0.32f, 0.4f));
            _slotLleno = Panel("hud_slot_lleno", 32, 7, new Color(0.06f, 0.16f, 0.2f), new Color(0.03f, 0.08f, 0.11f), 0.98f, Cian);
            _marco = MarcoRetrato();
            _circulo = CirculoMapa();
            _flecha = Flecha();
            _vineta = TexturaVineta();
            _boton = Panel("hud_boton", 48, 10, new Color(0.06f, 0.13f, 0.19f), new Color(0.03f, 0.07f, 0.11f), 0.96f, CianTenue);
            _botonHover = Panel("hud_boton_hover", 48, 10, new Color(0.1f, 0.2f, 0.24f), new Color(0.05f, 0.1f, 0.13f), 0.98f, Oro);
            _botonActivo = Panel("hud_boton_activo", 48, 10, new Color(0.14f, 0.22f, 0.24f), new Color(0.07f, 0.12f, 0.14f), 1f, OroClaro);
            _botonOro = Panel("hud_boton_oro", 48, 10, new Color(0.32f, 0.22f, 0.07f), new Color(0.15f, 0.1f, 0.03f), 1f, Oro);
            _botonOroHover = Panel("hud_boton_oro_hover", 48, 10, new Color(0.48f, 0.34f, 0.1f), new Color(0.22f, 0.15f, 0.04f), 1f, OroClaro);

            _stPanel = EstiloCaja(_panel, 16);
            _stChip = EstiloCaja(_chip, 12);
            _stBarra = EstiloCaja(_barra, 8);
            _stBoton = EstiloBoton(_boton);
            _stBotonHover = EstiloBoton(_botonHover);
            _stBotonActivo = EstiloBoton(_botonActivo);
            _stBotonOro = EstiloBoton(_botonOro);
            _stBotonOroHover = EstiloBoton(_botonOroHover);
            _stBotonChico = EstiloBoton(_boton);
            _stBotonChico.fontSize = 12;
            _stBotonChicoHover = EstiloBoton(_botonHover);
            _stBotonChicoHover.fontSize = 12;
        }

        public static GUIStyle Estilo(int tam, FontStyle estilo, TextAnchor ancla, Color color)
        {
            Asegurar();
            int uso = Escalar(tam);
            int clave = uso * 1000 + (int)estilo * 100 + (int)ancla;
            if (!_textos.TryGetValue(clave, out GUIStyle s))
            {
                s = new GUIStyle(GUI.skin.label);
                s.font = Fuente;
                s.fontSize = uso;
                s.fontStyle = estilo;
                s.alignment = ancla;
                s.richText = false;
                _textos[clave] = s;
            }
            s.normal.textColor = color;
            return s;
        }

        public static void Panel(Rect r) { Asegurar(); GUI.Box(r, GUIContent.none, _stPanel); }

        /// Estilo de GUI.Window con el panel del tema; la ventana del registro conserva
        /// el arrastre y el scroll nativos pero deja de verse como la ventana gris de Unity.
        public static GUIStyle Ventana()
        {
            Asegurar();
            var s = new GUIStyle(GUI.skin.window);
            s.font = Fuente;
            s.fontSize = 16;
            s.fontStyle = FontStyle.Bold;
            s.normal.background = _panel;
            s.onNormal.background = _panel;
            s.normal.textColor = OroClaro;
            s.onNormal.textColor = OroClaro;
            s.border = new RectOffset(16, 16, 16, 16);
            s.padding = new RectOffset(18, 18, 22, 18);
            return s;
        }

        public static void Chip(Rect r) { Asegurar(); GUI.Box(r, GUIContent.none, _stChip); }

        /// Barra con fondo biselado, brillo superior y el tramo de dano retrasado. `retraso`
        /// es la fraccion que se dibuja en blanco antes de encogerse; -1 lo apaga.
        public static void Barra(Rect r, float pct, Color color, float retraso = -1f)
        {
            Asegurar();
            pct = Mathf.Clamp01(pct);
            Color prev = GUI.color;

            if (r.height >= 12f)
            {
                GUI.color = Color.white;
                GUI.Box(r, GUIContent.none, _stBarra);
            }
            else
            {
                GUI.color = Color.white;
                GUI.DrawTexture(r, _barra);
            }

            float margen = r.height >= 8f ? 2f : 1f;
            Rect dentro = new Rect(r.x + margen, r.y + margen, r.width - margen * 2f, r.height - margen * 2f);
            if (retraso > pct && dentro.width > 2f)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                GUI.DrawTexture(new Rect(dentro.x, dentro.y, dentro.width * Mathf.Clamp01(retraso), dentro.height),
                    Texture2D.whiteTexture);
            }
            if (pct > 0.001f && dentro.width > 0f && dentro.height > 0f)
            {
                float ancho = dentro.width * pct;
                GUI.color = color;
                GUI.DrawTexture(new Rect(dentro.x, dentro.y, ancho, dentro.height), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, 0.24f);
                GUI.DrawTexture(new Rect(dentro.x, dentro.y, ancho, Mathf.Max(1f, dentro.height * 0.18f)),
                    Texture2D.whiteTexture);
                GUI.color = new Color(0f, 0f, 0f, 0.22f);
                GUI.DrawTexture(new Rect(dentro.x, dentro.yMax - Mathf.Max(1f, dentro.height * 0.18f),
                    ancho, Mathf.Max(1f, dentro.height * 0.18f)), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(new Rect(dentro.x, dentro.y, Mathf.Min(2f, ancho), dentro.height),
                    Texture2D.whiteTexture);
                if (dentro.width > 90f && dentro.height >= 14f)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.18f);
                    for (int i = 1; i < 4; i++)
                        GUI.DrawTexture(new Rect(dentro.x + dentro.width * (i * 0.25f), dentro.y, 1f, dentro.height),
                            Texture2D.whiteTexture);
                }
            }
            GUI.color = prev;
        }

        public static void Retrato(Rect r, Texture2D tex, Color acento)
        {
            Asegurar();
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 1f);
            GUI.Box(r, GUIContent.none, _stChip);
            Rect interior = new Rect(r.x + 5f, r.y + 5f, r.width - 10f, r.height - 10f);
            GUI.color = new Color(0.02f, 0.06f, 0.1f, 0.9f);
            GUI.DrawTexture(interior, Texture2D.whiteTexture);
            if (tex != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(interior, tex, ScaleMode.ScaleToFit, true);
            }
            GUI.color = Color.white;
            GUI.DrawTexture(r, _marco);
            GUI.color = acento;
            GUI.DrawTexture(new Rect(r.x + 9f, r.yMax - 7f, r.width - 18f, 3f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void SlotTubo(Rect r, bool lleno, float recarga, Color color)
        {
            Asegurar();
            Color prev = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(r, lleno ? _slotLleno : _slotVacio);
            Rect dentro = new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f);
            if (lleno)
            {
                GUI.color = color;
                GUI.DrawTexture(dentro, Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                GUI.DrawTexture(new Rect(dentro.x, dentro.y, dentro.width, 1f), Texture2D.whiteTexture);
            }
            else if (recarga > 0f)
            {
                float h = dentro.height * Mathf.Clamp01(recarga);
                GUI.color = new Color(0.95f, 0.78f, 0.26f, 0.85f);
                GUI.DrawTexture(new Rect(dentro.x, dentro.yMax - h, dentro.width, h), Texture2D.whiteTexture);
            }
            GUI.color = prev;
        }

        public static void Circulo(Rect r) { Asegurar(); Color p = GUI.color; GUI.color = Color.white; GUI.DrawTexture(r, _circulo); GUI.color = p; }

        public static void Flecha(Rect r, float angulo)
        {
            Asegurar();
            Color prev = GUI.color;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(angulo, new Vector2(r.center.x, r.center.y));
            GUI.color = Color.white;
            GUI.DrawTexture(r, _flecha);
            GUI.matrix = m;
            GUI.color = prev;
        }

        public static void Vineta()
        {
            Asegurar();
            Color prev = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _vineta);
            GUI.color = prev;
        }

        /// Esquinas en L: el detalle que hace que un panel se lea como marco y no como caja.
        public static void Esquinas(Rect r, Color color, float largo = 12f, float grosor = 2f)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, largo, grosor), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, grosor, largo), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - largo, r.y, largo, grosor), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - grosor, r.y, grosor, largo), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - grosor, largo, grosor), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - largo, grosor, largo), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - largo, r.yMax - grosor, largo, grosor), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - grosor, r.yMax - largo, grosor, largo), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void Linea(Rect r, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// Reaplica la escala de texto a los botones cacheados; no recrea estilos por cuadro.
        static void EscalarBotones()
        {
            if (Mathf.Approximately(_escalaBotones, EscalaTexto)) return;
            _escalaBotones = EscalaTexto;
            int normal = Escalar(17), chico = Escalar(12);
            if (_stBoton != null) _stBoton.fontSize = normal;
            if (_stBotonHover != null) _stBotonHover.fontSize = normal;
            if (_stBotonActivo != null) _stBotonActivo.fontSize = normal;
            if (_stBotonOro != null) _stBotonOro.fontSize = normal;
            if (_stBotonOroHover != null) _stBotonOroHover.fontSize = normal;
            if (_stBotonChico != null) _stBotonChico.fontSize = chico;
            if (_stBotonChicoHover != null) _stBotonChicoHover.fontSize = chico;
        }

        public static bool Boton(Rect r, string texto)
        {
            Asegurar();
            EscalarBotones();
            bool hover = r.Contains(Event.current.mousePosition);
            bool apretado = hover && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag);
            GUIStyle st = apretado ? _stBotonActivo : (hover ? _stBotonHover : _stBoton);
            st.normal.textColor = hover ? OroClaro : Texto;
            st.hover.textColor = OroClaro;
            st.active.textColor = Color.white;
            return GUI.Button(r, texto, st);
        }

        /// Variante compacta para filas de tres acciones (relevos del duelo).
        public static bool BotonChico(Rect r, string texto)
        {
            Asegurar();
            EscalarBotones();
            bool hover = r.Contains(Event.current.mousePosition);
            GUIStyle st = hover ? _stBotonChicoHover : _stBotonChico;
            st.normal.textColor = Texto;
            st.hover.textColor = OroClaro;
            return GUI.Button(r, texto, st);
        }

        /// Boton de accion principal (confirmar ataque / siguiente ronda): relleno calido y
        /// filo dorado, como el CTA de la referencia del duelo.
        public static bool BotonOro(Rect r, string texto)
        {
            Asegurar();
            EscalarBotones();
            bool hover = r.Contains(Event.current.mousePosition);
            GUIStyle st = hover ? _stBotonOroHover : _stBotonOro;
            st.normal.textColor = OroClaro;
            st.hover.textColor = Color.white;
            st.active.textColor = Color.white;
            return GUI.Button(r, texto, st);
        }

        // ---------------------------------------------------------------- generacion

        private static GUIStyle EstiloCaja(Texture2D tex, int borde)
        {
            var s = new GUIStyle();
            s.normal.background = tex;
            s.border = new RectOffset(borde, borde, borde, borde);
            s.margin = new RectOffset();
            s.padding = new RectOffset();
            return s;
        }

        private static GUIStyle EstiloBoton(Texture2D tex)
        {
            var s = new GUIStyle(GUI.skin.button);
            s.normal.background = tex;
            s.hover.background = tex;
            s.active.background = tex;
            s.focused.background = tex;
            s.font = Fuente;
            s.fontSize = 17;
            s.fontStyle = FontStyle.Bold;
            s.alignment = TextAnchor.MiddleCenter;
            s.border = new RectOffset(14, 14, 14, 14);
            s.padding = new RectOffset(10, 10, 6, 6);
            s.normal.textColor = Texto;
            s.hover.textColor = OroClaro;
            s.active.textColor = Color.white;
            return s;
        }

        private static Texture2D Panel(string nombre, int lado, float corte, Color arriba, Color abajo,
                                       float alfa, Color borde)
        {
            var t = Nueva(nombre, lado, lado);
            var px = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
            {
                float v = y / (lado - 1f);
                Color relleno = Color.Lerp(arriba, abajo, v);
                for (int x = 0; x < lado; x++)
                {
                    float d = DistanciaBisel(x, y, lado, lado, corte);
                    Color c;
                    if (d < 0f) c = new Color(0f, 0f, 0f, 0f);
                    else if (d < 2.2f) c = new Color(borde.r, borde.g, borde.b, alfa * 0.95f);
                    else if (d < 6f)
                    {
                        c = Color.Lerp(relleno, borde, (6f - d) / 6f * 0.16f);
                        c.a = alfa;
                    }
                    else { c = relleno; c.a = alfa; }
                    px[y * lado + x] = c;
                }
            }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        private static Texture2D MarcoRetrato()
        {
            const int lado = 96;
            const float corte = 18f;
            var t = Nueva("hud_marco_retrato", lado, lado);
            var px = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
                for (int x = 0; x < lado; x++)
                {
                    float d = DistanciaBisel(x, y, lado, lado, corte);
                    Color c = new Color(0f, 0f, 0f, 0f);
                    if (d >= 0f && d < 1.5f) c = new Color(0.02f, 0.04f, 0.06f, 0.95f);
                    else if (d >= 1.5f && d < 4.2f) c = new Color(Cian.r, Cian.g, Cian.b, 0.95f);
                    else if (d >= 4.2f && d < 6.5f) c = new Color(Cian.r * 0.7f, Cian.g * 0.7f, Cian.b * 0.7f, 0.35f);
                    // muescas doradas en las esquinas
                    bool esquina = (x < 16 || x > lado - 17) && (y < 16 || y > lado - 17);
                    if (esquina && d >= 0f && d < 7f) c = new Color(Oro.r, Oro.g, Oro.b, 0.9f);
                    px[y * lado + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        private static Texture2D CirculoMapa()
        {
            const int lado = 256;
            const float r = 122f;
            float c0 = (lado - 1) * 0.5f;
            var t = Nueva("hud_minimapa", lado, lado);
            var px = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
                for (int x = 0; x < lado; x++)
                {
                    float dx = x - c0, dy = y - c0;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = new Color(0f, 0f, 0f, 0f);
                    if (dist <= r + 5f)
                    {
                        if (dist > r - 2f)
                        {
                            float a = Mathf.Clamp01((r + 5f - dist) / 7f);
                            c = new Color(Cian.r, Cian.g, Cian.b, 0.28f * a);
                        }
                        else if (dist > r - 4f)
                            c = new Color(Cian.r, Cian.g, Cian.b, 0.9f);
                        else
                        {
                            float v = dist / r;
                            Color fondo = Color.Lerp(new Color(0.05f, 0.13f, 0.19f), new Color(0.012f, 0.035f, 0.06f), v);
                            c = new Color(fondo.r, fondo.g, fondo.b, 0.93f);
                            // anillos y cruz tenues
                            if (dist > 1f)
                            {
                                float m33 = Mathf.Abs(dist - r * 0.33f);
                                float m66 = Mathf.Abs(dist - r * 0.66f);
                                if (m33 < 0.8f || m66 < 0.8f) c = Color.Lerp(c, Cian, 0.18f);
                            }
                            if (Mathf.Abs(dx) < 0.8f || Mathf.Abs(dy) < 0.8f)
                                c = Color.Lerp(c, Cian, 0.1f);
                        }
                    }
                    px[y * lado + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        private static Texture2D Flecha()
        {
            const int lado = 32;
            var t = Nueva("hud_flecha", lado, lado);
            var px = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
                for (int x = 0; x < lado; x++)
                {
                    float centro = (lado - 1) * 0.5f;
                    float ancho = Mathf.Max(0f, (y - 2f) * 0.38f);
                    bool dentro = y >= 2f && y <= lado - 3f && Mathf.Abs(x - centro) <= ancho;
                    px[y * lado + x] = dentro ? new Color(1f, 1f, 1f, 0.96f) : new Color(0f, 0f, 0f, 0f);
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        private static Texture2D TexturaVineta()
        {
            const int lado = 128;
            var t = Nueva("hud_vineta", lado, lado);
            var px = new Color32[lado * lado];
            float c0 = (lado - 1) * 0.5f;
            for (int y = 0; y < lado; y++)
                for (int x = 0; x < lado; x++)
                {
                    float dx = (x - c0) / c0, dy = (y - c0) / c0;
                    float v = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 1f, v)) * 0.5f;
                    px[y * lado + x] = new Color(0.01f, 0.02f, 0.035f, a);
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        private static Texture2D Nueva(string nombre, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.name = nombre;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        /// Distancia al borde de un rectangulo con las cuatro esquinas cortadas en diagonal.
        /// Negativa afuera: alcanza para dibujar relleno, filo y halo con una sola pasada.
        private static float DistanciaBisel(float x, float y, float w, float h, float corte)
        {
            float d = Mathf.Min(Mathf.Min(x, w - 1f - x), Mathf.Min(y, h - 1f - y));
            d = Mathf.Min(d, (x + y - corte) * 0.7071f);
            d = Mathf.Min(d, (w - 1f - x + y - corte) * 0.7071f);
            d = Mathf.Min(d, (x + h - 1f - y - corte) * 0.7071f);
            d = Mathf.Min(d, (w - 1f - x + h - 1f - y - corte) * 0.7071f);
            return d;
        }


        private static Texture2D _siluetaBabosa;

        /// Silueta neutra para especies sin retrato: se tinta con el color del
        /// elemento en vez de mostrar una letra grande.
        public static Texture2D SiluetaBabosa()
        {
            if (_siluetaBabosa == null)
                _siluetaBabosa = Resources.Load<Texture2D>("Sprites/silueta_babosa");
            return _siluetaBabosa;
        }
    }
}
