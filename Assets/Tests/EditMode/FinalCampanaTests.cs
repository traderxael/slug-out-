using NUnit.Framework;
using SlugOut.World;

namespace SlugOut.EditModeTests
{
    public class FinalCampanaTests
    {
        [Test] public void ElEpilogoCierraElNivel28()
        {
            SlugStory.Fragmento e = SlugStory.Epilogo();
            Assert.That(e.nivel, Is.EqualTo(28));
            Assert.That(e.titulo, Is.Not.Null.And.Not.Empty);
            Assert.That(e.texto, Is.Not.Null.And.Not.Empty);
            Assert.That(e.texto.Length, Is.InRange(60, 380));
            Assert.That(SlugStory.EsEpilogo(e), Is.True, "el cierre no se reconoce como epilogo");
            Assert.That(SlugStory.EsEpilogo(SlugStory.DeCampana(28)), Is.False,
                "el fragmento del nivel 28 se confunde con el epilogo");
        }

        [Test] public void ElFinalSoloSeActivaAlGanarEl28DeCampana()
        {
            Assert.That(GameFlow.EsFinalDeCampana(true, false, 28), Is.True);
            Assert.That(GameFlow.EsFinalDeCampana(true, false, 27), Is.False);
            Assert.That(GameFlow.EsFinalDeCampana(false, false, 28), Is.False);
            Assert.That(GameFlow.EsFinalDeCampana(true, true, 28), Is.False);
        }
    }
}
