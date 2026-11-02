using UnityEngine;
using System.Collections.Generic;

namespace SlugOut.World
{
    /// Controlador del mundo abierto: gestiona zonas, aparición de NPCs y eventos simples.
    /// Mantiene baja carga para GT 640: sin fisicas pesadas, usando Terrain3D para alturas.
    public class MundoAbierto : MonoBehaviour
    {
        [Header("Zonas")]
        [Tooltip("Prefab de zona vacía (solo vacío con collider opcional para límites)")]
        public GameObject zonaPrefab;
        [Tooltip("Radio donde aparecen NPCs alrededor del centro de la zona")]
        public float radioNPC = 12f;
        [Tooltip("Número máximo de NPCs simultáneos por zona")]
        public int maxNPCTipo = 3;

        [Header("Prefabs NPC")]
        [Tooltip("Prefab o recurso para NPC tipo sabio")]
        public GameObject npcSabioPrefab;
        [Tooltip("Prefab o recurso para NPC tipo explorador")]
        public GameObject npcExploradorPrefab;
        [Tooltip("Prefab o recurso para NPC tipo bárbaro")]
        public GameObject npcBarbaroPrefab;

        [Header("Tiempo")]
        [Tooltip("Segundos entre olas de aparición de nuevos NPCs")]
        public float tiempoEntreOlas = 20f;
        [Tooltip("Desviación aleatoria del tiempo entre olas (±)")]
        public float variacionOlas = 5f;

        private List<Transform> _zonasActivas = new List<Transform>();
        private List<NpcAmbulante> _npcsActivos = new List<NpcAmbulante>();
        private float _timerOla;

        private void Start()
        {
            // Detectar zonas existentes en la escena (tag "Zona")
            var zonasObj = GameObject.FindGameObjectsWithTag("Zona");
            foreach (var z in zonasObj)
                _zonasActivas.Add(z.transform);

            // Si no hay zonas, crear una zona predeterminada alrededor del origen
            if (_zonasActivas.Count == 0)
            {
                Debug.Log("[MundoAbierto] No se encontraron zonas con tag 'Zona', creando zona predeterminada.");
                var zonaGO = new GameObject("Zona_Predeterminada");
                zonaGO.tag = "Zona";
                zonaGO.transform.position = Vector3.zero;
                _zonasActivas.Add(zonaGO.transform);
            }

            _timerOla = TiempoEntreOlas();
            AparecerNpcsIniciales();
        }

        private void Update()
        {
            _timerOla -= Time.deltaTime;
            if (_timerOla <= 0f)
            {
                _timerOla = TiempoEntreOlas();
                AparecerNpcsOla();
            }
        }

        private float TiempoEntreOlas() => tiempoEntreOlas + Random.Range(-variacionOlas, variacionOlas);

        private void AparecerNpcsIniciales()
        {
            // Aparecer pocos NPCs al start para que el mundo no empiece vacío
            foreach (var zona in _zonasActivas)
            {
                int sabios = Random.Range(1, maxNPCTipo + 1);
                int exploradores = Random.Range(0, maxNPCTipo);
                int barbaros = Random.Range(0, maxNPCTipo);

                AparecerNpcsEnZona(zona, npcSabioPrefab, sabios, "Sabio");
                AparecerNpcsEnZona(zona, npcExploradorPrefab, exploradores, "Explorador");
                AparecerNpcsEnZona(zona, npcBarbaroPrefab, barbaros, "Barbaro");
            }
        }

        private void AparecerNpcsOla()
        {
            foreach (var zona in _zonasActivas)
            {
                // Ola ligera: 0-2 NPCs adicionales por tipo
                int sabios = Random.Range(0, 2);
                int exploradores = Random.Range(0, 2);
                int barbaros = Random.Range(0, 2);

                AparecerNpcsEnZona(zona, npcSabioPrefab, sabios, "Sabio");
                AparecerNpcsEnZona(zona, npcExploradorPrefab, exploradores, "Explorador");
                AparecerNpcsEnZona(zona, npcBarbaroPrefab, barbaros, "Barbaro");
            }
        }

        private void AparecerNpcsEnZona(Transform zonaCentro, GameObject prefabTipo, int cantidad, string tipoNombre)
        {
            if (prefabTipo == null || cantidad <= 0) return;

            for (int i = 0; i < cantidad; i++)
            {
                // Elegir punto aleatorio dentro del radio de la zona
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float rad = Random.Range(0f, radioNPC);
                Vector3 punto = zonaCentro.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rad;
                punto.y = Terrain3D.Altura(punto.x, punto.z); // apoiar en terreno

                // Instanciar NPC
                GameObject npcGO = Instantiate(prefabTipo, punto, Quaternion.identity);
                npcGO.name = $"NPC_{tipoNombre}_{Random.Range(1000, 9999)}";

                // Asegurar componente NpcAmbulante (si no lo tiene el prefab)
                var npcComp = npcGO.GetComponent<NpcAmbulante>();
                if (npcComp == null)
                    npcComp = npcGO.AddComponent<NpcAmbulante>();

                // Opcional: asignar modelo si el prefab no lo trae
                if (npcComp != null && string.IsNullOrEmpty(npcComp.modeloPath))
                {
                    // intentar asignar un modelo basado en tipo (si los Resources existen)
                    string modeloPath = "";
                    if (tipoNombre.Contains("Sabio")) modeloPath = "Characters/NpcSabio";
                    else if (tipoNombre.Contains("Explorador")) modeloPath = "Characters/NpcExplorador";
                    else if (tipoNombre.Contains("Barbaro")) modeloPath = "Characters/NpcBarbaro";

                    if (!string.IsNullOrEmpty(modeloPath))
                        npcComp.modeloPath = modeloPath;
                }

                _npcsActivos.Add(npcComp);
            }
        }

        /// Limpieza llamada por otros sistemas (ej. al cambiar de escena)
        public void Limpiar()
        {
            foreach (var npc in _npcsActivos)
                if (npc != null) Destroy(npc.gameObject);
            _npcsActivos.Clear();
        }
    }
}