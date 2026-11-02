#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using SlugOut.Core;
using SlugOut.Slugs;
using SlugOut.Battle;
using SlugOut.Board;
using SlugOut.UI;
using SlugOut.Audio;

namespace SlugOut.EditorTools
{
    public static class SlugOutSetupMenu
    {
        [MenuItem("SlugOut/1. Generar ScriptableObjects de Babosas", false, 1)]
        public static void GenerateDefaultSlugs()
        {
            string folder = "Assets/ScriptableObjects/Slugs";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            CreateOrUpdateSlug(folder, "Infierno", "Infierno", ElementType.Fire, 40f, 45f, 95f, 100f, 55f, SlugAbilityType.BurnOverTime, 12f, 3f, new Color(1f, 0.3f, 0.1f));
            CreateOrUpdateSlug(folder, "SanadoraDoc", "Doc (Sanadora)", ElementType.Light, 20f, 40f, 80f, 90f, 35f, SlugAbilityType.Heal, 45f, 0f, new Color(0.2f, 0.9f, 0.4f));
            CreateOrUpdateSlug(folder, "Electroshock", "Electroshock", ElementType.Electric, 38f, 50f, 110f, 100f, 50f, SlugAbilityType.Stun, 1.5f, 1.5f, new Color(1f, 0.95f, 0.2f));
            CreateOrUpdateSlug(folder, "Aquabeek", "Aquabeek", ElementType.Water, 32f, 42f, 85f, 95f, 45f, SlugAbilityType.DirectDamage, 0f, 0f, new Color(0.2f, 0.6f, 1f));
            CreateOrUpdateSlug(folder, "Demoledora", "Demoledora", ElementType.Earth, 45f, 35f, 75f, 110f, 65f, SlugAbilityType.DirectDamage, 0f, 0f, new Color(0.75f, 0.45f, 0.2f));
            CreateOrUpdateSlug(folder, "Congelada", "Congelada", ElementType.Ice, 30f, 40f, 90f, 100f, 45f, SlugAbilityType.Freeze, 2.0f, 2.0f, new Color(0.4f, 0.9f, 1f));
            CreateOrUpdateSlug(folder, "DarkFurnace", "Dark Furnace (Ghoul)", ElementType.Shadow, 55f, 48f, 100f, 120f, 70f, SlugAbilityType.BurnOverTime, 20f, 4f, new Color(0.6f, 0.1f, 0.8f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Slug Out", "Babosas generadas con exito en 'Assets/ScriptableObjects/Slugs'!", "Aceptar");
        }

        private static void CreateOrUpdateSlug(string folder, string filename, string slugName, ElementType elem,
            float dmg, float launchSpd, float accel, float reqEnergy, float clash, SlugAbilityType ability, float magnitude, float duration, Color color)
        {
            string path = $"{folder}/{filename}.asset";
            SlugData slug = AssetDatabase.LoadAssetAtPath<SlugData>(path);
            if (slug == null)
            {
                slug = ScriptableObject.CreateInstance<SlugData>();
                AssetDatabase.CreateAsset(slug, path);
            }

            slug.slugId = filename.ToLower();
            slug.slugName = slugName;
            slug.element = elem;
            slug.baseDamage = dmg;
            slug.launchSpeedMph = launchSpd;
            slug.accelerationRate = accel;
            slug.requiredEnergy = reqEnergy;
            slug.clashPower = clash;
            slug.abilityType = ability;
            slug.abilityMagnitude = magnitude;
            slug.effectDuration = duration;
            slug.elementColor = color;
            slug.velocimorphAuraColor = Color.Lerp(color, Color.white, 0.4f);

            // Intentar vincular sprites si existen
            Sprite proto = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Slugs/Protoforms/{slugName}.webp");
            Sprite velo = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Slugs/Velocimorphs/{filename.ToLower()}.webp");
            if (proto != null) slug.protoformSprite = proto;
            if (velo != null) slug.velocimorphSprite = velo;

            EditorUtility.SetDirty(slug);
        }

        [MenuItem("SlugOut/2. Configurar Escena de Batalla Completa", false, 2)]
        public static void SetupBattleScene()
        {
            // 1. Audio Manager
            GameObject audioGo = GameObject.Find("ProceduralBattleAudio");
            if (audioGo == null)
            {
                audioGo = new GameObject("ProceduralBattleAudio", typeof(ProceduralBattleAudio));
                Undo.RegisterCreatedObjectUndo(audioGo, "Crear Audio Manager");
            }

            // 2. Battle Manager
            GameObject bmGo = GameObject.Find("BattleManager");
            if (bmGo == null)
            {
                bmGo = new GameObject("BattleManager", typeof(BattleManager));
                Undo.RegisterCreatedObjectUndo(bmGo, "Crear Battle Manager");
            }
            BattleManager battleManager = bmGo.GetComponent<BattleManager>();

            // 3. Duelista Jugador
            GameObject playerGo = GameObject.Find("PlayerDuelist");
            if (playerGo == null)
            {
                playerGo = new GameObject("PlayerDuelist", typeof(Duelist), typeof(CircleCollider2D));
                playerGo.transform.position = new Vector3(-6f, 0.5f, 0f);
                var col = playerGo.GetComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 1.0f;
                Undo.RegisterCreatedObjectUndo(playerGo, "Crear Player Duelist");
            }
            Duelist playerDuelist = playerGo.GetComponent<Duelist>();

            // 4. Duelista Enemigo (Dr. Blakk)
            GameObject enemyGo = GameObject.Find("EnemyDuelist");
            if (enemyGo == null)
            {
                enemyGo = new GameObject("EnemyDuelist", typeof(Duelist), typeof(EnemyAI), typeof(CircleCollider2D));
                enemyGo.transform.position = new Vector3(6f, 0.5f, 0f);
                var col = enemyGo.GetComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 1.0f;
                Undo.RegisterCreatedObjectUndo(enemyGo, "Crear Enemy Duelist");
            }
            Duelist enemyDuelist = enemyGo.GetComponent<Duelist>();

            // Asignar babosas de muestra al jugador
            string slugsFolder = "Assets/ScriptableObjects/Slugs";
            var infierno = AssetDatabase.LoadAssetAtPath<SlugData>($"{slugsFolder}/Infierno.asset");
            var doc = AssetDatabase.LoadAssetAtPath<SlugData>($"{slugsFolder}/SanadoraDoc.asset");
            var electro = AssetDatabase.LoadAssetAtPath<SlugData>($"{slugsFolder}/Electroshock.asset");
            var dem = AssetDatabase.LoadAssetAtPath<SlugData>($"{slugsFolder}/Demoledora.asset");
            var ghoul = AssetDatabase.LoadAssetAtPath<SlugData>($"{slugsFolder}/DarkFurnace.asset");

            if (infierno != null && doc != null && electro != null)
            {
                var list = new System.Collections.Generic.List<SlugData> { infierno, doc, electro, dem };
                playerDuelist.SetEquippedSlugs(list);
            }

            if (ghoul != null)
            {
                enemyDuelist.SetEquippedSlugs(new System.Collections.Generic.List<SlugData> { ghoul });
            }

            EditorUtility.DisplayDialog("Slug Out", "Escena de batalla y duelistas configurados exitosamente!", "Aceptar");
        }
    }
}
#endif
