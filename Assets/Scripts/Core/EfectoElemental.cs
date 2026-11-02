using UnityEngine;
using SlugOut.World;

namespace SlugOut.Core
{
    /// Efecto que cada elemento aplica al impactar. Estatico puro y al lado de ElementalChart,
    /// que es donde ya vive la otra tabla de datos del elemento: no hace falta un manager.
    ///
    /// Antes TakeDamage era lo unico que hacia un tiro, y Quemar/Congelar/Aturdir/DarEscudo solo
    /// se invocaban desde la Q. O sea la promesa de "a carga maxima el efecto esta garantizado"
    /// no existia: el elemento solo multiplicaba el dano.
    public static class EfectoElemental
    {
        /// Probabilidad de que el efecto salga con un tiro que NO venia a carga maxima.
        /// Con carga maxima quien llama fuerza el efecto y esto no se mira.
        public const float ProbabilidadBase = 0.38f;

        /// Aplica el efecto del elemento sobre el objetivo despues del dano. `plena` = el tiro salio
        /// desde el ultimo decil de la barra (FaseDe >= 2 en PlayerShooter), y ahi el efecto es
        /// seguro, cero azar: es el premio al tradeoff de recarga.
        /// Devuelve un resumen para log/sondas.
        public static string Aplicar(ElementType elemento, SlugActor atacante, SlugActor objetivo, float danoBase, bool plena, float probabilidad)
        {
            if (objetivo == null || !objetivo.Alive) return "nulo";
            float roll = plena ? 0f : Random.Range(0f, 1f);
            if (!plena && roll > probabilidad) return "sin efecto (" + roll.ToString("F2") + " > " + probabilidad.ToString("F2") + ")";

            switch (elemento)
            {
                case ElementType.Fire:
                    objetivo.Quemar(danoBase * 0.25f, plena ? 4f : 2.5f);
                    return "quema";
                case ElementType.Ice:
                    objetivo.Congelar(plena ? 1.6f : 0.9f);
                    return "congelado";
                case ElementType.Earth:
                    objetivo.Aturdir(plena ? 0.9f : 0.5f);
                    return "aturdido";
                case ElementType.Water:
                    // "ralentiza el contraataque": en este proyecto el ritmo del rival se compra
                    // con energia, asi que el agua le baja la barra en vez de un status nuevo.
                    DrenarEnergia(objetivo, plena ? 45f : 22f);
                    return "dreno energia";
                case ElementType.Energy:
                    // La sonda violo esta rama: Energy/Light/Toxic caian al "neutro", o sea un
                    // tiro a carga plena con esas babosas seguia sin garantir ningun efecto.
                    DrenarEnergia(objetivo, plena ? 90f : 45f);
                    return "sobrecarga";
                case ElementType.Light:
                    if (atacante != null) atacante.DarEscudo(danoBase * (plena ? 0.8f : 0.4f));
                    return "escudo";
                case ElementType.Toxic:
                    // Quemar es el unico dano por segundo que existe en SlugActor; el veneno es
                    // lo mismo con otro nombre, asi que se reutiliza en vez de inventar un status.
                    objetivo.Quemar(danoBase * (plena ? 0.20f : 0.12f), plena ? 5f : 3f);
                    return "envenena";
                case ElementType.Shadow:
                    float robado = objetivo.TakeDamage(danoBase * 0.35f, elemento);
                    if (atacante != null) atacante.Heal(robado * 0.5f);
                    return "robo vida";
                case ElementType.Plant:
                    if (atacante != null) atacante.Heal(danoBase * (plena ? 0.6f : 0.35f));
                    return "curo";
                case ElementType.Air:
                    // Su premio es la cadencia, no un status. Sin esto el aire era el unico
                    // elemento que a carga plena no garantizaba nada (0 de 11 en la sonda).
                    if (atacante != null) atacante.DevolverEnergia(plena ? 26f : 12f);
                    return "perfora + cadencia";
                case ElementType.Electric:
                    objetivo.Aturdir(plena ? 0.45f : 0.25f);
                    return "chisporroteo";
            }
            return "neutro";
        }

        /// GastaEnergia devuelve false y no toca nada si no alcanza: para un efecto eso es un
        /// fallo silencioso, asi que se pide solo lo que el objetivo tenga.
        static void DrenarEnergia(SlugActor objetivo, float cantidad)
        {
            objetivo.GastaEnergia(Mathf.Min(cantidad, objetivo.Energia));
        }
    }
}
