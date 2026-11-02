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

        public Vinculo()
        {
            Actual = Etapa.Lejos;
            Mensaje = "";
        }

        /// Devuelve true la primera vez que se aprende algo y la primera vez que se vincula,
        /// para que quien llama registre el descubrimiento y sume al equipo una sola vez.
        public bool Paso(float dt, float distancia, float velocidad)
        {
            bool hito = false;
            if (Actual == Etapa.Vinculada) return false;

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
                            Mensaje = "Aprendiste como vive. Ahora acercate despacio.";
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
                        Asustar("Te moviste rapido y se asusto.");
                        break;
                    }
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
                        Mensaje = "Acercate despacio, sin correr.";
                    }
                    break;

                case Etapa.Asustada:
                    _t += dt;
                    Progreso = 1f - Mathf.Clamp01(_t / tiempoRecuperacion);
                    if (_t >= tiempoRecuperacion)
                    {
                        Actual = Etapa.Detectada;
                        _t = 0f;
                        Progreso = 0f;
                        Mensaje = "Volvio a su puesto. Proba otra vez.";
                    }
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
        private float _velocidadJugador;
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

        private void Start()
        {
            if (_vinculo == null) _vinculo = new Vinculo();
            _puesto = transform.position;
            _fase = (transform.position.x + transform.position.z) * 0.37f;
            CreamosModelo();
            PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                _jugador = pc.transform;
                _ultimaPosJugador = _jugador.position;
            }
        }

        private void CreamosModelo()
        {
            GameObject prefab = Resources.Load<GameObject>("Slugs/" + slugId);
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
            if (_jugador != null)
            {
                Vector3 p = _jugador.position;
                // El primer cuadro no hay medicion previa: si se usara el origen (0,0,0)
                // la "velocidad" saldria enorme y la salvaje se asustaba sola al aparecer.
                if (!_medido)
                {
                    _medido = true;
                    _ultimaPosJugador = p;
                }
                _velocidadJugador = Vector3.Distance(p, _ultimaPosJugador) / dt;
                _ultimaPosJugador = p;
                DistanciaJugador = Vector3.Distance(p, transform.position);
            }

            bool hito = Ciclo.Paso(dt, DistanciaJugador, _velocidadJugador);
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

            float w = Screen.width;
            // El banner de nivel ocupa y=12..144 en el centro; el panel de vinculo nace debajo
            // para que el mensaje de confianza no se monte sobre el titulo ni el texto del nivel.
            Rect panel = new Rect(w * 0.5f - 210f, 158f, 420f, 74f);
            Color prev = GUI.color;
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.86f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, panel.width - 24f, 22f),
                slugId.ToUpperInvariant() + "  ·  " + habitat,
                Estilo(13, FontStyle.Bold, new Color(0.72f, 0.95f, 0.85f)));
            GUI.Label(new Rect(panel.x + 12f, panel.y + 26f, panel.width - 24f, 20f),
                _vinculo.Mensaje, Estilo(12, FontStyle.Normal, new Color(0.88f, 0.91f, 0.95f)));

            Rect barra = new Rect(panel.x + 12f, panel.y + 54f, panel.width - 24f, 8f);
            GUI.color = new Color(0.14f, 0.16f, 0.20f, 1f);
            GUI.DrawTexture(barra, Texture2D.whiteTexture);
            GUI.color = _vinculo.Actual == Vinculo.Etapa.Asustada
                ? new Color(0.85f, 0.35f, 0.30f) : new Color(0.45f, 0.80f, 0.72f);
            GUI.DrawTexture(new Rect(barra.x, barra.y, barra.width * _vinculo.Progreso, barra.height),
                Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static GUIStyle _estilo;
        private static GUIStyle Estilo(int tam, FontStyle f, Color c)
        {
            if (_estilo == null)
            {
                _estilo = new GUIStyle(GUI.skin.label) { clipping = TextClipping.Clip };
            }
            _estilo.fontSize = tam;
            _estilo.fontStyle = f;
            _estilo.alignment = TextAnchor.MiddleLeft;
            _estilo.normal.textColor = c;
            return _estilo;
        }
    }
}
