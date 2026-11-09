using NUnit.Framework;
using SlugOut.World;

namespace SlugOut.EditModeTests
{
    public class DuelistaVocesTests
    {
        static readonly string[] Ids = { "kord", "trixie", "pronto", "dr-blakk" };

        [Test] public void LosCuatroDuelistasTienenVoz()
        {
            foreach (string id in Ids)
            {
                DuelistaVoces.Voz v;
                Assert.That(DuelistaVoces.TryVoz(id, out v), Is.True, id);
                Assert.That(v.nombre, Is.Not.Null.And.Not.Empty, id);
                Assert.That(v.frases, Is.Not.Null.And.Not.Empty, id);
                foreach (string frase in v.frases)
                    Assert.That(frase, Is.Not.Null.And.Not.Empty, id);
            }
        }

        [Test] public void LasFrasesRotanSinRepetirHastaAgotarse()
        {
            DuelistaVoces.ReiniciarPartida();
            DuelistaVoces.Voz v;
            DuelistaVoces.TryVoz("dr-blakk", out v);

            System.Collections.Generic.HashSet<string> oidas =
                new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < v.frases.Length; i++)
                Assert.That(oidas.Add(DuelistaVoces.Siguiente("dr-blakk")), Is.True,
                    "repitio una frase antes de agotar la lista");

            Assert.That(oidas.Count, Is.EqualTo(v.frases.Length));
            Assert.That(DuelistaVoces.Siguiente("dr-blakk"), Is.EqualTo(v.frases[0]),
                "al agotarse no vuelve al principio");
        }

        [Test] public void ReiniciarPartidaVuelveAlPrincipio()
        {
            DuelistaVoces.ReiniciarPartida();
            string primera = DuelistaVoces.Siguiente("kord");
            DuelistaVoces.Siguiente("kord");
            DuelistaVoces.ReiniciarPartida();
            Assert.That(DuelistaVoces.Siguiente("kord"), Is.EqualTo(primera));
        }

        [Test] public void IdDesconocidoNoRompe()
        {
            Assert.That(DuelistaVoces.Siguiente("fantasma"), Is.Empty);
            Assert.That(DuelistaVoces.NombreDe("fantasma"), Is.EqualTo("FANTASMA"));
        }
    }
}
