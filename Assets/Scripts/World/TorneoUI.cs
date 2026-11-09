using System.Collections.Generic;
using UnityEngine;
using SlugOut.Core;
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
        public bool Terminado { get { return _duelo != null && _duelo.Terminado; } }

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
            HudTheme.Asegurar();
            float w = Screen.width, h = Screen.height;
            float m = 24f;

            DibujarFondo(w, h);

            float ladoW = Mathf.Clamp(w * 0.29f, 236f, 330f);
            float centroX = m + ladoW + 16f;
            float centroW = Mathf.Min(720f, Mathf.Max(220f, w - 2f * centroX));
            centroX = (w - centroW) * 0.5f;
            float yPaneles = 68f;

            DibujarTitulo(m, ladoW);
            DibujarBanner(centroX, 12f, centroW, 40f);
            DibujarOrden(centroX, 54f, centroW);

            DibujarCombatiente(new Rect(m, yPaneles, ladoW, 98f), _duelo.ActivoJ, true);
            DibujarCombatiente(new Rect(w - m - ladoW, yPaneles, ladoW, 98f), _duelo.ActivoR, false);
            DibujarMiniEquipo(new Rect(w - m - ladoW, 182f, ladoW, 54f), _duelo.rival, _duelo.activoRival);

            float altoCentro = h - 88f - 200f;
            DibujarCentro(new Rect(centroX, 88f, centroW, Mathf.Max(180f, altoCentro)));

            const float altoInferior = 124f;
            float yInferior = h - m - altoInferior - 18f;
            float accionesW = Mathf.Clamp(w * 0.32f, 300f, 400f);
            float equipoW = w - 2f * m - accionesW - 16f;
            DibujarEquipo(new Rect(m, yInferior, equipoW, altoInferior));
            DibujarAcciones(new Rect(w - m - accionesW, yInferior, accionesW, altoInferior));

            if (_duelo.Terminado)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0.01f, 0.015f, 0.025f, 0.92f);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
                GUI.color = prev;
                DibujarResultado(w, h);
            }
        }

        private void DibujarFondo(float w, float h)
        {
            Color prev = GUI.color;
            // Deja ver la cueva/arena de fondo entre los paneles, como la referencia.
            GUI.color = new Color(0.012f, 0.022f, 0.038f, 0.8f);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            GUI.color = prev;
            HudTheme.Vineta();
            HudTheme.Linea(new Rect(0f, 59f, w, 1f), new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.22f));
            HudTheme.Linea(new Rect(0f, h - 47f, w, 1f), new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.16f));
        }

        private void DibujarTitulo(float x, float ancho)
        {
            GUI.Label(new Rect(x, 14f, ancho, 24f), "DUELO · TORNEO",
                HudTheme.Estilo(18, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Oro));
            string sub = "RONDA " + _torneo.Ronda + " DE " + _torneo.RondasTotales
                + "   ·   " + (_duelo.Terminado ? "RESULTADO" : "TURNO " + (_duelo.turno + 1));
            GUI.Label(new Rect(x, 38f, ancho, 16f), sub,
                HudTheme.Estilo(10, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Tenue));
            HudTheme.Linea(new Rect(x, 58f, Mathf.Min(180f, ancho), 2f),
                new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.6f));
        }

        private void DibujarBanner(float x, float y, float ancho, float alto)
        {
            Rect r = new Rect(x, y, ancho, alto);
            HudTheme.Chip(r);
            HudTheme.Esquinas(r, new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.8f), 10f, 1.5f);
            string txt = _duelo.Terminado
                ? "DUELO TERMINADO"
                : "TURNO " + (_duelo.turno + 1).ToString("00") + "   ·   TU TURNO";
            GUI.Label(r, txt, HudTheme.Estilo(18, FontStyle.Bold, TextAnchor.MiddleCenter, HudTheme.OroClaro));
        }

        /// Orden de acciones como en la referencia: el turno del jugador, la respuesta IA y
        /// el siguiente turno. Los colores acompanan para no depender solo del tono.
        private void DibujarOrden(float x, float y, float ancho)
        {
            const float gap = 6f;
            float anchoChip = (ancho - gap * 2f) / 3f;
            string[] etiquetas = { "1   TÚ", "2   RIVAL IA", "3   TÚ" };
            Color[] colores = {
                HudTheme.OroClaro,
                new Color(1f, 0.62f, 0.55f),
                new Color(0.56f, 0.66f, 0.75f)
            };
            for (int i = 0; i < 3; i++)
            {
                Rect r = new Rect(x + i * (anchoChip + gap), y, anchoChip, 24f);
                HudTheme.Chip(r);
                if (i == 0)
                    HudTheme.Esquinas(r, new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.9f), 8f, 1.5f);
                GUI.Label(r, etiquetas[i], HudTheme.Estilo(10, FontStyle.Bold, TextAnchor.MiddleCenter, colores[i]));
            }
        }

        private void DibujarCombatiente(Rect r, Combatiente c, bool jugador)
        {
            Color acento = jugador ? HudTheme.Cian : new Color(1f, 0.48f, 0.4f);
            HudTheme.Panel(r);
            HudTheme.Esquinas(r, new Color(acento.r, acento.g, acento.b, 0.6f), 12f, 1.5f);

            Color colorElem = c != null && c.datos != null ? c.datos.elementColor : acento;
            Retrato(new Rect(r.x + 10f, r.y + 17f, 64f, 64f), c, colorElem);

            GUI.Label(new Rect(r.x + 84f, r.y + 8f, r.width - 94f, 14f),
                jugador ? "TU BABOSA · ACTÚA" : "RIVAL IA · RESPONDE",
                HudTheme.Estilo(9, FontStyle.Bold, TextAnchor.UpperLeft, acento));
            GUI.Label(new Rect(r.x + 84f, r.y + 21f, r.width - 94f, 20f),
                c != null ? c.Nombre : "-",
                HudTheme.Estilo(16, FontStyle.Bold, TextAnchor.UpperLeft,
                    c != null && c.Vivo ? HudTheme.Texto : HudTheme.Tenue));
            GUI.Label(new Rect(r.x + 84f, r.y + 40f, r.width - 94f, 14f),
                c != null ? c.Elemento.ToString().ToUpperInvariant() : "SIN DATOS",
                HudTheme.Estilo(9, FontStyle.Bold, TextAnchor.UpperLeft, colorElem));

            float pct = c != null && c.hpMax > 0f ? Mathf.Clamp01(c.hp / c.hpMax) : 0f;
            HudTheme.Barra(new Rect(r.x + 84f, r.y + 55f, r.width - 94f, 9f), pct,
                jugador ? HudTheme.Verde : new Color(0.92f, 0.38f, 0.34f));

            string vida = c == null ? "-"
                : (c.Vivo ? "VIDA " + Mathf.CeilToInt(c.hp) + " / " + Mathf.CeilToInt(c.hpMax) : "FUERA DE COMBATE");
            if (c != null && c.Vivo && c.escudo > 0f) vida += "   ESC " + Mathf.CeilToInt(c.escudo);
            if (c != null && c.Vivo && c.quemaduraTurnos > 0) vida += "   ARDE";
            if (c != null && c.Vivo && c.congeladoTurnos > 0) vida += "   " + c.Estado.ToUpperInvariant();
            GUI.Label(new Rect(r.x + 84f, r.y + 68f, r.width - 94f, 16f), vida,
                HudTheme.Estilo(9, FontStyle.Normal, TextAnchor.UpperLeft, HudTheme.Tenue));
        }

        private void DibujarMiniEquipo(Rect r, List<Combatiente> equipo, int activo)
        {
            GUI.Label(new Rect(r.x, r.y - 13f, r.width, 12f), "SU EQUIPO",
                HudTheme.Estilo(9, FontStyle.Bold, TextAnchor.UpperLeft,
                    new Color(1f, 0.62f, 0.55f)));
            const float gap = 6f;
            float ancho = (r.width - gap * 2f) / 3f;
            for (int i = 0; i < equipo.Count && i < 3; i++)
            {
                Combatiente c = equipo[i];
                Rect carta = new Rect(r.x + i * (ancho + gap), r.y, ancho, r.height);
                HudTheme.Chip(carta);
                if (i == activo && c.Vivo)
                    HudTheme.Esquinas(carta, new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.85f), 7f, 1.5f);

                Color acento = c.datos != null ? c.datos.elementColor : HudTheme.Tenue;
                Retrato(new Rect(carta.x + 4f, carta.y + 9f, 34f, 34f), c, acento);
                GUI.Label(new Rect(carta.x + 42f, carta.y + 7f, carta.width - 47f, 14f), c.Nombre,
                    HudTheme.Estilo(9, FontStyle.Bold, TextAnchor.UpperLeft,
                        c.Vivo ? HudTheme.Texto : HudTheme.Tenue));
                float pct = c.hpMax > 0f ? Mathf.Clamp01(c.hp / c.hpMax) : 0f;
                HudTheme.Barra(new Rect(carta.x + 42f, carta.y + 25f, carta.width - 48f, 5f), pct,
                    new Color(0.92f, 0.38f, 0.34f));
                GUI.Label(new Rect(carta.x + 42f, carta.y + 33f, carta.width - 47f, 13f),
                    c.Vivo ? Mathf.CeilToInt(c.hp) + "/" + Mathf.CeilToInt(c.hpMax) : "FUERA",
                    HudTheme.Estilo(8, FontStyle.Normal, TextAnchor.UpperLeft, HudTheme.Tenue));
            }
        }

        private void DibujarCentro(Rect r)
        {
            HudTheme.Panel(r);
            HudTheme.Esquinas(r, new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.42f), 12f, 1.5f);
            HudTheme.Linea(new Rect(r.x, r.y, r.width, 2f),
                new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.8f));

            GUI.Label(new Rect(r.x + 14f, r.y + 10f, r.width - 28f, 16f),
                _duelo.Terminado ? "DUELO TERMINADO" : "TU TURNO",
                HudTheme.Estilo(11, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Oro));
            GUI.Label(new Rect(r.x + 14f, r.y + 28f, r.width - 28f, 22f),
                "Actúa: " + _duelo.ActivoJ.Nombre,
                HudTheme.Estilo(16, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Texto));
            GUI.Label(new Rect(r.x + 14f, r.y + 51f, r.width - 28f, 16f),
                "Responde: " + _duelo.ActivoR.Nombre,
                HudTheme.Estilo(10, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 0.62f, 0.55f)));

            SlugData d = _duelo.ActivoJ.datos;
            string efecto = d != null ? Duelo.NombreEfecto(d.abilityType) : "sin habilidad";
            string texto = "EFECTO   " + efecto + "\nLÍMITE   una vez cada "
                + Duelo.EnfriamientoHabilidad + " turnos";
            if (d != null && Contrajuego.Tiene(d.abilityType))
                texto += "\nCONTRAJUEGO   " + Contrajuego.De(d.abilityType).texto;
            if (d != null && !string.IsNullOrEmpty(d.limitacion))
                texto += "\nEN EL CAMPO   " + d.limitacion;

            float yEfecto = r.y + 72f;
            float altoEfecto = Mathf.Clamp(r.height * 0.32f, 54f, 110f);
            GUI.Label(new Rect(r.x + 14f, yEfecto, r.width - 28f, altoEfecto), texto,
                Envolver(10, new Color(0.74f, 0.81f, 0.88f)));

            float yLog = yEfecto + altoEfecto + 6f;
            GUI.Label(new Rect(r.x + 14f, yLog, r.width - 28f, 16f), "ÚLTIMOS MOVIMIENTOS",
                HudTheme.Estilo(9, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.CianTenue));
            int capacidad = Mathf.Clamp(Mathf.FloorToInt((r.y + r.height - 14f - (yLog + 18f)) / 16f), 2, 12);
            int desde = Mathf.Max(0, _duelo.registro.Count - capacidad);
            for (int i = desde; i < _duelo.registro.Count; i++)
            {
                string linea = _duelo.registro[i];
                Color color = new Color(0.80f, 0.85f, 0.9f);
                if (linea.StartsWith("Tu ")) color = new Color(0.62f, 0.90f, 0.74f);
                else if (linea.StartsWith("El rival")) color = new Color(0.95f, 0.68f, 0.62f);
                else if (linea.StartsWith("Ganaste")) color = new Color(0.55f, 0.95f, 0.72f);
                else if (linea.StartsWith("Perdiste")) color = new Color(0.97f, 0.55f, 0.50f);
                GUI.Label(new Rect(r.x + 14f, yLog + 18f + (i - desde) * 16f, r.width - 28f, 15f),
                    "› " + linea, HudTheme.Estilo(9, FontStyle.Normal, TextAnchor.UpperLeft, color));
            }
        }

        private void DibujarEquipo(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y - 20f, r.width, 16f),
                _eligiendoCambio ? "TU EQUIPO   ·   ELEGÍ EL RELEVO ABAJO" : "TU EQUIPO",
                HudTheme.Estilo(11, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Oro));

            const float gap = 8f;
            float ancho = (r.width - gap * 2f) / 3f;
            for (int i = 0; i < _duelo.jugador.Count && i < 3; i++)
            {
                Combatiente c = _duelo.jugador[i];
                Rect carta = new Rect(r.x + i * (ancho + gap), r.y, ancho, r.height);
                bool activo = i == _duelo.activoJugador;
                HudTheme.Panel(carta);
                if (activo && c.Vivo)
                    HudTheme.Esquinas(carta, new Color(HudTheme.Oro.r, HudTheme.Oro.g, HudTheme.Oro.b, 0.9f), 11f, 1.8f);
                Color acento = c.datos != null ? c.datos.elementColor : HudTheme.Cian;
                HudTheme.Linea(new Rect(carta.x + 1f, carta.y + 1f, carta.width - 2f, 2f),
                    new Color(acento.r, acento.g, acento.b, c.Vivo ? 0.9f : 0.25f));

                Retrato(new Rect(carta.x + 10f, carta.y + 28f, 64f, 64f), c, acento);

                GUI.Label(new Rect(carta.x + 82f, carta.y + 10f, carta.width - 92f, 18f), c.Nombre,
                    HudTheme.Estilo(14, FontStyle.Bold, TextAnchor.UpperLeft,
                        c.Vivo ? HudTheme.Texto : HudTheme.Tenue));
                GUI.Label(new Rect(carta.x + 82f, carta.y + 30f, carta.width - 92f, 13f),
                    (activo ? "ACTIVA" : "EN ESPERA") + "   ·   " + c.Elemento.ToString().ToUpperInvariant(),
                    HudTheme.Estilo(8, FontStyle.Bold, TextAnchor.UpperLeft,
                        activo && c.Vivo ? HudTheme.OroClaro : acento));

                float pct = c.hpMax > 0f ? Mathf.Clamp01(c.hp / c.hpMax) : 0f;
                HudTheme.Barra(new Rect(carta.x + 82f, carta.y + 51f, carta.width - 92f, 8f), pct,
                    c.Vivo ? HudTheme.Verde : new Color(0.4f, 0.42f, 0.46f));
                GUI.Label(new Rect(carta.x + 82f, carta.y + 63f, carta.width - 92f, 14f),
                    c.Vivo ? "VIDA " + Mathf.CeilToInt(c.hp) + " / " + Mathf.CeilToInt(c.hpMax) : "FUERA",
                    HudTheme.Estilo(9, FontStyle.Normal, TextAnchor.UpperLeft, HudTheme.Tenue));
                if (c.Vivo && c.escudo > 0f)
                    GUI.Label(new Rect(carta.x + 82f, carta.y + 78f, carta.width - 92f, 13f),
                        "ESCUDO " + Mathf.CeilToInt(c.escudo),
                        HudTheme.Estilo(8, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Cian));
            }
        }

        private void DibujarAcciones(Rect r)
        {
            HudTheme.Panel(r);
            HudTheme.Esquinas(r, new Color(HudTheme.Cian.r, HudTheme.Cian.g, HudTheme.Cian.b, 0.42f), 11f, 1.5f);

            if (_eligiendoCambio)
            {
                GUI.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, 16f), "ELIGE EL RELEVO",
                    HudTheme.Estilo(11, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Oro));
                const float gap = 6f;
                float ancho = (r.width - 28f - gap * 2f) / 3f;
                for (int i = 0; i < 3; i++)
                {
                    Combatiente c = _duelo.jugador[i];
                    bool puede = c.Vivo && i != _duelo.activoJugador;
                    Rect b = new Rect(r.x + 14f + i * (ancho + gap), r.y + 30f, ancho, 40f);
                    if (puede)
                    {
                        if (HudTheme.BotonChico(b, c.Nombre))
                        {
                            _eligiendoCambio = false;
                            Turno(AccionDuelo.Cambiar, i);
                        }
                    }
                    else Deshabilitado(b, c.Nombre);
                }
                if (HudTheme.Boton(new Rect(r.x + 14f, r.y + 78f, r.width - 28f, 34f), "CANCELAR"))
                    _eligiendoCambio = false;
                return;
            }

            GUI.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, 16f), "ACCIONES",
                HudTheme.Estilo(11, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Oro));

            bool puedeActuar = !_duelo.Terminado && _duelo.ActivoJ.Vivo;
            if (puedeActuar)
            {
                if (HudTheme.BotonOro(new Rect(r.x + 14f, r.y + 30f, r.width - 28f, 42f), "ATACAR"))
                    Turno(AccionDuelo.Atacar);
            }
            else Deshabilitado(new Rect(r.x + 14f, r.y + 30f, r.width - 28f, 42f), "ATACAR");

            float mitad = (r.width - 28f - 8f) * 0.5f;
            Rect hab = new Rect(r.x + 14f, r.y + 80f, mitad, 34f);
            bool puedeHabilidad = puedeActuar && _duelo.ActivoJ.enfriamiento == 0;
            string etiquetaH = _duelo.ActivoJ.enfriamiento > 0
                ? "HABILIDAD · " + _duelo.ActivoJ.enfriamiento
                : "HABILIDAD";
            if (puedeHabilidad)
            {
                if (HudTheme.Boton(hab, etiquetaH)) Turno(AccionDuelo.Habilidad);
            }
            else Deshabilitado(hab, etiquetaH);

            Rect cambiar = new Rect(r.x + 14f + mitad + 8f, r.y + 80f, mitad, 34f);
            if (puedeActuar)
            {
                if (HudTheme.Boton(cambiar, "CAMBIAR")) _eligiendoCambio = true;
            }
            else Deshabilitado(cambiar, "CAMBIAR");
        }

        /// Si el roster no tiene sprite, `LoadSprite` devuelve un cuadro liso de 8x8. En el
        /// duelo eso se ve como un hueco gris; aca se dibuja un emblema con la inicial y el
        /// elemento, sin inventar retratos que el juego no tiene.
        private static void Retrato(Rect r, Combatiente c, Color acento)
        {
            Texture2D tex = c != null && c.datos != null ? SlugArenaGame.LoadSprite(c.datos, true) : null;
            bool real = tex != null && !tex.name.StartsWith("retrato_");
            if (real)
            {
                HudTheme.Retrato(r, tex, acento);
                return;
            }

            HudTheme.Chip(r);
            Color prev = GUI.color;
            GUI.color = new Color(acento.r, acento.g, acento.b, 0.22f);
            GUI.DrawTexture(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), Texture2D.whiteTexture);
            GUI.color = prev;
            HudTheme.Esquinas(r, new Color(acento.r, acento.g, acento.b, 0.85f),
                Mathf.Min(10f, r.height * 0.25f), 1.5f);
            string inicial = c != null && c.datos != null && !string.IsNullOrEmpty(c.datos.slugName)
                ? c.datos.slugName.Substring(0, 1).ToUpperInvariant() : "?";
            GUI.Label(r, inicial, HudTheme.Estilo(Mathf.Max(12, Mathf.RoundToInt(r.height * 0.42f)),
                FontStyle.Bold, TextAnchor.MiddleCenter, acento));
            if (r.height >= 50f)
                GUI.Label(new Rect(r.x, r.yMax - 14f, r.width, 12f),
                    c != null ? c.Elemento.ToString().ToUpperInvariant() : "",
                    HudTheme.Estilo(8, FontStyle.Bold, TextAnchor.MiddleCenter,
                        new Color(acento.r, acento.g, acento.b, 0.9f)));
        }

        private static void Deshabilitado(Rect r, string texto)
        {
            HudTheme.Chip(r);
            GUI.Label(r, texto, HudTheme.Estilo(12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.46f, 0.5f, 0.56f)));
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
            float alto = 218f;
            Rect panel = new Rect((w - ancho) * 0.5f, (h - alto) * 0.5f, ancho, alto);
            Color acento = gano ? HudTheme.Verde : HudTheme.Rojo;

            HudTheme.Panel(panel);
            HudTheme.Esquinas(panel, acento, 16f, 2f);
            HudTheme.Linea(new Rect(panel.x, panel.y, panel.width, 3f), acento);

            GUI.Label(new Rect(panel.x, panel.y + 16f, panel.width, 36f),
                gano ? "GANASTE EL DUELO" : "PERDISTE EL DUELO",
                HudTheme.Estilo(24, FontStyle.Bold, TextAnchor.MiddleCenter, acento));

            string detalle;
            if (gano && ultima) detalle = "Torneo completado: ganaste las " + _torneo.RondasTotales + " rondas.";
            else if (gano) detalle = "Ronda " + _torneo.Ronda + " superada. Siguiente: ronda " + (_torneo.Ronda + 1) + ".";
            else detalle = "El torneo termina en la ronda " + _torneo.Ronda + ". Podes armar otro equipo.";
            GUI.Label(new Rect(panel.x + 24f, panel.y + 60f, panel.width - 48f, 42f), detalle,
                HudTheme.Estilo(12, FontStyle.Normal, TextAnchor.UpperCenter, HudTheme.Texto));

            float bw = (panel.width - 56f) * 0.5f;
            float by = panel.y + panel.height - 64f;
            if (gano && !ultima)
            {
                if (HudTheme.BotonOro(new Rect(panel.x + 20f, by, bw, 40f), "SIGUIENTE RONDA"))
                {
                    _torneo.Continuar();
                    _duelo = _torneo.DueloActual;
                    _eligiendoCambio = false;
                }
            }
            else
            {
                if (HudTheme.BotonOro(new Rect(panel.x + 20f, by, bw, 40f), "NUEVO TORNEO"))
                    EmpezarTorneo(Random.Range(1, 999999));
            }

            if (HudTheme.Boton(new Rect(panel.x + 36f + bw, by, bw, 40f), "VOLVER AL MENU"))
                Cerrar();
        }

        private static GUIStyle _envolver;

        private static GUIStyle Envolver(int tam, Color color)
        {
            if (_envolver == null)
            {
                _envolver = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    richText = false,
                    clipping = TextClipping.Clip
                };
                _envolver.font = HudTheme.Fuente;
            }
            _envolver.fontSize = tam;
            _envolver.normal.textColor = color;
            _envolver.alignment = TextAnchor.UpperLeft;
            return _envolver;
        }
    }
}
