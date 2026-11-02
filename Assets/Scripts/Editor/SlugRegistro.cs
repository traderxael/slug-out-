#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.EditorTools
{
    /// Completa los campos del registro de campo (habitat, conducta, uso, limitacion)
    /// en todos los SlugData que los tengan vacios. No pisa texto ya escrito: si alguien
    /// edita una ficha a mano, volver a correr esto no la borra.
    public static class SlugRegistro
    {
        const string Carpeta = "Assets/ScriptableObjects/Slugs";

        [MenuItem("SlugOut/5. Completar fichas de registro", false, 5)]
        public static void Menu()
        {
            EditorUtility.DisplayDialog("Slug Out", Rellenar(), "Aceptar");
        }

        /// Version sin modal, para poder accionarla desde una sonda.
        public static string Rellenar()
        {
            int total = 0, tocados = 0;
            var sb = new StringBuilder();
            foreach (string ruta in Directory.GetFiles(Carpeta, "*.asset"))
            {
                SlugData d = AssetDatabase.LoadAssetAtPath<SlugData>(ruta.Replace('\\', '/'));
                if (d == null) continue;
                total++;
                Ficha f = FichaDe(d.element);
                bool cambio = false;
                if (string.IsNullOrEmpty(d.habitat)) { d.habitat = f.habitat; cambio = true; }
                if (string.IsNullOrEmpty(d.conducta)) { d.conducta = f.conducta; cambio = true; }
                if (string.IsNullOrEmpty(d.uso)) { d.uso = f.uso; cambio = true; }
                if (string.IsNullOrEmpty(d.limitacion)) { d.limitacion = f.limitacion; cambio = true; }
                if (cambio) { EditorUtility.SetDirty(d); tocados++; }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            sb.Append("fichas completadas: " + tocados + " de " + total);
            Debug.Log("[SlugRegistro] " + sb);
            return sb.ToString();
        }

        struct Ficha
        {
            public string habitat, conducta, uso, limitacion;
        }

        static Ficha FichaDe(ElementType e)
        {
            switch (e)
            {
                case ElementType.Fire:
                    return new Ficha {
                        habitat = "Grietas calientes, cerca de las fumarolas.",
                        conducta = "Se queda quieta juntando calor; un paso fuerte la hace saltar.",
                        uso = "Quema y obliga al rival a retroceder.",
                        limitacion = "El agua la apaga y a los que esquivan de lado no los toca." };
                case ElementType.Water:
                    return new Ficha {
                        habitat = "Pozas y cursos de agua subterraneos.",
                        conducta = "Se mueve pegada al agua; desconfia si te acercas por la orilla seca.",
                        uso = "Frena al rival y le corta la carga.",
                        limitacion = "Lejos del agua pierde casi toda la fuerza." };
                case ElementType.Earth:
                    return new Ficha {
                        habitat = "Galerias de roca suelta y derrumbes.",
                        conducta = "Camina lento y se esconde al sentir vibraciones.",
                        uso = "Aturde y reinicia la carga del rival.",
                        limitacion = "Los tiros rapidos y por el aire la pasan de largo." };
                case ElementType.Air:
                    return new Ficha {
                        habitat = "Corrientes de aire en las grietas altas.",
                        conducta = "Se deja llevar por la corriente; no tolera el encierro.",
                        uso = "Perfora defensas y llega lejos.",
                        limitacion = "Pega poco si el rival esta contra la pared." };
                case ElementType.Ice:
                    return new Ficha {
                        habitat = "Estalactitas y charcos congelados.",
                        conducta = "Duerme sobre el hielo; el calor la despierta de mal humor.",
                        uso = "Congela y baja la carga del blaster rival.",
                        limitacion = "El fuego la derrite y le recorta el alcance." };
                case ElementType.Electric:
                    return new Ficha {
                        habitat = "Vetas metalicas y cristales cargados.",
                        conducta = "Salta entre paredes cargadas; el ruido la atrae.",
                        uso = "Golpea dos veces y es de las mas rapidas.",
                        limitacion = "Se descarga: necesita recargar tras dos tiros." };
                case ElementType.Plant:
                    return new Ficha {
                        habitat = "Raices y musgo junto al agua.",
                        conducta = "Se enrosca en las raices; se cierra si la tocas.",
                        uso = "Cura al explorador cuando impacta.",
                        limitacion = "El fuego la quema y si falla el tiro no cura nada." };
                case ElementType.Shadow:
                    return new Ficha {
                        habitat = "Galerias sin luz, detras de las rocas.",
                        conducta = "Solo se mueve en sombra; la luz directa la paraliza.",
                        uso = "Roba vida al impactar.",
                        limitacion = "En zonas iluminadas no puede esconderse." };
                case ElementType.Energy:
                    return new Ficha {
                        habitat = "Camaras de cristal que vibran.",
                        conducta = "Flota y pulsa; se aleja del metal.",
                        uso = "Empuja con fuerza sin quemar.",
                        limitacion = "Es inestable: falla si el tiro sale muy corto." };
                case ElementType.Light:
                    return new Ficha {
                        habitat = "Claros donde entra luz desde arriba.",
                        conducta = "Busca la luz y huye del ruido.",
                        uso = "Cura y protege al explorador.",
                        limitacion = "En la oscuridad pierde casi todo el poder." };
                case ElementType.Toxic:
                    return new Ficha {
                        habitat = "Charcos turbios y hongos.",
                        conducta = "Deja un rastro humedo; se esconde si pisas fuerte.",
                        uso = "Envenena con el tiempo.",
                        limitacion = "Tarda en hacer efecto y el viento la desvia." };
                default:
                    return new Ficha {
                        habitat = "Galerias de la cueva seca.",
                        conducta = "Se mueve despacio y observa antes de acercarse.",
                        uso = "Apoya al explorador en el combate.",
                        limitacion = "No resuelve sola ninguna situacion." };
            }
        }
    }
}
#endif
