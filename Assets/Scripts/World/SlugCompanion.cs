using UnityEngine;

namespace SlugOut.World
{
    /// La babosa equipada acompaña al heroe en exploracion. Es un root aparte (no hijo)
    /// para que la rotacion/escala del jugador no la arrastre; se apoya en Terrain3D.
    public class SlugCompanion : MonoBehaviour
    {
        public Transform objetivo;
        public Vector3 offset = new Vector3(0.65f, 0f, 0.35f);
        public float followSpeed = 8f;
        public float bob = 0.025f;

        private void Start()
        {
            SombraContacto.Agregar(transform, 0.30f);
        }

        private void LateUpdate()
        {
            if (objetivo == null) return;
            Vector3 desired = objetivo.position + objetivo.rotation * offset;
            desired.y = Terrain3D.Altura(desired.x, desired.z) + 0.02f;
            desired.y += Mathf.Sin(Time.time * 3.2f) * bob;

            float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, k);
            Quaternion yaw = Quaternion.Euler(0f, objetivo.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, yaw, k);
            transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 2.1f) * 0.012f);
        }
    }
}
