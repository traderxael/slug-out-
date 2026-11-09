using System;
using System.Collections.Generic;
using UnityEngine;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.Battle
{
    public class Duelist : MonoBehaviour, IDuelistTarget
    {
        [Header("Configuracion")]
        [SerializeField] private bool isPlayer = true;
        [SerializeField] private ElementType defenderElement = ElementType.Fire;

        [Header("Salud y Escudo")]
        [SerializeField] private float maxHealth = 500f;
        [SerializeField] private float currentHealth = 500f;
        [SerializeField] private float currentShield = 0f;

        [Header("Babosas Equipadas (Arsenal)")]
        [SerializeField] private List<SlugData> equippedSlugs = new List<SlugData>();
        [SerializeField] private float[] slugEnergy = new float[4];

        [Header("Puntos de Disparo")]
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private GameObject projectilePrefab;

        [Header("Efectos de Estado")]
        public float freezeTimer = 0f;
        public float burnTimer = 0f;
        public float burnDamageTick = 0f;
        public float stunTimer = 0f;

        public bool IsPlayer => isPlayer;
        public ElementType DefenderElement => defenderElement;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentShield => currentShield;
        public int EquippedCount => equippedSlugs.Count;

        public event Action<float, float> OnHealthChanged; // current, max
        public event Action<float> OnShieldChanged;
        public event Action<int, float, float> OnSlugEnergyChanged; // slot, current, required
        public event Action<SlugData, bool> OnSlugFired;
        public event Action<float, ElementType> OnDamageTaken;
        public event Action OnDefeated;

        private void Awake()
        {
            currentHealth = maxHealth;
            if (equippedSlugs.Count > 4)
            {
                equippedSlugs = equippedSlugs.GetRange(0, 4);
            }
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnShieldChanged?.Invoke(currentShield);
            for (int i = 0; i < equippedSlugs.Count; i++)
            {
                float req = equippedSlugs[i] != null ? equippedSlugs[i].requiredEnergy : 100f;
                OnSlugEnergyChanged?.Invoke(i, slugEnergy[i], req);
            }
        }

        private void Update()
        {
            HandleStatusEffects();
        }

        private void HandleStatusEffects()
        {
            // Efecto de quemadura
            if (burnTimer > 0f)
            {
                burnTimer -= Time.deltaTime;
                currentHealth -= burnDamageTick * Time.deltaTime;
                if (currentHealth <= 0f)
                {
                    currentHealth = 0f;
                    OnDefeated?.Invoke();
                }
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }

            // Congelamiento y aturdimiento
            if (freezeTimer > 0f) freezeTimer -= Time.deltaTime;
            if (stunTimer > 0f) stunTimer -= Time.deltaTime;
        }

        public SlugData GetSlug(int slot)
        {
            if (slot >= 0 && slot < equippedSlugs.Count) return equippedSlugs[slot];
            return null;
        }

        public float GetSlugEnergy(int slot)
        {
            if (slot >= 0 && slot < slugEnergy.Length) return slugEnergy[slot];
            return 0f;
        }

        public void SetEquippedSlugs(List<SlugData> slugs)
        {
            equippedSlugs = new List<SlugData>(slugs);
            Array.Clear(slugEnergy, 0, slugEnergy.Length);
        }

        public void AddEnergyToSlot(int slot, float amount)
        {
            if (slot < 0 || slot >= equippedSlugs.Count) return;
            if (equippedSlugs[slot] == null) return;

            // Si esta congelado, la carga se reduce a la mitad
            if (freezeTimer > 0f) amount *= 0.5f;

            float req = equippedSlugs[slot].requiredEnergy;
            slugEnergy[slot] = Mathf.Clamp(slugEnergy[slot] + amount, 0f, req);
            OnSlugEnergyChanged?.Invoke(slot, slugEnergy[slot], req);
        }

        public void AddEnergyByElement(ElementType element, float amount)
        {
            for (int i = 0; i < equippedSlugs.Count; i++)
            {
                if (equippedSlugs[i] != null && equippedSlugs[i].element == element)
                {
                    AddEnergyToSlot(i, amount);
                }
            }
        }

        public bool CanFire(int slot)
        {
            if (stunTimer > 0f) return false;
            if (slot < 0 || slot >= equippedSlugs.Count) return false;
            if (equippedSlugs[slot] == null) return false;

            return slugEnergy[slot] >= equippedSlugs[slot].requiredEnergy;
        }

        public SlugProjectile FireSlug(int slot)
        {
            if (!CanFire(slot)) return null;

            SlugData slug = equippedSlugs[slot];
            slugEnergy[slot] = 0f;
            OnSlugEnergyChanged?.Invoke(slot, 0f, slug.requiredEnergy);

            Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position;
            Vector2 dir = isPlayer ? Vector2.right : Vector2.left;

            GameObject projGo;
            if (projectilePrefab != null)
            {
                projGo = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                projGo = new GameObject($"SlugProj_{slug.slugName}");
                projGo.transform.position = spawnPos;
                var col = projGo.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.5f;
                var rb = projGo.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            SlugProjectile projectile = projGo.GetComponent<SlugProjectile>();
            if (projectile == null)
            {
                projectile = projGo.AddComponent<SlugProjectile>();
            }

            projectile.Initialize(slug, isPlayer, dir);
            OnSlugFired?.Invoke(slug, isPlayer);

            return projectile;
        }

        public void TakeDamage(float amount, ElementType attackerElement)
        {
            if (currentHealth <= 0f) return;

            // Reduccion por escudo
            if (currentShield > 0f)
            {
                if (currentShield >= amount)
                {
                    currentShield -= amount;
                    amount = 0f;
                }
                else
                {
                    amount -= currentShield;
                    currentShield = 0f;
                }
                OnShieldChanged?.Invoke(currentShield);
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnDamageTaken?.Invoke(amount, attackerElement);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                OnDefeated?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (currentHealth <= 0f) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void AddShield(float amount)
        {
            currentShield += amount;
            OnShieldChanged?.Invoke(currentShield);
        }

        public void ApplyFreeze(float duration)
        {
            freezeTimer = Mathf.Max(freezeTimer, duration);
        }

        public void ApplyBurn(float damagePerSec, float duration)
        {
            burnDamageTick = Mathf.Max(burnDamageTick, damagePerSec);
            burnTimer = Mathf.Max(burnTimer, duration);
        }

        public void ApplyStun(float duration)
        {
            stunTimer = Mathf.Max(stunTimer, duration);
        }
    }
}
