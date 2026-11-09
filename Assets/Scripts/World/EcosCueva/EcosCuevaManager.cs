using System.Collections.Generic;
using UnityEngine;

namespace SlugOut.World
{
    /// Menú de mejoras dentro del nivel (S1: ecos de la cueva).
    /// Cuando se cumple un disparador (una captura o cada 20 s) ofrece 3 ecos;
    /// elegir uno aplica el bono hasta el final del nivel y reanuda la partida.
    ///
    /// REGLA INNEGOCIABLE: este menú NUNCA puede dejar la partida congelada. Todo lo que lo
    /// controla va en tiempo REAL (Time.unscaledTime), no en tiempo de juego: así el
    /// auto-elegido funciona aunque el juego esté pausado, y si nadie elige (arnés de medida,
    /// pruebas, jugador distraido) el menú se cierra solo y el combate sigue.
    public class EcosCuevaManager : MonoBehaviour
    {
        public static EcosCuevaManager Instancia { get; private set; }

        [Header("Disparadores")]
        [SerializeField] private float segundosEntreOfertas = 20f;
        [Tooltip("También se ofrece tras cada captura, además del temporizador.")]
        [SerializeField] private bool ofrecerTrasCaptura = true;
        [Tooltip("Separación mínima (segundos reales) entre dos ofertas: evita que una ráfaga de capturas deje el nivel en pausa permanente.")]
        [SerializeField] private float segundosMinimosEntreOfertas = 12f;
        [Tooltip("Margen real para elegir a mano. Al agotarse se elige el primer eco: la partida nunca queda congelada.")]
        [SerializeField] private float margenAutoEleccion = 5f;

        /// True mientras el menú de ecos está abierto.
        public bool Activo { get; private set; }

        /// Oferta actual (3 ecos) para que el UI la dibuje.
        public EcoOferta[] OfertaActual { get; private set; }

        /// Segundos REALES que quedan para elegir a mano (0 si el menú está cerrado).
        public float TiempoRestante
        {
            get
            {
                if (!Activo) return 0f;
                return Mathf.Max(0f, margenAutoEleccion - (Time.unscaledTime - _abiertoEn));
            }
        }

        /// Margen real para elegir a mano (segundos). Se puede ajustar en caliente.
        public float MargenAutoEleccion
        {
            get { return margenAutoEleccion; }
            set { margenAutoEleccion = Mathf.Max(0.5f, value); }
        }

        /// Cuántas ofertas se han hecho en este nivel (diagnóstico).
        public int OfertasHechas { get; private set; }

        /// Nivel de un eco en el nivel en curso (0 si no se ha cogido nunca).
        public int NivelDeEco(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            int nivel;
            return _nivelEco.TryGetValue(id, out nivel) ? nivel : 0;
        }

        // Nivel alcanzado de cada eco dentro del nivel actual (se reinicia cada nivel).
        private readonly Dictionary<string, int> _nivelEco = new Dictionary<string, int>();
        private float _cronometro;      // segundos REALES desde la última oferta
        private float _ultimaOferta;    // Time.unscaledTime de la última oferta
        private float _abiertoEn;       // Time.unscaledTime al abrir el menú
        private bool _ofertaPendiente;
        private bool _nivelVigente;     // hay un nivel en marcha
        private int _ultimoFrame = -1;  // el manager y la arena pueden llamar en el mismo cuadro
        private bool _pausaPropia;      // la pausa del menú es nuestra (la levantamos al cerrar)
        private SlugArenaGame _arena;   // el gestor vive en el mismo objeto que la arena

        private PlayerShooter _shooter;
        private SlugActor _jugador;

        private void Awake()
        {
            // Duplicado: se apaga SOLO este componente. Destruir el gameObject mataria la
            // arena entera (el jugador vive en el mismo objeto). La arena ya es persistente
            // porque GameFlow la marca; aqui no hace falta DontDestroyOnLoad (y en las pruebas
            // filtraria el manager entre casos).
            if (Instancia != null && Instancia != this) { Destroy(this); return; }
            Instancia = this;
        }

        private void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        /// Llama la arena al reiniciar un nivel: limpia ecos y temporizador.
        public void ReiniciarNivel()
        {
            _nivelEco.Clear();
            _cronometro = 0f;
            _ofertaPendiente = false;
            _nivelVigente = true;
            _ultimaOferta = float.NegativeInfinity;   // la primera oferta del nivel no espera
            OfertasHechas = 0;
            CerrarMenu();
            // Un nivel nuevo nunca hereda una pausa: si el menu anterior quedo abierto, el
            // combate arrancaria congelado (y el arnés de medida se quedaria colgado).
            GameFeel.Pausa = false;
            _pausaPropia = false;
            _shooter = null;
            _jugador = null;
            // Resetea bonos de daño por nivel (los ecos no se llevan al siguiente nivel).
            var shooter = Object.FindAnyObjectByType<PlayerShooter>();
            if (shooter != null) shooter.ReiniciarBonosNivel();
        }

        /// Llama la arena cada vez que se captura una babosa.
        public void NotificarCaptura()
        {
            if (Activo) return;
            if (ofrecerTrasCaptura) _ofertaPendiente = true;
        }

        /// Tick por frame: cuenta el temporizador y ofrece si corresponde.
        /// Shim de compatibilidad: la arena llama a Tick(Time.deltaTime) desde su Update, pero
        /// el dt se ignora a proposito (el menu va en tiempo real, ver la nota de la clase).
        /// El gestor tiene su propio Update, asi que funciona aunque nadie lo llame.
        public void Tick(float dt)
        {
            Procesar();
        }

        private void Update()
        {
            Procesar();
        }

        /// Una sola pasada por cuadro: el manager se llama desde su Update y desde la arena.
        private void Procesar()
        {
            if (_ultimoFrame == Time.frameCount) return;
            _ultimoFrame = Time.frameCount;

            if (Activo)
            {
                // Nadie ha elegido (arnes, pruebas, jugador distraido): se auto-elige el
                // primero y la partida sigue. Es la garantia de que el menu no congela nada.
                if (Time.unscaledTime - _abiertoEn >= margenAutoEleccion) AutoElegir();
                return;
            }

            if (!_nivelVigente) return;
            // Si otro sistema tiene la pausa (menu principal, voz de la historia), no se ofrece
            // nada: el menu de ecos no pelea por el mismo boton de pausa.
            if (GameFeel.Pausa) return;

            if (_arena == null) _arena = GetComponent<SlugArenaGame>();
            if (_arena == null) return;
            if (_arena.Termino) { _nivelVigente = false; return; }

            _cronometro += Time.unscaledDeltaTime;
            bool toca = _ofertaPendiente || _cronometro >= segundosEntreOfertas;
            if (!toca) return;
            // Tras una captura se ofrece al instante, pero no mas de una vez cada X segundos.
            if (Time.unscaledTime - _ultimaOferta < segundosMinimosEntreOfertas) return;
            _ofertaPendiente = false;
            _cronometro = 0f;
            Ofrecer();
        }

        /// Abre el menú de 3 ecos y pausa la partida.
        private void Ofrecer()
        {
            // La semilla depende del nivel y del numero de oferta: las mismas ofertas salen
            // siempre en los mismos niveles, y la medida de campana es reproducible.
            int nivel = GameFlow.Activo != null ? GameFlow.Activo.Nivel : 0;
            System.Random rng = new System.Random(nivel * 1000 + OfertasHechas);
            EcoDef[] tres = EcoCatalogo.ElegirTres(rng);
            OfertaActual = new EcoOferta[tres.Length];
            for (int i = 0; i < tres.Length; i++)
            {
                int nivelActual;
                _nivelEco.TryGetValue(tres[i].id, out nivelActual);
                OfertaActual[i] = new EcoOferta { def = tres[i], nivelResultado = nivelActual + 1 };
            }
            Activo = true;
            _abiertoEn = Time.unscaledTime;
            _ultimaOferta = _abiertoEn;
            OfertasHechas++;
            // Solo se pausa si nadie mas tiene la pausa puesta.
            _pausaPropia = !GameFeel.Pausa;
            if (_pausaPropia) GameFeel.Pausa = true;
            Debug.Log("[EcosCueva] oferta " + OfertasHechas + " en el nivel " + nivel
                + " (" + OfertaActual.Length + " ecos, margen " + margenAutoEleccion.ToString("F1") + " s)");
        }

        /// El jugador elige un eco: aplica el bono, cierra el menú y reanuda.
        public void Elegir(EcoDef def)
        {
            if (!Activo) return;
            if (def == null) return;
            int nivelAntes;
            _nivelEco.TryGetValue(def.id, out nivelAntes);
            int nivelDespues = nivelAntes + 1;
            _nivelEco[def.id] = nivelDespues;
            AplicarBono(def, nivelDespues);

            CerrarMenu();
            Debug.Log("[EcosCueva] elegido " + def.nombre + " nivel=" + nivelDespues);
        }

        /// Elige el eco de una fila del menú (0 = la primera). Es lo que usan los atajos 1/2/3.
        public void ElegirPorIndice(int indice)
        {
            if (!Activo || OfertaActual == null) return;
            if (indice < 0 || indice >= OfertaActual.Length) return;
            Elegir(OfertaActual[indice].def);
        }

        /// Nadie eligió a tiempo: se toma el primer eco para que la partida NO se congele.
        private void AutoElegir()
        {
            if (OfertaActual == null || OfertaActual.Length == 0) { CerrarMenu(); return; }
            EcoDef def = OfertaActual[0].def;
            Debug.Log("[EcosCueva] nadie eligió en " + margenAutoEleccion.ToString("F1")
                + " s: se auto-elige " + (def != null ? def.nombre : "?"));
            Elegir(def);
        }

        /// Cierra el menú y devuelve el control. Solo levanta la pausa que puso este menu.
        private void CerrarMenu()
        {
            Activo = false;
            OfertaActual = null;
            if (_pausaPropia)
            {
                GameFeel.Pausa = false;
                _pausaPropia = false;
            }
        }

        /// Atajos de teclado: 1, 2 y 3 eligen el eco de su fila sin ratón.
        /// Las teclas llegan por el IMGUI (Event.current) y no por Input.GetKeyDown: el proyecto
        /// usa el Input System nuevo (Input.GetKey lanza excepcion) y, con el menu pausado
        /// (timeScale=0), la Game View solo entrega las pulsaciones al IMGUI.
        private void LeerTeclas()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            int fila;
            if (!FilaDeTecla(e.keyCode, out fila)) return;
            ElegirPorIndice(fila);
            e.Use();
        }

        /// Tecla -> fila del menu (0 = la primera). El teclado de arriba y el numerico valen.
        public static bool FilaDeTecla(KeyCode tecla, out int fila)
        {
            if (tecla == KeyCode.Alpha1 || tecla == KeyCode.Keypad1) { fila = 0; return true; }
            if (tecla == KeyCode.Alpha2 || tecla == KeyCode.Keypad2) { fila = 1; return true; }
            if (tecla == KeyCode.Alpha3 || tecla == KeyCode.Keypad3) { fila = 2; return true; }
            fila = -1;
            return false;
        }

        private void AplicarBono(EcoDef def, int nivel)
        {
            float valorTotal = def.valor * nivel;
            ResolverReferencias();
            switch (def.tipo)
            {
                case EcoTipo.Danio:
                    if (_shooter != null) _shooter.AgregarBonoDanio(valorTotal);
                    break;
                case EcoTipo.Cadencia:
                    if (_shooter != null)
                    {
                        float nueva = _shooter.RecargaPorTubo * (1f - valorTotal);
                        _shooter.SetRecargaPorTubo(Mathf.Max(0.15f, nueva));
                    }
                    break;
                case EcoTipo.Cargador:
                    if (_shooter != null) _shooter.AumentarCapacidad((int)valorTotal);
                    break;
                case EcoTipo.VelocidadDisparo:
                    if (_shooter != null) _shooter.AgregarVelocidadDisparo(valorTotal);
                    break;
                case EcoTipo.VidaMax:
                    if (_jugador != null)
                    {
                        _jugador.AumentarVidaMaxima(valorTotal);
                        _jugador.Heal(valorTotal);
                    }
                    break;
                case EcoTipo.CapturaUmbral:
                    if (_jugador != null) _jugador.FijarUmbralCaptura(Mathf.Max(0.15f, _jugador.UmbralCaptura - valorTotal));
                    break;
                case EcoTipo.Energia:
                    if (_jugador != null) _jugador.AumentarEnergiaMax(valorTotal);
                    break;
            }
        }

        private void ResolverReferencias()
        {
            if (_shooter == null) _shooter = Object.FindAnyObjectByType<PlayerShooter>();
            if (_jugador == null)
            {
                foreach (var a in SlugActor.Vivos)
                    if (a != null && a.IsPlayer) { _jugador = a; break; }
            }
        }

        /// UI mínima (IMGUI): dibuja las 3 ofertas mientras el menú está abierto.
        private void OnGUI()
        {
            if (!Activo || OfertaActual == null || OfertaActual.Length == 0) return;

            GUI.depth = -2;
            LeerTeclas();
            float w = Screen.width, h = Screen.height;
            float ancho = Mathf.Min(700f, w * 0.75f);
            float x = (w - ancho) * 0.5f;
            float y = h * 0.35f;
            float altoFila = 70f;

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var estiloTitulo = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                normal = { textColor = Color.white },
            };
            GUI.Label(new Rect(x, y - 30f, ancho, 30f),
                "ECOS DE LA CUEVA — elige una mejora para este nivel", estiloTitulo);

            var estiloNombre = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white },
            };
            var estiloDesc = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.8f, 0.85f, 0.9f) },
            };
            var estiloPista = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = new Color(1f, 0.85f, 0.35f) },
            };

            for (int i = 0; i < OfertaActual.Length; i++)
            {
                var oferta = OfertaActual[i];
                Rect r = new Rect(x, y + i * (altoFila + 10f), ancho, altoFila);
                bool hover = r.Contains(Event.current.mousePosition);

                GUI.color = hover ? new Color(0.3f, 0.35f, 0.45f) : new Color(0.15f, 0.18f, 0.25f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;

                float tx = r.x + 10f;
                GUI.Label(new Rect(tx, r.y + 6f, r.width - 20f, 20f),
                    "[" + (i + 1) + "] " + oferta.def.nombre, estiloNombre);
                GUI.Label(new Rect(tx, r.y + 28f, r.width - 20f, 22f),
                    oferta.def.descripcion + "  (nivel " + oferta.nivelResultado + ")", estiloDesc);

                if (GUI.Button(new Rect(r.xMax - 130f, r.y + 8f, 120f, altoFila - 16f), "ELEGIR"))
                {
                    Elegir(oferta.def);
                }
            }

            // El menu se cierra solo: la partida nunca se queda congelada por no elegir.
            GUI.Label(new Rect(x, y + OfertaActual.Length * (altoFila + 10f) + 6f, ancho, 22f),
                "Pulsa 1, 2 o 3 — si no eliges se toma el primero en "
                + TiempoRestante.ToString("F1") + " s", estiloPista);
        }
    }
}
