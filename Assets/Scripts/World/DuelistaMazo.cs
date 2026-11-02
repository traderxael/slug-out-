using UnityEngine;

namespace SlugOut.World
{
    /// Los duelistas del mazo de diseno (Canva "bajoterra") que pelean en la arena:
    /// rivales que aparecen cada cierta cantidad de enemigos y un jefe de tramo.
    /// Los modelos vienen de Resources/Duelists/<id>, generados en Blender.
    public static class DuelistaMazo
    {
        public const string Jefe = "dr-blakk";

        static readonly string[] Rivales = { "trixie", "kord", "pronto" };

        /// Cada cuantos enemigos aparece un rival. Con 7 se los ve seguido en una
        /// campana de 8-12 enemigos por nivel sin que la arena se llenen de humanos.
        public const int CadaRival = 7;

        /// En infinito no existe el "ultimo enemigo", entonces el jefe marca la oleada.
        public const int CadaJefeEnInfinito = 25;

        /// Id del duelistas que tiene que aparecer en este spawn, o null si va babosa normal.
        /// `spawned` es cuantos nacieron antes de este; se cuenta el numero de orden (1-based)
        /// para que el primer rival caiga en el enemigo 7 y no en el 0.
        public static string Elegir(int spawned, int totalEnemies, bool infinito, int indiceRival)
        {
            int numero = spawned + 1;
            if (!infinito && numero >= totalEnemies) return Jefe;
            if (infinito && numero % CadaJefeEnInfinito == 0) return Jefe;
            if (numero % CadaRival == 0) return Rivales[Mathf.Abs(indiceRival) % Rivales.Length];
            return null;
        }

        public static GameObject Cargar(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Resources.Load<GameObject>("Duelists/" + id);
        }
    }
}
