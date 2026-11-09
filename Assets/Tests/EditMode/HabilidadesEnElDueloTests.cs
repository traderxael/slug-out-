using System.Collections.Generic;
using NUnit.Framework;
using SlugOut.Core;
using SlugOut.Slugs;
using SlugOut.World;

/// El pedido dice que la misma habilidad de cada babosa se reutiliza en los dos modos. Este
/// archivo cubre el lado del duelo: `Duelo` es una clase plana, asi que se puede armar un 3 vs 3
/// y jugar el turno sin escena ni Play. El lado del campo (Q) se mide en Play con
/// Tools/ArneltHabilidadesCampo.cs, porque ahi si hacen falta actores vivos.
public class HabilidadesEnElDueloTests
{
    static SlugData ConHabilidad(SlugAbilityType t)
    {
        foreach (SlugData s in SlugRoster.Todos)
            if (s != null && s.abilityType == t) return s;
        return null;
    }

    static SlugData RivalCualquiera()
    {
        foreach (SlugData s in SlugRoster.Todos)
            if (s != null && s.abilityType != SlugAbilityType.Heal) return s;
        return null;
    }

    /// La vida del equipo propio se infla para el test: si la IA rival noquea al activo en medio
    /// de la prueba, `TurnoJugador` hace entrar al siguiente y el estado que se queria medir ya
    /// no pertenece al mismo combatiente.
    static void Reforzar(Duelo d)
    {
        foreach (Combatiente c in d.jugador) { c.hpMax = 9999f; c.hp = 9999f; }
    }

    [Test] public void TodaHabilidadTieneAlMenosUnaBabosaEnElRoster()
    {
        foreach (SlugAbilityType t in Contrajuego.Todas)
            Assert.That(ConHabilidad(t), Is.Not.Null,
                "la habilidad " + t + " no la trae ninguna babosa: en el duelo nunca se ve");
    }

    /// Cada habilidad tiene que dejar un efecto **distinto de "no pasó nada"**. Se comprueba con
    /// la frase que el duelo escribe y, en las dos que hacen daño, con la vida del rival: las
    /// otras cinco son de estado (quemadura, congelación, aturdimiento, curación, escudo) y por
    /// eso no bajan la vida. Medido en el codigo: `Habilidad()` solo dana en DirectDamage y
    /// EnergyDrain, igual que `PlayerAbilities.Usar()` en el campo.
    static string FraseEsperada(SlugAbilityType t)
    {
        switch (t)
        {
            case SlugAbilityType.DirectDamage: return "usa su habilidad: golpe directo";
            case SlugAbilityType.BurnOverTime: return "queda ardiendo";
            case SlugAbilityType.Freeze: return "queda congelado";
            case SlugAbilityType.Stun: return "queda aturdido";
            case SlugAbilityType.Heal: return "recupera";
            case SlugAbilityType.Shield: return "levanta un escudo";
            case SlugAbilityType.EnergyDrain: return "recupera";
        }
        return "?";
    }

    static bool HaceDano(SlugAbilityType t)
    {
        return t == SlugAbilityType.DirectDamage || t == SlugAbilityType.EnergyDrain;
    }

    [Test] public void CadaHabilidadSeResuelveEnElDueloYDejaSuRastro()
    {
        foreach (SlugAbilityType t in Contrajuego.Todas)
        {
            SlugData propia = ConHabilidad(t);
            Duelo d = new Duelo();
            d.Cargar(new[] { propia, propia, propia },
                     new[] { RivalCualquiera(), RivalCualquiera(), RivalCualquiera() }, 6f);
            Reforzar(d);
            // Se lastima al propio antes del turno: curarse o escudarse con la vida llena no deja
            // un numero que se pueda leer, y "recupera 0" pasaria igual.
            d.ActivoJ.hp = d.ActivoJ.hpMax - 500f;

            float vidaRivalAntes = d.ActivoR.hp;
            List<string> lineas = d.TurnoJugador(AccionDuelo.Habilidad);
            string log = string.Join(" | ", lineas.ToArray());

            Assert.That(log, Does.Contain(FraseEsperada(t)),
                t + ": el duelo no aplico su efecto (" + log + ")");
            if (t == SlugAbilityType.Heal || t == SlugAbilityType.Shield)
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(log, @"(recupera|escudo de) [1-9]"),
                    Is.True, t + ": el efecto se nombro pero con magnitud cero (" + log + ")");
            Assert.That(d.ActivoJ.enfriamiento, Is.GreaterThan(0),
                t + ": usar la habilidad no dejo enfriamiento");
            if (HaceDano(t))
                Assert.That(d.ActivoR.hp, Is.LessThan(vidaRivalAntes),
                    t + ": paso por el registro pero no le hizo nada al rival");
        }
    }

    [Test] public void LaHabilidadNoSePuedeEncadenarTurnoAFreno()
    {
        SlugData propia = ConHabilidad(SlugAbilityType.Shield);
        Duelo d = new Duelo();
        d.Cargar(new[] { propia, propia, propia },
                 new[] { RivalCualquiera(), RivalCualquiera(), RivalCualquiera() }, 6f);
            Reforzar(d);

        d.TurnoJugador(AccionDuelo.Habilidad);
        Assert.That(d.ActivoJ.enfriamiento, Is.GreaterThan(0),
            "despues de usarla no quedo enfriamiento: el limite que anuncia la ficha es decorativo");

        // Segundo intento inmediato: tiene que negarse por enfriamiento, no aplicar otro efecto.
        List<string> lineas = d.TurnoJugador(AccionDuelo.Habilidad);
        string log = string.Join(" | ", lineas.ToArray());
        Assert.That(log, Does.Contain("espera"),
                "la habilidad se volvio a usar sin respetar el limite: " + log);
    }

    [Test] public void ElLimiteDelRegistroYElDelCartelSonElMismoNumero()
    {
        // El cartel de la ficha dice "una vez cada N turnos" con Duelo.EnfriamientoHabilidad.
        // Si alguien cambia el numero, la ficha y la regla siguen diciendo lo mismo; si se
        // duplicara el literal en la UI, esta prueba no lo puede detectar — por eso se compara
        // contra el valor que dejo el duelo despues de usar la habilidad.
        SlugData propia = ConHabilidad(SlugAbilityType.Stun);
        Duelo d = new Duelo();
        d.Cargar(new[] { propia, propia, propia },
                 new[] { RivalCualquiera(), RivalCualquiera(), RivalCualquiera() }, 6f);
        Reforzar(d);
        d.TurnoJugador(AccionDuelo.Habilidad);
        Assert.That(d.ActivoJ.enfriamiento, Is.EqualTo(Duelo.EnfriamientoHabilidad));
        Assert.That(Duelo.EnfriamientoHabilidad, Is.GreaterThan(1),
            "con enfriamiento 1 el limite anunciado no significaria nada");
    }
}
