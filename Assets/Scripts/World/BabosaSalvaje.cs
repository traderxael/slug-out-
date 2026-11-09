using UnityEngine;
using SlugOut.Slugs;

namespace SlugOut.World
{
    /// Reglas del vinculo con una babosa salvaje, sin escena ni input: entra dt, distancia y
    /// velocidad, sale etapa y mensaje. Asi se puede probar el ciclo entero con un arnes.
    ///
    /// El diseño es a proposito: primero se observa (quieto y lejos), recien despues se
    /// acerca, y acercarse rapido o antes de tiempo la asusta. Asustarse NUNCA la borra:
    /// vuelve a su puesto tras unos segundos y se puede reintentar sin gastar nada.
    public class Vinculo
    {
        public enum Etapa { Lejos, Detectada, Observando, Conoce, Confiando, Vinculada, Asustada }

        /// Velocidad del paso cuidado, la unica que puede entrar en la bola de acercamiento.
        /// Vive aqui y no en PlayerController para que la regla del vinculo y el control del
        /// jugador no vuelvan a separarse: antes el jugador solo tenia 0 y 6 m/s y el umbral
        /// era 2,2, asi que Vinculada era imposible caminando.
        public const float VelocidadPasoCuidado = 1.8f;

        /// Tecla que lo produce, nombrada en el mensaje para que el jugador sepa apretarla.
        public const string TeclaPasoCuidado = "CTRL o LT";

        public float detectar = 12f;
        public float observarDist = 9f;
        public float acercarDist = 4.5f;
        public float velocidadQuieto = 0.45f;
        public float velocidadCuidado = 2.2f;
        public float tiempoObservar = 2.5f;
        public float tiempoConfiar = 2.2f;
        public float tiempoRecuperacion = 5f;

        public Etapa Actual { get; private set; }
        public float Progreso { get; private set; }
        public string Mensaje { get; private set; }
        public bool Aprendida { get; private set; }
        public bool Vinculada { get { return Actual == Etapa.Vinculada; } }

        private float _t;
        /// Ventanas de medicion SEGUIDAS por encima del umbral. La velocidad llega promediada en
        /// ventanas de 0,12 s: una sola ventana alta puede ser un pico de un cuadro (medido en
        /// Play el 2026-10-19: 6,31 m/s mientras el jugador avanzaba a 1,81). Se exigen dos
        /// seguidas para asustar; el umbral sigue siendo el mismo. El llamador dice cuando hay
        /// ventana nueva: contar cambios de valor seria depender de los decimales.
        private int _ventanasRapidas;

        public Vinculo()
        {
            Actual = Etapa.Lejos;
            Mensaje = "";
        }

        /// Devuelve true la primera vez que se aprende algo y la primera vez que se vincula,
        /// para que quien llama registre el descubrimiento y sume al equipo una sola vez.
        public bool Paso(float dt, float distancia, float velocidad, bool nuevaVentana = false)
        {
            bool hito = false;
            if (Actual == Etapa.Vinculada) return false;
            // El contador de ventanas rapidas solo tiene sentido mientras se acerca; en cualquier
            // otra etapa (o despues de un susto) arranca de cero.
            if (Actual != Etapa.Conoce && Actual != Etapa.Confiando)
                _ventanasRapidas = 0;

            switch (Actual)
            {
                case Etapa.Lejos:
                    if (distancia < detectar)
                    {
                        Actual = Etapa.Detectada;
                        Mensaje = "Rastros frescos. Observala desde lejos, sin moverte.";
                    }
                    break;

                case Etapa.Detectada:
                case Etapa.Observando:
                    if (distancia > detectar)
                    {
                        Actual = Etapa.Lejos;
                        Progreso = 0f;
                        Mensaje = "";
                        break;
                    }
                    // Acercarse antes de conocerla la asusta: es el error que hay que enseñar.
                    if (distancia < acercarDist)
                    {
                        Asustar("Te acercaste antes de observarla.");
                        break;
                    }
                    if (distancia < observarDist && velocidad < velocidadQuieto)
                    {
                        Actual = Etapa.Observando;
                        _t += dt;
                        Progreso = Mathf.Clamp01(_t / tiempoObservar);
                        Mensaje = "Observando... no te muevas (" + Mathf.RoundToInt(Progreso * 100f) + "%)";
                        if (_t >= tiempoObservar)
                        {
                            Actual = Etapa.Conoce;
                            Aprendida = true;
                            Progreso = 1f;
                            Mensaje = "Aprendiste como vive. Acerate con " + TeclaPasoCuidado
                                + " apretado (paso cuidado).";
                            hito = true;
                        }
                    }
                    else
                    {
                        Actual = Etapa.Detectada;
                        _t = 0f;
                        Progreso = 0f;
                        Mensaje = distancia >= observarDist
                            ? "Todavia esta lejos. Acercate unos pasos y quedate quieto."
                            : "Se mueve distinto si te movés. Quedate quieto.";
                    }
                    break;

                case Etapa.Conoce:
                case Etapa.Confiando:
                    if (distancia > detectar)
                    {
                        Actual = Etapa.Detectada;
                        Progreso = 0f;
                        _t = 0f;
                        Mensaje = "Se alejo. Volve a observarla.";
                        break;
                    }
                    if (distancia < acercarDist && velocidad > velocidadCuidado)
                    {
                        // Cuenta ventanas de medicion, no cuadros: el valor llega promediado y
                        // cambia cada 0,12 s. Con una sola ventana alta el jugador quedaba
                        // castigado por un pico de un cuadro.
                        if (nuevaVentana) _ventanasRapidas++;
                        if (_ventanasRapidas >= 2)
                        {
                            Asustar("Te moviste rapido y se asusto.");
                            break;
                        }
                        // Todavia no: se conserva la etapa y no se avanza la confianza.
                        break;
                    }
                    if (nuevaVentana) _ventanasRapidas = 0;
                    if (distancia < acercarDist && velocidad <= velocidadCuidado)
                    {
                        Actual = Etapa.Confiando;
                        _t += dt;
                        Progreso = Mathf.Clamp01(_t / tiempoConfiar);
                        Mensaje = "Ganando su confianza... acercate despacio (" + Mathf.RoundToInt(Progreso * 100f) + "%)";
                        if (_t >= tiempoConfiar)
                        {
                            Actual = Etapa.Vinculada;
                            Progreso = 1f;
                            Mensaje = "Se vinculo con vos. Ya esta en el equipo.";
                            hito = true;
                        }
                    }
                    else
                    {
                        Actual = Etapa.Conoce;
                        _t = 0f;
                        Progreso = 0f;
                        Mensaje = "Acerate con " + TeclaPasoCuidado + " apretado: paso cuidado.";
                    }
                    break;

                case Etapa.Asustada:
                    // La recuperacion no puede reabrir Detectada con el jugador todavia encima:
                    // la rama de Detectada asusta por estar dentro de acercarDist, y el ciclo
                    // se volvía un bucle de sustos infinito (medido: Asustada -> Detectada ->
                    // Asustada cada 5 s sin que el jugador pudiera salir). Primero hay que
                    // darle espacio.
                    if (distancia > detectar)
                    {
                        Actual = Etapa.Lejos;
                        _t = 0f;
                        Progreso = 0f;
                        Mensaje = "";
                        break;
                    }
                    _t += dt;
                    Progreso = 1f - Mathf.Clamp01(_t / tiempoRecuperacion);
                    if (_t < tiempoRecuperacion) break;
                    if (distancia < acercarDist)
                    {
                        Mensaje = "Dale espacio: aleate y volvé a observarla.";
                        break;
                    }
                    Actual = Etapa.Detectada;
                    _t = 0f;
                    Progreso = 0f;
                    Mensaje = "Volvio a su puesto. Proba otra vez.";
                    break;
            }
            return hito;
        }

        private void Asustar(string motivo)
        {
            Actual = Etapa.Asustada;
            _t = 0f;
            Progreso = 1f;
            Mensaje = motivo + " Volve a intentarlo en unos segundos.";
        }
    }

    /// Babosa salvaje de un microhabitat: pasea alrededor de su puesto, alimenta el vinculo
    /// con lo que mide del jugador y se registra al vincularse. No pelea ni es un enemigo.
    public class BabosaSalvaje : MonoBehaviour
    {
        public string slugId = "fuego";
        public string habitat = "Fumarolas";
        [SerializeField] private float radioPaseo = 2.2f;
        [SerializeField] private float velocidadPaseo = 0.5f;

        private Vinculo _vinculo;
        private Transform _jugador;
        private Vector3 _puesto;
        private Vector3 _ultimaPosJugador;

        /// Distancia recorrida en el plano XZ entre dos posiciones del jugador. Publica y pura
        /// para que la prueba le entregue un paso con salto vertical grande y compruebe que no
        /// infla la velocidad de avance.
        public static float PasoHorizontal(Vector3 desde, Vector3 hasta)
        {
            float dx = hasta.x - desde.x, dz = hasta.z - desde.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private float _fase;
        private float _escalaBase = 1f;
        private float _pulso;
        private bool _registrada;
        private bool _medido;

        /// Perezoso a proposito: Start puede no haber corrido todavia cuando la escena
        /// recien se esta armando, y la sonda encontro el campo en null con el modelo ya
        /// creado. Con esto el ciclo existe desde el primer acceso, venga de donde venga.
        public Vinculo Ciclo
        {
            get
            {
                if (_vinculo == null) _vinculo = new Vinculo();
                return _vinculo;
            }
        }
        public float DistanciaJugador { get; private set; }

        /// Que vio la regla la ultimo: metros por segundo del jugador promediados en una
        /// ventana. Publico para que un arnes pueda comparar lo que mide el juego con lo que
        /// realmente se movio el explorador; si difieren, el vinculo miente.
        public float VelocidadJugadorMedida { get; private set; }

        /// Ventana de medicion. Cuadro a cuadro, el muestreo entre dos Update desordenados
        /// leyo 1,8 m/s reales como mas de 2,2 y asusto a una salvaje aunque el jugador
        /// viniera en paso cuidado: con promedio por ventana el ruido no cruza el umbral.
        /// Es corta (0,12 s) a proposito: corriendo se llena en dos tercios de la distancia
        /// que hay que recorrer para entrar en la bola, asi que acelerar sigue asustando.
        private const float Ventana = 0.12f;
        private float _recorrido, _reloj;

        private void Start()
        {
            if (_vinculo == null) _vinculo = new Vinculo();
            _puesto = transform.position;
            _fase = (transform.position.x + transform.position.z) * 0.37f;
            CreamosModelo();
            SombraContacto.Agregar(transform, 0.30f);
            PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                _jugador = pc.transform;
                _ultimaPosJugador = _jugador.position;
            }
        }

        private void CreamosModelo()
        {
            // Las salvajes tambien usan el modelo Pixar texturizado; el prefab legacy queda
            // solo como respaldo. Antes cargaban el OBJ viejo y desentonaban con el heroe.
            GameObject prefab = Resources.Load<GameObject>("Slugs/" + slugId + "_pixar");
            bool pixar = prefab != null;
            if (prefab == null) prefab = Resources.Load<GameObject>("Slugs/" + slugId);
            Debug.Log("[SlugArena] salvaje " + slugId + " = " + (pixar ? "pixar" : "legacy"));
            if (prefab == null)
            {
                // Respaldo: una capsula del color del elemento para no romper la escena.
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cap.name = "Salvaje_" + slugId;
                Object.Destroy(cap.GetComponent<Collider>());
                cap.transform.SetParent(transform, false);
                cap.transform.localScale = Vector3.one * 0.5f;
                return;
            }
            GameObject inst = Object.Instantiate(prefab, transform);
            inst.name = "Salvaje_" + slugId;
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            if (pixar)
            {
                SlugData d = SlugRoster.Buscar(slugId);
                MaterialesPixar.PrepararBabosa(inst, slugId,
                    d != null ? d.elementColor : new Color(0.4f, 0.85f, 1f));
            }
            _escalaBase = inst.transform.localScale.x;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_jugador == null)
            {
                PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
                if (pc != null) _jugador = pc.transform;
            }
            bool nuevaVentana = false;
            if (_jugador != null)
            {
                Vector3 p = _jugador.position;
                // El primer cuadro no hay medicion previa: si se usara el origen (0,0,0)
                // la "velocidad" saldria enorme y la salvaje se asustaba sola al aparecer.
                // Se mide en el plano: el controlador pega al jugador al piso en cada cuadro, y
                // subir una pendiente en paso cuidado sumaba el eje Y a la distancia recorrida
                // (medido en Play: 1,80 m/s de paso cuidado se leyo como 3,96 y asusto a la
                // salvaje en La Cueva Seca). La regla es sobre velocidad de avance, no sobre
                // cuanto sube el terreno.
                float paso = PasoHorizontal(_ultimaPosJugador, p);
                _ultimaPosJugador = p;
                if (!_medido) _medido = true;
                else if (paso > 5f)
                {
                    // Un salto mayor que cualquier cuadro de movimiento es un teletransporte
                    // (el viaje de region apoya al jugador a 3 m de la boca del tunel, y si la
                    // salvaje vive cerca de esa boca eso se leeria como un sprint y la asustaria
                    // sin que el jugador hiciera nada). Se descarta la muestra, no se promedia.
                    _recorrido = 0f;
                    _reloj = 0f;
                }
                else
                {
                    _recorrido += paso;
                    _reloj += dt;
                    if (_reloj >= Ventana)
                    {
                        VelocidadJugadorMedida = _recorrido / _reloj;
                        _recorrido = 0f;
                        _reloj = 0f;
                        nuevaVentana = true;
                    }
                }
                DistanciaJugador = Vector3.Distance(p, transform.position);
            }

            bool hito = Ciclo.Paso(dt, DistanciaJugador, VelocidadJugadorMedida, nuevaVentana);
            if (hito && _vinculo.Aprendida && !_registrada)
            {
                _registrada = true;
                GameFlow.RegistrarDescubierta(slugId);
                Debug.Log("[Vinculo] descubierta " + slugId + " en " + habitat);
            }
            if (hito && _vinculo.Vinculada)
            {
                GameFlow.RegistrarDescubierta(slugId);
                if (GameFlow.Activo != null) GameFlow.Activo.SumarAlEquipo(slugId);
                Debug.Log("[Vinculo] vinculada " + slugId + " (" + habitat + ")");
            }

            Pasear(dt);
            _pulso = Mathf.MoveTowards(_pulso, _vinculo.Vinculada ? 1f : 0f, dt * 2f);
            if (_pulso > 0f) transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 6f) * 0.06f * _pulso);
        }

        /// Pasea en circulo lento alrededor del puesto. Mientras la estas observando o
        /// acercandote se queda quieta: si se moviera, "quedate quieto" no significaria nada.
        private void Pasear(float dt)
        {
            // Si el jugador esta cerca, la salvaje se queda donde esta. Sin esto podia
            // pasearse ella sola dentro del radio de acercamiento y asustarse sin que el
            // jugador hiciera nada: el ciclo se volvia una loteria.
            if (DistanciaJugador < Ciclo.detectar) return;
            if (Ciclo.Actual == Vinculo.Etapa.Observando ||
                Ciclo.Actual == Vinculo.Etapa.Confiando ||
                Ciclo.Actual == Vinculo.Etapa.Conoce) return;

            _fase += dt * velocidadPaseo * 0.35f;
            float r = radioPaseo * (0.6f + 0.4f * Mathf.Sin(_fase * 0.7f));
            Vector3 destino = _puesto + new Vector3(Mathf.Cos(_fase) * r, 0f, Mathf.Sin(_fase) * r);
            destino.y = Terrain3D.Altura(destino.x, destino.z);
            transform.position = Vector3.MoveTowards(transform.position, destino, dt * velocidadPaseo);
        }

        private void OnGUI()
        {
            if (GameFlow.Activo != null && GameFlow.Activo.EnMenu) return;
            if (_vinculo == null || DistanciaJugador > _vinculo.detectar) return;
            HudTheme.Asegurar();

            SlugData d = SlugRoster.Buscar(slugId);
            Color acento = d != null ? d.elementColor : HudTheme.Cian;
            string nombre = d != null ? d.slugName : slugId.ToUpperInvariant();
            Color colorBarra = _vinculo.Actual == Vinculo.Etapa.Asustada ? HudTheme.Rojo
                : (_vinculo.Vinculada ? HudTheme.Verde : HudTheme.Cian);

            float w = Screen.width;
            // Debajo de la tarjeta de historia (que ahora llega a y=180): el prompt de vinculo
            // no compite con el titulo ni con el texto del nivel.
            Rect panel = new Rect(w * 0.5f - 230f, 196f, 460f, 100f);
            HudTheme.Panel(panel);
            HudTheme.Esquinas(panel, new Color(acento.r, acento.g, acento.b, 0.65f), 12f, 1.5f);
            HudTheme.Linea(new Rect(panel.x, panel.y, panel.width, 2f),
                new Color(acento.r, acento.g, acento.b, 0.8f));

            Texture2D tex = d != null ? SlugArenaGame.LoadSprite(d, true) : null;
            Retrato(new Rect(panel.x + 10f, panel.y + 12f, 56f, 56f), tex, nombre, acento,
                d != null ? d.element.ToString() : "");

            GUI.Label(new Rect(panel.x + 78f, panel.y + 8f, panel.width - 210f, 20f), nombre,
                HudTheme.Estilo(16, FontStyle.Bold, TextAnchor.UpperLeft, HudTheme.Texto));
            GUI.Label(new Rect(panel.x + 78f, panel.y + 29f, panel.width - 210f, 14f),
                (d != null ? "TIPO " + d.element.ToString().ToUpperInvariant() : "BABOSA SALVAJE")
                + "   ·   " + habitat.ToUpperInvariant(),
                HudTheme.Estilo(9, FontStyle.Bold, TextAnchor.UpperLeft, acento));
            GUI.Label(new Rect(panel.x + 78f, panel.y + 46f, panel.width - 100f, 30f),
                _vinculo.Mensaje,
                Estilo(11, FontStyle.Normal, new Color(0.88f, 0.92f, 0.96f)));

            HudTheme.Barra(new Rect(panel.x + 78f, panel.y + 80f, panel.width - 204f, 7f),
                _vinculo.Progreso, colorBarra);
            Rect tecla = new Rect(panel.xMax - 114f, panel.y + 72f, 100f, 22f);
            HudTheme.Chip(tecla);
            GUI.Label(tecla, "CTRL   PASO",
                HudTheme.Estilo(9, FontStyle.Bold, TextAnchor.MiddleCenter, HudTheme.OroClaro));
        }

        private static void Retrato(Rect r, Texture2D tex, string nombre, Color acento, string tipo)
        {
            if (tex != null && !tex.name.StartsWith("retrato_"))
            {
                HudTheme.Retrato(r, tex, acento);
                return;
            }
            HudTheme.Chip(r);
            Color prev = GUI.color;
            GUI.color = new Color(acento.r, acento.g, acento.b, 0.2f);
            GUI.DrawTexture(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), Texture2D.whiteTexture);
            GUI.color = prev;
            HudTheme.Esquinas(r, new Color(acento.r, acento.g, acento.b, 0.85f), 9f, 1.5f);
            string inicial = !string.IsNullOrEmpty(nombre) ? nombre.Substring(0, 1).ToUpperInvariant() : "?";
            GUI.Label(r, inicial, HudTheme.Estilo(22, FontStyle.Bold, TextAnchor.MiddleCenter, acento));
            GUI.Label(new Rect(r.x, r.yMax - 13f, r.width, 11f), tipo,
                HudTheme.Estilo(7, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Color(acento.r, acento.g, acento.b, 0.9f)));
        }

        private static GUIStyle _estilo;
        private static GUIStyle Estilo(int tam, FontStyle f, Color c)
        {
            if (_estilo == null)
            {
                _estilo = new GUIStyle(GUI.skin.label)
                {
                    clipping = TextClipping.Clip,
                    wordWrap = true
                };
                _estilo.font = HudTheme.Fuente;
            }
            _estilo.fontSize = tam;
            _estilo.fontStyle = f;
            _estilo.alignment = TextAnchor.MiddleLeft;
            _estilo.normal.textColor = c;
            return _estilo;
        }
    }
}
