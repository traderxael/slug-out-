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
        private float _vidaRetrasada = 1f;

        // OnGUI crea diez estilos por cuadro y GUI se llama varias veces por frame: era la fuente
        // de GC mas grande del HUD. Se crean una sola vez y por frame solo se toca el color, que es
        // lo unico que cambia de verdad. Si alguien mueve `fuente` en el Inspector habria que
        // invalidar: es un valor de diseno, no de partida.
        private GUIStyle _stVida, _stSombra, _stEstado, _stColeccion, _stAyuda, _stHabilidad;
        private GUIStyle _stResultado, _stTubos, _stCarga, _stAnillo, _stMini, _stMiniPie;
        private RegionCuevaSeca _region;
        private Transform _mFuego, _mAgua, _mCristal, _mTunel;
        private BabosaSalvaje[] _salvajes;

        private GUIStyle Etiqueta(ref GUIStyle campo, int tam, FontStyle estilo, TextAnchor ancla)
        {
            if (campo == null)
            {
                campo = new GUIStyle(GUI.skin.label);
                campo.fontSize = tam;
                campo.fontStyle = estilo;
                campo.alignment = ancla;
            }
            return campo;
        }

        private void Start()
        {
            if (jugador == null)
            {
                PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
                if (pc != null) jugador = pc.transform;
            }
            if (juego == null) juego = GetComponent<SlugArenaGame>();
        }

        private void OnGUI()
        {
            if (!mostrar) return;
            // Con el menu abierto el HUD de partida no tiene nada que decir y se dibujaba
            // encima del menu nuevo: vida, tubos y matchup sobre la portada.
            if (GameFlow.Activo != null && GameFlow.Activo.EnMenu) return;
            // El torneo pausa la partida y dibuja su propia pantalla; el HUD de exploracion
            // no tiene que quedar debajo.
            if (TorneoUI.Activo) return;
            if (Actor() == null) return;
            GUI.depth = 0;   // por encima del banner de historia (depth 10)

            DibujarMatchups();
            DibujarAnillo();
            DibujarMinimapa();

            float hp = Actor().Hp;
            float max = Actor().MaxHp;
            float pct = max > 0f ? Mathf.Clamp01(hp / max) : 0f;

            float ancho = Mathf.Min(520f, Screen.width * 0.5f);
            float alto = 30f;
            float x = 24f;
            float y = Screen.height - alto - 56f;   // deja lugar a la ayuda y a los tubos de abajo

            // retrato del jugador: su sprite oficial a la izquierda de la barra de vida
            if (Actor() != null && Actor().Data != null)
            {
                Texture2D retrato = SlugArenaGame.LoadSprite(Actor().Data, true);
                if (retrato != null)
                {
                    GUI.DrawTexture(new Rect(x, y - 62f, 90f, 90f), retrato, ScaleMode.ScaleToFit);
                    x += 102f;
                }
            }

            Rect fondo = new Rect(x, y, ancho, alto);

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(fondo, Texture2D.whiteTexture);
            GUI.color = prev;

            Color vida = pct > 0.5f ? new Color(0.25f, 0.85f, 0.45f) : (pct > 0.25f ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.95f, 0.3f, 0.25f));

            // barra de dano retrasada: el tramo perdido queda blanco y se encoge despues
            if (Event.current.type == EventType.Repaint)
                _vidaRetrasada = pct > _vidaRetrasada ? pct : Mathf.MoveTowards(_vidaRetrasada, pct, Time.unscaledDeltaTime * 0.6f);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(fondo.x, fondo.y, fondo.width * _vidaRetrasada, fondo.height), Texture2D.whiteTexture);
            GUI.color = Actor().Invulnerable && !GameFlow.ReducirDestellos
                && Mathf.Repeat(Time.unscaledTime * 12f, 1f) < 0.5f ? Color.white : vida;
            GUI.DrawTexture(new Rect(fondo.x, fondo.y, fondo.width * pct, fondo.height), Texture2D.whiteTexture);
            GUI.color = prev;

            GUIStyle st = Etiqueta(ref _stVida, Mathf.Max(16, fuente - 6), FontStyle.Bold, TextAnchor.MiddleLeft);
            st.normal.textColor = Color.white;
            GUIStyle sombra = Etiqueta(ref _stSombra, Mathf.Max(16, fuente - 6), FontStyle.Bold, TextAnchor.MiddleLeft);
            sombra.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            string textoVida = "VIDA  " + Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(max);
            GUI.Label(new Rect(x + 10f, y + 1f, ancho - 16f, alto), textoVida, sombra);
            GUI.Label(new Rect(x + 8f, y - 1f, ancho - 16f, alto), textoVida, st);

            GUIStyle st2 = Etiqueta(ref _stEstado, Mathf.Max(13, fuente - 14), FontStyle.Bold, TextAnchor.UpperLeft);
            st2.normal.textColor = new Color(0.95f, 0.95f, 0.97f);

            string estado = "EN CURSO";
            if (juego != null && juego.outcome == "GANASTE") estado = "GANASTE";
            if (juego != null && juego.outcome == "PERDISTE") estado = "PERDISTE";

            // En infinito el total es int.MaxValue (el modo no se acaba): imprimirlo tal cual
            // daria "0/2147483647". Ahi se muestra la oleada, que es lo que el modo mide.
            string cuenta = juego == null ? "CAPTURADAS -"
                : (juego.Infinito ? "CAPTURADAS " + juego.Capturadas + "   OLEADA " + juego.OlaActual
                                  : "CAPTURADAS " + juego.Capturadas + "/" + juego.TotalEnemies);
            GUI.Label(new Rect(x + 8f, y - 87f, ancho, 24f),
                cuenta
                + (juego != null ? "   ENEMIGOS " + juego.Alive + "/" + juego.Spawned : "")
                + (juego != null && juego.GoalCapturas > 0 ? "   OBJETIVO " + juego.Capturadas + "/" + juego.GoalCapturas : ""),
                st2);
            GUI.Label(new Rect(x + ancho + 12f, y - 87f, 180f, 24f), estado, st2);
            GUIStyle st4 = Etiqueta(ref _stColeccion, Mathf.Max(13, fuente - 10), FontStyle.Normal, TextAnchor.UpperLeft);
            st4.normal.textColor = new Color(0.85f, 0.88f, 0.6f);
            GUI.Label(new Rect(x + 8f, y - 112f, ancho * 1.6f, 22f),
                (juego != null && juego.ColeccionTexto.Length > 0 ? "COLECCION: " + juego.ColeccionTexto
                    : (GameFlow.Activo != null && GameFlow.Activo.EnMenu ? "COLECCION: " + GameFlow.ColeccionGuardada() : "COLECCION: (vacia)")), st4);

            GUIStyle st3 = Etiqueta(ref _stAyuda, Mathf.Max(11, fuente - 17), FontStyle.Normal, TextAnchor.UpperLeft);
            st3.normal.textColor = new Color(0.72f, 0.75f, 0.8f);
            if (_hab == null && Actor() != null) _hab = Actor().GetComponent<PlayerAbilities>();
            if (_hab != null && Actor().Data != null)
            {
                float cd = _hab.CooldownRestante;
                float ePct = Mathf.Clamp01(Actor().Energia / Mathf.Max(Actor().EnergiaMax, 1f));
                GUIStyle stH = Etiqueta(ref _stHabilidad, Mathf.Max(14, fuente - 13), FontStyle.Bold, TextAnchor.UpperLeft);
                stH.normal.textColor = cd <= 0f ? new Color(0.55f, 0.95f, 0.6f) : new Color(0.6f, 0.62f, 0.68f);
                string efecto = Duelo.NombreEfecto(Actor().Data.abilityType);
                GUI.Label(new Rect(x + 8f, y - 65f, ancho - 128f, 24f),
                    "EQUIPADA  " + Actor().Data.slugName + "  ·  " + efecto
                    + (cd > 0f ? "  " + cd.ToString("F1") + "s" : ""), stH);
                Rect energia = new Rect(x + ancho - 116f, y - 62f, 104f, 7f);
                GUI.color = new Color(0.1f, 0.14f, 0.18f, 0.95f);
                GUI.DrawTexture(energia, Texture2D.whiteTexture);
                GUI.color = new Color(0.2f, 0.75f, 0.9f);
                GUI.DrawTexture(new Rect(energia.x, energia.y, energia.width * ePct, energia.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            PlayerShooter sh = Shooter();
            DibujarCarga(sh, x, y, ancho, prev);
            DibujarTubos(sh, prev);

            GUI.Label(new Rect(24f, Screen.height - 22f, 320f, 18f), "WASD · ESPACIO carga · Q habilidad · TAB ruta", st3);

            DibujarRacha(prev);
            DibujarResultado();
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
            }
            float restante = a.VentanaCombo * a.ComboRestante;
            _estiloCombo.fontSize = Mathf.Max(17, fuente - 5);
            _estiloCombo.normal.textColor = new Color(1f, 0.84f, 0.42f, 0.55f + 0.45f * a.ComboRestante);
            float bx = Screen.width - 250f, by = Screen.height - 150f;
            GUI.Label(new Rect(bx, by, 234f, 26f), a.Combo + "x  ·  " + restante.ToString("F1") + "s", _estiloCombo);
            GUI.color = new Color(1f, 0.84f, 0.42f, 0.9f);
            GUI.DrawTexture(new Rect(bx + 234f - 234f * a.ComboRestante, by + 28f, 234f * a.ComboRestante, 3f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// Sin esto la partida termina en un texto: Reintentar() y SiguienteNivel() no tenían
        /// ni un llamador y había que reiniciar la escena para volver a jugar.
        private void DibujarResultado()
        {
            SlugArenaGame j = juego != null ? juego : GetComponent<SlugArenaGame>();
            if (j == null || !j.Termino) return;

            bool gano = j.outcome == "GANASTE";
            float w = Mathf.Min(520f, Screen.width * 0.7f);
            float hgt = 150f;
            float x = (Screen.width - w) * 0.5f;
            float y = Mathf.Max(80f, Screen.height * 0.32f);
            Rect caja = new Rect(x, y, w, hgt);

            Color prev = GUI.color;
            GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.95f);
            GUI.DrawTexture(caja, Texture2D.whiteTexture);
            GUI.color = gano ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.9f, 0.35f, 0.35f);
            GUI.DrawTexture(new Rect(caja.x, caja.y, caja.width, 4f), Texture2D.whiteTexture);
            GUI.color = prev;

            GUIStyle st = Etiqueta(ref _stResultado, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            st.normal.textColor = gano ? new Color(0.55f, 0.95f, 0.62f) : new Color(0.98f, 0.6f, 0.6f);
            GUI.Label(new Rect(x, y + 12f, w, 34f), j.outcome, st);

            if (_estiloCombo == null)
            {
                _estiloCombo = new GUIStyle(GUI.skin.label);
                _estiloCombo.alignment = TextAnchor.MiddleCenter;
                _estiloCombo.fontStyle = FontStyle.Bold;
                _estiloCombo.fontSize = 15;
            }
            SlugActor actorResultado = Actor();
            if (actorResultado != null)
            {
                _estiloCombo.normal.textColor = new Color(1f, 0.84f, 0.42f);
                GUI.Label(new Rect(x, y + 36f, w, 20f),
                    "RACHA MAXIMA  " + actorResultado.ComboMaximo + "x", _estiloCombo);
            }

            GameFlow gf = GameFlow.Activo;
            float by = y + 58f, bh = 38f, bw = (w - 48f) / 3f;
            if (gf != null)
            {
                if (GameFlow.BotonImgui(new Rect(x + 16f, by, bw, bh), "REINTENTAR")) gf.Reintentar();
                bool puedeSiguiente = gano && !gf.Infinito && gf.Nivel < SlugStory.TotalCampana;
                Rect rSig = new Rect(x + 16f + bw + 8f, by, bw, bh);
                if (puedeSiguiente && GameFlow.BotonImgui(rSig, "SIGUIENTE NIVEL")) gf.SiguienteNivel();
                else if (!puedeSiguiente)
                {
                    Color p2 = GUI.color; GUI.color = new Color(0.4f, 0.42f, 0.46f, 0.5f);
                    GUI.DrawTexture(rSig, Texture2D.whiteTexture); GUI.color = p2;
                }
                if (GameFlow.BotonImgui(new Rect(x + 16f + 2f * (bw + 8f), by, bw, bh), "MENU")) gf.VolverAlMenu();
            }
        }

        /// Radar de la region: jugador y rumbo, tres microhabitats, tunel, salvajes y enemigos.
        /// Es un panel fijo arriba a la derecha; no tapa la vida ni las acciones.
        private void DibujarMinimapa()
        {
            CacheMapa();
            if (_region == null || _region.Root == null) return;
            Transform p = jugador != null ? jugador : (Actor() != null ? Actor().transform : null);
            if (p == null) return;

            float lado = 168f;
            Rect marco = new Rect(Screen.width - lado - 24f, 24f, lado, lado + 26f);
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.9f);
            GUI.DrawTexture(marco, Texture2D.whiteTexture);
            GUI.color = new Color(0.5f, 0.55f, 0.62f, 0.35f);
            GUI.DrawTexture(new Rect(marco.x, marco.y, marco.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(marco.x, marco.y + marco.height - 1f, marco.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(marco.x, marco.y, 1f, marco.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(marco.x + marco.width - 1f, marco.y, 1f, marco.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.73f, 0.33f, 0.85f);
            GUI.DrawTexture(new Rect(marco.x, marco.y, marco.width, 2f), Texture2D.whiteTexture);
            GUI.color = prev;

            float cx = marco.x + lado * 0.5f;
            float cy = marco.y + lado * 0.5f + 6f;
            float radioMapa = lado * 0.5f - 18f;
            float escala = radioMapa / 30f;   // 30 unidades de mundo alrededor del jugador
            GUIStyle mini = Etiqueta(ref _stMini, 9, FontStyle.Bold, TextAnchor.MiddleCenter);
            mini.normal.textColor = new Color(0.85f, 0.90f, 0.96f);
            GUI.Label(new Rect(cx - 8f, marco.y + 4f, 16f, 14f), "N", mini);

            GUI.color = new Color(1f, 1f, 1f, 0.05f);
            GUI.DrawTexture(new Rect(cx, marco.y + 22f, 1f, lado - 28f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(marco.x + 12f, cy, lado - 24f, 1f), Texture2D.whiteTexture);
            GUI.color = prev;

            MarcarMapa(_mFuego, "F", new Color(1f, 0.55f, 0.2f), p.position, cx, cy, escala, radioMapa);
            MarcarMapa(_mAgua, "A", new Color(0.3f, 0.8f, 0.9f), p.position, cx, cy, escala, radioMapa);
            MarcarMapa(_mCristal, "C", new Color(0.6f, 0.5f, 1f), p.position, cx, cy, escala, radioMapa);
            MarcarMapa(_mTunel, "T", new Color(1f, 0.78f, 0.35f), p.position, cx, cy, escala, radioMapa);

            if (_salvajes != null)
            {
                for (int i = 0; i < _salvajes.Length; i++)
                {
                    if (_salvajes[i] == null) continue;
                    PuntoMapa(_salvajes[i].transform.position, new Color(0.4f, 0.95f, 0.95f),
                        p.position, cx, cy, escala, radioMapa, 4f);
                }
            }

            System.Collections.Generic.IReadOnlyList<SlugActor> vivos = SlugActor.Vivos;
            for (int i = 0; i < vivos.Count; i++)
            {
                SlugActor a = vivos[i];
                if (a == null || a.IsPlayer || !a.Alive || a.Capturada) continue;
                PuntoMapa(a.transform.position, new Color(0.95f, 0.35f, 0.3f),
                    p.position, cx, cy, escala, radioMapa, 4f);
            }

            float angulo = Mathf.Atan2(p.forward.x, p.forward.z) * Mathf.Rad2Deg;
            Matrix4x4 matriz = GUI.matrix;
            GUIUtility.RotateAroundPivot(angulo, new Vector2(cx, cy));
            GUI.color = new Color(1f, 0.9f, 0.45f, 0.95f);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 13f, 2f, 13f), Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.matrix = matriz;
            GUI.color = new Color(1f, 0.9f, 0.45f, 1f);
            GUI.DrawTexture(new Rect(cx - 4f, cy - 4f, 8f, 8f), Texture2D.whiteTexture);
            GUI.color = prev;

            GUIStyle leyenda = Etiqueta(ref _stMiniPie, 8, FontStyle.Normal, TextAnchor.MiddleCenter);
            leyenda.normal.textColor = new Color(0.62f, 0.67f, 0.74f);
            GUI.Label(new Rect(marco.x + 6f, marco.y + lado + 2f, marco.width - 12f, 16f),
                "F fuego  A agua  C cristal  T tunel", leyenda);
        }

        private void CacheMapa()
        {
            if (_region == null) _region = Object.FindFirstObjectByType<RegionCuevaSeca>();
            if (_region == null || _region.Root == null) return;
            if (_mFuego == null) _mFuego = _region.Root.Find("Microhabitat_Fumarolas");
            if (_mAgua == null) _mAgua = _region.Root.Find("Microhabitat_PozoHumedo");
            if (_mCristal == null) _mCristal = _region.Root.Find("Microhabitat_Cristales");
            if (_mTunel == null)
                _mTunel = _region.Root.Find("Tunel_" + RegionCuevaSeca.DestinoTunel.Replace(' ', '_'));
            if (_salvajes == null) _salvajes = Object.FindObjectsByType<BabosaSalvaje>(FindObjectsSortMode.None);
        }

        private void MarcarMapa(Transform t, string letra, Color color, Vector3 origen,
                                float cx, float cy, float escala, float radio)
        {
            if (t == null) return;
            Vector3 d = t.position - origen;
            float x = Mathf.Clamp(d.x * escala, -radio, radio);
            float z = Mathf.Clamp(d.z * escala, -radio, radio);
            Rect r = new Rect(cx + x - 7f, cy - z - 7f, 14f, 14f);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
            GUIStyle letraEstilo = Etiqueta(ref _stMini, 9, FontStyle.Bold, TextAnchor.MiddleCenter);
            letraEstilo.normal.textColor = new Color(0.05f, 0.05f, 0.07f);
            GUI.Label(r, letra, letraEstilo);
        }

        private void PuntoMapa(Vector3 mundo, Color color, Vector3 origen,
                               float cx, float cy, float escala, float radio, float tam)
        {
            Vector3 d = mundo - origen;
            float x = Mathf.Clamp(d.x * escala, -radio, radio);
            float z = Mathf.Clamp(d.z * escala, -radio, radio);
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(cx + x - tam * 0.5f, cy - z - tam * 0.5f, tam, tam),
                Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// Los tubos del blaster, abajo al centro: son el ritmo del combate.
        private void DibujarTubos(PlayerShooter s, Color prev)
        {
            if (s == null) return;
            int cap = s.TubosCapacidad;
            float tam = 20f, sep = 5f;
            float x0 = (Screen.width - (cap * (tam + sep) - sep)) * 0.5f;
            float y0 = Screen.height - 26f;
            for (int i = 0; i < cap; i++)
            {
                Rect r = new Rect(x0 + i * (tam + sep), y0, tam, 18f);
                GUI.color = new Color(0f, 0f, 0f, 0.65f);
                GUI.DrawTexture(new Rect(r.x - 1.5f, r.y - 1.5f, r.width + 3f, r.height + 3f), Texture2D.whiteTexture);
                bool lleno = i < s.Tubos;
                GUI.color = lleno ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.5f, 0.55f, 0.62f, 0.35f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                if (!lleno && i == s.Tubos)
                {
                    GUI.color = new Color(0.95f, 0.8f, 0.25f);
                    GUI.DrawTexture(new Rect(r.x, r.y + r.height * (1f - s.RecargaProgreso), r.width, r.height * s.RecargaProgreso), Texture2D.whiteTexture);
                }
            }
            GUI.color = prev;
            GUIStyle stT = Etiqueta(ref _stTubos, 13, FontStyle.Bold, TextAnchor.UpperCenter);
            stT.normal.textColor = s.SinTubos ? new Color(1f, 0.45f, 0.4f) : new Color(0.8f, 0.85f, 0.9f);
            GUI.Label(new Rect(x0 - 80f, y0 - 20f, cap * 25f + 160f, 18f),
                s.SinTubos ? "SIN TUBOS - recargando" : "TUBOS " + s.Tubos + "/" + cap, stT);
        }

        /// Barra de carga con la muesca del umbral: sin marca no hay forma de saber
        /// cuando el tiro va a transformarse.
        private void DibujarCarga(PlayerShooter s, float x, float y, float ancho, Color prev)
        {
            if (s == null) return;
            float carga = s.CargaProgreso;
            float muesca = s.MuescaActual();
            Rect barra = new Rect(x + 8f, y - 12f, ancho - 16f, 9f);

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(barra, Texture2D.whiteTexture);
            GUI.color = carga >= muesca ? new Color(1f, 0.78f, 0.2f) : new Color(0.5f, 0.56f, 0.64f);
            GUI.DrawTexture(new Rect(barra.x, barra.y, barra.width * carga, barra.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(barra.x + barra.width * muesca - 1.5f, barra.y - 4f, 3f, barra.height + 8f), Texture2D.whiteTexture);
            GUI.color = prev;

            GUIStyle stV = Etiqueta(ref _stCarga, Mathf.Max(12, fuente - 12), FontStyle.Bold, TextAnchor.UpperLeft);
            stV.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            GUI.Label(new Rect(barra.x, barra.y - 21f, barra.width + 60f, 20f),
                Mathf.RoundToInt(s.VelocidadActual * 2.23694f) + " MPH   ·   umbral " + Mathf.RoundToInt(s.UmbralMPH * 2.23694f) + " MPH", stV);
        }

        /// Sobre cada enemigo: cuanto le pega el jugador (tabla elemental) y un ! rojo si el
        /// enemigo le pega fuerte al jugador. El objetivo del auto-aim va resaltado.
        private void DibujarMatchups()
        {
            Camera cam = Camera.main;
            if (cam == null || !Actor().Alive) return;
            PlayerShooter sh = Shooter();
            SlugActor objetivo = sh != null ? sh.NearestEnemy() : null;
            if (_estiloMatchup == null)
            {
                _estiloMatchup = new GUIStyle(GUI.skin.label);
                _estiloMatchup.fontStyle = FontStyle.Bold;
                _estiloMatchup.alignment = TextAnchor.MiddleCenter;
                _estiloMatchup.richText = true;
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
                string flecha = ataque > 1f ? "\u25B2 " : (ataque < 1f ? "\u25BC " : "");
                string txt = "<color=" + color + ">" + flecha + "x" + ataque.ToString("0.##") + "</color>";
                if (defensa > 1f) txt += " <color=#FF4D4D>!</color>";
                bool esObjetivo = a == objetivo;
                if (esObjetivo) txt = "<color=#FFD54A>[</color> " + txt + " <color=#FFD54A>]</color>";

                _estiloMatchup.fontSize = esObjetivo ? 24 : 18;
                float y = Screen.height - sp.y;
                Rect r = new Rect(sp.x - 90f, y - 16f, 180f, 32f);
                Color prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), StripColor(txt), _estiloMatchup);
                GUI.color = prev;
                GUI.Label(r, txt, _estiloMatchup);
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
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(w * 0.5f - 268f, y - 4f, 536f, 32f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.42f, 0.28f, 1f);
            GUI.DrawTexture(new Rect(w * 0.5f - 268f, y - 4f, 4f, 32f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.62f, 0.42f);
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
