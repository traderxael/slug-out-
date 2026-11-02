#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.EditorTools
{
    /// Genera las 40 babosas del roster (8 elementos x 5 rarezas) como SlugData.
    /// Los modelos 3D se resuelven por convencion: Resources/Slugs/<slugId>.glb
    /// (SlugArenaGame hace Resources.Load<GameObject>("Slugs/" + slugId)).
    public static class SlugRosterGenerator
    {
        static readonly string[] RarezaNames = { "Comun", "Especial", "Rara", "Epica", "Legendaria" };
        static readonly float[] RarezaMult = { 1.00f, 1.20f, 1.45f, 1.80f, 2.30f };

        struct Row
        {
            public string slugId; public string displayName; public int tier;
            public ElementType element; public SlugAbilityType ability;
            public float dmg; public float speed; public float accel;
            public float energy; public float clash; public float magnitude; public float duration;
            public Color color; public string description;
        }

        static readonly Dictionary<string, ElementType> ElementMap = new Dictionary<string, ElementType>
        {
            { "fuego", ElementType.Fire }, { "agua", ElementType.Water },
            { "tierra", ElementType.Earth }, { "aire", ElementType.Air },
            { "hielo", ElementType.Ice }, { "rayo", ElementType.Electric },
            { "planta", ElementType.Plant }, { "sombra", ElementType.Shadow }
        };

        // (nombre, hexadecimal) por elemento; el resto se deriva de la rareza.
        static readonly Dictionary<string, ElementSpec> Specs = new Dictionary<string, ElementSpec>
        {
            { "fuego",  new ElementSpec("#e05a30", SlugAbilityType.BurnOverTime, 34f, 46f, 92f, 100f, 52f, 12f, 3.0f, "Quema al objetivo durante un tiempo") },
            { "agua",   new ElementSpec("#3f95cf", SlugAbilityType.EnergyDrain,  28f, 44f, 86f,  96f, 46f, 10f, 2.5f, "Ralentiza el contraataque del rival") },
            { "tierra", new ElementSpec("#a67c4e", SlugAbilityType.Stun,         38f, 36f, 74f, 108f, 62f,  0.9f, 2.0f, "Puede aturdir y reiniciar la carga rival") },
            // aire y rayo estaban en DirectDamage con magnitud 0: la Q gastaba energia y cooldown
            // para hacer 0 de dano. Ojo con que Stun y Freeze solo leen effectDuration, asi que a
            // aire se le da duracion real y magnitud irrelevante, no un numero que nadie consume.
            { "aire",   new ElementSpec("#c3d3d8", SlugAbilityType.Stun,         26f, 56f, 110f, 88f, 40f,  0f,   0.45f, "Perfora resistencias: ignora armaduras") },
            { "hielo",  new ElementSpec("#6fd6e6", SlugAbilityType.Freeze,       27f, 42f, 88f, 100f, 44f,  1.0f, 1.6f, "Puede congelar al rival y bajar su carga") },
            { "rayo",   new ElementSpec("#e8d44a", SlugAbilityType.DirectDamage, 30f, 60f, 118f, 92f, 48f,  18f,  0f,   "Rapido; puede golpear dos veces") },
            { "planta", new ElementSpec("#57b85a", SlugAbilityType.Heal,         22f, 40f, 80f,  94f, 42f, 14f,  2.0f, "Cura al lanzador al impactar") },
            { "sombra", new ElementSpec("#8a5fd6", SlugAbilityType.EnergyDrain,  36f, 48f, 96f, 112f, 58f,  18f,  2.2f, "Roba parte del dana como vida") }
        };

        class ElementSpec
        {
            public string hex; public SlugAbilityType ability;
            public float dmg, speed, accel, energy, clash, magnitude, duration;
            public string abilityText;
            public ElementSpec(string h, SlugAbilityType a, float d, float s, float ac, float e, float c, float m, float du, string ab)
            { hex = h; ability = a; dmg = d; speed = s; accel = ac; energy = e; clash = c; magnitude = m; duration = du; abilityText = ab; }
        }

        static readonly Dictionary<string, string[]> Roster = new Dictionary<string, string[]>
        {
            { "fuego",  new[] { "Ascua", "Chispa", "Ignia", "Vulcano", "Fénix" } },
            { "agua",   new[] { "Gotita", "Marea", "Corriente", "Abisal", "Leviatán" } },
            { "tierra", new[] { "Guijarro", "Roquito", "Bloque", "Coloso", "Titán" } },
            { "aire",   new[] { "Brisa", "Remolino", "Ráfaga", "Tifón", "Huracán" } },
            { "hielo",  new[] { "Copo", "Escarcha", "Glaciar", "Ventisca", "Eterna" } },
            { "rayo",   new[] { "Zumbido", "Centella", "Relámpago", "Tormenta", "Zeus" } },
            { "planta", new[] { "Brote", "Liana", "Espina", "Selva", "Primigenia" } },
            { "sombra", new[] { "Mota", "Ténue", "Oculta", "Abismo", "Vacío" } }
        };

        // slugId = nombre sin acentos, en minusculas: coincide con Resources/Slugs/<id>.glb
        static string ToId(string nombre)
        {
            var normalized = nombre.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char ch in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (ch < 128) sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        /// SlugRoster.Todos busca primero Resources/SlugsData/SlugRoster. Sin ese asset el roster
        /// cae al fallback de "cosas ya cargadas", que en un build solo conoce los 22 SlugData a
        /// los que referencia la escena: los otros 40 quedaban inalcanzables sin ningun error.
        /// Poblar() es de editor y serializa el array dentro del asset, asi que el build lo hereda.
        [MenuItem("SlugOut/4. Crear contenedor del roster en Resources", false, 4)]
        public static void CrearContenedorRoster()
        {
            EditorUtility.DisplayDialog("Slug Out", CrearContenedor(), "Aceptar");
        }

        /// Version sin modal para poder accionarla desde una sonda sin colgar el hilo del editor.
        public static string CrearContenedor()
        {
            const string ruta = "Assets/Resources/SlugsData/SlugRoster.asset";
            if (!Directory.Exists("Assets/Resources/SlugsData")) Directory.CreateDirectory("Assets/Resources/SlugsData");

            SlugRoster cont = AssetDatabase.LoadAssetAtPath<SlugRoster>(ruta);
            bool nuevo = cont == null;
            if (nuevo)
            {
                cont = ScriptableObject.CreateInstance<SlugRoster>();
                AssetDatabase.CreateAsset(cont, ruta);
            }
            cont.Poblar();
            int n = cont.Lista != null ? cont.Lista.Length : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SlugRoster.Invalidar();

            string msg = (nuevo ? "creado " : "recargado ") + ruta + " con " + n + " babosas serializadas.";
            Debug.Log("[SlugRoster] " + msg);
            return msg;
        }

        [MenuItem("SlugOut/3. Generar roster completo (40 babosas + GLB)", false, 3)]
        public static void GenerateRoster()
        {
            EditorUtility.DisplayDialog("Slug Out", Generar(), "Aceptar");
        }

        /// Mismo trabajo sin modal: permite accionarlo desde una sonda sin colgar el hilo del editor.
        public static string Generar()
        {
            const string folder = "Assets/ScriptableObjects/Slugs";
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var rows = BuildRows();
            int created = 0;
            var missingModels = new List<string>();
            foreach (var r in rows)
            {
                // El archivo se nombra por slugId, no por displayName: slugId es la clave canonica
                // en tres sitios (modelo Resources/Slugs/<id>, retrato y la seleccion guardada en
                // GameFlow). Con displayName acentuado, re-correr el menu no encontraba a
                // "Fenix" y creaba un asset duplicado con otro GUID.
                string path = $"{folder}/{r.slugId}.asset";
                SlugData s = AssetDatabase.LoadAssetAtPath<SlugData>(path);
                if (s == null) { s = ScriptableObject.CreateInstance<SlugData>(); AssetDatabase.CreateAsset(s, path); }

                s.slugId = r.slugId;
                s.slugName = r.displayName;
                s.element = r.element;
                s.baseDamage = r.dmg;
                s.launchSpeedMph = r.speed;
                s.accelerationRate = r.accel;
                s.requiredEnergy = r.energy;
                s.clashPower = r.clash;
                s.abilityType = r.ability;
                s.abilityMagnitude = r.magnitude;
                s.effectDuration = r.duration;
                s.description = r.description;
                s.elementColor = r.color;
                s.velocimorphAuraColor = Color.Lerp(r.color, Color.white, 0.4f);

                GameObject model = Resources.Load<GameObject>("Slugs/" + r.slugId);
                if (model == null) missingModels.Add(r.slugId);

                EditorUtility.SetDirty(s);
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // El roster cachea en un static: sin esto, quien pida la lista despues de regenerar
            // seguiria viendo los datos viejos.
            SlugOut.Slugs.SlugRoster.Invalidar();

            var sb = new StringBuilder();
            sb.Append($"SlugData creados/actualizados: {created} de {rows.Count}.\n");
            sb.Append(missingModels.Count == 0
                ? "Todos los GLB se resolvieron por Resources/Slugs/<slugId>."
                : $"FALTAN {missingModels.Count} modelos: {string.Join(", ", missingModels)}");
            Debug.Log($"[SlugRoster] {sb}");
            return sb.ToString();
        }

        static List<Row> BuildRows()
        {
            var list = new List<Row>();
            foreach (var kv in Roster)
            {
                var spec = Specs[kv.Key];
                var el = ElementMap[kv.Key];
                Color c = HexToColor(spec.hex);
                for (int tier = 0; tier < kv.Value.Length; tier++)
                {
                    float m = RarezaMult[tier];
                    string name = kv.Value[tier];
                    list.Add(new Row
                    {
                        slugId = ToId(name),
                        displayName = name,
                        tier = tier,
                        element = el,
                        ability = spec.ability,
                        dmg = Mathf.Round(spec.dmg * m * 10f) / 10f,
                        speed = Mathf.Round(spec.speed * (1f + tier * 0.04f)),
                        accel = Mathf.Round(spec.accel * (1f + tier * 0.05f)),
                        energy = Mathf.Round(spec.energy * (1f + tier * 0.07f)),
                        clash = Mathf.Round(spec.clash * m * 10f) / 10f,
                        magnitude = Mathf.Round(spec.magnitude * m * 100f) / 100f,
                        duration = Mathf.Round(spec.duration * (1f + tier * 0.08f) * 10f) / 10f,
                        color = c,
                        description = $"{name} ({RarezaNames[tier]}, {el}): {spec.abilityText}. Escala {m:0.00}x sobre la base del elemento."
                    });
                }
            }
            return list;
        }

        static Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");
            return new Color(
                System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f,
                System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f,
                System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f, 1f);
        }
    }
}
#endif
