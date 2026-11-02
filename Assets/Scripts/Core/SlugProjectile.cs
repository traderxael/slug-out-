using System;
using UnityEngine;
using SlugOut.Slugs;

namespace SlugOut.Core
{
    public class SlugProjectile : MonoBehaviour
    {
        [Header("Datos de la Babosa")]
        public SlugData data;
        public bool isPlayerProjectile = true;

        [Header("Fisica de Vuelo")]
        public float currentSpeedMph = 40f;
        public float maxSpeedMph = 140f;
        public Vector2 travelDirection = Vector2.right;

        [Header("Estado de Transformacion")]
        public bool isVelocimorph = false;
        public const float VELOCIMORPH_THRESHOLD = 100f;

        [Header("Componentes Visuales")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TrailRenderer trailRenderer;

        public event Action<SlugProjectile> OnTransformed;
        public event Action<SlugProjectile, Vector2> OnClashed;
        public event Action<SlugProjectile, float> OnImpact;

        private float _flightTime = 0f;
        private Vector3 _initialScale;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }
            _initialScale = transform.localScale;
        }

        public void Initialize(SlugData slugData, bool fromPlayer, Vector2 direction)
        {
            data = slugData;
            isPlayerProjectile = fromPlayer;
            travelDirection = direction.normalized;

            currentSpeedMph = slugData != null ? slugData.launchSpeedMph : 40f;
            isVelocimorph = false;

            if (data != null && data.protoformSprite != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = data.protoformSprite;
            }

            // Orientar sprite segun direccion
            if (travelDirection.x < 0)
            {
                transform.localScale = new Vector3(-Mathf.Abs(_initialScale.x), _initialScale.y, _initialScale.z);
            }
            else
            {
                transform.localScale = new Vector3(Mathf.Abs(_initialScale.x), _initialScale.y, _initialScale.z);
            }

            SetupTrail();
        }

        private void SetupTrail()
        {
            if (trailRenderer == null)
            {
                trailRenderer = GetComponent<TrailRenderer>();
                if (trailRenderer == null)
                {
                    trailRenderer = gameObject.AddComponent<TrailRenderer>();
                    trailRenderer.time = 0.25f;
                    trailRenderer.startWidth = 0.35f;
                    trailRenderer.endWidth = 0.05f;
                }
            }

            if (data != null && trailRenderer != null)
            {
                trailRenderer.startColor = data.elementColor;
                trailRenderer.endColor = new Color(data.elementColor.r, data.elementColor.g, data.elementColor.b, 0f);
            }
        }

        private void Update()
        {
            _flightTime += Time.deltaTime;

            // Aceleracion hacia las 100 MPH y mas
            float accel = data != null ? data.accelerationRate : 80f;
            currentSpeedMph = Mathf.MoveTowards(currentSpeedMph, maxSpeedMph, accel * Time.deltaTime);

            // Verificar si supera la barrera de las 100 MPH para transformacion a Velocimorfo
            if (!isVelocimorph && currentSpeedMph >= VELOCIMORPH_THRESHOLD)
            {
                TriggerVelocimorph();
            }

            // Movimiento espacial basado en MPH (escala de 100 MPH ~ 12 unidades/segundo)
            float worldSpeed = (currentSpeedMph / VELOCIMORPH_THRESHOLD) * 12f;
            transform.position += (Vector3)(travelDirection * worldSpeed * Time.deltaTime);

            // Efecto de ondulacion o pulso en vuelo
            if (isVelocimorph)
            {
                float pulse = 1f + Mathf.Sin(_flightTime * 14f) * 0.08f;
                float sign = travelDirection.x < 0 ? -1f : 1f;
                transform.localScale = new Vector3(sign * Mathf.Abs(_initialScale.x) * 1.35f * pulse, _initialScale.y * 1.35f * pulse, _initialScale.z);
            }

            // Destruir por limite fuera de pantalla
            if (Mathf.Abs(transform.position.x) > 25f || Mathf.Abs(transform.position.y) > 15f)
            {
                Destroy(gameObject);
            }
        }

        private void TriggerVelocimorph()
        {
            isVelocimorph = true;

            if (data != null && data.velocimorphSprite != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = data.velocimorphSprite;
            }

            if (trailRenderer != null && data != null)
            {
                trailRenderer.startColor = data.velocimorphAuraColor;
                trailRenderer.startWidth = 0.6f;
            }

            OnTransformed?.Invoke(this);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            HandleCollision(collision.gameObject);
        }

        private void HandleCollision(GameObject other)
        {
            // Choque en el aire contra proyectil rival
            SlugProjectile opposing = other.GetComponent<SlugProjectile>();
            if (opposing != null && opposing.isPlayerProjectile != this.isPlayerProjectile)
            {
                ResolveMidAirClash(opposing);
                return;
            }

            // Impacto en Duelista (usando interface desacoplada)
            IDuelistTarget target = other.GetComponent<IDuelistTarget>();
            if (target != null && target.IsPlayer != this.isPlayerProjectile)
            {
                float finalDamage = CalculateDamage(target);
                target.TakeDamage(finalDamage, data != null ? data.element : ElementType.Fire);
                ApplyAbilityEffects(target);

                OnImpact?.Invoke(this, finalDamage);
                Destroy(gameObject);
            }
        }

        private void ResolveMidAirClash(SlugProjectile other)
        {
            float myPower = GetCurrentPower();
            float otherPower = other.GetCurrentPower();

            Vector2 clashPoint = (transform.position + other.transform.position) * 0.5f;
            OnClashed?.Invoke(this, clashPoint);

            if (Mathf.Approximately(myPower, otherPower) || Mathf.Abs(myPower - otherPower) < 5f)
            {
                // Choque parejo: ambas babosas se anulan mutuamente
                Destroy(other.gameObject);
                Destroy(gameObject);
            }
            else if (myPower > otherPower)
            {
                // Nuestra babosa sobrevive pero pierde algo de velocidad/potencia
                currentSpeedMph = Mathf.Max(60f, currentSpeedMph * 0.75f);
                Destroy(other.gameObject);
            }
            else
            {
                // La otra babosa sobrevive
                other.currentSpeedMph = Mathf.Max(60f, other.currentSpeedMph * 0.75f);
                Destroy(gameObject);
            }
        }

        public float GetCurrentPower()
        {
            float power = data != null ? data.clashPower : 40f;
            if (isVelocimorph) power *= 1.5f;
            power += (currentSpeedMph / VELOCIMORPH_THRESHOLD) * 20f;
            return power;
        }

        private float CalculateDamage(IDuelistTarget target)
        {
            float dmg = data != null ? data.baseDamage : 25f;
            if (isVelocimorph)
            {
                dmg *= 1.6f; // Bono por transformacion a 100 MPH
            }

            // Multiplicador elemental
            if (data != null && target != null)
            {
                float multiplier = ElementalChart.GetMultiplier(data.element, target.DefenderElement);
                dmg *= multiplier;
            }

            return dmg;
        }

        private void ApplyAbilityEffects(IDuelistTarget target)
        {
            if (data == null) return;

            switch (data.abilityType)
            {
                case SlugAbilityType.Freeze:
                    target.ApplyFreeze(data.effectDuration);
                    break;
                case SlugAbilityType.BurnOverTime:
                    target.ApplyBurn(data.abilityMagnitude, data.effectDuration);
                    break;
                case SlugAbilityType.Stun:
                    target.ApplyStun(data.effectDuration);
                    break;
            }
        }
    }
}
