using UnityEngine;
using SlugOut.Core;

namespace SlugOut.Slugs
{
    [CreateAssetMenu(fileName = "NewSlugData", menuName = "SlugOut/Slug Data", order = 1)]
    public class SlugData : ScriptableObject
    {
        [Header("Identidad")]
        public string slugId = "infierno";
        public string slugName = "Infierno";
        [TextArea(2, 4)]
        public string description = "Babosa tipo Fuego capaz de desatar llamaradas devastadoras al superar las 100 MPH.";
        public ElementType element = ElementType.Fire;

        [Header("Registro de campo")]
        [Tooltip("Donde vive. Si queda vacio, el generador lo completa por elemento.")]
        public string habitat = "";
        [Tooltip("Que hace al observarla: es lo que el jugador aprende antes de acercarse.")]
        public string conducta = "";
        [Tooltip("Para que sirve su habilidad, en palabras claras.")]
        public string uso = "";
        [Tooltip("Que no puede resolver. Ninguna babosa gana sola.")]
        public string limitacion = "";

        [Header("Estadisticas de Combate")]
        [Tooltip("Danio directo infligido al impacar al oponente")]
        public float baseDamage = 35f;
        [Tooltip("Velocidad de aceleracion inicial en MPH")]
        public float launchSpeedMph = 45f;
        [Tooltip("Aceleracion hacia las 100 MPH (MPH por segundo)")]
        public float accelerationRate = 85f;
        [Tooltip("Energia requerida para cargar y disparar")]
        public float requiredEnergy = 100f;
        [Tooltip("Prioridad / Poder de choque en colision en el aire")]
        public float clashPower = 50f;

        [Header("Habilidad Especial al Transformarse")]
        public SlugAbilityType abilityType = SlugAbilityType.DirectDamage;
        [Tooltip("Magnitud del efecto (curacion, escudo, danio por quemadura, duracion de congelacion)")]
        public float abilityMagnitude = 15f;
        public float effectDuration = 3f;

        [Header("Rol de enemigo (solo si no es la babosa del jugador)")]
        [Tooltip("Determina el arquetipo de EnemySlugAI para esta especie enemiga.")]
        public SlugData.RolEnemigo rolEnemigo = SlugData.RolEnemigo.Normal;

        [Header("Recursos Visuales")]
        public Sprite protoformSprite;
        public Sprite velocimorphSprite;
        public Color elementColor = new Color(1f, 0.35f, 0.1f);
        public Color velocimorphAuraColor = new Color(1f, 0.7f, 0.2f);

        /// Rol de enemigo: Normal = arquetipo derivado de los stats, Volador/Tejedor/Excavador = rol fijo.
        public enum RolEnemigo { Normal, Volador, Tejedor, Excavador }
    }
}
