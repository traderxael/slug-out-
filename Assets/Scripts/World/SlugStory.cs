using UnityEngine;
using SlugOut.Core;

namespace SlugOut.World
{
    /// La historia de SLUG OUT, extraida del juego web real (traderxael/slug-out-).
    /// 28 fragmentos de campana (uno por nivel) + 8 del modo infinito.
    /// El color de cada fragmento sale del anillo de elementos del repo
    /// (fuego, hielo, planta, rayo, tierra, sombra, aire, agua) ciclando por nivel,
    /// igual que en el juego web cada fragmento se pinta con su elemento.
    public static class SlugStory
    {
        public struct Fragmento
        {
            public int nivel;
            public string titulo;
            public string texto;
            public string elemento;
            public Color color;

            public Fragmento(int n, string t, string x, string elem)
            {
                nivel = n;
                titulo = t;
                texto = x;
                elemento = elem;
                color = ColorDeElemento(elem);
            }
        }

        private static readonly string[] ANILLO = { "Fuego", "Hielo", "Planta", "Rayo", "Tierra", "Sombra", "Aire", "Agua" };

        public static ElementType ElementoDe(string elem)
        {
            switch (elem)
            {
                case "Fuego": return ElementType.Fire;
                case "Hielo": return ElementType.Ice;
                case "Planta": return ElementType.Plant;
                case "Rayo": return ElementType.Electric;
                case "Tierra": return ElementType.Earth;
                case "Sombra": return ElementType.Shadow;
                case "Aire": return ElementType.Air;
                case "Agua": return ElementType.Water;
                case "Energia": return ElementType.Energy;
                case "Luz": return ElementType.Light;
                case "Toxico": return ElementType.Toxic;
                default: return ElementType.Fire;
            }
        }

        /// El tema elemental del nivel sigue el mismo ciclo del anillo que pinta el banner
        /// (nivel 1 = Fuego ... nivel 8 = Agua, y vuelve), asi tinta y enemigos van juntos.
        public static ElementType TemaDeNivel(int nivel)
        {
            int n = nivel < 1 ? 1 : nivel;
            return ElementoDe(ANILLO[(n - 1) % ANILLO.Length]);
        }

        public static Color ColorDeElemento(string elem)
        {
            switch (elem)
            {
                case "Fuego": return new Color(1.00f, 0.35f, 0.10f);
                case "Hielo": return new Color(0.50f, 0.85f, 1.00f);
                case "Planta": return new Color(0.25f, 0.75f, 0.35f);
                case "Rayo": return new Color(1.00f, 0.90f, 0.10f);
                case "Tierra": return new Color(0.78f, 0.52f, 0.20f);
                case "Sombra": return new Color(0.55f, 0.25f, 0.70f);
                case "Aire": return new Color(0.80f, 0.90f, 0.95f);
                case "Agua": return new Color(0.15f, 0.55f, 1.00f);
                default: return Color.white;
            }
        }

        /// Fuerza el armado perezoso: leido antes de Campana() dejaba _campana en null.
        public static int TotalCampana { get { return Campana().Length; } }

        private static Fragmento[] _campana;
        private static Fragmento[] _infinito;

        public static Fragmento[] Campana()
        {
            if (_campana == null)
            {
                _campana = new Fragmento[]
                {
            new Fragmento(1, "La última fuente secó", "Las fuentes de la cueva cantaban. Ahora son piedra seca. Los que quedamos cargamos el elemento a pulso.", "Fuego"),
            new Fragmento(2, "Todavía llora", "El agua ya no cae: gotea. Cada gota la pelean tres. Aprendí a llegar antes que ellos, y a no mirar atrás.", "Hielo"),
            new Fragmento(3, "No piden permiso", "Los guardianes de roca no hablan. Se plantan y esperan a que te canses. No hay que esquivarlos: hay que decidir cuándo tirar.", "Planta"),
            new Fragmento(4, "Las raíces recuerdan", "Aquí hubo verde, y las raíces no lo olvidan. Se molestan cuando alguien baja a recordárselo.", "Rayo"),
            new Fragmento(5, "El frío guarda lo que queda", "El hielo no destruye: conserva. Eso entendí en la cámara, cuando el frío me dejó las manos duras y la cabeza clara.", "Tierra"),
            new Fragmento(6, "El primero que no era basura", "El guardián de escarcha no me atacó al verme. Me miró, cargó el hielo y esperó a que yo decidiera. Fue el primero que me trató como a alguien.", "Sombra"),
            new Fragmento(7, "Chispas en la oscuridad", "El nido de centellas no duerme. Cada chispa es un guardián chico probando si sirvo para esto.", "Aire"),
            new Fragmento(8, "Lo que se lleva la sombra", "La sombra no pelea: drena. Le vi quitarle el aire a un guardián y quedarse con su color. Algo está juntando los elementos, y no es para protegerlos.", "Agua"),
            new Fragmento(9, "El fuego que no calienta", "Ardía sin dar calor. Un fuego vacío, como una promesa que nadie cumplió: ya no quedaba nadie a quien calentar.", "Fuego"),
            new Fragmento(10, "Los ecos del pasado", "El laberinto repite cada paso que doy. Los ecos ya sabían que iba a llegar: los habían oído antes, a otros.", "Hielo"),
            new Fragmento(11, "La corriente que viene de abajo", "El rayo no cae del cielo. Sube. Algo abajo lo empuja, y ese algo tiene prisa.", "Planta"),
            new Fragmento(12, "La verdad", "En el corazón de la cueva no hay un monstruo. Hay un rey que selló los ocho elementos para que nadie los peleara nunca más. Yo traigo ocho, y él tiene razón: cada uno que libero enciende una guerra. Pero la cueva ya está muerta, y una cueva viva vale más que una cueva en paz.", "Rayo"),
            new Fragmento(13, "Despertar", "La cueva respira. No es un consuelo: respirar es querer algo, y lo que quiere no soy yo.", "Tierra"),
            new Fragmento(14, "No caben ocho", "Los elementos libres no saben compartir. El agua discute con el fuego, el rayo con la sombra, y yo estoy en el medio sin poder poner orden.", "Sombra"),
            new Fragmento(15, "Pasos que no son míos", "Alguien bajó detrás de mí. Los oí en el cristal: lentos, cuidadosos, de alguien que no quiere romper nada.", "Aire"),
            new Fragmento(16, "El que no es guardián", "No trae elemento. Cargó el suyo hace años y se le acabó. Me mira como se mira a alguien que va a morir y todavía no lo sabe.", "Agua"),
            new Fragmento(17, "Un odre para dos", "Queda agua para uno. Si la reparto llegamos los dos flojos; si me la quedo, llego yo. Es la cuenta más difícil de la cueva.", "Fuego"),
            new Fragmento(18, "El rey no murió", "El rey no era un hombre: era la cueva queriendo descansar. Selló los ocho elementos para dejar de sentir, y al abrirlos le devolví el dolor.", "Hielo"),
            new Fragmento(19, "El sello, otra vez", "Puedo volver a sellar. Ocho elementos quietos, una cueva en paz, y nadie peleando nunca más. Es justo lo que el rey quería, y ahora entiendo por qué.", "Planta"),
            new Fragmento(20, "Lo que queda", "No sellé nada. Dejé los ocho en manos de dos que todavía no saben usarlos, y bajé. La cueva va a doler un rato largo. También va a volver a cantar, y eso no lo había oído nadie en años.", "Rayo"),
            new Fragmento(21, "La garganta", "Bajar no es caer: es decidir, otra vez, que lo de arriba ya está. La garganta es angosta y pulida, como si algo la hubiera usado muchas veces. Yo fui el último.", "Tierra"),
            new Fragmento(22, "Lo que suena", "Las fuentes secas no cantan, pero acá abajo suena. No es agua: es lo que el agua era antes de secarse, y todavía espera. Me dio miedo, y seguí.", "Sombra"),
            new Fragmento(23, "Ocho manos", "La fuente tiene brazos. Ocho, del color de los ocho, y todos se mueven a la vez. No pelean entre ellos: pelean conmigo, porque yo fui el que los repartió.", "Aire"),
            new Fragmento(24, "El que selló primero", "El rey no fue el primero. Antes de él hubo otro que selló una sola cosa, y le fue tan bien que selló las demás. Su nombre se perdió; su costumbre, no.", "Agua"),
            new Fragmento(25, "Reglas del fondo", "Acá el anillo no funciona. Lo que vencía, pierde, y lo que perdía, vence. Entendí tarde que el anillo nunca fue una ley: era una costumbre de la cueva, y acá nadie la tiene.", "Fuego"),
            new Fragmento(26, "Al revés de todo", "Veinte niveles de saber me sirven de poco acá. Hay que tirar, mirar qué pasa, y volver a tirar. Es más lento, y es más honesto.", "Hielo"),
            new Fragmento(27, "El elemento sin nombre", "Entre los ocho hay un hueco, y en el hueco hay algo sin color. No lo puedo cargar. Me mira como se mira a alguien que va a morir y todavía no lo sabe: la misma mirada del que no es guardián.", "Planta"),
            new Fragmento(28, "El manantial", "No hay ocho. Hay uno, que aprendió a tener ocho caras para que alguien pudiera elegir. El rey selló una cara por miedo; yo abrí las ocho y bajé hasta la única. Ahora sé qué cargo: no un elemento, la decisión de con cuál pararme. La cueva va a cantar, y esta vez va a haber alguien que sepa escucharla.", "Rayo"),
                };
            }
            return _campana;
        }

        public static Fragmento[] Infinito()
        {
            if (_infinito == null)
            {
                _infinito = new Fragmento[]
                {
            new Fragmento(3, "La cueva no se acaba", "Bajaste hasta el manantial y el manantial tenía fondo. Ocho elementos, ocho nombres, ocho puertas: y ninguna cerrada del todo. La que selló el rey se abre desde el otro lado.", "Fuego"),
            new Fragmento(10, "Lo que vuelve", "Las babosas de la primera camada aparecen otra vez, en el mismo orden. No es que el tiempo repita: es que la cueva no aprende, y vos sí.", "Hielo"),
            new Fragmento(17, "El escudo y su coste", "El escudado no es más fuerte: es más paciente. Mientras lo tenga, tus tiros hacen la mitad. El que espera a que baje el escudo gana la misma pelea con menos vida.", "Planta"),
            new Fragmento(24, "El anillo, de nuevo", "Llega la ola veinticinco y el anillo se da vuelta otra vez. Veinte niveles te enseñaron qué vence a qué; acá eso vuelve a no servir, y hay que tirar y mirar.", "Rayo"),
            new Fragmento(31, "Vueltas", "El anillo se invierte cada ocho olas, derecho e invertido, derecho e invertido. Después de un par de ciclos lo lees sin pensarlo, y eso es todo el juego.", "Tierra"),
            new Fragmento(38, "Lo que juntaste", "Las gemas no se van cuando caés. Se quedan en el arma, en las babosas, en el escudo. Cada vuelta a la cueva la hacés con lo que trajiste de la anterior.", "Sombra"),
            new Fragmento(45, "El que no tiene nombre", "Entre los ocho elementos sigue habiendo un hueco sin color. No lo llena nadie, y la cueva no parece echarlo de menos.", "Aire"),
            new Fragmento(52, "Sin fondo", "No hay un nivel que cierre. Hay la ola que sigue, y la de después. El manantial no era el final de la cueva: era el principio de la parte que no tiene mapa.", "Agua"),
                };
            }
            return _infinito;
        }

        /// Fragmento del nivel de campana (1..28). Fuera de rango devuelve el ultimo.
        public static Fragmento DeCampana(int nivel)
        {
            Fragmento[] c = Campana();
            int i = nivel - 1;
            if (i < 0) i = 0;
            if (i >= c.Length) i = c.Length - 1;
            return c[i];
        }

        /// Fragmento del modo infinito: sale cada 7 olas desde la 3, y da vueltas.
        public static Fragmento DeOlaInfinito(int ola)
        {
            Fragmento[] f = Infinito();
            if (ola < 3) return f[0];
            int i = (ola - 3) / 7;
            return f[i % f.Length];
        }

        /// Las olas 1 y 2 no tienen fragmento propio. Antes DeOlaInfinito devolvia el de la ola 3
        /// para cualquier ola menor, asi que en la primera tanda ya se leia "La cueva no se acaba"
        /// dos veces. Quien muestra la historia tiene que preguntar esto.
        public static bool TieneFragmentoDeOla(int ola) { return ola >= 3; }
    }
}
