using NUnit.Framework;
using UnityEngine;
using SlugOut.World;

/// Dos solapamientos que aparecieron a 1024x576 y no se veian a 1360x768: el pie del menu se
/// dibujaba encima de la ultima fila, y el boton QUITAR DEL EQUIPO encima del tercer slot.
/// Las formulas viven en GameFlow (helpers publicos) para que esta prueba mire el mismo calculo
/// que usa la pantalla, no una copia.
public class LayoutPantallasChicasTests
{
    static readonly float[] Alturas = { 576f, 720f, 768f, 900f, 1080f };
    static readonly float[] Anchos = { 1024f, 1280f, 1360f, 1920f };

    [Test] public void ElMenuNoPisaElPieDePantalla()
    {
        foreach (float h in Alturas)
        {
            // 8 filas sin progreso guardado, 9 con CONTINUAR (REGISTRO incluida). Con 7/8 el
            // barrido no veia la novena fila: a 576 SALIR caia en 549,5 y el pie arranca en 532.
            foreach (int filas in new[] { 8, 9 })
            {
                float paso = GameFlow.PasoFilaMenu(h, filas);
                float ultimoFondo = GameFlow.ArranqueMenu(h) + filas * (paso + 6f);
                float techoDelPie = h - 44f;
                Assert.That(ultimoFondo, Is.LessThanOrEqualTo(techoDelPie),
                    "a " + h + " px de alto con " + filas + " filas la ultima fila ("
                    + ultimoFondo.ToString("F0") + ") cae sobre el pie ("
                    + techoDelPie.ToString("F0") + ")");
                Assert.That(paso, Is.GreaterThanOrEqualTo(34f),
                    h + ": las filas se aplastaron tanto que no se leen");
            }
        }
    }

    [Test] public void LaFilaDeEquipoTerminaAntesDeLaFicha()
    {
        foreach (float w in Anchos)
        {
            float paso = GameFlow.PasoSlotEquipo(w);
            float ancho = GameFlow.AnchoSlotEquipo(w);
            float fin = 120f + 2f * paso + ancho;
            float bordeIzquierdoDeLaFicha = w * 0.60f;
            Assert.That(fin, Is.LessThanOrEqualTo(bordeIzquierdoDeLaFicha),
                w + " px de ancho: el tercer slot llega a " + fin.ToString("F0")
                + " y la ficha empieza en " + bordeIzquierdoDeLaFicha.ToString("F0"));
            Assert.That(ancho, Is.GreaterThanOrEqualTo(120f),
                w + ": los slots se achicaron tanto que el nombre no cabe");
        }
    }

    /// A 1360x768 es donde esta verificado el aspecto actual: si una de estas dos cambia, la
    /// pantalla que ya fue mirada cambia con ella.
    [Test] public void LaConfiguracionVerificadaA_1360x768_NoSeMueve()
    {
        Assert.That(GameFlow.PasoFilaMenu(768f, 8), Is.EqualTo(54f).Within(0.01f));
        Assert.That(GameFlow.ArranqueMenu(768f), Is.EqualTo(158f).Within(0.01f));
        Assert.That(GameFlow.AnchoSlotEquipo(1360f), Is.EqualTo(168f).Within(0.01f));
    }
}
