using System;
using System.Collections.Generic;
using UnityEngine;
using SlugOut.Slugs;

namespace SlugOut.World
{
    /// Una accion pedida por el lado del jugador. `indiceCambio` solo importa en Cambiar; en las
    /// otras dos el jugador no elige indice y queda en -1.
    [Serializable]
    public class Jugada
    {
        public AccionDuelo accion;
        public int indiceCambio = -1;

        public Jugada() { }
        public Jugada(AccionDuelo a, int i = -1) { accion = a; indiceCambio = i; }
        public override string ToString() { return accion + "(" + indiceCambio + ")"; }
    }

    /// Punto de partida de un duelo (equipos por id, escala del rival y semilla) mas las jugadas
    /// que se pidieron, en ids y enteros: lo que tendria que viajar por un cable para que el otro
    /// lado reconstruya el mismo duelo sin mandarle el estado. Alcanza con esto porque `Duelo` es
    /// determinista: siembra su propio RNG y la IA no mira el generador global.
    ///
    /// **No es transporte de red.** No hay sockets, salas, relay, timeouts, reconexion ni
    /// anti-trampa en este archivo, y ninguna pantalla del juego lo usa: el torneo sigue siendo
    /// local contra IA. Es la costura para que, si algun dia hay transporte, el estado del duelo no
    /// se invente apurado. `Duelo.AccionLegal` es la otra mitad de esa costura: el host valida aca
    /// antes de resolver.
    [Serializable]
    public class DueloCheckpoint
    {
        public const int TamEquipo = 3;

        public string[] equipoJugador = new string[TamEquipo];
        public string[] equipoRival = new string[TamEquipo];
        public float escalaRival = 1f;
        public int semilla;
        public List<Jugada> jugadas = new List<Jugada>();

        static string IdDe(Combatiente c)
        {
            return c != null && c.datos != null ? c.datos.slugId : null;
        }

        static SlugData[] ADatos(string[] ids)
        {
            SlugData[] d = new SlugData[TamEquipo];
            for (int i = 0; i < TamEquipo; i++)
                d[i] = string.IsNullOrEmpty(ids[i]) ? null : SlugRoster.Buscar(ids[i]);
            return d;
        }

        /// Copia el punto de partida y las jugadas de un duelo ya cargado. El registro no viaja: se
        /// reproduce jugandolo, que es exactamente lo que garantiza la semilla.
        public static DueloCheckpoint Desde(Duelo d)
        {
            if (d == null) throw new ArgumentNullException("d");
            DueloCheckpoint c = new DueloCheckpoint();
            for (int i = 0; i < TamEquipo; i++)
            {
                c.equipoJugador[i] = IdDe(i < d.jugador.Count ? d.jugador[i] : null);
                c.equipoRival[i] = IdDe(i < d.rival.Count ? d.rival[i] : null);
            }
            c.escalaRival = d.EscalaRival;
            c.semilla = d.Semilla;
            if (d.jugadas != null) c.jugadas.AddRange(d.jugadas);
            return c;
        }

        public string AJson() { return JsonUtility.ToJson(this); }

        /// Devuelve null con cualquier JSON que no sea un checkpoint de tres contra tres. Un cable
        /// roto o un mensaje ajeno no tiene que producir un duelo a medias.
        public static DueloCheckpoint DesdeJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            DueloCheckpoint c;
            try { c = JsonUtility.FromJson<DueloCheckpoint>(json); }
            catch (Exception) { return null; }
            if (c == null) return null;
            if (c.equipoJugador == null || c.equipoRival == null) return null;
            if (c.equipoJugador.Length != TamEquipo || c.equipoRival.Length != TamEquipo) return null;
            if (c.jugadas == null) c.jugadas = new List<Jugada>();
            return c;
        }

        /// Un id vacio es un hueco del equipo (lo que `Duelo.Cargar` ya tolera); un id que no esta
        /// en el roster es un mensaje corrupto o tramposo.
        public bool EquiposValidos(out string motivo)
        {
            for (int lado = 0; lado < 2; lado++)
            {
                string[] ids = lado == 0 ? equipoJugador : equipoRival;
                if (ids == null || ids.Length != TamEquipo)
                {
                    motivo = (lado == 0 ? "equipoJugador" : "equipoRival") + " no trae " + TamEquipo + " ids";
                    return false;
                }
                for (int i = 0; i < TamEquipo; i++)
                {
                    if (string.IsNullOrEmpty(ids[i])) continue;
                    if (SlugRoster.Buscar(ids[i]) == null)
                    {
                        motivo = "el id '" + ids[i] + "' no esta en el roster";
                        return false;
                    }
                }
            }
            motivo = null;
            return true;
        }

        /// Vuelve a armar el duelo y repetir las jugadas. Con `estricta`, cada jugada pasa antes por
        /// `Duelo.AccionLegal` — que es lo que haria un host con la accion de un cliente — y la
        /// primera ilegal corta la reconstruccion con el motivo. Sin estricta se replayea como lo
        /// haria la pantalla local (que tolera una habilidad enfriada o un cambio sin indice).
        public Duelo Reconstruir(out string motivo, bool estricta = true)
        {
            if (!EquiposValidos(out motivo)) return null;
            Duelo d = new Duelo();
            d.Cargar(ADatos(equipoJugador), ADatos(equipoRival), escalaRival, semilla);
            for (int i = 0; i < jugadas.Count; i++)
            {
                Jugada j = jugadas[i];
                if (j == null)
                {
                    motivo = "la jugada " + (i + 1) + " viene vacia";
                    return null;
                }
                string porQue;
                if (estricta && !d.AccionLegal(j.accion, j.indiceCambio, out porQue))
                {
                    motivo = "la jugada " + (i + 1) + " (" + j + ") no es legal: " + porQue;
                    return null;
                }
                d.TurnoJugador(j.accion, j.indiceCambio);
            }
            motivo = null;
            return d;
        }

        /// Huella comparable del estado final: vida de los seis, quien actua, turno y resultado. El
        /// registro esta recortado a 60 lineas, asi que la huella es lo que se compara entre lados.
        public static string Huella(Duelo d)
        {
            if (d == null) return "(null)";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("sem=").Append(d.Semilla)
              .Append(" esc=").Append(d.EscalaRival.ToString("F2"))
              .Append(" turno=").Append(d.turno)
              .Append(" fin=").Append(d.Terminado).Append("/").Append(d.GanoJugador)
              .Append(" act=").Append(d.activoJugador).Append(",").Append(d.activoRival);
            for (int i = 0; i < d.jugador.Count; i++)
                sb.Append(" J").Append(i).Append('=').Append(d.jugador[i].hp.ToString("F2"))
                  .Append('/').Append(d.jugador[i].hpMax.ToString("F2"))
                  .Append('/').Append(d.jugador[i].enfriamiento);
            for (int i = 0; i < d.rival.Count; i++)
                sb.Append(" R").Append(i).Append('=').Append(d.rival[i].hp.ToString("F2"))
                  .Append('/').Append(d.rival[i].hpMax.ToString("F2"))
                  .Append('/').Append(d.rival[i].enfriamiento);
            sb.Append(" | ").Append(string.Join(" / ", d.registro.ToArray()));
            return sb.ToString();
        }
    }
}
