using UnityEngine;
using SlugOut.Core;

namespace SlugOut.World
{
    /// La historia de SLUG OUT: 28 fragmentos de campana (uno por nivel) + 8 del modo infinito.
    /// Arco propio sobre los nombres del homenaje: Eli baja a buscar a su padre, Kord, Trixie y
    /// Pronto pasan de rivales a aliados, y Blakk persigue el noveno asiento del manantial.
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
        private static Fragmento _epilogo;

        public static Fragmento[] Campana()
        {
            if (_campana == null)
            {
                _campana = new Fragmento[]
                {
            new Fragmento(1, "La última fuente secó", "La fuente de casa se secó en marzo. Mi padre bajó a ver por qué y no volvió. Hoy entré yo, con su blaster y una libreta a medio llenar. La cueva no me recibió: me dejó pasar, que no es lo mismo.", "Fuego"),
            new Fragmento(2, "Todavía llora", "El agua ya no cae: gotea. Cada gota la pelean tres, y los tres tienen más sed que yo. Aprendí a llegar antes y a no mirar atrás. Todavía no sé si hay vuelta, pero guardo la mitad de la cantimplora por si la hay.", "Hielo"),
            new Fragmento(3, "No piden permiso", "Los guardianes de roca no hablan. Se plantan y esperan a que te canses. No hay que esquivarlos: hay que decidir cuándo tirar. Mi padre decía que la cueva enseña a pelear, no a ganar. Tenía razón.", "Planta"),
            new Fragmento(4, "Las raíces recuerdan", "Acá hubo verde, y las raíces no lo olvidan. Ahora caen piedras del techo, como si la cueva se estuviera cerrando por dentro. No es la edad: es algo que chupa la fuerza de abajo hacia arriba. Lo siento en los pies antes de oírlo.", "Rayo"),
            new Fragmento(5, "El frío guarda lo que queda", "El hielo no destruye: conserva. En una cámara encontré un campamento viejo, intacto bajo la escarcha. El cuaderno era de mi padre: la letra, el mapa, la mancha de aceite. La última página tiene un mapa a medio hacer. Bajé por donde decía.", "Tierra"),
            new Fragmento(6, "El primero que no era basura", "El guardián de escarcha no me atacó al verme. Me miró, cargó el hielo y esperó a que yo decidiera. Fue el primero que me trató como a alguien. Mi padre decía que una babosa elige a su slinger; yo creía que era una forma linda de decir que la atrapás.", "Sombra"),
            new Fragmento(7, "Chispas en la oscuridad", "El nido de centellas no duerme. Cada chispa es un guardián chico probando si sirvo. Esta vez no estaba solo: un grandote con casco me cortó la salida y dijo que el que ganaba se quedaba con el nido. Peleamos, empatamos y compartimos las chispas. Se llama Kord.", "Aire"),
            new Fragmento(8, "Lo que se lleva la sombra", "Desde la cornisa vi a un hombre de capa drenar la veta con un farol. No la rompía: la sorbía, y la sombra le obedecía. Detrás había acólitos con lámparas. Al lado mío, una chica de gafas tomaba notas sin hacer ruido. Se presentó como Trixie. Al de la capa, dijo, todos le dicen Blakk.", "Agua"),
            new Fragmento(9, "El fuego que no calienta", "Ardía sin dar calor. Un fuego vacío, como una promesa que nadie cumplió: ya no quedaba nadie a quien calentar. Al fondo del pasillo había una puerta sellada con la marca del rey. La marca estaba abierta un dedo. Alguien llegó antes que yo, y no fue mi padre.", "Fuego"),
            new Fragmento(10, "Los ecos del pasado", "El laberinto repite cada paso que doy. Entre los ecos reconocí la voz de mi padre, contando los tubos que le quedaban. Trixie dice que los ecos no repiten: guardan. Que la cueva no olvida a los que bajaron, y que por eso a veces contesta.", "Hielo"),
            new Fragmento(11, "La corriente que viene de abajo", "El rayo no cae del cielo: sube. Algo abajo lo empuja, y ese algo tiene prisa. Trixie midió el pulso de la piedra y dijo que están perforando. Blakk no vino a robar un elemento: vino a abrir la puerta que los tiene a los ocho.", "Planta"),
            new Fragmento(12, "La verdad", "En el fondo no hay un monstruo. Hay un rey que selló los ocho elementos para que nadie peleara por ellos, y una marca en el pecho por cada uno. Blakk quiere abrir el sello y quedarse con la llave. Yo traigo ocho babosas y una pregunta: si la cueva está muerta, ¿de qué sirve que esté en paz?", "Rayo"),
            new Fragmento(13, "Despertar", "La cueva respira. No es un consuelo: respirar es querer algo, y lo que quiere no soy yo. Desde que abrí el primer sello me mira distinto, como se mira a un socio al que todavía no le pagaron. Trixie dice que no me asuste. Se le nota que a ella también.", "Tierra"),
            new Fragmento(14, "No caben ocho", "Los elementos libres no saben compartir. El agua discute con el fuego, el rayo con la sombra, y yo estoy en el medio sin poder poner orden. Trixie los mira pelear y toma notas: dice que el anillo no es una ley, es una costumbre. Y las costumbres se pueden cambiar.", "Sombra"),
            new Fragmento(15, "Pasos que no son míos", "Alguien bajó detrás de mí. Los oí en el cristal: lentos, cuidadosos, de alguien que no quiere romper nada. Era un topo con un cargador de cinco tubos y hambre de tres días. No vino a pelear: vino a saber si mi mochila tenía algo que valiera la pena.", "Aire"),
            new Fragmento(16, "El que no es guardián", "Era mi padre. Estaba vivo y vacío: gastó su elemento abriéndole paso a Blakk y ya no le quedaba ni para reconocerme. Se quedó sentado en el umbral, sosteniendo algo que no se veía. Me miró como se mira a alguien que va a morir y todavía no lo sabe. Me dijo que no bajara por él. Bajé igual.", "Agua"),
            new Fragmento(17, "Un odre para dos", "Queda agua para uno. Pronto la quiere y no lo culpo: también tiene sed. Si la reparto llegamos los dos flojos; si me la quedo, llego yo. Le di la mitad. Tardamos más, llegamos juntos, y desde entonces el topo camina un paso detrás de mí.", "Fuego"),
            new Fragmento(18, "El rey no murió", "El rey no era un hombre: era la cueva queriendo descansar. Selló los ocho elementos para dejar de sentir, y al abrirlos le devolví el dolor. Ahora sé por qué me dejó pasar. No buscaba un heredero: buscaba a alguien que se animara a cargar con lo que él no pudo.", "Hielo"),
            new Fragmento(19, "El sello, otra vez", "Puedo volver a sellar. Ocho elementos quietos, una cueva en paz, y nadie peleando nunca más. Es justo lo que el rey quería, y ahora entiendo por qué. También entiendo el precio: una cueva en paz es una cueva que no canta. Mi padre eligió el silencio. Yo no.", "Planta"),
            new Fragmento(20, "Lo que queda", "No sellé nada. Repartí los ocho: la piedra para Kord, el eco para Trixie, el fuego para Pronto. No caben ocho, pero caben tres. Les pedí que no me siguieran y bajé solo, que es como se baja a las cosas que no tienen mapa.", "Rayo"),
            new Fragmento(21, "La garganta", "Bajar no es caer: es decidir, otra vez, que lo de arriba ya está. La garganta es angosta y pulida, como si algo la hubiera usado muchas veces. Cuento las marcas en la pared y me doy cuenta de que soy el último. No hay nadie detrás de mí.", "Tierra"),
            new Fragmento(22, "Lo que suena", "Las fuentes secas no cantan, pero acá abajo suena. No es agua: es lo que el agua era antes de secarse, y todavía espera. Me dio miedo, y seguí. Mi padre decía que el miedo es solo la cueva avisando que estás cerca de algo verdadero.", "Sombra"),
            new Fragmento(23, "Ocho manos", "La fuente tiene brazos. Ocho, del color de los ocho, y todos se mueven a la vez. No pelean entre ellos: pelean conmigo, porque yo fui el que los repartió. Kord tiene la piedra; Trixie, el eco; Pronto, el fuego. Y los tres están arriba, esperando que vuelva.", "Aire"),
            new Fragmento(24, "El que selló primero", "El rey no fue el primero. Antes de él hubo otro que selló una sola cosa: a sí mismo. Le fue tan bien que después selló las demás. Su nombre se perdió; su costumbre, no. Blakk ya está abajo, con su farol, esperándome.", "Agua"),
            new Fragmento(25, "Reglas del fondo", "Acá el anillo no funciona. Lo que vencía, pierde, y lo que perdía, vence. Entendí tarde que el anillo nunca fue una ley: era una costumbre de la cueva, y acá nadie la tiene. Tiro, miro qué pasa y vuelvo a tirar. Ya no me sé de memoria nada.", "Fuego"),
            new Fragmento(26, "Al revés de todo", "Veinte niveles de saber me sirven de poco acá. Hay que tirar, mirar qué pasa, y volver a tirar. Es más lento, y es más honesto. Arriba creía que el anillo era el mundo; abajo el mundo es esto: una babosa, un blaster y lo que veo.", "Hielo"),
            new Fragmento(27, "El elemento sin nombre", "Entre los ocho hay un hueco, y en el hueco hay algo sin color. No lo puedo cargar. Tiene la mirada de mi padre: la misma del que no es guardián. Entendí lo que falta: el noveno no es un elemento, es el asiento del que elige. Y mi padre se sentó ahí para que el sello aguantara.", "Planta"),
            new Fragmento(28, "El manantial", "El manantial no tenía ocho caras: tenía una, y las otras siete eran miedo. El rey selló por miedo; Blakk quería abrirlo por ambición. Yo abrí las ocho y no me senté en el noveno asiento. Dejé que mi padre se fuera con el agua. La cueva va a cantar otra vez, y esta vez hay alguien que sabe escuchar.", "Rayo"),
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
            new Fragmento(3, "La cueva no se acaba", "Bajaste hasta el manantial y el manantial tenía fondo. Ocho elementos, ocho nombres, ocho puertas, y ninguna cerrada del todo. La que selló el rey se abre desde el otro lado. Abajo no hay niveles: hay olas, y la cueva no te debe un final.", "Fuego"),
            new Fragmento(10, "Lo que vuelve", "Las babosas de la primera camada aparecen otra vez, en el mismo orden. No es que el tiempo repita: es que la cueva no aprende, y vos sí. Cada vuelta traés la misma cara y una idea nueva. Las dos cosas le sirven.", "Hielo"),
            new Fragmento(17, "El escudo y su coste", "El escudado no es más fuerte: es más paciente. El escudo se come tus tiros enteros hasta que se gasta, y mientras tanto él sigue de pie. El que espera a que baje la guardia gana la misma pelea con menos vida.", "Planta"),
            new Fragmento(24, "El anillo, de nuevo", "Acá el anillo no se da vuelta: abajo del manantial la ley vuelve a estar derecha, y esa es la trampa. Lo único que cambia es la prisa: cada ola entra más rápido y pega más fuerte. Blakk vuelve cada veinticinco criaturas, siempre al final de un tramo, siempre con su farol.", "Rayo"),
            new Fragmento(31, "Vueltas", "Arriba contabas olas; acá contás respiraciones. Cuatro pueden estar vivas a la vez, y la quinta espera. No hay anillo que leer ni costumbre que gastar: hay que mirar quién entra, por dónde, y con qué te recibís. Después de un par de vueltas lo hacés sin pensar.", "Tierra"),
            new Fragmento(38, "Lo que juntaste", "Abajo no hay alforja que aguante: lo que capturás en una bajada se queda en la bajada. Elegí con qué entrás, porque el equipo es lo único que traés. Las babosas que vinculaste arriba te esperan en el registro, con la ficha y la conducta. La cueva no guarda nada más.", "Sombra"),
            new Fragmento(45, "El que no tiene nombre", "Entre los ocho elementos sigue habiendo un hueco sin color. No lo llena nadie, y la cueva no parece echarlo de menos. A veces, cuando el farol de Blakk pasa cerca, el hueco se mueve. Después se queda quieto, como si hubiera decidido no mirarte.", "Aire"),
            new Fragmento(52, "Sin fondo", "No hay un nivel que cierre. Hay la ola que sigue, y la de después. El manantial no era el final de la cueva: era el principio de la parte que no tiene mapa. Bajá todo lo que puedas, y cuando caigas, acordate de que abajo no hay nadie esperándote.", "Agua"),
                };
            }
            return _infinito;
        }

        /// Cierre de la campana. Vive fuera de Campana() para no alterar el barrido de 28
        /// (CampanaTests cuenta fragmentos y textura por nivel) y reutiliza la lamina 28.
        public static Fragmento Epilogo()
        {
            if (_epilogo.titulo == null)
            {
                _epilogo = new Fragmento(28, "La cara que faltaba",
                    "Blakk se fue con una cara del manantial. No lo perseguí: la cueva se abre hacia abajo y no hay mapa para lo que sigue. Cada tantos tramos lo vuelvo a ver, siempre al final, siempre midiéndome. El manantial no era el final: era la puerta.",
                    "Rayo");
            }
            return _epilogo;
        }

        /// El cierre comparte nivel 28 con el ultimo fragmento de campana: el panel lo rotula
        /// distinto para que no parezca que el nivel 28 tiene dos entradas.
        public static bool EsEpilogo(Fragmento f)
        {
            return f.nivel == 28 && f.titulo == Epilogo().titulo;
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
