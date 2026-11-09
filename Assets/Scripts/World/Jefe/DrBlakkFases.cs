using UnityEngine;

namespace SlugOut.World
{
    /// Dr. Blakk a 3 fases (S4).
    /// Fase 1: normal. Fase 2 (~60% HP): anillo al revés (reusa la bandera del nivel 25) +
    /// vida rellena parcialmente + más agresivo. Fase 3 (~30% HP): sin anillo, daño y velocidad ×1.5,
    /// última fase.
    public class DrBlakkFases : MonoBehaviour
    {
        public enum Fase { Uno, Dos, Tres }

        [Header("Umbrales de fase (fracción de vida máxima)")]
        [SerializeField] private float umbralFase2 = 0.60f;
        [SerializeField] private float umbralFase3 = 0.30f;

        [Header("Bonos por fase")]
        [SerializeField] private float vidaRellenoFase2 = 0.40f;   // +40% de la máxima al entrar fase 2
        [SerializeField] private float vidaRellenoFase3 = 0.20f;   // +20% al entrar fase 3
        [SerializeField] private float danoMultFase3 = 1.5f;
        [SerializeField] private float velocidadMultFase3 = 1.5f;

        public Fase FaseActual { get; private set; } = Fase.Uno;

        private SlugActor _actor;
        private EnemySlugAI _ai;
        private float _hpMaxInicial;
        private bool _fase2Anuncio;
        private bool _fase3Anuncio;
        // El anillo invertido de la fase 2 reusa una bandera GLOBAL del nivel: si Blakk cae en
        // fase 2 (sin llegar a la 3), hay que devolverle al nivel su valor de antes, si no el
        // cartel "ANILLO AL REVES" queda pegado hasta el final de la partida.
        private bool _anilloAntes;
        private bool _anilloTocado;

        // Banner de fase: texto + color y un plazo en tiempo real (el HUD corre por OnGUI).
        private string _bannerTxt;
        private Color _bannerColor = Color.white;
        private float _bannerHasta;

        private void Awake()
        {
            _actor = GetComponent<SlugActor>();
            _ai = GetComponent<EnemySlugAI>();
            if (_actor != null) _hpMaxInicial = _actor.MaxHp;
        }

        private void OnDisable()
        {
            // Muerte o descarga del jefe: el anillo vuelve a como estaba el nivel. Si cayo en
            // fase 2, sin esto el nivel entero seguia con el chart invertido (y el HUD avisando)
            // despues de que Blakk muriera.
            if (_anilloTocado) SlugActor.AnilloInvertido = _anilloAntes;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_bannerTxt) || Time.unscaledTime >= _bannerHasta) return;
            float w = Screen.width;
            Rect caja = new Rect(w * 0.5f - 240f, 64f, 480f, 38f);
            HudTheme.Chip(caja);
            HudTheme.Linea(new Rect(caja.x, caja.y, 4f, caja.height), _bannerColor);
            HudTheme.Esquinas(caja, _bannerColor, 10f, 1.5f);
            GUIStyle st = HudTheme.Estilo(20, FontStyle.Bold, TextAnchor.MiddleCenter, _bannerColor);
            GUI.Label(new Rect(caja.x + 8f, caja.y + 7f, caja.width - 16f, 26f), _bannerTxt, st);
        }

        /// Cartel de cambio de fase: 2,5 s en tiempo real, igual que los demas avisos del HUD.
        private void Anunciar(string txt, Color color)
        {
            _bannerTxt = txt;
            _bannerColor = color;
            _bannerHasta = Time.unscaledTime + 2.5f;
        }

        private void Update()
        {
            if (_actor == null || !_actor.Alive) return;
            float fraccion = _actor.Hp / Mathf.Max(_hpMaxInicial, 1f);

            if (FaseActual == Fase.Uno && fraccion <= umbralFase2) EntrarFase2();
            else if (FaseActual == Fase.Dos && fraccion <= umbralFase3) EntrarFase3();
        }

        private void EntrarFase2()
        {
            FaseActual = Fase.Dos;
            // fase 2 = anillo al revés (nivel 25). Se guarda el estado del nivel ANTES de tocar
            // la bandera: en el 25+ ya estaba invertida y al restaurar no hay que apagarla.
            _anilloAntes = SlugActor.AnilloInvertido;
            _anilloTocado = true;
            SlugActor.AnilloInvertido = true;
            if (_actor != null)
            {
                float antes = _actor.MaxHp;
                _actor.AumentarVidaMaxima(_hpMaxInicial * vidaRellenoFase2);
                _actor.Heal(_actor.MaxHp - antes);
            }
            _fase2Anuncio = true;
            Anunciar("DR. BLAKK — FASE 2 · ANILLO AL REVÉS", new Color(1f, 0.66f, 0.46f));
            Debug.Log("[DrBlakk] FASE 2 — ANILLO AL REVÉS. Vida=" + _actor.Hp.ToString("F0"));
        }

        private void EntrarFase3()
        {
            FaseActual = Fase.Tres;
            SlugActor.AnilloInvertido = false;   // fase 3 = sin anillo, ira total
            if (_actor != null)
            {
                float antes = _actor.MaxHp;
                _actor.AumentarVidaMaxima(_hpMaxInicial * vidaRellenoFase3);
                _actor.Heal(_actor.MaxHp - antes);
            }
            if (_ai != null)
            {
                _ai.SetDamage(_ai.GetDamage() * danoMultFase3);
                _ai.SetSpeed(_ai.GetSpeed() * velocidadMultFase3);
            }
            _fase3Anuncio = true;
            Anunciar("DR. BLAKK — FASE 3 · IRA", new Color(0.98f, 0.42f, 0.38f));
            Debug.Log("[DrBlakk] FASE 3 — IRA. Daño ×" + danoMultFase3 + " velocidad ×" + velocidadMultFase3);
            Logros.Desbloquear("blakk_fase3");
        }

        /// Si la partida termina, apaga el anillo para que no contamine el siguiente nivel.
        public void Resetear()
        {
            FaseActual = Fase.Uno;
            if (_anilloTocado) SlugActor.AnilloInvertido = _anilloAntes;
            else SlugActor.AnilloInvertido = false;
            _anilloTocado = false;
            _fase2Anuncio = false;
            _fase3Anuncio = false;
            _bannerHasta = 0f;
            _bannerTxt = null;
        }

        public bool AnuncioFase2 { get { return _fase2Anuncio; } }
        public bool AnuncioFase3 { get { return _fase3Anuncio; } }
    }
}
