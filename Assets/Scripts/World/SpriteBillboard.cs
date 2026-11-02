using UnityEngine;

namespace SlugOut.World
{
    /// El retrato gira SOLO en Y para mirar a la camara: se lee siempre de frente
    /// sin aplastarse cuando la camara esta arriba.
    public class SpriteBillboard : MonoBehaviour
    {
        private Transform _cam;

        private void LateUpdate()
        {
            if (_cam == null)
            {
                Camera c = Camera.main;
                if (c != null) _cam = c.transform;
            }
            if (_cam == null) return;
            Vector3 d = _cam.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
    }
}
