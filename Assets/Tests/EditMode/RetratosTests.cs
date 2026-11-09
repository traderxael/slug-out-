using NUnit.Framework;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// Las diez especies que caian al color de elemento ahora tienen retrato propio.
    /// Protoforms usa el nombre capitalizado y Velocimorphs el id; el juego busca ambos.
    public class RetratosTests
    {
        static readonly string[,] Especies =
        {
            { "bloque", "Bloque" },
            { "abisal", "Abisal" },
            { "abismo", "Abismo" },
            { "corriente", "Corriente" },
            { "brisa", "Brisa" },
            { "brote", "Brote" },
            { "centella", "Centella" },
            { "elemental-energia", "Elemental de Energia" },
            { "ghoul", "Ghoul" },
            { "primigenia", "Primigenia" },
        };

        [Test] public void LasEspeciesSinArteTienenRetratoEnResources()
        {
            for (int i = 0; i < Especies.GetLength(0); i++)
            {
                string id = Especies[i, 0];
                string nombre = Especies[i, 1];
                Texture2D proto = Resources.Load<Texture2D>("Sprites/Protoforms/" + nombre);
                Texture2D velo = Resources.Load<Texture2D>("Sprites/Velocimorphs/" + id);
                Assert.IsTrue(proto != null || velo != null,
                    "sin retrato para " + id + " (" + nombre + ") en Protoforms ni Velocimorphs");
                // Los renders salen a 512; el arte oficial de Elemental es 128 y se usa tal cual.
                if (proto != null)
                    Assert.GreaterOrEqual(proto.width, 128, "retrato proto de " + id);
                if (velo != null)
                    Assert.GreaterOrEqual(velo.width, 128, "retrato velo de " + id);
            }
        }
    }
}
