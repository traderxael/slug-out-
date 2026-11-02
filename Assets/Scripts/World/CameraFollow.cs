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

        [Header("Temblor (trauma de GameFeel)")]
        [SerializeField] private float temblorMaxDesplazamiento = 0.5f;
        [SerializeField] private float temblorMaxGiro = 5f;
        [SerializeField] private float temblorFrecuencia = 20f;

        // el temblor se suma encima de esta pose: si se sumara al transform, el lerp lo arrastraria
        private Vector3 _base;
        private Quaternion _rotBase;

        public void SetTarget(Transform t) { target = t; }

        private void Start()
        {
            if (target == null)
            {
                PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
                if (pc != null) target = pc.transform;
            }
            // la arena se veia desde muy arriba: las babosas quedaban a ~60 px y no se leia
            // ni el aviso del embestidor. Se arrima sin tocar el .unity (que puede tener cambios).
            if (offset.y > 10f) offset = new Vector3(0f, 8f, -8f);
            if (target != null)
            {
                transform.position = target.position + offset;
                Debug.Log("[CameraFollow] siguiendo a " + target.name);
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
            Vector3 desired = target.position + offset;
            float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            _base = Vector3.Lerp(_base, desired, k);
            Quaternion rot = _rotBase;
            if (lookAtTarget)
            {
                Vector3 focus = target.position + Vector3.up * 1f;
                rot = Quaternion.LookRotation(focus - _base, Vector3.up);
            }

            float s = GameFeel.Trauma * GameFeel.Trauma;
            float t = Time.unscaledTime * temblorFrecuencia;
            Vector3 sacudida = new Vector3(Mathf.PerlinNoise(t, 0.1f) * 2f - 1f, Mathf.PerlinNoise(0.2f, t) * 2f - 1f, 0f) * temblorMaxDesplazamiento * s;
            float giro = (Mathf.PerlinNoise(t, t * 0.5f + 3.3f) * 2f - 1f) * temblorMaxGiro * s;
            transform.position = _base + rot * sacudida;
            transform.rotation = rot * Quaternion.Euler(0f, 0f, giro);
        }
    }
}
