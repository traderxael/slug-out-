using System.Collections.Generic;
using UnityEngine;

namespace SlugOut.World
{
    /// Jugo de combate: temblor por trauma, hitstop y numeros de dano.
    /// Trauma: suma acotada a 1, decae 1.3*(t+0.3)/s, la camara tiembla con t^2.
    /// Hitstop: escala de tiempo baja por un rato real; si se solapan gana el que termina mas tarde.
    /// timeScale se resuelve cada cuadro como el minimo de las capas (hitstop, pausa): ninguna pisa a otra.
    public class GameFeel : MonoBehaviour
    {
        [Header("Trauma por evento")]
        [SerializeField] private float traumaJugadorGolpeado = 0.45f;
        [SerializeField] private float traumaGolpeNormal = 0.12f;
        [SerializeField] private float traumaGolpeFuerte = 0.3f;
        [SerializeField] private float traumaGolpeDebil = 0.06f;
        [SerializeField] private float traumaBaja = 0.25f;

        [Header("Hitstop (segundos reales)")]
        [SerializeField] private float hitstopEscala = 0.05f;
        [SerializeField] private float hitstopJugador = 0.05f;
        [SerializeField] private float hitstopFuerte = 0.04f;
        [SerializeField] private float hitstopBaja = 0.08f;

        [Header("Numeros de dano")]
        [SerializeField] private float numeroDuracion = 0.8f;
        [SerializeField] private float numeroSubida = 1.2f;

        [Header("Contadores (solo lectura)")]
        [SerializeField] private int golpesVistos = 0;
        [SerializeField] private int hitstops = 0;

        public static float Trauma { get; private set; }

        private static bool _pausa;
        private static System.Action _aplicador;

        /// Pausa con efecto inmediato: si no hay un GameFeel vivo que la aplique, la aplica
        /// Time.timeScale directamente. Sin esto el menu arrancaba sin congelar la arena.
        public static bool Pausa
        {
            get { return _pausa; }
            set
            {
                _pausa = value;
                if (_aplicador != null) _aplicador();
                else Time.timeScale = value ? 0f : 1f;
            }
        }

        public static void RegistrarAplicador(System.Action a) { _aplicador = a; }
        public static void DesregistrarAplicador(System.Action a) { if (_aplicador == a) _aplicador = null; }

        private float _hitstopHasta = -1f;

        private struct Numero
        {
            public Vector3 pos;
            public string texto;
            public Color color;
            public float t0;
            public int tamano;
        }
        private readonly List<Numero> _numeros = new List<Numero>();
        private GUIStyle _estilo;

        public int GolpesVistos { get { return golpesVistos; } }
        public int Hitstops { get { return hitstops; } }
        public int NumerosEnPantalla { get { return _numeros.Count; } }

        private void OnEnable()
        {
            Trauma = 0f;
            RegistrarAplicador(AplicarEscala);
            SlugActor.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            SlugActor.Damaged -= OnDamaged;
            DesregistrarAplicador(AplicarEscala);
            Time.timeScale = 1f;
            Trauma = 0f;
            _pausa = false;
        }

        private void OnDamaged(SlugActor actor, float dano, float mult)
        {
            golpesVistos++;
            bool baja = !actor.Alive;
            Color color;
            int tamano = 26;
            string texto = Mathf.CeilToInt(dano).ToString();

            if (actor.IsPlayer)
            {
                AddTrauma(traumaJugadorGolpeado);
                Hitstop(hitstopJugador);
                color = new Color(1f, 0.35f, 0.3f);
            }
            else if (mult > 1f)
            {
                AddTrauma(traumaGolpeFuerte);
                Hitstop(hitstopFuerte);
                color = new Color(1f, 0.85f, 0.2f);
                texto += "!";
                tamano = 34;
            }
            else if (mult < 1f)
            {
                AddTrauma(traumaGolpeDebil);
                color = new Color(0.7f, 0.75f, 0.8f);
                tamano = 20;
            }
            else
            {
                AddTrauma(traumaGolpeNormal);
                color = Color.white;
            }

            if (baja && !actor.IsPlayer)
            {
                AddTrauma(traumaBaja);
                Hitstop(hitstopBaja);
            }

            _numeros.Add(new Numero
            {
                pos = actor.transform.position + Vector3.up * 1.6f,
                texto = texto,
                color = color,
                t0 = Time.unscaledTime,
                tamano = tamano,
            });
        }

        private void AplicarEscala()
        {
            Time.timeScale = Pausa ? 0f : (_hitstopHasta >= 0f && Time.unscaledTime < _hitstopHasta ? hitstopEscala : 1f);
        }

        public static void AddTrauma(float cantidad)
        {
            if (GameFlow.ReducirDestellos) cantidad *= 0.3f;
            Trauma = Mathf.Clamp01(Trauma + cantidad);
        }

        public void Hitstop(float duracion)
        {
            _hitstopHasta = Mathf.Max(_hitstopHasta, Time.unscaledTime + duracion);
            Time.timeScale = Pausa ? 0f : hitstopEscala;
            hitstops++;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Trauma = Mathf.Max(0f, Trauma - dt * 1.3f * (Trauma + 0.3f));

            float escala = 1f;
            if (_hitstopHasta >= 0f)
            {
                if (Time.unscaledTime >= _hitstopHasta) _hitstopHasta = -1f;
                else escala = Mathf.Min(escala, hitstopEscala);
            }
            if (Pausa) escala = 0f;
            Time.timeScale = escala;

            for (int i = _numeros.Count - 1; i >= 0; i--)
                if (Time.unscaledTime - _numeros[i].t0 > numeroDuracion) _numeros.RemoveAt(i);
        }

        private void OnGUI()
        {
            GUI.depth = -2;   // los numeros flotan por encima del menu (-1) y del HUD (0)
            Camera cam = Camera.main;
            if (cam == null || _numeros.Count == 0) return;
            if (_estilo == null)
            {
                _estilo = new GUIStyle(GUI.skin.label);
                _estilo.fontStyle = FontStyle.Bold;
                _estilo.alignment = TextAnchor.MiddleCenter;
                _estilo.normal.textColor = Color.white;
            }

            Color prev = GUI.color;
            foreach (Numero n in _numeros)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - n.t0) / numeroDuracion);
                Vector3 sp = cam.WorldToScreenPoint(n.pos + Vector3.up * numeroSubida * k);
                if (sp.z <= 0f) continue;
                float y = Screen.height - sp.y;
                float pop = k < 0.15f ? Mathf.Lerp(1.4f, 1f, k / 0.15f) : 1f;
                _estilo.fontSize = Mathf.RoundToInt(n.tamano * pop);
                Rect r = new Rect(sp.x - 60f, y - 20f, 120f, 40f);

                GUI.color = new Color(0f, 0f, 0f, 0.6f * (1f - k));
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), n.texto, _estilo);
                GUI.color = new Color(n.color.r, n.color.g, n.color.b, 1f - k * k);
                GUI.Label(r, n.texto, _estilo);
            }
            GUI.color = prev;
        }
    }
}
