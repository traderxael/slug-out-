using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SlugOut.Core;
using SlugOut.UI;
using SlugOut.Board;

namespace SlugOut.Battle
{
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        [Header("Duelistas")]
        [SerializeField] private Duelist playerDuelist;
        [SerializeField] private Duelist enemyDuelist;

        [Header("Interfaz y Tablero")]
        [SerializeField] private BattleHUD battleHUD;
        [SerializeField] private Match3Board match3Board;

        [Header("Efectos y Camara")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GameObject clashSparkPrefab;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Start()
        {
            if (playerDuelist != null)
            {
                playerDuelist.OnSlugFired += HandleSlugFired;
            }
            if (enemyDuelist != null)
            {
                enemyDuelist.OnSlugFired += HandleSlugFired;
            }
        }

        private void Update()
        {
            UpdateFastestProjectileSpeed();
        }

        private void HandleSlugFired(Slugs.SlugData slug, bool isPlayer)
        {
            // Efecto de disparo
            TriggerCameraShake(0.08f, 0.05f);
        }

        private void UpdateFastestProjectileSpeed()
        {
            // Buscar proyectiles activos para mostrar velocimetro en vivo
            SlugProjectile[] projs = FindObjectsByType<SlugProjectile>();
            float topSpeed = 0f;

            foreach (var p in projs)
            {
                if (p.isPlayerProjectile && p.currentSpeedMph > topSpeed)
                {
                    topSpeed = p.currentSpeedMph;
                }
            }

            if (battleHUD != null)
            {
                battleHUD.DisplaySpeed(topSpeed);
            }
        }

        public void TriggerClashEffect(Vector2 position)
        {
            TriggerCameraShake(0.2f, 0.2f);
            if (clashSparkPrefab != null)
            {
                Instantiate(clashSparkPrefab, position, Quaternion.identity);
            }
        }

        public void TriggerCameraShake(float duration, float magnitude)
        {
            if (mainCamera != null)
            {
                StartCoroutine(CameraShakeRoutine(duration, magnitude));
            }
        }

        private IEnumerator CameraShakeRoutine(float duration, float magnitude)
        {
            Vector3 originalPos = mainCamera.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                float y = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                mainCamera.transform.position = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
                yield return null;
            }

            mainCamera.transform.position = originalPos;
        }
    }
}
