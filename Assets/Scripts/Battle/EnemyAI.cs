using UnityEngine;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.Battle
{
    public class EnemyAI : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Duelist enemyDuelist;
        [SerializeField] private Duelist targetPlayerDuelist;

        [Header("Dificultad e Inteligencia")]
        [Tooltip("Velocidad de carga de energia pasiva por segundo")]
        [SerializeField] private float energyGainRate = 18f;
        [Tooltip("Intervalo de decision tactica (segundos)")]
        [SerializeField] private float decisionInterval = 1.2f;

        private float _decisionTimer = 0f;

        private void Awake()
        {
            if (enemyDuelist == null)
            {
                enemyDuelist = GetComponent<Duelist>();
            }
        }

        private void Update()
        {
            if (enemyDuelist == null || enemyDuelist.CurrentHealth <= 0f) return;
            if (targetPlayerDuelist != null && targetPlayerDuelist.CurrentHealth <= 0f) return;

            // Simular carga de energia de fichas enemigas
            float charge = energyGainRate * Time.deltaTime;
            for (int i = 0; i < enemyDuelist.EquippedCount; i++)
            {
                enemyDuelist.AddEnergyToSlot(i, charge);
            }

            _decisionTimer += Time.deltaTime;
            if (_decisionTimer >= decisionInterval)
            {
                _decisionTimer = 0f;
                MakeTacticalDecision();
            }
        }

        private void MakeTacticalDecision()
        {
            // Evaluar cual babosa lista tiene mayor ventaja
            int bestSlot = -1;
            float bestScore = -1f;

            for (int i = 0; i < enemyDuelist.EquippedCount; i++)
            {
                if (enemyDuelist.CanFire(i))
                {
                    SlugData slug = enemyDuelist.GetSlug(i);
                    float score = EvaluateSlugScore(slug);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSlot = i;
                    }
                }
            }

            if (bestSlot >= 0)
            {
                enemyDuelist.FireSlug(bestSlot);
            }
        }

        private float EvaluateSlugScore(SlugData slug)
        {
            if (slug == null) return 0f;

            float score = slug.baseDamage;

            // Si el rival esta bajo de vida y la babosa cura o da escudo
            if (enemyDuelist.CurrentHealth < enemyDuelist.MaxHealth * 0.4f)
            {
                if (slug.abilityType == SlugAbilityType.Heal || slug.abilityType == SlugAbilityType.Shield)
                {
                    score += 50f;
                }
            }

            // Ventaja elemental frente al jugador
            if (targetPlayerDuelist != null)
            {
                float mult = ElementalChart.GetMultiplier(slug.element, targetPlayerDuelist.DefenderElement);
                score *= mult;
            }

            return score;
        }
    }
}
