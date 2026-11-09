using System.Collections.Generic;
using UnityEngine;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.World
{
    public class Combatiente
    {
        public SlugData datos;
        public float hp;
        public float hpMax;
        public float escudo;
        public int quemaduraTurnos;
        public float quemaduraDano;
        public int congeladoTurnos;
        public bool aturdido;
        public int enfriamiento;

        public bool Vivo { get { return hp > 0f; } }
        public string Nombre { get { return datos != null ? datos.slugName : "?"; } }
        public ElementType Elemento { get { return datos != null ? datos.element : ElementType.Fire; } }
        public string Estado
        {
            get
            {
                if (congeladoTurnos <= 0) return "";
                return aturdido ? "aturdido" : "congelado";
            }
        }
    }

    public enum AccionDuelo { Atacar, Habilidad, Cambiar }

    /// Duelo por turnos puro: no toca escena, input ni tiempo real, asi que un arnes puede
    /// jugarlo entero. Reutiliza la tabla elemental y los SlugData del combate de exploracion,
    /// pero adapta cada habilidad a reglas de turnos (escudo, quemadura, congelacion, curacion).
    public class Duelo
    {
        public const float VidaBase = 62f;
        /// Cuantos turnos tardan en volver a habilitarse las habilidades dentro del duelo. unica
        /// fuente: el panel del torneo lee este numero en vez de repetirlo a mano, asi el cartel
        /// no puede quedar mentiroso si algun dia depende de la babosa.
        public const int EnfriamientoHabilidad = 2;

        public readonly List<Combatiente> jugador = new List<Combatiente>();
        public readonly List<Combatiente> rival = new List<Combatiente>();
        public readonly List<string> registro = new List<string>();

        /// Las acciones que pidio el lado del jugador, en orden. No se usa para jugar: es el
        /// historial que un checkpoint tendria que mandar para que el otro lado reconstruya el
        /// mismo duelo (ver `DueloCheckpoint`). Se limpia en `Cargar`, como el resto del estado.
        public readonly List<Jugada> jugadas = new List<Jugada>();

        public int activoJugador;
        public int activoRival;
        public int turno;

        public bool Terminado { get; private set; }
        public bool GanoJugador { get; private set; }

        /// Semilla con la que se armo ESTE duelo. La IA del rival decide con esta y no con
        /// `UnityEngine.Random`: con el RNG global, dos torneos con la misma semilla daban
        /// resultados distintos, o sea que el torneo no se podía reproducir ni verificar jugada por
        /// jugada — que es justamente lo que hace falta antes de pensar en un rival remoto.
        public int Semilla { get; private set; }
        private System.Random _rng;

        /// Cuanto se le infló la vida al rival en ESTE duelo. Se guarda (y no se deduce de `hpMax`)
        /// para que un checkpoint pueda reconstruir el mismo duelo sin adivinar la escala.
        public float EscalaRival { get; private set; }

        public Combatiente ActivoJ { get { return jugador[activoJugador]; } }
        public Combatiente ActivoR { get { return rival[activoRival]; } }

        public void Cargar(SlugData[] equipoJugador, SlugData[] equipoRival, float escalaRival = 1f,
                           int semilla = 0)
        {
            Semilla = semilla;
            EscalaRival = escalaRival;
            _rng = new System.Random(semilla);
            jugador.Clear();
            rival.Clear();
            registro.Clear();
            jugadas.Clear();
            turno = 0;
            activoJugador = 0;
            activoRival = 0;
            Terminado = false;
            GanoJugador = false;

            for (int i = 0; i < 3; i++)
                jugador.Add(Nuevo(equipoJugador != null && i < equipoJugador.Length ? equipoJugador[i] : null, 1f));
            for (int i = 0; i < 3; i++)
                rival.Add(Nuevo(equipoRival != null && i < equipoRival.Length ? equipoRival[i] : null, escalaRival));

            Anotar("Duelo de 3 contra 3: " + Resumen(jugador) + " vs " + Resumen(rival) + ".");
            Anotar("Turno 1. Elegi una accion.");
        }

        private static Combatiente Nuevo(SlugData d, float escala)
        {
            float hp = VidaBase * escala;
            return new Combatiente
            {
                datos = d,
                hp = hp,
                hpMax = hp,
                escudo = 0f,
                quemaduraTurnos = 0,
                congeladoTurnos = 0,
                aturdido = false,
                enfriamiento = 0
            };
        }

        private static string Resumen(List<Combatiente> equipo)
        {
            return equipo[0].Nombre + ", " + equipo[1].Nombre + " y " + equipo[2].Nombre;
        }

        public static string NombreEfecto(SlugAbilityType tipo)
        {
            switch (tipo)
            {
                case SlugAbilityType.DirectDamage: return "golpe directo";
                case SlugAbilityType.Heal: return "curacion";
                case SlugAbilityType.Shield: return "escudo";
                case SlugAbilityType.Freeze: return "congelacion";
                case SlugAbilityType.BurnOverTime: return "quemadura";
                case SlugAbilityType.Stun: return "aturdimiento";
                case SlugAbilityType.EnergyDrain: return "drenaje de vida";
                default: return "efecto";
            }
        }

        private void Anotar(string linea)
        {
            registro.Add(linea);
            if (registro.Count > 60) registro.RemoveAt(0);
        }

        // ------------------------------------------------------------------ turno del jugador

        /// Decide si una accion es legal en el estado actual, sin resolverla. La UI ya deshabilita
        /// los botones con estas mismas condiciones; aca sirven el mismo control para un origen que
        /// NO esta en esta maquina: un cliente remoto puede mandar lo que sea, y el host
        /// necesita una unica funcion donde apoyarse para rechazarlo. `TurnoJugador` sigue siendo
        /// tolerante (es lo que la UI verificada espera), asi que quien valide por red llama a esta
        /// primero y solo despues resuelve.
        public bool AccionLegal(AccionDuelo accion, int indiceCambio, out string motivo)
        {
            if (Terminado) { motivo = "el duelo ya termino"; return false; }
            if (!ActivoJ.Vivo) { motivo = "tu babosa activa no esta en combate"; return false; }
            switch (accion)
            {
                case AccionDuelo.Habilidad:
                    if (ActivoJ.enfriamiento > 0)
                    {
                        motivo = "la habilidad espera " + ActivoJ.enfriamiento + " turno/s mas";
                        return false;
                    }
                    break;
                case AccionDuelo.Cambiar:
                    if (indiceCambio < 0 || indiceCambio >= jugador.Count)
                    {
                        motivo = "el indice de cambio " + indiceCambio + " no existe";
                        return false;
                    }
                    if (indiceCambio == activoJugador)
                    {
                        motivo = "esa babosa ya esta en combate";
                        return false;
                    }
                    if (!jugador[indiceCambio].Vivo)
                    {
                        motivo = "esa babosa no esta viva";
                        return false;
                    }
                    break;
            }
            motivo = null;
            return true;
        }

        /// Resuelve la accion del jugador y despues la respuesta de la IA. Devuelve las lineas
        /// nuevas para que la UI muestre exactamente lo que paso en este turno.
        public List<string> TurnoJugador(AccionDuelo accion, int indiceCambio = -1)
        {
            var salida = new List<string>();
            if (Terminado) return salida;
            jugadas.Add(new Jugada(accion, indiceCambio));
            int antes = registro.Count;
            turno++;

            if (!ActivoJ.Vivo) EntraJugador();

            Combatiente actor = ActivoJ;
            Combatiente objetivo = ActivoR;
            bool usoHabilidad = false;

            if (actor.congeladoTurnos > 0)
                Anotar("Tu " + actor.Nombre + " esta " + actor.Estado + " y pierde el turno.");
            else
            {
                switch (accion)
                {
                    case AccionDuelo.Atacar:
                        Golpe(actor, objetivo, "Tu");
                        break;

                    case AccionDuelo.Habilidad:
                        if (actor.enfriamiento > 0)
                            Anotar(actor.Nombre + " todavia espera " + actor.enfriamiento + " turno/s para su habilidad.");
                        else
                        {
                            Habilidad(actor, objetivo, "Tu");
                            usoHabilidad = true;
                        }
                        break;

                    case AccionDuelo.Cambiar:
                        int destino = indiceCambio;
                        if (destino < 0 || destino >= jugador.Count ||
                            destino == activoJugador || !jugador[destino].Vivo)
                        {
                            destino = SiguienteVivo(jugador, activoJugador);
                        }
                        if (destino >= 0)
                        {
                            activoJugador = destino;
                            Anotar("Cambias a " + ActivoJ.Nombre + ".");
                        }
                        else Anotar("No tenes otra babosa disponible.");
                        break;
                }
            }

            if (!usoHabilidad && actor.enfriamiento > 0) actor.enfriamiento--;
            Tic(ActivoJ);
            if (ComprobarFin()) return Recorte(antes);

            TurnoIA();
            Tic(ActivoR);
            ComprobarFin();
            if (!Terminado && !ActivoJ.Vivo) EntraJugador();
            return Recorte(antes);
        }

        private void EntraJugador()
        {
            int i = SiguienteVivo(jugador, activoJugador);
            if (i < 0) return;
            activoJugador = i;
            Anotar("Tu " + ActivoJ.Nombre + " entra al combate.");
        }

        private List<string> Recorte(int desde)
        {
            var salida = new List<string>();
            for (int i = desde; i < registro.Count; i++) salida.Add(registro[i]);
            return salida;
        }

        private void Tic(Combatiente c)
        {
            if (!c.Vivo) return;
            if (c.congeladoTurnos > 0)
            {
                c.congeladoTurnos--;
                if (c.congeladoTurnos == 0) c.aturdido = false;
            }
            if (c.quemaduraTurnos > 0)
            {
                c.quemaduraTurnos--;
                Danar(c, c.quemaduraDano, " por la quemadura");
            }
        }

        // ------------------------------------------------------------------ respuesta de la IA

        private void TurnoIA()
        {
            if (Terminado) return;
            if (!ActivoR.Vivo)
            {
                int i = SiguienteVivo(rival, activoRival);
                if (i >= 0)
                {
                    activoRival = i;
                    Anotar("El rival manda a " + ActivoR.Nombre + ".");
                }
                return;
            }

            Combatiente el = ActivoR;
            if (el.congeladoTurnos > 0)
            {
                Anotar("El rival " + el.Nombre + " esta " + el.Estado + " y no puede responder.");
                return;
            }

            bool usoHabilidad = false;
            if (el.enfriamiento == 0 && el.datos != null &&
                el.datos.abilityType == SlugAbilityType.Heal && el.hp < el.hpMax * 0.38f)
            {
                Habilidad(el, ActivoJ, "El rival");
                usoHabilidad = true;
            }
            else if (el.enfriamiento == 0 && el.datos != null &&
                     el.datos.abilityType != SlugAbilityType.Heal && _rng.NextDouble() < 0.45)
            {
                Habilidad(el, ActivoJ, "El rival");
                usoHabilidad = true;
            }
            else
            {
                Golpe(el, ActivoJ, "El rival");
            }
            if (!usoHabilidad && el.enfriamiento > 0) el.enfriamiento--;
        }

        private void Golpe(Combatiente atacante, Combatiente defensor, string lado)
        {
            Anotar(lado + " " + atacante.Nombre + " ataca a " + defensor.Nombre + ".");
            float mult = ElementalChart.GetMultiplier(atacante.Elemento, defensor.Elemento);
            float baseD = atacante.datos != null ? atacante.datos.baseDamage : 30f;
            string nota = mult > 1.05f ? " (muy eficaz)" : mult < 0.95f ? " (poco eficaz)" : "";
            Danar(defensor, baseD * mult, nota);
        }

        private void Habilidad(Combatiente usuario, Combatiente objetivo, string lado)
        {
            SlugData d = usuario.datos;
            if (d == null)
            {
                Anotar(lado + " " + usuario.Nombre + " no tiene habilidad.");
                return;
            }

            usuario.enfriamiento = EnfriamientoHabilidad;
            Anotar(lado + " " + usuario.Nombre + " usa su habilidad: " + NombreEfecto(d.abilityType) + ".");

            switch (d.abilityType)
            {
                case SlugAbilityType.DirectDamage:
                    Danar(objetivo, d.abilityMagnitude, "");
                    break;

                case SlugAbilityType.BurnOverTime:
                    objetivo.quemaduraTurnos = Mathf.Max(1, Mathf.RoundToInt(d.effectDuration));
                    objetivo.quemaduraDano = Mathf.Max(2f, d.abilityMagnitude * 0.35f);
                    Anotar(objetivo.Nombre + " queda ardiendo " + objetivo.quemaduraTurnos + " turnos.");
                    break;

                case SlugAbilityType.Freeze:
                    objetivo.congeladoTurnos = Mathf.Max(1, Mathf.RoundToInt(d.effectDuration));
                    objetivo.aturdido = false;
                    Anotar(objetivo.Nombre + " queda congelado " + objetivo.congeladoTurnos + " turnos.");
                    break;

                case SlugAbilityType.Stun:
                    objetivo.congeladoTurnos = Mathf.Max(1, Mathf.RoundToInt(d.effectDuration));
                    objetivo.aturdido = true;
                    Anotar(objetivo.Nombre + " queda aturdido " + objetivo.congeladoTurnos + " turnos.");
                    break;

                case SlugAbilityType.Heal:
                    float curado = Mathf.Min(usuario.hpMax - usuario.hp, Mathf.Max(6f, d.abilityMagnitude));
                    usuario.hp += curado;
                    Anotar(usuario.Nombre + " recupera " + Mathf.RoundToInt(curado) + " de vida.");
                    break;

                case SlugAbilityType.Shield:
                    float esc = Mathf.Max(6f, d.abilityMagnitude);
                    usuario.escudo += esc;
                    Anotar(usuario.Nombre + " levanta un escudo de " + Mathf.RoundToInt(esc) + ".");
                    break;

                case SlugAbilityType.EnergyDrain:
                    float robado = Mathf.Max(4f, d.abilityMagnitude);
                    Danar(objetivo, robado, "");
                    float ganado = Mathf.Min(usuario.hpMax - usuario.hp, robado * 0.5f);
                    usuario.hp += ganado;
                    Anotar(usuario.Nombre + " recupera " + Mathf.RoundToInt(ganado) + " de vida.");
                    break;
            }
        }

        private void Danar(Combatiente victima, float dano, string nota)
        {
            if (!victima.Vivo) return;

            float resto = dano;
            if (victima.escudo > 0f)
            {
                float absorbido = Mathf.Min(victima.escudo, resto);
                victima.escudo -= absorbido;
                resto -= absorbido;
                if (absorbido > 0f)
                    Anotar("El escudo de " + victima.Nombre + " absorbe " + Mathf.RoundToInt(absorbido) + ".");
            }

            victima.hp = Mathf.Max(0f, victima.hp - resto);
            if (resto > 0f)
                Anotar(victima.Nombre + " recibe " + Mathf.RoundToInt(resto) + " de dano" + nota + ".");
            if (victima.hp <= 0f) Anotar(victima.Nombre + " cae.");
        }

        private bool ComprobarFin()
        {
            if (Terminado) return true;
            bool vivosJ = AlgunVivo(jugador), vivosR = AlgunVivo(rival);
            if (vivosJ && vivosR) return false;
            Terminado = true;
            GanoJugador = vivosJ;
            Anotar(GanoJugador ? "Ganaste el duelo." : "Perdiste el duelo.");
            return true;
        }

        private static bool AlgunVivo(List<Combatiente> l)
        {
            for (int i = 0; i < l.Count; i++) if (l[i].Vivo) return true;
            return false;
        }

        private static int SiguienteVivo(List<Combatiente> l, int desde)
        {
            if (l.Count == 0) return -1;
            for (int k = 1; k <= l.Count; k++)
            {
                int i = (desde + k) % l.Count;
                if (l[i].Vivo) return i;
            }
            return -1;
        }
    }

    /// Torneo de tres rondas contra equipos IA. El equipo del jugador sale del menu (3 activas);
    /// si falta alguna se completa con el roster para respetar el formato de 3 vs 3.
    public class Torneo
    {
        public int Ronda { get; private set; }
        public int RondasTotales { get; private set; }
        public bool Terminado { get; private set; }
        public bool Ganado { get; private set; }
        public Duelo DueloActual { get; private set; }

        private SlugData[] _equipo;
        private int _semilla;

        public Torneo(SlugData[] equipo, int rondas = 3, int semilla = 12345)
        {
            _equipo = equipo;
            RondasTotales = Mathf.Max(1, rondas);
            _semilla = semilla;
            Ronda = 1;
            EmpezarRonda();
        }

        private void EmpezarRonda()
        {
            SlugData[] rivales = EquipoRival(_semilla + Ronda * 977, Ronda);
            float escala = 0.85f + Ronda * 0.22f;
            DueloActual = new Duelo();
            // La misma semilla que eligio los rivales gobierna las decisiones de la IA: con eso,
            // "torneo con semilla 48831" significa una secuencia concreta de jugadas, repetible.
            DueloActual.Cargar(_equipo, rivales, escala, _semilla + Ronda * 31);
        }

        /// Siguiente ronda si se gano, o cierre del torneo si se perdio.
        public void Continuar()
        {
            if (Terminado) return;
            if (!DueloActual.Terminado) return;
            if (!DueloActual.GanoJugador)
            {
                Terminado = true;
                Ganado = false;
                return;
            }
            if (Ronda >= RondasTotales)
            {
                Terminado = true;
                Ganado = true;
                return;
            }
            Ronda++;
            _semilla += 733;
            EmpezarRonda();
        }

        public void Reiniciar(int semilla)
        {
            _semilla = semilla;
            Ronda = 1;
            Terminado = false;
            Ganado = false;
            EmpezarRonda();
        }

        public static SlugData[] EquipoRival(int semilla, int ronda)
        {
            SlugData[] todos = SlugRoster.Todos;
            var salida = new SlugData[3];
            if (todos == null || todos.Length == 0) return salida;

            var rng = new System.Random(semilla);
            var usados = new HashSet<int>();
            for (int i = 0; i < 3; i++)
            {
                int intento = 0;
                int idx;
                do
                {
                    idx = rng.Next(todos.Length);
                    intento++;
                }
                while (usados.Contains(idx) && intento < 60);
                usados.Add(idx);
                salida[i] = todos[idx];
            }
            return salida;
        }

        public static SlugData[] EquipoJugador(SlugData[] roster)
        {
            return EquipoJugador(roster, GameFlow.Equipo(), GameFlow.BabosaElegida());
        }

        /// Version pura de la regla: el torneo juega con el equipo de 3 que el jugador armo en
        /// la coleccion, y solo completa los huecos con el roster. Separarla del estado vivo
        /// existe para que esa invariante (el vinculo y la coleccion tienen que servir para algo
        /// en el otro modo) se pueda probar sin escena ni PlayerPrefs: con la otra firma, en
        /// EditMode `GameFlow.Activo` es null, `Equipo()` devuelve vacio, y el test daria verde
        /// midiendo solamente el relleno.
        public static SlugData[] EquipoJugador(SlugData[] roster, string[] idsEquipo, SlugData elegida)
        {
            var salida = new SlugData[3];
            string[] ids = idsEquipo != null ? idsEquipo : new string[0];
            int n = 0;
            for (int i = 0; i < ids.Length && n < 3; i++)
            {
                SlugData d = SlugRoster.Buscar(ids[i]);
                if (d != null) salida[n++] = d;
            }

            if (n == 0 && elegida != null) salida[n++] = elegida;

            if (roster != null)
            {
                for (int i = 0; i < roster.Length && n < 3; i++)
                {
                    if (roster[i] == null) continue;
                    bool ya = false;
                    for (int k = 0; k < n; k++) if (salida[k] == roster[i]) ya = true;
                    if (!ya) salida[n++] = roster[i];
                }
            }
            return salida;
        }
    }
}