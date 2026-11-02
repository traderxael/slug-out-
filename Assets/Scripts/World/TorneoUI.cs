using System.Collections.Generic;
using UnityEngine;
using SlugOut.Slugs;

namespace SlugOut.World
{
    /// Pantalla del Modo Duelo: torneo por turnos contra equipos IA, 3 babosas activas por
    /// lado. Mientras esta abierta el juego se pausa (timeScale 0) y todo se resuelve en
    /// Duelo, que es puro y se puede probar sin escena.
    public class TorneoUI : MonoBehaviour
    {
        public static TorneoUI Instancia { get; private set; }
        public static bool Activo { get { return Instancia != null; } }

        private Torneo _torneo;
        private Duelo _duelo;
        private float _pausaPrevia;
        private bool _restaurarPausa;
        private bool _eligiendoCambio;

        public static void Abrir()
        {
            if (Instancia != null) return;
            var go = new GameObject("TorneoUI");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<TorneoUI>();
        }

        private void Awake()
        {
            Instancia = this;
            _pausaPrevia = Time.timeScale;
            Time.timeScale = 0f;
            _restaurarPausa = true;
            EmpezarTorneo(Random.Range(1, 999999));
        }

        private void EmpezarTorneo(int semilla)
        {
            SlugData[] roster = SlugRoster.Todos;
            _torneo = new Torneo(Torneo.EquipoJugador(roster), 3, semilla);
            _duelo = _torneo.DueloActual;
            _eligiendoCambio = false;
            Debug.Log("[Torneo] ronda 1 de " + _torneo.RondasTotales + " | semilla " + semilla
                + " | tu equipo: " + Nombres(_duelo.jugador));
        }

        private static string Nombres(List<Combatiente> equipo)
        {
            string s = "";
            for (int i = 0; i < equipo.Count; i++)
            {
                if (i > 0) s += ", ";
                s += equipo[i].Nombre;
            }
            return s;
        }

        private void OnDestroy()
        {
            if (_restaurarPausa) Time.timeScale = _pausaPrevia;
            if (Instancia == this) Instancia = null;
        }

        private void Cerrar()
        {
            if (GameFlow.Activo != null) GameFlow.Activo.VolverAlMenu();
            Destroy(gameObject);
        }

        private void OnGUI()
        {
            if (_duelo == null) return;
            GUI.depth = -2;
            float w = Screen.width, h = Screen.height;
            float m = 40f;

            DibujarFondo(w, h);

            GUI.Label(new Rect(m, 8f, w - m * 2f, 26f), "DUELO · TORNEO",
                Estilo(20, FontStyle.Bold, new Color(1f, 0.73f, 0.33f), TextAnchor.MiddleLeft));
            string subtitulo = _duelo.Terminado
                ? "RONDA " + _torneo.Ronda + " DE " + _torneo.RondasTotales + "   ·   RESULTADO DEL DUELO"
                : "RONDA " + _torneo.Ronda + " DE " + _torneo.RondasTotales
                    + "   ·   TURNO " + _duelo.turno
                    + "   ·   elegis primero; el rival responde";
            GUI.Label(new Rect(m, 34f, w - m * 2f, 18f), subtitulo,
                Estilo(11, FontStyle.Bold, new Color(0.66f, 0.74f, 0.84f), TextAnchor.MiddleLeft));

            float colDer = Mathf.Min(w * 0.36f, 430f);
            float xDer = w - m - colDer;
            float anchoCarta = Mathf.Min(230f, (xDer - m - 36f) / 3f);
            if (anchoCarta < 86f) anchoCarta = 86f;

            float yRival = 92f;
            float yAcciones = h - 70f;
            float yJugador = yAcciones - 96f;

            Fila(m, yRival, anchoCarta, "RIVAL · 3 BABOSAS", _duelo.rival, _duelo.activoRival, false);
            Fila(m, yJugador, anchoCarta, "TU EQUIPO · 3 BABOSAS", _duelo.jugador, _duelo.activoJugador, true);

            DibujarAcciones(m, yAcciones, anchoCarta, xDer);

            if (_duelo.Terminado)
            {
                // El resultado es modal: el tablero queda de fondo y no se corta el registro
                // contra el borde del panel.
                Color prev = GUI.color;
                GUI.color = new Color(0.01f, 0.015f, 0.025f, 0.84f);
                GUI.DrawTexture(new Rect(0f, 60f, w, h - 60f), Texture2D.whiteTexture);
                GUI.color = prev;
                DibujarResultado(w, h);
            }
            else
            {
                float disponible = yJugador - 208f;
                float altoPanel = Mathf.Min(220f, disponible);
                if (altoPanel < 112f) altoPanel = 112f;
                float yPanel = 176f + Mathf.Max(0f, (disponible - altoPanel) * 0.5f);
                DibujarPanelTurno(m, yPanel, xDer - m - 12f, altoPanel);
                DibujarRegistro(xDer, 176f, colDer, yJugador - 202f);
            }
        }

        private void DibujarFondo(float w, float h)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.96f);
            GUI.DrawTexture(new Rect(0f, 0f, w, 60f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, h - 56f, w, 56f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void Fila(float x, float y, float anchoCarta, string titulo,
                          List<Combatiente> equipo, int activo, bool esJugador)
        {
            GUI.Label(new Rect(x, y - 22f, 280f, 18f), titulo,
                Estilo(11, FontStyle.Bold,
                    esJugador ? new Color(0.55f, 0.9f, 0.68f) : new Color(0.95f, 0.52f, 0.47f),
                    TextAnchor.MiddleLeft));

            for (int i = 0; i < equipo.Count && i < 3; i++)
            {
                Combatiente c = equipo[i];
                float cx = x + i * (anchoCarta + 12f);
                Rect r = new Rect(cx, y, anchoCarta, 74f);

                Color prev = GUI.color;
                GUI.color = !c.Vivo
                    ? new Color(0.05f, 0.05f, 0.06f, 0.88f)
                    : i == activo
                        ? new Color(0.14f, 0.18f, 0.23f, 0.97f)
                        : new Color(0.08f, 0.09f, 0.12f, 0.92f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);

                GUI.color = c.datos != null ? c.datos.elementColor : Color.gray;
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), Texture2D.whiteTexture);
                if (i == activo && c.Vivo)
                    GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), Texture2D.whiteTexture);
                GUI.color = prev;

                Color colorNombre = c.Vivo
                    ? (i == activo ? new Color(1f, 0.86f, 0.55f) : new Color(0.93f, 0.95f, 0.98f))
                    : new Color(0.42f, 0.44f, 0.48f);
                GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 20f),
                    c.Nombre + (i == activo && c.Vivo ? "  ·  ACTUA" : ""),
                    Estilo(12, FontStyle.Bold, colorNombre, TextAnchor.MiddleLeft));

                string elemento = c.datos != null
                    ? c.Elemento.ToString().ToUpperInvariant()
                    : "SIN DATOS";
                GUI.Label(new Rect(r.x + 10f, r.y + 25f, r.width - 20f, 15f), elemento,
                    Estilo(10, FontStyle.Bold,
                        c.datos != null ? c.datos.elementColor : new Color(0.6f, 0.6f, 0.6f),
                        TextAnchor.MiddleLeft));

                Rect barra = new Rect(r.x + 10f, r.y + 44f, r.width - 20f, 8f);
                GUI.color = new Color(0.14f, 0.16f, 0.20f, 1f);
                GUI.DrawTexture(barra, Texture2D.whiteTexture);
                if (c.Vivo)
                {
                    GUI.color = esJugador ? new Color(0.45f, 0.82f, 0.62f) : new Color(0.88f, 0.42f, 0.38f);
                    GUI.DrawTexture(new Rect(barra.x, barra.y, barra.width * Mathf.Clamp01(c.hp / c.hpMax), barra.height),
                        Texture2D.whiteTexture);
                }
                GUI.color = prev;

                string estado = c.Vivo
                    ? "vida " + Mathf.CeilToInt(c.hp) + "/" + Mathf.CeilToInt(c.hpMax)
                    : "fuera de combate";
                if (c.Vivo && c.escudo > 0f) estado += "  esc " + Mathf.CeilToInt(c.escudo);
                if (c.Vivo && c.quemaduraTurnos > 0) estado += "  arde";
                if (c.Vivo && c.congeladoTurnos > 0) estado += "  " + c.Estado;
                GUI.Label(new Rect(r.x + 10f, r.y + 54f, r.width - 20f, 16f), estado,
                    Estilo(10, FontStyle.Normal, new Color(0.66f, 0.70f, 0.76f), TextAnchor.MiddleLeft));
            }
        }

        private void DibujarPanelTurno(float x, float y, float ancho, float alto)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0.06f, 0.075f, 0.10f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, ancho, alto), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.73f, 0.33f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, ancho, 2f), Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(x + 16f, y + 12f, ancho - 32f, 18f),
                _duelo.Terminado ? "DUELO TERMINADO" : "TU TURNO",
                Estilo(11, FontStyle.Bold, new Color(1f, 0.73f, 0.33f), TextAnchor.MiddleLeft));
            GUI.Label(new Rect(x + 16f, y + 32f, ancho - 32f, 30f),
                "Actua: " + _duelo.ActivoJ.Nombre,
                Estilo(18, FontStyle.Bold, new Color(0.94f, 0.96f, 0.99f), TextAnchor.MiddleLeft));
            GUI.Label(new Rect(x + 16f, y + 62f, ancho - 32f, 20f),
                "Responde: " + _duelo.ActivoR.Nombre,
                Estilo(12, FontStyle.Bold, new Color(0.95f, 0.62f, 0.56f), TextAnchor.MiddleLeft));

            SlugData d = _duelo.ActivoJ.datos;
            string efecto = d != null ? Duelo.NombreEfecto(d.abilityType) : "sin habilidad";
            string texto = "EFECTO: " + efecto + "\nLIMITE EN DUELO: una vez cada dos turnos";
            if (d != null && !string.IsNullOrEmpty(d.limitacion))
                texto += "\nEN EL CAMPO: " + d.limitacion;
            float yTexto = y + 88f;
            float altoTexto = alto - 118f;
            if (altoTexto >= 28f)
                GUI.Label(new Rect(x + 16f, yTexto, ancho - 32f, altoTexto), texto,
                    Estilo(11, FontStyle.Normal, new Color(0.74f, 0.79f, 0.86f), TextAnchor.UpperLeft));

            if (!_duelo.Terminado && alto >= 170f)
                GUI.Label(new Rect(x + 16f, y + alto - 26f, ancho - 32f, 18f),
                    "Elegi abajo. El rival responde despues de tu accion.",
                    Estilo(10, FontStyle.Normal, new Color(0.55f, 0.61f, 0.70f), TextAnchor.MiddleLeft));
        }

        private void DibujarRegistro(float x, float y, float ancho, float alto)
        {
            GUI.Label(new Rect(x, y, ancho, 18f), "ULTIMOS MOVIMIENTOS",
                Estilo(11, FontStyle.Bold, new Color(0.58f, 0.72f, 0.82f), TextAnchor.MiddleLeft));

            int capacidad = Mathf.Clamp(Mathf.FloorToInt((alto - 28f) / 19f), 3, 20);
            int desde = Mathf.Max(0, _duelo.registro.Count - capacidad);
            for (int i = desde; i < _duelo.registro.Count; i++)
            {
                string linea = _duelo.registro[i];
                Color color = new Color(0.80f, 0.84f, 0.89f);
                if (linea.StartsWith("Tu ")) color = new Color(0.62f, 0.90f, 0.74f);
                else if (linea.StartsWith("El rival")) color = new Color(0.95f, 0.68f, 0.62f);
                else if (linea.StartsWith("Ganaste")) color = new Color(0.55f, 0.95f, 0.72f);
                else if (linea.StartsWith("Perdiste")) color = new Color(0.97f, 0.55f, 0.50f);

                GUI.Label(new Rect(x, y + 24f + (i - desde) * 19f, ancho, 18f), "· " + linea,
                    Estilo(10, FontStyle.Normal, color, TextAnchor.MiddleLeft));
            }
        }

        private void DibujarAcciones(float x, float y, float anchoCarta, float xDer)
        {
            if (_duelo.Terminado) return;
            bool puedeActuar = _duelo.ActivoJ.Vivo;

            if (_eligiendoCambio)
            {
                float anchoCancelar = 90f;
                float anchoSlot = (xDer - x - anchoCancelar - 36f) / 3f;
                for (int i = 0; i < 3; i++)
                {
                    Combatiente c = _duelo.jugador[i];
                    bool puede = c.Vivo && i != _duelo.activoJugador;
                    string texto = c.Nombre + " · " + Mathf.CeilToInt(c.hp) + "/" + Mathf.CeilToInt(c.hpMax);
                    if (Boton(new Rect(x + i * (anchoSlot + 12f), y, anchoSlot, 42f), texto, puede))
                    {
                        _eligiendoCambio = false;
                        Turno(AccionDuelo.Cambiar, i);
                    }
                }
                if (Boton(new Rect(x + 3f * (anchoSlot + 12f), y, anchoCancelar, 42f), "CANCELAR", true))
                    _eligiendoCambio = false;
                return;
            }

            if (Boton(new Rect(x, y, anchoCarta, 42f), "ATACAR", puedeActuar))
                Turno(AccionDuelo.Atacar);

            bool puedeHabilidad = puedeActuar && _duelo.ActivoJ.enfriamiento == 0;
            string etiquetaH = _duelo.ActivoJ.enfriamiento > 0
                ? "HABILIDAD · " + _duelo.ActivoJ.enfriamiento
                : "HABILIDAD";
            if (Boton(new Rect(x + anchoCarta + 12f, y, anchoCarta, 42f), etiquetaH, puedeHabilidad))
                Turno(AccionDuelo.Habilidad);

            if (Boton(new Rect(x + (anchoCarta + 12f) * 2f, y, anchoCarta, 42f), "CAMBIAR", puedeActuar))
                _eligiendoCambio = true;
        }

        private void Turno(AccionDuelo accion, int indiceCambio = -1)
        {
            List<string> lineas = _duelo.TurnoJugador(accion, indiceCambio);
            for (int i = 0; i < lineas.Count; i++) Debug.Log("[Torneo] " + lineas[i]);
            if (_duelo.Terminado)
                Debug.Log("[Torneo] duelo " + (_duelo.GanoJugador ? "ganado" : "perdido")
                    + " en la ronda " + _torneo.Ronda);
        }

        private void DibujarResultado(float w, float h)
        {
            bool gano = _duelo.GanoJugador;
            bool ultima = _torneo.Ronda >= _torneo.RondasTotales;
            float ancho = Mathf.Min(560f, w - 80f);
            float alto = 216f;
            Rect panel = new Rect((w - ancho) * 0.5f, (h - alto) * 0.5f, ancho, alto);

            Color prev = GUI.color;
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.98f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = gano ? new Color(0.45f, 0.9f, 0.68f) : new Color(0.95f, 0.45f, 0.4f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.y + panel.height - 3f, panel.width, 3f), Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(panel.x, panel.y + 18f, panel.width, 36f),
                gano ? "GANASTE EL DUELO" : "PERDISTE EL DUELO",
                Estilo(24, FontStyle.Bold,
                    gano ? new Color(0.5f, 0.92f, 0.72f) : new Color(0.97f, 0.52f, 0.47f),
                    TextAnchor.MiddleCenter));

            string detalle;
            if (gano && ultima) detalle = "Torneo completado: ganaste las " + _torneo.RondasTotales + " rondas.";
            else if (gano) detalle = "Ronda " + _torneo.Ronda + " superada. Siguiente: ronda " + (_torneo.Ronda + 1) + ".";
            else detalle = "El torneo termina en la ronda " + _torneo.Ronda + ". Podes armar otro equipo.";

            GUI.Label(new Rect(panel.x + 20f, panel.y + 62f, panel.width - 40f, 42f), detalle,
                Estilo(12, FontStyle.Normal, new Color(0.82f, 0.86f, 0.91f), TextAnchor.UpperCenter));

            float bw = (panel.width - 56f) * 0.5f;
            float by = panel.y + panel.height - 64f;
            if (gano && !ultima)
            {
                if (Boton(new Rect(panel.x + 20f, by, bw, 40f), "SIGUIENTE RONDA", true))
                {
                    _torneo.Continuar();
                    _duelo = _torneo.DueloActual;
                    _eligiendoCambio = false;
                }
            }
            else
            {
                if (Boton(new Rect(panel.x + 20f, by, bw, 40f), "NUEVO TORNEO", true))
                    EmpezarTorneo(Random.Range(1, 999999));
            }

            if (Boton(new Rect(panel.x + 36f + bw, by, bw, 40f), "VOLVER AL MENU", true))
                Cerrar();
        }

        private bool Boton(Rect r, string texto, bool activo)
        {
            bool hover = activo && r.Contains(Event.current.mousePosition);
            Color prev = GUI.color;
            GUI.color = !activo
                ? new Color(0.07f, 0.08f, 0.10f, 0.88f)
                : hover
                    ? new Color(0.22f, 0.26f, 0.32f, 1f)
                    : new Color(0.13f, 0.16f, 0.21f, 1f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = activo
                ? new Color(1f, 0.73f, 0.33f, 0.9f)
                : new Color(0.30f, 0.32f, 0.36f, 0.8f);
            GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), Texture2D.whiteTexture);
            GUI.color = prev;

            if (!activo)
            {
                GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), texto,
                    Estilo(12, FontStyle.Bold, new Color(0.50f, 0.53f, 0.58f), TextAnchor.MiddleCenter));
                return false;
            }
            return GUI.Button(new Rect(r.x + 3f, r.y + 2f, r.width - 6f, r.height - 4f), texto,
                Estilo(12, FontStyle.Bold, new Color(0.94f, 0.95f, 0.98f), TextAnchor.MiddleCenter));
        }

        private static GUIStyle _estilo;

        private static GUIStyle Estilo(int tam, FontStyle f, Color c, TextAnchor alineacion)
        {
            if (_estilo == null)
                _estilo = new GUIStyle(GUI.skin.label) { clipping = TextClipping.Clip, richText = false };
            _estilo.fontSize = tam;
            _estilo.fontStyle = f;
            _estilo.alignment = alineacion;
            _estilo.normal.textColor = c;
            return _estilo;
        }
    }
}
