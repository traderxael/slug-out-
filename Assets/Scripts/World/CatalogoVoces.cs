using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SlugOut.World
{
    /// Una linea de voz: id estable, perfil de timbre y el texto exacto que se narra.
    public struct EntradaVoz
    {
        public string id;
        public string perfil;
        public string texto;
    }

    /// Fuente unica de rutas, textos y hashes de las voces. La usan el reproductor, el generador
    /// offline (Tools/Voces) y los tests: si la prosa cambia, el hash del manifiesto deja de coincidir.
    public static class CatalogoVoces
    {
        public const string Raiz = "Voces/";

        /// 28 de campana + 8 del infinito + epilogo + 18 frases de duelistas.
        public const int Total = 55;

        public static EntradaVoz[] Entradas()
        {
            List<EntradaVoz> lista = new List<EntradaVoz>(Total);

            for (int n = 1; n <= SlugStory.TotalCampana; n++)
                lista.Add(Nueva("campana/" + n, "eli", SlugStory.DeCampana(n).texto));

            foreach (SlugStory.Fragmento f in SlugStory.Infinito())
                lista.Add(Nueva("infinito/" + f.nivel, "eli", f.texto));

            lista.Add(Nueva("epilogo", "eli", SlugStory.Epilogo().texto));

            foreach (DuelistaVoces.Voz v in DuelistaVoces.Voces())
            {
                string perfil = v.id == "dr-blakk" ? "blakk" : v.id;
                for (int i = 0; i < v.frases.Length; i++)
                    lista.Add(Nueva("duelistas/" + v.id + "/" + i, perfil, v.frases[i]));
            }

            return lista.ToArray();
        }

        public static string RutaDeFragmento(SlugStory.Fragmento f, bool infinito)
        {
            if (SlugStory.EsEpilogo(f)) return Raiz + "epilogo";
            return infinito ? Raiz + "infinito/" + f.nivel : Raiz + "campana/" + f.nivel;
        }

        public static string RutaDuelista(string id, int indice)
        {
            return Raiz + "duelistas/" + id + "/" + indice;
        }

        public static string TextoDe(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";

            if (id == "epilogo") return SlugStory.Epilogo().texto;

            if (id.StartsWith("campana/"))
            {
                int n;
                if (int.TryParse(id.Substring("campana/".Length), out n))
                    return SlugStory.DeCampana(n).texto;
                return "";
            }

            if (id.StartsWith("infinito/"))
            {
                int n;
                if (!int.TryParse(id.Substring("infinito/".Length), out n)) return "";
                foreach (SlugStory.Fragmento f in SlugStory.Infinito())
                    if (f.nivel == n) return f.texto;
                return "";
            }

            if (id.StartsWith("duelistas/"))
            {
                string[] partes = id.Split('/');
                if (partes.Length != 3) return "";
                int i;
                if (!int.TryParse(partes[2], out i)) return "";
                DuelistaVoces.Voz v;
                if (!DuelistaVoces.TryVoz(partes[1], out v)) return "";
                if (i < 0 || i >= v.frases.Length) return "";
                return v.frases[i];
            }

            return "";
        }

        /// SHA-256 del texto original en UTF-8, hex minuscula. El generador PowerShell escribe el
        /// mismo valor en el manifiesto; el test falla si la prosa cambio sin regenerar la voz.
        public static string HashDe(string texto)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(texto ?? ""));
                StringBuilder sb = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static EntradaVoz Nueva(string id, string perfil, string texto)
        {
            EntradaVoz e;
            e.id = id;
            e.perfil = perfil;
            e.texto = texto;
            return e;
        }
    }
}
