using UnityEngine;

namespace SlugOut.World
{
    /// NPC que deambula en un radio alrededor de su punto de origen, apoyado en el terreno
    /// procedural. Controla el Animator del modelo 3D con el parametro "Speed" (0 = idle,
    /// 1 = walk) para que el personaje camine solo cuando se mueve. Barato para GT 640: sin
    /// fisicas, solo movimiento por senos y Terrain3D.Altura.
    public class NpcAmbulante : MonoBehaviour
    {
        [SerializeField] private float radio = 6f;
        [SerializeField] private float velocidad = 1.6f;
        [SerializeField] private float esperaMin = 1f, esperaMax = 4f;
        public string modeloPath = "";   // ej. "Characters/NpcSabio"

        private Animator _anim;
        private Vector3 _origen;
        private Vector3 _destino;
        private float _timer;
        private bool _caminando;

        private void Start()
        {
            _origen = transform.position;
            _destino = _origen;
            if (!string.IsNullOrEmpty(modeloPath))
            {
                var prefab = Resources.Load<GameObject>(modeloPath);
                if (prefab != null)
                {
                    var go = Instantiate(prefab, transform.position, Quaternion.identity, transform);
                    go.transform.localPosition = Vector3.zero;
                    _anim = go.GetComponentInChildren<Animator>();
                }
                else Debug.LogWarning("[NpcAmbulante] falta Resources/" + modeloPath);
            }
            else _anim = GetComponentInChildren<Animator>();
            ElegirDestino();
        }

        private void ElegirDestino()
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(0f, radio);
            _destino = _origen + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            _destino.y = Terrain3D.Altura(_destino.x, _destino.z);
            _timer = Random.Range(esperaMin, esperaMax);
            _caminando = true;
        }

        private void Update()
        {
            if (!_caminando)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f) ElegirDestino();
                if (_anim != null) _anim.SetFloat("Speed", 0f);
                return;
            }
            Vector3 dir = _destino - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.05f)
            {
                _caminando = false;
                _timer = Random.Range(esperaMin, esperaMax);
                if (_anim != null) _anim.SetFloat("Speed", 0f);
                return;
            }
            dir.Normalize();
            transform.position += dir * velocidad * Time.deltaTime;
            transform.position = new Vector3(transform.position.x, Terrain3D.Altura(transform.position.x, transform.position.z), transform.position.z);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
            if (_anim != null) _anim.SetFloat("Speed", 1f);
        }
    }
}
