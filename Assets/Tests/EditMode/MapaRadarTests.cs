using System.Collections.Generic;
using NUnit.Framework;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    public class MapaRadarTests
    {
        [Test] public void ElCentroYElBordeSeMapeanAlDiscoFijo()
        {
            Vector2 centro = MapaRadar.MundoADisco(Vector3.zero, 100f, 100f, 67f);
            Assert.That(centro.x, Is.EqualTo(100f).Within(0.01f));
            Assert.That(centro.y, Is.EqualTo(100f).Within(0.01f));

            Vector2 borde = MapaRadar.MundoADisco(new Vector3(24.6f, 0f, 0f), 100f, 100f, 67f);
            Assert.That(borde.x, Is.GreaterThan(100f));
            Assert.That(borde.x, Is.LessThan(100f + 67f));
            Assert.That(borde.y, Is.EqualTo(100f).Within(0.01f));
        }

        [Test] public void LoQueQuedaFueraSeClampaAlFilo()
        {
            Vector2 fuera = MapaRadar.MundoADisco(new Vector3(100f, 0f, 100f), 100f, 100f, 67f);
            float distancia = (fuera - new Vector2(100f, 100f)).magnitude;
            Assert.That(distancia, Is.EqualTo(67f).Within(0.01f));
        }

        [Test] public void LasCoberturasYLasBocasCabenEnElDisco()
        {
            for (int r = 0; r < Regiones.Todas.Length; r++)
            {
                RegionDefinicion d = Regiones.De(r);
                for (int i = 0; i < d.coberturas.Length; i++)
                {
                    Vector2 q = MapaRadar.CoberturaADisco(
                        d.coberturas[i].angulo + d.anguloBase, d.coberturas[i].radio,
                        0f, 0f, 67f);
                    Assert.That(q.magnitude, Is.LessThanOrEqualTo(67.01f),
                        d.nombre + ": la cobertura " + i + " se sale del minimapa");
                }
                for (int i = 0; i < d.angulosTunel.Length; i++)
                {
                    float a = (d.angulosTunel[i] + d.anguloBase) * Mathf.Deg2Rad;
                    Vector2 q = MapaRadar.MundoADisco(
                        Mathf.Cos(a) * 23.2f, Mathf.Sin(a) * 23.2f, 0f, 0f, 67f);
                    Assert.That(q.magnitude, Is.LessThanOrEqualTo(67.01f),
                        d.nombre + ": la boca " + i + " se sale del minimapa");
                }
            }
        }

        [Test] public void LosCodigosDeRegionSonDistintosYLegibles()
        {
            var codigos = new HashSet<string>();
            for (int i = 0; i < Regiones.Todas.Length; i++)
            {
                string c = MapaRadar.CodigoRegion(i);
                Assert.That(c.Length, Is.EqualTo(2));
                Assert.That(codigos.Add(c), Is.True, "codigo repetido: " + c);
                Assert.That(MapaRadar.NombreCorto(i), Is.Not.Empty);
            }
        }
    }
}
