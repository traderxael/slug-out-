using UnityEngine;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.World
{
    /// Habilidad activa del jugador, en Q. Usa los tres campos de SlugData que estaban
    /// muertos: abilityType, abilityMagnitude y effectDuration.
    public class PlayerAbilities : MonoBehaviour
    {
        [SerializeField] private SlugData data;
        [SerializeField] private SlugActor actor;
        [SerializeField] private float enfriamiento = 6f;
        [SerializeField] private float radioEfecto = 9f;
        [SerializeField] private int usos = 0;
        [SerializeField] private string ultimoResultado = "-";

        private float _cd;
        private SlugFx _fx;

        public int Usos { get { return usos; } }
        public float CooldownRestante { get { return Mathf.Max(0f, _cd); } }
        public string UltimoResultado { get { return ultimoResultado; } }
        public bool Lista { get { return _cd <= 0f && actor != null && actor.Alive; } }

        public void Configurar(SlugData d, SlugActor a) { data = d; actor = a; }

        private void Update()
        {
            // Mismo motivo que en PlayerShooter: con timeScale=0 este Update sigue corriendo y
            // mantener Q conjura la habilidad detras del menu.
            if (GameFeel.Pausa || SlugArenaGame.CombateCongelado) return;
            _cd -= Time.deltaTime;
            EntradaFrame e = EntradaJuego.Leer();
            if (e.habilidad) Usar();
        }

        /// Publica: las pruebas la llaman directo, sin depender del foco de la ventana.
        public bool Usar()
        {
            if (!Lista || data == null) return false;

            // El objetivo se resuelve ANTES de gastar: antes un Q fuera de alcance pagaba
            // enfriamiento y energia para devolver false, y el reintento devolvia la energia
            // pero no _cd, asi que el castigo se quedaba puesto.
            SlugActor objetivo = ObjetivoCercano();
            bool requiereObjetivo = data.abilityType != SlugAbilityType.Heal && data.abilityType != SlugAbilityType.Shield;
            if (objetivo == null && requiereObjetivo) { ultimoResultado = "sin objetivo"; return false; }

            usos++;
            float costo = Mathf.Max(5f, data.requiredEnergy * 0.25f);
            if (!actor.GastaEnergia(costo)) { ultimoResultado = "sin energia"; _cd = 0.5f; usos--; return false; }
            _cd = enfriamiento;

            SlugFx f = _fx != null ? _fx : (_fx = GetComponent<SlugFx>());
            // Piso del dano: un SlugData con abilityMagnitude en 0 hacia de la Q un puro despilfarro.
            float mag = Mathf.Max(data.abilityMagnitude, data.baseDamage * 0.6f);

            switch (data.abilityType)
            {
                case SlugAbilityType.DirectDamage:
                    objetivo.TakeDamage(mag, data.element);
                    ultimoResultado = "directo " + mag;
                    break;

                case SlugAbilityType.Heal:
                    actor.Heal(data.abilityMagnitude);
                    ultimoResultado = "curo " + data.abilityMagnitude;
                    break;

                case SlugAbilityType.Shield:
                    actor.DarEscudo(data.abilityMagnitude);
                    ultimoResultado = "escudo " + data.abilityMagnitude;
                    break;

                case SlugAbilityType.Freeze:
                    objetivo.Congelar(data.effectDuration);
                    ultimoResultado = "congelado " + data.effectDuration + "s";
                    break;

                case SlugAbilityType.BurnOverTime:
                    objetivo.Quemar(mag * 0.1f, data.effectDuration);
                    ultimoResultado = "quemando " + data.effectDuration + "s";
                    break;

                case SlugAbilityType.Stun:
                    objetivo.Aturdir(data.effectDuration);
                    ultimoResultado = "aturdido " + data.effectDuration + "s";
                    break;

                case SlugAbilityType.EnergyDrain:
                    float robado = objetivo.TakeDamage(mag, data.element);
                    actor.Heal(robado * 0.5f);
                    ultimoResultado = "dreno " + robado;
                    break;
            }
            if (objetivo != null) actor.RegistrarImpacto();   // curarse o ponerse escudo no es un impacto
            if (f != null) f.Capture(data.elementColor);
            return true;
        }

        private SlugActor ObjetivoCercano()
        {
            System.Collections.Generic.IReadOnlyList<SlugActor> todos = SlugActor.Vivos;
            SlugActor mejor = null;
            float mejorSq = radioEfecto * radioEfecto;
            for (int i = 0, n = todos.Count; i < n; i++)
            {
                SlugActor a = todos[i];
                if (a == null || a.IsPlayer || !a.Alive || a.Capturada) continue;
                Vector3 d = a.transform.position - transform.position;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < mejorSq) { mejorSq = sq; mejor = a; }
            }
            return mejor;
        }
    }
}
