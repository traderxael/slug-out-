using UnityEngine;

namespace SlugOut.World
{
    /// Camara que sigue al jugador con un offset fijo.
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 11f, -11f);
        [SerializeField] private float followSpeed = 8f;
        [SerializeField] private bool lookAtTarget = true;

        [Header("Tercera persona")]
        [SerializeField] private bool terceraPersona = false;
        [SerializeField] private bool seguirOrientacion = true;
        [SerializeField] private float alturaFoco = 1.2f;
        [SerializeField] private LayerMask paredes = ~0;

        [Header("Temblor (trauma de GameFeel)")]
        [SerializeField] private float temblorMaxDesplazamiento = 0.5f;
        [SerializeField] private float temblorMaxGiro = 5f;
        [SerializeField] private float temblorFrecuencia = 20f;

        [Header("Anticipacion")]
        [Tooltip("Cuanto mira adelante del jugador, en metros")]
        [SerializeField] private float adelanto = 0.5f;
        [SerializeField] private float adelantoVelocidad = 4f;

        [Header("Suelo")]
        [Tooltip("Altura minima de la camara sobre Terrain3D.Altura")]
        [SerializeField] private float alturaSobreSuelo = 0.6f;

        [Header("FOV por velocidad")]
        [SerializeField] private float fovExtraVelocidad = 4f;
        [SerializeField] private float fovSuavizado = 5f;

        // el temblor se suma encima de esta pose: si se sumara al transform, el lerp lo arrastraria
        private Vector3 _base;
        private Quaternion _rotBase;
        private PlayerController _pc;
        private Camera _cam;
        private Vector3 _adelantoActual;
        private float _distanciaActual = -1f;
        private float _fovBase = 52f;
        private Vector3 _ultimaPosObjetivo;
        private float _velocidadMedida;

        /// Para pruebas y capturas: a donde mira la camara este cuadro.
        public Vector3 FocoActual { get; private set; }

        public void SetTarget(Transform t)
        {
            target = t;
            _pc = t != null ? t.GetComponent<PlayerController>() : null;
            _distanciaActual = -1f;
            _ultimaPosObjetivo = t != null ? t.position : Vector3.zero;
        }

        private void Start()
        {
            if (target == null)
            {
                PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
                if (pc != null) target = pc.transform;
            }
            // la arena se veia desde muy arriba: las babosas quedaban a ~60 px y no se leia
            // ni el aviso del embestidor. Se arrima sin tocar el .unity (que puede tener cambios).
            if (offset.y > 10f) offset = new Vector3(0f, 8f, -8f);
            Camera camara = GetComponent<Camera>();
            if (camara != null && camara.fieldOfView > 52f) camara.fieldOfView = 52f;
            if (camara != null) _fovBase = camara.fieldOfView;
            _cam = camara;
            _pc = target != null ? target.GetComponent<PlayerController>() : null;
            _distanciaActual = offset.magnitude;
            _ultimaPosObjetivo = target != null ? target.position : Vector3.zero;
            if (target != null)
            {
                Vector3 inicial = terceraPersona && seguirOrientacion
                    ? target.position + target.rotation * offset
                    : target.position + offset;
                transform.position = inicial;
                Debug.Log("[CameraFollow] siguiendo a " + target.name + " (terceraPersona=" + terceraPersona + ")");
            }
            else
            {
                Debug.LogWarning("[CameraFollow] no encontre PlayerController para seguir.");
            }
            _base = transform.position;
            _rotBase = transform.rotation;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (_pc == null) _pc = target.GetComponent<PlayerController>();
            float dt = Time.deltaTime;

            // Anticipacion: la camara mira un poco mas adelante de hacia donde va el
            // jugador. Se mide con el desplazamiento real para que funcione tambien con
            // input simulado o empujones, no solo con MoveInput.
            Vector3 delta = target.position - _ultimaPosObjetivo;
            _ultimaPosObjetivo = target.position;
            delta.y = 0f;
            _velocidadMedida = dt > 0.0001f ? Mathf.Min(delta.magnitude / dt, 12f) : 0f;
            if (delta.magnitude < 0.0005f) _velocidadMedida = 0f;
            Vector3 adelantoDeseado = delta.sqrMagnitude > 0.0001f
                ? delta.normalized * adelanto
                : Vector3.zero;
            if (_pc != null && adelantoDeseado.sqrMagnitude < 0.0001f)
            {
                Vector3 mov = _pc.MoveInput;
                mov.y = 0f;
                if (mov.sqrMagnitude > 0.0001f)
                    adelantoDeseado = mov.normalized * adelanto;
                else if (_pc.Velocidad > 0.01f)
                    adelantoDeseado = _pc.transform.forward * adelanto;
            }
            _adelantoActual = Vector3.Lerp(_adelantoActual, adelantoDeseado,
                1f - Mathf.Exp(-adelantoVelocidad * dt));

            Vector3 offsetMundo = terceraPersona && seguirOrientacion
                ? target.rotation * offset
                : offset;
            Vector3 desired = target.position + offsetMundo;
            Vector3 focus = target.position + Vector3.up * (terceraPersona ? alturaFoco : 1f)
                + _adelantoActual;
            FocoActual = focus;
            if (terceraPersona)
            {
                Vector3 dir = desired - focus;
                float dist = dir.magnitude;
                if (dist > 0.01f)
                {
                    float distObjetivo = dist;
                    // El rayo nace en la cabeza, no en el foco adelantado: al retroceder, el
                    // foco puede nacer dentro del pilar y el Raycast ya no ve la pared.
                    Vector3 origenRayo = target.position + Vector3.up * alturaFoco;
                    if (Physics.SphereCast(origenRayo, 0.22f, dir / dist, out RaycastHit hit,
                            dist, paredes, QueryTriggerInteraction.Ignore))
                    {
                        // Piso de 0.35: si el pilar esta mas cerca, la camara se arrima al
                        // jugador en vez de quedar dentro de la piedra.
                        distObjetivo = Mathf.Clamp(hit.distance - 0.2f, 0.35f, dist);
                    }
                    if (_distanciaActual < 0f) _distanciaActual = dist;
                    // Acercarse es inmediato (no se atraviesa la piedra); alejarse se suaviza.
                    if (distObjetivo < _distanciaActual) _distanciaActual = distObjetivo;
                    else _distanciaActual = Mathf.Lerp(_distanciaActual, distObjetivo,
                        1f - Mathf.Exp(-12f * dt));
                    desired = focus + dir / dist * _distanciaActual;
                }
            }

            // La camara nunca queda enterrada en una loma.
            float suelo = Terrain3D.Altura(desired.x, desired.z) + alturaSobreSuelo;
            if (desired.y < suelo) desired.y = suelo;

            float k = 1f - Mathf.Exp(-followSpeed * dt);
            _base = Vector3.Lerp(_base, desired, k);
            float sueloBase = Terrain3D.Altura(_base.x, _base.z) + alturaSobreSuelo;
            if (_base.y < sueloBase) _base.y = sueloBase;

            Quaternion rot = _rotBase;
            if (lookAtTarget)
                rot = Quaternion.LookRotation(focus - _base, Vector3.up);

            float s = GameFeel.Trauma * GameFeel.Trauma;
            float t = Time.unscaledTime * temblorFrecuencia;
            Vector3 sacudida = new Vector3(Mathf.PerlinNoise(t, 0.1f) * 2f - 1f, Mathf.PerlinNoise(0.2f, t) * 2f - 1f, 0f) * temblorMaxDesplazamiento * s;
            float giro = (Mathf.PerlinNoise(t, t * 0.5f + 3.3f) * 2f - 1f) * temblorMaxGiro * s;
            transform.position = _base + rot * sacudida;
            transform.rotation = rot * Quaternion.Euler(0f, 0f, giro);

            if (_cam != null)
            {
                float vel = Mathf.Max(_velocidadMedida, _pc != null ? _pc.Velocidad : 0f);
                float fovObjetivo = _fovBase + fovExtraVelocidad * Mathf.Clamp01(vel / 6f);
                // El confort tambien apaga este movimiento: es la misma preferencia que
                // reduce sacudida y destellos.
                if (GameFlow.ReducirDestellos) fovObjetivo = _fovBase;
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fovObjetivo,
                    1f - Mathf.Exp(-fovSuavizado * dt));
            }
        }
    }
}
