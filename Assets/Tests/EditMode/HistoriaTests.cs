using NUnit.Framework;
using SlugOut.World;

namespace SlugOut.EditModeTests
{
    /// Guardas del arco narrativo: los 28 fragmentos siguen siendo 28, entran en la tarjeta,
    /// no traen texto roto y el elenco aparece. No fija frases exactas: eso es prosa, no contrato.
    public class HistoriaTests
    {
        [Test] public void LosFragmentosSonConsecutivosYSinHuecos()
        {
            for (int n = 1; n <= SlugStory.TotalCampana; n++)
                Assert.That(SlugStory.DeCampana(n).nivel, Is.EqualTo(n), "nivel " + n);
        }

        [Test] public void TitulosUnicosYNoVacios()
        {
            System.Collections.Generic.HashSet<string> vistos =
                new System.Collections.Generic.HashSet<string>();
            foreach (SlugStory.Fragmento f in SlugStory.Campana())
            {
                Assert.That(f.titulo, Is.Not.Null.And.Not.Empty, "nivel " + f.nivel);
                Assert.That(vistos.Add(f.titulo), Is.True, "titulo repetido: " + f.titulo);
            }
        }

        [Test] public void CadaTextoEntraEnLaTarjeta()
        {
            foreach (SlugStory.Fragmento f in SlugStory.Campana())
            {
                Assert.That(f.texto, Is.Not.Null.And.Not.Empty, "nivel " + f.nivel);
                Assert.That(f.texto.Length, Is.InRange(60, 380),
                    "nivel " + f.nivel + " mide " + f.texto.Length + " caracteres");
            }
            foreach (SlugStory.Fragmento f in SlugStory.Infinito())
            {
                Assert.That(f.texto.Length, Is.InRange(60, 380),
                    "fragmento infinito " + f.nivel + " mide " + f.texto.Length + " caracteres");
            }
        }

        [Test] public void NoHayMojibakeNiPlaceholders()
        {
            foreach (SlugStory.Fragmento f in SlugStory.Campana())
            {
                // Puntos de codigo, no literales: el propio archivo de test se guardo con
                // caracteres de reemplazo y la guarda terminaba midiendo otra cosa.
                Assert.That(f.texto.IndexOf((char)0x00C3), Is.EqualTo(-1), "C3 en nivel " + f.nivel);
                Assert.That(f.texto.IndexOf((char)0x00C2), Is.EqualTo(-1), "C2 en nivel " + f.nivel);
                Assert.That(f.texto.IndexOf((char)0xFFFD), Is.EqualTo(-1), "reemplazo en nivel " + f.nivel);
                Assert.That(f.texto, Does.Not.Contain("???"), "nivel " + f.nivel);
            }
        }

        [Test] public void ElElencoApareceEnLaCampana()
        {
            string todo = string.Join(" ", System.Array.ConvertAll(
                SlugStory.Campana(), delegate (SlugStory.Fragmento f) { return f.texto; }));

            Assert.That(todo, Does.Contain("Kord"), "Kord no aparece en la campana");
            Assert.That(todo, Does.Contain("Trixie"), "Trixie no aparece en la campana");
            Assert.That(todo, Does.Contain("Pronto"), "Pronto no aparece en la campana");
            Assert.That(todo, Does.Contain("Blakk"), "Blakk no aparece en la campana");
            Assert.That(todo, Does.Contain("padre"), "el motivo de Eli no aparece en la campana");
        }

        /// El lore del infinito prometia gemas y un anillo que se invierte; ninguna de las dos
        /// cosas existe en el modo. La guarda mira lo que el texto afirma, no una subcadena suelta:
        /// el fragmento 24 dice justamente "no se da vuelta", que es lo correcto.
        [Test] public void ElInfinitoNoPrometeMecanicasInexistentes()
        {
            foreach (SlugStory.Fragmento f in SlugStory.Infinito())
            {
                string t = f.texto.ToLowerInvariant();
                Assert.That(t, Does.Not.Contain("gema"), "fragmento " + f.nivel);
                Assert.That(t, Does.Not.Contain("se invierte"), "fragmento " + f.nivel);
            }

            string ola24 = SlugStory.DeOlaInfinito(24).texto.ToLowerInvariant();
            Assert.That(ola24, Does.Contain("no se da vuelta"),
                "el fragmento 24 tiene que decir que el anillo NO se invierte en infinito");
        }

        /// La carta de campana congela la arena, asi que le cede la pantalla al HUD; la del
        /// infinito suena mientras se pelea y ahi el HUD de combate tiene que seguir visible.
        [Test] public void SoloLaCartaDeCampanaCedeLaPantallaAlHud()
        {
            Assert.IsTrue(GameHUD.CartaTapaElHud(true, false), "carta de campana sin ceder el HUD");
            Assert.IsFalse(GameHUD.CartaTapaElHud(true, true), "el HUD desaparece durante el infinito");
            Assert.IsFalse(GameHUD.CartaTapaElHud(false, false), "HUD oculto sin carta en pantalla");
        }
    }
}
