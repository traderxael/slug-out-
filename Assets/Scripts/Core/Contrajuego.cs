using System.Collections.Generic;

namespace SlugOut.Core
{
    /// Que corta a cada habilidad, como dato. Antes el contrarrestador solo existia de dos
    /// maneras: la tabla elemental (1,5x / 0,75x) y el campo `limitacion` de la babosa, que el
    /// torneo rotulaba como "EN EL CAMPO". O sea: la limitacion y el contrajuego eran el mismo
    /// string, y "cada habilidad tiene un contrarrestador" del pedido no estaba en ningun lado
    /// consultable.
    ///
    /// La mapping es propuesta, no canon: cada fila dice que habilidad hay que jugarle en contra
    /// y como se explica en una linea. El nombre corto del contrajuego sale de
    /// `SlugOut.World.Duelo.NombreEfecto`, para no tener dos tablas de nombres.
    public struct ContrajuegoDato
    {
        public SlugAbilityType contrarresta;
        public string texto;

        public ContrajuegoDato(SlugAbilityType c, string t)
        {
            contrarresta = c;
            texto = t;
        }
    }

    public static class Contrajuego
    {
        static readonly Dictionary<SlugAbilityType, ContrajuegoDato> FILAS =
            new Dictionary<SlugAbilityType, ContrajuegoDato>
        {
            { SlugAbilityType.DirectDamage, new ContrajuegoDato(SlugAbilityType.Shield,
                "Aguantalo con el escudo arriba: el golpe directo choca contra eso.") },
            { SlugAbilityType.Heal, new ContrajuegoDato(SlugAbilityType.Stun,
                "Interrumpilo aturdiendo antes de que cierre la curacion.") },
            { SlugAbilityType.Shield, new ContrajuegoDato(SlugAbilityType.EnergyDrain,
                "Drenalo: sin energia el escudo se cae solo.") },
            { SlugAbilityType.Freeze, new ContrajuegoDato(SlugAbilityType.BurnOverTime,
                "Quemalo: el calor le devuelve el movimiento.") },
            { SlugAbilityType.BurnOverTime, new ContrajuegoDato(SlugAbilityType.Freeze,
                "Congelalo: el hielo apaga la quemadura.") },
            { SlugAbilityType.Stun, new ContrajuegoDato(SlugAbilityType.Shield,
                "Con el escudo puesto no te corta la carga.") },
            { SlugAbilityType.EnergyDrain, new ContrajuegoDato(SlugAbilityType.DirectDamage,
                "Golpe rapido: cierra la pelea antes del drenaje.") },
        };

        /// Todas las habilidades que el juego puede dar. Si se agrega una al enum sin fila aqui,
        /// `De` devuelve un dato vacio y la prueba de cobertura lo corta.
        public static readonly SlugAbilityType[] Todas =
        {
            SlugAbilityType.DirectDamage, SlugAbilityType.Heal, SlugAbilityType.Shield,
            SlugAbilityType.Freeze, SlugAbilityType.BurnOverTime, SlugAbilityType.Stun,
            SlugAbilityType.EnergyDrain
        };

        public static ContrajuegoDato De(SlugAbilityType habilidad)
        {
            ContrajuegoDato d;
            return FILAS.TryGetValue(habilidad, out d) ? d : default(ContrajuegoDato);
        }

        public static bool Tiene(SlugAbilityType habilidad)
        {
            return FILAS.ContainsKey(habilidad);
        }

        public static int Filas { get { return FILAS.Count; } }
    }
}
