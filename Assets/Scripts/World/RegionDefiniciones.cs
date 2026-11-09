using UnityEngine;

namespace SlugOut.World
{
    /// Que forma cada microhabitat. No es un dato de tuning: cada tipo es una silueta distinta
    /// construida con las primitivas del propio region builder.
    public enum TipoHabitat
    {
        Fumarolas,        // chimeneas de basalto + brasas
        PozoHumedo,       // cubeta de agua con juncos
        Cristales,        // racimo de cristales altos
        Estalactitas,     // columnas que cuelgan sobre un monticulo de ripio
        CharcoEco,        // charco quieto con anillos concentricos que devuelven el sonido
        ColumnasRunas,    // pilares grabados que brillan de a uno
        ArcoRocoso,       // arco de piedra natural sobre un hueco: se ve de lejos por la luz que pasa
        RaicesAnudadas,   // raices que cuelgan en arco hasta un piso humedo
        ColadaEscalonada, // escalones de travertino con laminas de agua cayendo
        ChimeneaCeniza,   // chimenea alta y partida con brasas en la boca
        PuenteRoto,       // puente de piedra quebrado sobre una grieta
        LagunaAzufre,     // laguna amarilla con anillos y vapores
        Pagoda,           // torre de pisos con techos curvos, estilo oriental
        EstanqueKoi,      // estanque de agua jade con peces y puente arqueado
        BosqueBambu       // bambues altos segmentados con luz filtrada
    }

    /// Una de las tres zonas de una region: donde esta, como se llama, que letra le pone el
    /// minimapa y que babosa vive ahi.
    public struct HabitatDef
    {
        public TipoHabitat tipo;
        public string nombre;     // "Fumarolas" -> grupo Microhabitat_Fumarolas
        public string legible;    // como se lee en el minimapa y en el panel de vinculo
        public string letra;      // F / A / C ...
        public float angulo;      // grados alrededor de la plaza
        public string slugId;     // tiene que existir en el roster Y tener malla
    }

    /// Cobertura jugable: un pilar que se ve, frena al jugador (proxy en capa 8) y corta la
    /// linea de tiro. El builder lo crea desde la definicion de la region.
    public struct CoberturaDef
    {
        public float angulo;
        public float radio;
        public Vector3 tam;
    }

    /// Una region jugable. La paleta y la niebla son lo que hace que la segunda se lea como
    /// otro lado y no como el mismo decorado con otros objetos.
    public sealed class RegionDefinicion
    {
        public string nombre;
        public HabitatDef[] habitats;
        /// Las bocas de esta cueva: cada indice es la region a la que lleva, y su angulo esta en
        /// la misma posicion de `angulosTunel`. Con tres cuevas y una sola boca por cueva no hay
        /// topologia que no deje a alguna sin camino de entrada, asi que cada region tiene una
        /// boca por vecina (las otras dos) y el viaje sale por la boca que apunta a la cueva de
        /// donde uno viene.
        public int[] destinosTunel;
        public float[] angulosTunel;
        /// Coberturas de la arena; null/empty en las regiones que no las traen.
        public CoberturaDef[] coberturas;

        public Color roca, rocaClara, rocaPared, camino, madera, agua, cristal;
        public Color acento, emisionAcento;
        public Color emisionAgua, emisionCristal;
        public Color ambiente, niebla, fondoCamara, luzDireccional;
        public float nieblaInicio, nieblaFin, intensidadLuz;
        public Color luzHabitat1, luzHabitat2, luzHabitat3;

        /// Relieve propio: desplaza la funcion de altura (para que el contorno del piso no sea
        /// el mismo calcado en las dos cuevas) y multiplica cuanto rompe. Cueva Seca se queda en
        /// cero/uno: su relieve es el que ya esta medido y jugado.
        public Vector2 relieveDesfase;
        public float relieveAmplitud = 1f;

        /// Rotacion de toda la cueva (habitats, bocas y coberturas) y radio de los habitats.
        /// Cueva Seca queda en cero para conservar los angulos medidos; las otras giran su
        /// esqueleto para que no se lean como la misma arena con otro color.
        public float anguloBase;
        public float radioHabitat = 20f;
    }

    /// Las cinco regiones del prototipo y los tuneles que las unen. Cada cueva tiene una boca
    /// por cada vecina (cuatro), con angulos propios para que ninguna tape un habitat ni otra
    /// boca. La Cueva Seca conserva sus bocas historicas (200/320/80) y suma la del templo.
    public static class Regiones
    {
        static RegionDefinicion[] _todas;

        public static RegionDefinicion[] Todas
        {
            get
            {
                if (_todas == null)
                    _todas = new[] { CuevaSeca(), GaleriasEco(), VenaVerde(), CanonCeniza(), TemploOlvidado() };
                return _todas;
            }
        }

        public static RegionDefinicion De(int i)
        {
            RegionDefinicion[] t = Todas;
            if (i < 0) i = 0;
            if (i >= t.Length) i = t.Length - 1;
            return t[i];
        }

        static RegionDefinicion CuevaSeca()
        {
            return new RegionDefinicion
            {
                nombre = "LA CUEVA SECA",
                destinosTunel = new[] { 1, 2, 3, 4 }, angulosTunel = new[] { 200f, 320f, 80f, 165f },
                anguloBase = 0f, radioHabitat = 20f,
                habitats = new[]
                {
                    new HabitatDef { tipo = TipoHabitat.Fumarolas, nombre = "Fumarolas", legible = "fumarolas", letra = "F", angulo = 12f, slugId = "ascua" },
                    new HabitatDef { tipo = TipoHabitat.PozoHumedo, nombre = "PozoHumedo", legible = "pozo", letra = "A", angulo = 140f, slugId = "corriente" },
                    new HabitatDef { tipo = TipoHabitat.Cristales, nombre = "Cristales", legible = "cristales", letra = "C", angulo = 252f, slugId = "centella" }
                },
                // Cobertura desde el nivel 1: el Escupidor puede salir en cualquier cueva y
                // sin un pilar su ataque a distancia no tiene contrapartida. Angulos a >25 de
                // habitats y bocas para no tapar accesos.
                coberturas = new[]
                {
                    new CoberturaDef { angulo = 45f, radio = 9f, tam = new Vector3(1.9f, 2.8f, 1.9f) },
                    new CoberturaDef { angulo = 110f, radio = 9.5f, tam = new Vector3(1.6f, 2.5f, 1.6f) },
                    new CoberturaDef { angulo = 285f, radio = 9f, tam = new Vector3(2.1f, 3.0f, 1.4f) }
                },
                // Paleta Pixar final. Antes vivia en LookCuevaPixar y se aplicaba por encima
                // de estos campos; ahora es la unica fuente y no se pisa al cambiar de cueva.
                roca = new Color(0.10f, 0.13f, 0.18f),
                rocaClara = new Color(0.18f, 0.22f, 0.28f),
                rocaPared = new Color(0.08f, 0.10f, 0.15f),
                camino = new Color(0.16f, 0.20f, 0.25f),
                madera = new Color(0.10f, 0.08f, 0.07f),
                agua = new Color(0.05f, 0.35f, 0.50f),
                cristal = new Color(0.35f, 0.85f, 1f),
                acento = new Color(0.95f, 0.42f, 0.12f),
                emisionAcento = new Color(1f, 0.45f, 0.10f),
                emisionAgua = new Color(0.05f, 0.45f, 0.65f),
                emisionCristal = new Color(0.20f, 0.75f, 1f),
                ambiente = new Color(0.24f, 0.30f, 0.39f),
                niebla = new Color(0.015f, 0.04f, 0.07f),
                fondoCamara = new Color(0.02f, 0.04f, 0.06f),
                luzDireccional = new Color(0.55f, 0.75f, 1f),
                nieblaInicio = 16f, nieblaFin = 80f, intensidadLuz = 0.80f,
                luzHabitat1 = new Color(1f, 0.55f, 0.22f),
                luzHabitat2 = new Color(0.35f, 0.80f, 0.85f),
                luzHabitat3 = new Color(0.55f, 0.50f, 1f)
            };
        }

        static RegionDefinicion GaleriasEco()
        {
            return new RegionDefinicion
            {
                nombre = "GALERIAS DEL ECO",
                destinosTunel = new[] { 0, 2, 3, 4 }, angulosTunel = new[] { 200f, 320f, 80f, 165f },
                anguloBase = 30f, radioHabitat = 20.5f,
                habitats = new[]
                {
                    new HabitatDef { tipo = TipoHabitat.Estalactitas, nombre = "Estalactitas", legible = "estalactitas", letra = "E", angulo = 20f, slugId = "guijarro" },
                    new HabitatDef { tipo = TipoHabitat.CharcoEco, nombre = "CharcoEco", legible = "charco", letra = "H", angulo = 130f, slugId = "marea" },
                    new HabitatDef { tipo = TipoHabitat.ColumnasRunas, nombre = "ColumnasRunas", legible = "runas", letra = "S", angulo = 250f, slugId = "tenue" }
                },
                coberturas = new[]
                {
                    new CoberturaDef { angulo = 50f, radio = 8.5f, tam = new Vector3(1.8f, 2.7f, 1.8f) },
                    new CoberturaDef { angulo = 165f, radio = 10.5f, tam = new Vector3(2.0f, 3.0f, 1.5f) },
                    new CoberturaDef { angulo = 285f, radio = 9.2f, tam = new Vector3(1.5f, 2.5f, 1.7f) }
                },
                roca = new Color(0.185f, 0.185f, 0.235f),
                rocaClara = new Color(0.355f, 0.35f, 0.44f),
                rocaPared = new Color(0.125f, 0.125f, 0.175f),
                camino = new Color(0.50f, 0.52f, 0.60f),
                madera = new Color(0.24f, 0.21f, 0.28f),
                agua = new Color(0.14f, 0.34f, 0.52f),
                cristal = new Color(0.42f, 0.62f, 0.86f),
                acento = new Color(0.52f, 0.62f, 0.95f),
                emisionAcento = new Color(0.32f, 0.45f, 1f),
                emisionAgua = new Color(0.12f, 0.40f, 0.62f),
                emisionCristal = new Color(0.35f, 0.65f, 1f),
                ambiente = new Color(0.155f, 0.165f, 0.225f),
                niebla = new Color(0.042f, 0.048f, 0.075f),
                fondoCamara = new Color(0.026f, 0.028f, 0.045f),
                luzDireccional = new Color(0.78f, 0.84f, 1f),
                nieblaInicio = 40f, nieblaFin = 88f, intensidadLuz = 0.72f,
                relieveDesfase = new Vector2(91.5f, -63.25f), relieveAmplitud = 1.6f,
                luzHabitat1 = new Color(0.62f, 0.68f, 0.92f),
                luzHabitat2 = new Color(0.28f, 0.62f, 0.86f),
                luzHabitat3 = new Color(0.55f, 0.28f, 0.72f)
            };
        }

        /// Tercera cueva: verde oliva, con la luz mas cerca y mas baja que las otras dos, y con
        /// tres siluetas que no existen en las otras cuevas (un arco de piedra que deja pasar la
        /// luz, raices colgando sobre piso mojado y una cascada escalonada). El relieve va por
        /// otro lado y con otra amplitud para que el contorno del piso tampoco se calcado.
        static RegionDefinicion VenaVerde()
        {
            return new RegionDefinicion
            {
                nombre = "LA VENA VERDE",
                destinosTunel = new[] { 0, 1, 3, 4 }, angulosTunel = new[] { 200f, 320f, 80f, 175f },
                anguloBase = -25f, radioHabitat = 19f,
                habitats = new[]
                {
                    new HabitatDef { tipo = TipoHabitat.ArcoRocoso, nombre = "ArcoRocoso", legible = "arco", letra = "R", angulo = 35f, slugId = "roquito" },
                    new HabitatDef { tipo = TipoHabitat.RaicesAnudadas, nombre = "RaicesAnudadas", legible = "raices", letra = "L", angulo = 150f, slugId = "liana" },
                    new HabitatDef { tipo = TipoHabitat.ColadaEscalonada, nombre = "ColadaEscalonada", legible = "cascada", letra = "G", angulo = 265f, slugId = "gotita" }
                },
                coberturas = new[]
                {
                    new CoberturaDef { angulo = 110f, radio = 8.8f, tam = new Vector3(2.2f, 2.6f, 1.4f) },
                    new CoberturaDef { angulo = 232f, radio = 9.8f, tam = new Vector3(1.7f, 2.9f, 1.7f) },
                    new CoberturaDef { angulo = 350f, radio = 10.2f, tam = new Vector3(1.9f, 2.5f, 1.9f) }
                },
                roca = new Color(0.190f, 0.235f, 0.185f),
                rocaClara = new Color(0.360f, 0.420f, 0.310f),
                rocaPared = new Color(0.130f, 0.165f, 0.130f),
                camino = new Color(0.460f, 0.520f, 0.360f),
                madera = new Color(0.330f, 0.250f, 0.150f),
                agua = new Color(0.160f, 0.420f, 0.340f),
                cristal = new Color(0.450f, 0.800f, 0.420f),
                acento = new Color(0.550f, 0.850f, 0.400f),
                emisionAcento = new Color(0.350f, 0.800f, 0.300f),
                emisionAgua = new Color(0.120f, 0.500f, 0.400f),
                emisionCristal = new Color(0.400f, 0.850f, 0.350f),
                ambiente = new Color(0.160f, 0.220f, 0.160f),
                niebla = new Color(0.040f, 0.062f, 0.045f),
                fondoCamara = new Color(0.024f, 0.036f, 0.026f),
                luzDireccional = new Color(0.820f, 0.950f, 0.720f),
                nieblaInicio = 36f, nieblaFin = 78f, intensidadLuz = 0.66f,
                relieveDesfase = new Vector2(37.25f, 118.5f), relieveAmplitud = 1.35f,
                luzHabitat1 = new Color(0.70f, 0.90f, 0.50f),
                luzHabitat2 = new Color(0.35f, 0.75f, 0.45f),
                luzHabitat3 = new Color(0.30f, 0.85f, 0.70f)
            };
        }

        /// Cuarta cueva: ceniza y azufre. Tres siluetas nuevas (chimenea partida, puente roto y
        /// laguna de azufre), relieve por otro lado y tres pilares de cobertura a media distancia
        /// para que el Escupidor tenga con que cortar la linea de tiro y el jugador pueda cubrirse.
        static RegionDefinicion CanonCeniza()
        {
            return new RegionDefinicion
            {
                nombre = "EL CAÑON DE CENIZA",
                destinosTunel = new[] { 0, 1, 2, 4 }, angulosTunel = new[] { 80f, 200f, 320f, 165f },
                anguloBase = 15f, radioHabitat = 20.2f,
                habitats = new[]
                {
                    new HabitatDef { tipo = TipoHabitat.ChimeneaCeniza, nombre = "ChimeneaCeniza", legible = "chimenea", letra = "K", angulo = 10f, slugId = "vulcano" },
                    new HabitatDef { tipo = TipoHabitat.PuenteRoto, nombre = "PuenteRoto", legible = "puente", letra = "P", angulo = 130f, slugId = "titan" },
                    new HabitatDef { tipo = TipoHabitat.LagunaAzufre, nombre = "LagunaAzufre", legible = "azufre", letra = "Z", angulo = 250f, slugId = "abisal" }
                },
                coberturas = new[]
                {
                    new CoberturaDef { angulo = 45f, radio = 9.2f, tam = new Vector3(1.8f, 2.8f, 1.8f) },
                    new CoberturaDef { angulo = 165f, radio = 8.8f, tam = new Vector3(2.2f, 2.4f, 1.4f) },
                    new CoberturaDef { angulo = 285f, radio = 9.6f, tam = new Vector3(1.6f, 3.0f, 1.6f) }
                },
                roca = new Color(0.245f, 0.205f, 0.155f),
                rocaClara = new Color(0.420f, 0.350f, 0.250f),
                rocaPared = new Color(0.165f, 0.140f, 0.115f),
                camino = new Color(0.620f, 0.500f, 0.260f),
                madera = new Color(0.280f, 0.210f, 0.150f),
                agua = new Color(0.550f, 0.620f, 0.180f),
                cristal = new Color(0.850f, 0.450f, 0.180f),
                acento = new Color(0.950f, 0.550f, 0.160f),
                emisionAcento = new Color(1f, 0.50f, 0.12f),
                emisionAgua = new Color(0.55f, 0.62f, 0.16f),
                emisionCristal = new Color(0.90f, 0.35f, 0.12f),
                ambiente = new Color(0.270f, 0.210f, 0.130f),
                niebla = new Color(0.075f, 0.058f, 0.032f),
                fondoCamara = new Color(0.040f, 0.032f, 0.022f),
                luzDireccional = new Color(1f, 0.78f, 0.52f),
                nieblaInicio = 38f, nieblaFin = 96f, intensidadLuz = 0.74f,
                relieveDesfase = new Vector2(55.5f, -27.25f), relieveAmplitud = 1.75f,
                luzHabitat1 = new Color(1f, 0.55f, 0.18f),
                luzHabitat2 = new Color(0.85f, 0.72f, 0.35f),
                luzHabitat3 = new Color(0.75f, 0.80f, 0.25f)
            };
        }

        /// Quinta cueva: templo oriental olvidado. Paleta de granito oscuro con laca roja,
        /// losas doradas, agua jade y acentos bermellón. Tres siluetas nuevas (pagoda de
        /// techos curvos, estanque koi con puente arqueado, bosque de bambú). Cuatro bocas
        /// a 90° giradas 45° para no leer como las otras cuevas; habitats en los huecos.
        static RegionDefinicion TemploOlvidado()
        {
            return new RegionDefinicion
            {
                nombre = "EL TEMPLO OLVIDADO",
                destinosTunel = new[] { 0, 1, 2, 3 }, angulosTunel = new[] { 0f, 90f, 180f, 270f },
                anguloBase = 45f, radioHabitat = 20f,
                habitats = new[]
                {
                    new HabitatDef { tipo = TipoHabitat.Pagoda, nombre = "Pagoda", legible = "pagoda", letra = "T", angulo = 100f, slugId = "fenix" },
                    new HabitatDef { tipo = TipoHabitat.EstanqueKoi, nombre = "EstanqueKoi", legible = "estanque", letra = "K", angulo = 210f, slugId = "leviatan" },
                    new HabitatDef { tipo = TipoHabitat.BosqueBambu, nombre = "BosqueBambu", legible = "bambú", letra = "B", angulo = 330f, slugId = "selva" }
                },
                coberturas = new[]
                {
                    new CoberturaDef { angulo = 55f, radio = 9f, tam = new Vector3(1.9f, 3.2f, 1.9f) },
                    new CoberturaDef { angulo = 160f, radio = 9.6f, tam = new Vector3(1.6f, 2.6f, 1.6f) },
                    new CoberturaDef { angulo = 285f, radio = 9.2f, tam = new Vector3(2.1f, 2.9f, 1.5f) }
                },
                roca = new Color(0.16f, 0.14f, 0.13f),
                rocaClara = new Color(0.30f, 0.26f, 0.22f),
                rocaPared = new Color(0.10f, 0.09f, 0.10f),
                camino = new Color(0.55f, 0.45f, 0.25f),
                madera = new Color(0.45f, 0.10f, 0.08f),
                agua = new Color(0.15f, 0.45f, 0.40f),
                cristal = new Color(0.95f, 0.65f, 0.15f),
                acento = new Color(0.85f, 0.15f, 0.12f),
                emisionAcento = new Color(1f, 0.25f, 0.15f),
                emisionAgua = new Color(0.15f, 0.55f, 0.45f),
                emisionCristal = new Color(1f, 0.70f, 0.20f),
                ambiente = new Color(0.25f, 0.20f, 0.18f),
                niebla = new Color(0.10f, 0.07f, 0.04f),
                fondoCamara = new Color(0.05f, 0.035f, 0.03f),
                luzDireccional = new Color(1f, 0.85f, 0.65f),
                nieblaInicio = 30f, nieblaFin = 85f, intensidadLuz = 0.78f,
                relieveDesfase = new Vector2(-77.5f, 41.25f), relieveAmplitud = 1.5f,
                luzHabitat1 = new Color(1f, 0.30f, 0.20f),
                luzHabitat2 = new Color(1f, 0.80f, 0.35f),
                luzHabitat3 = new Color(1f, 0.65f, 0.75f)
            };
        }
    }
}
