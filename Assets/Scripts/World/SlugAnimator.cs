using UnityEngine;

namespace SlugOut.World
{
    /// Animacion procedural (sin AnimationClips ni Rig): respiracion de la panza,
    /// balanceo hacia el lado del movimiento y aplastado al morir.
    /// Pack de jugo: lunge de disparo, flash+temblor al impacto, pop de aparicion,
    /// espiral de captura y bamboleo cuando queda lista para capturar.
    public class SlugAnimator : MonoBehaviour
    {
        [Header("Respiracion")]
        [SerializeField] private float bobSpeed = 3.4f;
        [SerializeField] private float bobAmount = 0.07f;
        [SerializeField] private float breathe = 0.055f;

        [Header("Movimiento")]
        [SerializeField] private float tiltMax = 16f;
        [SerializeField] private float giroSuave = 6f;

        [Header("Muerte")]
        [SerializeField] private float squashAlMorir = 0.24f;

        [Header("Disparo (lunge)")]
        [SerializeField] private float shotDur = 0.28f;
        [SerializeField] private float shotStretch = 0.42f;
        [SerializeField] private float shotSquash = 0.28f;
        [SerializeField] private float shotPitch = 14f;

        [Header("Impacto")]
        [SerializeField] private float hitDur = 0.3f;
        [SerializeField] private float hitFlash = 1.6f;
        [SerializeField] private float hitSquash = 0.22f;
        [SerializeField] private float wobbleGrados = 9f;

        [Header("Captura")]
        [SerializeField] private float capturaDur = 0.7f;
        [SerializeField] private float capturaEscala = 0.15f;
        [SerializeField] private float capturaVueltas = 2.5f;

        [Header("Aviso del embestidor")]
        [SerializeField] private float avisoEscala = 1.07f;
        [SerializeField] private float avisoPulso = 0.03f;
        [SerializeField] private Color AvisoColor = new Color(1f, 0.55f, 0.2f);

        [Header("Aparecer")]
        [SerializeField] private float spawnDur = 0.45f;

        [Header("Victoria")]
        [SerializeField] private float victoriaDur = 1.6f;
        [SerializeField] private float victoriaSalto = 0.5f;

        private SlugActor _actor;
        private SlugFx _fx;
        private EnemySlugAI _ai;
        private Vector3 _anchor;
        private Vector3 _ultima;
        private Vector3 _escalaInicial;
        private float _t;
        private bool _muerto;
        private float _bobPrev;
        private float _victoryT = -1f;
        private float _shotT = -1f;
        private float _hitT = -1f;
        private float _spawnT = -1f;
        private float _captureT = -1f;
        private float _hpPrev = -1f;
        private bool _capturaLista;

        private Renderer _rend;
        private Material _mat;
        private Color _baseColor;
        private Color _baseEmission;
        private bool _emisivoBase;

        private void Awake()
        {
            _actor = GetComponent<SlugActor>();
            _escalaInicial = transform.localScale;
            _anchor = transform.position;
            _ultima = _anchor;
            _hpPrev = _actor != null ? _actor.Hp : -1f;

            // el cuerpo visible se resuelve PEREZOSO: en Awake la arena todavia no colgo
            // el modelo (y el renderer del root puede terminar apagado) — ver EnsureMat()
            EnsureMat();

            _fx = GetComponent<SlugFx>();
            if (_fx == null) _fx = gameObject.AddComponent<SlugFx>();
        }

        /// Busca el PRIMER renderer prendido y visible; crea la instancia de material.
        /// Se re-resuelve si el cuerpo cambio (la arena monta el modelo en Start).
        private void EnsureMat()
        {
            if (_rend != null && _rend.enabled && _rend.gameObject.activeInHierarchy && _mat != null) return;
            _rend = null;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                {
                    _rend = r;
                    break;
                }
            }
            if (_rend == null)
            {
                _mat = null;
                return;
            }
            _mat = _rend.material;   // instancia propia: el flash no toca a los demas slugs
            _baseColor = MaterialesPixar.ColorBase(_mat);
            _baseEmission = MaterialesPixar.Emision(_mat);
            _emisivoBase = _baseEmission != Color.black;
        }

        private void Start()
        {
            _spawnT = 0f;
        }

        private void OnDisable()
        {
            RestaurarMaterial();
        }

        /// Llamar al disparar o al dar un mordisco: estiron hacia adelante con rebote elastico.
        public void PlayShot()
        {
            _shotT = 0f;
            if (_fx != null) _fx.Shot();
        }

        /// Baile de victoria: saltos con giro y pulso de escala (la arena lo llama al GANASTE).
        public void PlayVictory()
        {
            if (_victoryT < 0f)
            {
                _victoryT = 0f;
                if (_fx != null) _fx.Victory(ColorOf());
            }
        }

        private Color ColorOf()
        {
            return (_actor != null && _actor.Data != null) ? _actor.Data.elementColor : Color.yellow;
        }

        private void RestaurarMaterial()
        {
            if (_mat == null) return;
            // devolver el glow EXACTO que tenia: la arena pinta emisivo elemental (color*0.6)
            MaterialesPixar.SetEmision(_mat, _emisivoBase ? _baseEmission : Color.black);
            MaterialesPixar.SetColorBase(_mat, _baseColor);
        }

        private void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            _t += dt;
            // OJO: la posicion actual YA incluye el bob del frame anterior — hay que
            // restarlo antes de anclar, si no el bob se integra y el slug deriva lento.
            _anchor = transform.position - Vector3.up * _bobPrev;

            // --- eventos por sondeo de estado (sin acoplar a los otros scripts) ---
            if (_actor != null)
            {
                if (_hpPrev >= 0f && _actor.Hp < _hpPrev - 0.01f)
                {
                    _hitT = 0f;
                    Flash();
                    if (_fx != null) _fx.Hit(ColorOf(), _actor.LastMultiplier);
                }
                _hpPrev = _actor.Hp;
                _capturaLista = _actor.ListaParaCapturar;

                if (_actor.Capturada)
                {
                    if (_captureT < 0f)
                    {
                        _captureT = 0f;
                        RestaurarMaterial();
                        if (_fx != null) _fx.Capture(ColorOf());
                    }
                }
            }

            // --- captura: girar encogiendose hasta quedar chiquita y congelada ---
            if (_captureT >= 0f)
            {
                _captureT += dt;
                float k = Mathf.Clamp01(_captureT / capturaDur);
                float s = Mathf.Lerp(1f, capturaEscala, k * k);
                transform.localScale = Vector3.Scale(_escalaInicial, new Vector3(s, s * (1f - 0.4f * k), s));
                transform.Rotate(0f, capturaVueltas * 360f * dt * (1f - k), 0f, Space.Self);
                if (k >= 1f) enabled = false;
                return;
            }

            float aviso = Aviso();

            if (_actor != null && !_actor.Alive)
            {
                if (!_muerto)
                {
                    _muerto = true;
                    // el timer del flash ya no corre en esta rama: sin esto el golpe mortal deja el cuerpo blanco
                    _hitT = -1f;
                    RestaurarMaterial();
                    if (_fx != null) _fx.Defeat(ColorOf());
                    transform.localScale = new Vector3(
                        _escalaInicial.x * (1f + squashAlMorir),
                        _escalaInicial.y * (1f - squashAlMorir),
                        _escalaInicial.z * (1f + squashAlMorir));
                }
                return;
            }

            // --- timers de animaciones puntuales ---
            if (_shotT >= 0f) { _shotT += dt; if (_shotT > shotDur) _shotT = -1f; }
            if (_hitT >= 0f) { _hitT += dt; if (_hitT > hitDur) { _hitT = -1f; RestaurarMaterial(); } }
            if (_spawnT >= 0f) { _spawnT += dt; if (_spawnT > spawnDur) _spawnT = -1f; }
            if (_victoryT >= 0f) { _victoryT += dt; if (_victoryT > victoriaDur) _victoryT = -1f; }

            Vector3 vel = (_anchor - _ultima) / dt;
            _ultima = _anchor;

            // --- escala combinada: respiracion * impacto * disparo * aparicion ---
            float sY = 1f + Mathf.Sin(_t * bobSpeed * 0.6f) * breathe;
            float sXZ = 1f - breathe * 0.6f;

            float pulsoShot = 0f;
            if (_shotT >= 0f) pulsoShot = Mathf.Sin(Mathf.PI * Mathf.Clamp01(_shotT / shotDur));
            sY *= 1f - shotSquash * pulsoShot;
            float sZ = 1f + shotStretch * pulsoShot;

            float pulsoHit = 0f;
            if (_hitT >= 0f) pulsoHit = 1f - Mathf.Clamp01(_hitT / hitDur);
            sY *= 1f - hitSquash * pulsoHit;
            sXZ *= 1f + hitSquash * 0.6f * pulsoHit;

            // victoria: pulso feliz de escala que decae
            float pulsoVictoria = 0f;
            if (_victoryT >= 0f)
            {
                float k = Mathf.Clamp01(_victoryT / victoriaDur);
                pulsoVictoria = 1f - k;
                sY *= 1f + 0.12f * Mathf.Sin(_victoryT * 14f) * pulsoVictoria;
                sXZ *= 1f - 0.06f * Mathf.Sin(_victoryT * 14f) * pulsoVictoria;
            }

            float sSpawn = 1f;
            if (_spawnT >= 0f)
            {
                float k = Mathf.Clamp01(_spawnT / spawnDur);
                sSpawn = k < 0.7f
                    ? Mathf.Lerp(0.05f, 1.14f, Mathf.SmoothStep(0f, 1f, k / 0.7f))
                    : Mathf.Lerp(1.14f, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.7f) / 0.3f));
            }

            transform.localScale = Vector3.Scale(_escalaInicial, new Vector3(sXZ * sSpawn, sY * sSpawn, sZ * sSpawn));

            float bob = Mathf.Sin(_t * bobSpeed) * bobAmount;

            // victoria: saltitos que decaen
            if (_victoryT >= 0f)
            {
                float k = Mathf.Clamp01(_victoryT / victoriaDur);
                bob += Mathf.Abs(Mathf.Sin(_victoryT * 7f)) * victoriaSalto * (1f - k);
            }

            _bobPrev = bob;
            transform.position = _anchor + Vector3.up * bob;

            // --- rotacion: rumbo + inclinacion (suave), y efectos rapidos encima del slerp ---
            float yaw = transform.rotation.eulerAngles.y;
            if (vel.sqrMagnitude > 0.04f)
                yaw = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
            float inclinacion = Mathf.Clamp(-vel.x * 3f, -tiltMax, tiltMax);

            float wiggle = 0f;
            if (_capturaLista) wiggle = Mathf.Sin(_t * 9f) * 6f;

            Quaternion objetivo = Quaternion.Euler(0f, yaw, inclinacion + wiggle);
            transform.rotation = Quaternion.Slerp(transform.rotation, objetivo, Mathf.Min(1f, giroSuave * Time.deltaTime));

            // rapidos: pitch del lunge, temblor del impacto y giro feliz de victoria van
            // DESPUES del slerp para no licuarse
            if (pulsoShot > 0f)
                transform.rotation *= Quaternion.Euler(-shotPitch * pulsoShot, 0f, 0f);
            if (pulsoHit > 0f)
                transform.rotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(_hitT * 42f) * wobbleGrados * pulsoHit);
            if (pulsoVictoria > 0f)
                transform.rotation *= Quaternion.Euler(0f, Mathf.Sin(_victoryT * 9f) * 24f * pulsoVictoria, 0f);

            // el aviso del embestidor: se agranda con pulso y se tinde a naranja. El windup dura
            // 1.05s, mas de lo que tarda cualquier otro efecto, por eso va al final y pisa lo demas.
            if (aviso > 0f)
            {
                float escala = 1f + (avisoEscala - 1f) * aviso + Mathf.Sin(aviso * 4f * Mathf.PI) * avisoPulso;
                transform.localScale = _escalaInicial * escala;
                if (_mat != null && _hitT < 0f)
                {
                    MaterialesPixar.SetEmision(_mat, Color.Lerp(_baseEmission, AvisoColor, aviso * 0.7f));
                }
            }
            else if (_mat != null && _hitT < 0f && _actor != null && _actor.Alive && !_actor.Capturada)
            {
                RestaurarMaterial();
            }
        }

        private EnemySlugAI _aiCache;
        private float Aviso()
        {
            if (_aiCache == null) _aiCache = GetComponent<EnemySlugAI>();
            return _aiCache != null ? _aiCache.ProgresoAviso : 0f;
        }

        private void Flash()
        {
            EnsureMat();   // el cuerpo visible puede haber cambiado desde Awake
            if (_mat == null) return;
            float fuerza = GameFlow.ReducirDestellos ? hitFlash * 0.2f : hitFlash;
            MaterialesPixar.SetEmision(_mat, Color.white * fuerza);
        }
    }
}
