using UnityEngine;

namespace SlugOut.World
{
    /// Gestiona el modelo 3D del jugador (GLB importado) y sus animaciones.
    [RequireComponent(typeof(PlayerController))]
    public class PlayerModel : MonoBehaviour
    {
        [SerializeField] private string modeloPath = "Characters/Jugador";
        [SerializeField] private string parametroVelocidad = "Speed";

        private Animator _anim;
        private PlayerController _ctrl;

        private void Awake()
        {
            _ctrl = GetComponent<PlayerController>();
            var modeloGO = Instantiate(Resources.Load<GameObject>(modeloPath));
            if (modeloGO == null)
            {
                Debug.LogError("[PlayerModel] No se pudo cargar el modelo en Resources/" + modeloPath);
                enabled = false;
                return;
            }
            modeloGO.transform.SetParent(transform, false);
            modeloGO.transform.localPosition = Vector3.zero;
            modeloGO.transform.localRotation = Quaternion.identity;
            modeloGO.transform.localScale = Vector3.one;

            _anim = modeloGO.GetComponentInChildren<Animator>();
            if (_anim == null)
                Debug.LogWarning("[PlayerModel] El modelo no tiene Animator.");
        }

        private void Update()
        {
            if (_anim == null) return;
            float velocidad = _ctrl.MoveInput.magnitude; // 0..1 (WASD)
            // opcional: usar velocidad real del rigidbody si hubiera
            _anim.SetFloat(parametroVelocidad, velocidad);
        }
    }
}
