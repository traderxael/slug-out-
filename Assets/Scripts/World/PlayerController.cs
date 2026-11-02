using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SlugOut.World
{
    /// Movimiento del jugador con WASD / flechas, apoyado en el terreno procedural.
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float speed = 6f;
        [SerializeField] private float turnSpeed = 720f;
        [SerializeField] private bool logMovement = true;
        [SerializeField] private float logEvery = 0.5f;
        [Tooltip("Cuanto sobresale el pivot del personaje sobre el piso.")]
        [SerializeField] private float alturaSuelo = 1f;

        private Vector3 _lastPosition;
        private float _logTimer;

        public Vector3 MoveInput { get; private set; }
        public float DistanceTravelled { get; private set; }

        /// Apoya el pivot en la altura del terreno. Publico para que una prueba pueda
        /// comprobar que el jugador sigue el relieve y no se queda flotando en y=1.
        public void ApoyarEnPiso()
        {
            Vector3 p = transform.position;
            float y = Terrain3D.Altura(p.x, p.z) + alturaSuelo;
            if (!Mathf.Approximately(p.y, y)) transform.position = new Vector3(p.x, y, p.z);
        }

        private void Start()
        {
            _lastPosition = transform.position;
            if (logMovement) Debug.Log("[PlayerController] inicio en " + transform.position);
            // asegurar modelo 3D del jugador
            if (GetComponent<PlayerModel>() == null) gameObject.AddComponent<PlayerModel>();
        }
        private Vector3 ReadInput()
        {
            float x = 0f, z = 0f;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) z -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) z += 1f;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            x = Input.GetAxisRaw("Horizontal");
            z = Input.GetAxisRaw("Vertical");
#endif
            return new Vector3(x, 0f, z);
        }

        private void Update()
        {
            Vector3 raw = ReadInput();
            if (raw.sqrMagnitude > 1f) raw.Normalize();
            MoveInput = raw;

            if (raw.sqrMagnitude > 0.0001f)
            {
                transform.position = transform.position + raw * speed * Time.deltaTime;
                Quaternion target = Quaternion.LookRotation(raw, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            }

            ApoyarEnPiso();

            DistanceTravelled += Vector3.Distance(transform.position, _lastPosition);
            _lastPosition = transform.position;

            if (logMovement)
            {
                _logTimer += Time.deltaTime;
                if (_logTimer >= logEvery)
                {
                    _logTimer = 0f;
                    Debug.Log("[PlayerController] pos=" + transform.position.ToString("F2") + " input=" + raw + " total=" + DistanceTravelled.ToString("F2"));
                }
            }
        }
    }
}
