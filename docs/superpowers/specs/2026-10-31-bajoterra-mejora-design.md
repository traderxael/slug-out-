# Diseño — Mejora de SLUG OUT (bajoterra): ecos de la cueva, evolución, enemigos, jefe, captura y meta-progreso

**Proyecto:** `C:\Users\USUARIO\SLUG OUT` · Unity 6000.6.5f1 · URP  
**Enfoque:** B (slices independientes, medidos uno tras otro con `Tools/ArneltCampanaMedida.cs`)  
**Estado:** pendiente de aprobación antes de escribir producto.

---

## 1. Intent y por qué

El bucle actual es lineal: entras al nivel, matas/enemigas, capturas, sales. No hay decisión del jugador **dentro del nivel**, ni colección con recorrido visible, ni jefe con fases, ni sensación de "momento" al capturar, ni nada que hacer fuera de la partida. Se añaden 6 sistemas que dan cuerpo al bucle sin tocar arte, cámara ni UI salvo lo estrictamente necesario para cada slice.

---

## 2. Alcance — 6 slices en orden (Enfoque B)

| Slice | Qué | Motor que añade |
|-------|-----|-----------------|
| S1 | **Ecos de la cueva** | Menú de mejoras que elegís dentro del nivel (estilo Vampire Survivors). |
| S2 | **Evolución protoform → velocimorph por XP de uso** | Colección con recorrido visible; ambos modelos ya están en `SlugData`. |
| S3 | **Enemigos nuevos** | Volador (solo 100 MPH), tejedor/soporte, excavador, élites con modificadores. |
| S4 | **Dr. Blakk a 3 fases** | Fase 2 reusa la bandera del anillo invertido del nivel 25. |
| S5 | **Momento de captura** | Hitstop + cámara lenta + chime, VFX por elemento, jingle al transformar el tiro. |
| S6 | **Afuera de la partida** | Escondite de mejoras permanentes, logros, estrellas por nivel, desafío diario por semilla. |

**Regla de entrega:** cada slice se implementa, se mide con el arnés y se commitea solo lo tocado (árbol sucio con ~800 cambios de otra sesión se deja intacto).

---

## 3. Metas / no-metas / anti-metas

**Metas**
- S1 ofrece 3 mejoras dentro del nivel y se siente al jugar.
- S2 cada babosa usada sube de nivel y cambia protoform → velocimorph con barra visible.
- S3 volador solo recibe daño con tiro ≥100 MPH (fase ≥1); tejedor invoca/soporta; excavador se entierra; élites traen modificador.
- S4 Dr. Blakk tiene 3 fases distintas; fase 2 = anillo invertido (reusa `AnilloInvertido` del nivel 25).
- S5 capturar se siente: hitstop, timeScale lento, chime, partículas del elemento, jingle al transformar tiro.
- S6 menú fuera de partida con mejoras permanentes, logros desbloqueables, 0-3 estrellas por nivel, desafío diario por semilla.

**No-metas (esta entrega)**
- No reescribir el balance de campaña (vida 120+10/nivel, cargador, daño enemigo) — ya en Task 3/4.
- No cambiar arte, cámara ni UI salvo HUD de eco, barra de evolución y HUD de captura.
- No multijugador ni online.

**Anti-metas (fracaso aunque funcione)**
- Romper los 28 niveles ganables (criterio del plan base).
- Dejar tests en rojo.
- Commitar archivos que no toqué.

---

## 4. Restricciones

- Commitear **solo** los archivos que toco; conservar `.meta`; no mover/renombrar assets.
- No tocar arte, cámara ni UI sin medición que lo justifique.
- Todo se mide con el arnés real en el editor vivo (pipeline HTTP del editor).
- Los tests EditMode/PlayMode existentes deben seguir verdes (`ProgresionCampanaTests`, `CampanaTests`, `BalanceTacticoTests`, `BalanceTacticoPlayTests`, `CurvaDeCargaYBarridoTests`).
- El árbol sucio (~800 cambios de otra sesión) no se toca ni se commitea.

---

## 5. Diseño por slice

### 5.1 S1 — Ecos de la cueva (mejoras dentro del nivel)

**Datos** — nuevo archivo `Assets/Scripts/World/EcosCueva/EcoDef.cs`:
```csharp
[System.Serializable]
public class EcoDef {
    public string id;
    public string nombre;
    public string descripcion;
    public EcoTipo tipo;
    public float valor;      // bonus aplicado por nivel de eco
    public int nivelMax = 5;
    public Sprite icono;
}
public enum EcoTipo { Danio, Cadencia, Cargador, VelocidadDisparo, VidaMax, CapturaUmbral, Energia }
```
**Catálogo de ecos** — `Assets/Scripts/World/EcosCueva/EcoCatalogo.cs` (`ScriptableObject` o clase estática): ~12 ecos definidos (daño +15%, recarga -12%, +1 tubo, velocidad +10%, vida +20, umbral captura 0.35→0.30, energía +25).

**Manager** — `Assets/Scripts/World/EcosCueva/EcosCuevaManager.cs` (MonoBehaviour, `DontDestroyOnLoad`):
- `bool Activo { get; }` — menú abierto (pausa el juego).
- `void Ofrecer()` — elige 3 ecos distintos ponderados del catálogo, expone al UI, pone `Time.timeScale = 0`.
- `void Aplicar(string ecoId)` — aplica el bonus al jugador para el resto del nivel, cierra menú, `Time.timeScale = 1`.
- Estado por nivel: `Dictionary<string,int> nivelEco` (reinicia al iniciar nivel).

**Integración en `SlugArenaGame`:**
- En `Update`, cuando se cumple un disparador (cada captura O cada 20 s, el primero que llegue) y `!Termino` y `!EcosCuevaManager.Activo`, llamar `EcosCuevaManager.Instancia.Ofrecer()`.
- Al iniciar nivel (`Iniciar`), reiniciar niveles de eco y cronómetro de eco.

**UI** — prefab mínimo `Assets/Prefabs/UI/EcosCuevaMenu.prefab` + `EcosCuevaUI.cs`: 3 botones (nombre + descripción + icono), fondo semitransparente. Si no hay prefab, fallback a texto en `GameHUD`.

**Criterio de aceptación S1:** al jugar un nivel, tras la primera captura aparece el menú de 3 mejoras; elegir una aplica el efecto (p. ej. daño sube) y el juego reanuda; el menú no bloquea el fin de nivel.

---

### 5.2 S2 — Evolución protoform → velocimorph por XP de uso

**Datos de progreso por babosa** — `Assets/Scripts/World/Evolucion/SlugProgreso.cs`:
```csharp
[System.Serializable]
public class SlugProgresoData { public string slugId; public int xp; public int nivel; public bool evoluido; }
[System.Serializable]
public class SlugProgresoSave { public List<SlugProgresoData> slugs; }
```
Persistido en `Application.persistentDataPath / slugout_progreso.json` (ademés de PlayerPrefs para compatibilidad).

**Motor** — `Assets/Scripts/World/Evolucion/SlugEvolucion.cs` (estático):
- `void GanarXP(string slugId, int xp)` — acumula, sube nivel, umbrales 100/250/500/1000/2000.
- `bool EstaEvoluido(string slugId)` — nivel >= 3 (protoform → velocimorph).
- `float BonusDanio(string slugId)`, `float BonusVelocidad(string slugId)` por nivel.
- `string IconoSlug(string slugId)` — devuelve `velocimorphSprite` si está evoluido, si no `protoformSprite`.

**Integración:**
- En `PlayerShooter.SetData(SlugData d)` y al disparar: registrar `SlugEvolucion.GanarXP(data.slugId, xpPorDisparo)` (xp bajo, p. ej. 2 por disparo, 10 por captura).
- El daño del tiro y velocidad usan `BonusDanio` / `BonusVelocidad`.
- `SlugActor` / `PlayerShooter` usa `IconoSlug` para el sprite del tiro/babosa (recorrido visible).
- UI de barra de evolución en HUD (número xp / umbral, opcional).

**Criterio de aceptación S2:** usar una babosa varias veces sube su XP; al llegar al umbral 3 cambia a velocimorph (sprite y daño/velocidad mejoran); persiste entre partidas.

---

### 5.3 S3 — Enemigos nuevos

**Base** — todos heredan el flujo existente: `SlugActor` (isPlayer=false) + `EnemySlugAI` con `Arquetipo`. Se añaden al roster y a `EnemySlugAI.ArquetipoDe`.

**Nuevos arquetipos** en `EnemySlugAI.Arquetipo`: `Volador`, `Tejedor`, `Excavador`. Se derivan del `SlugData` (o de una marca en el SlugData): añadir campo `bool esVolador / esTejedor / esExcavador` a `SlugData` o detectar por nombre/id del roster.

**Volador** (`EnemySlugAI.Arquetipo.Volador`):
- Vuela sobre obstáculos (ignorar capa de rocas/muros).
- **Solo se daña con tiro a 100 MPH:** en `SlugShot.Impactar` / `TakeDamage`, si el atacante es el jugador y `fase < 1` (tiro corto, <100 MPH) → daño 0 y el volador parpadea (invulnerable). Fase ≥1 (velocimorph/megamorph) daña normalmente.
- Esto hace que el skill shot (cargar a 100 MPH) deje de ser opcional contra voladores.

**Tejedor / soporte** (`Arquetipo.Tejedor`):
- No ataca al jugador directamente; cada N segundos invoca un "sífilo" pequeño (enemigo de 1 HP, rápido) o aplica un modificador de velocidad/dano a enemigos cercanos (`MultiplicarHp`, `SetDamage`, `SetSpeed` a aliados en radio).
- Prioridad de objetivo del jugador: eliminar al tejedor primero.

**Excavador** (`Arquetipo.Excavador`):
- Entierra: `EstaEnterrado = true`, no recibe daño, no se ve (o se ve semi-transparente), no avanza.
- Cada N segundos sale, ataca (mordida/embestida telegrafiada), y vuelve a enterrarse.
- Mientras está enterrado es invulnerable; al surfacear es vulnerable.

**Élites con modificadores** — `Assets/Scripts/World/Enemigos/EliteModificador.cs`:
```csharp
[System.Serializable]
public class EliteModificador {
    public string id; public string nombre;
    public float hpMultiplicador = 1f;
    public float danoMultiplicador = 1f;
    public float velocidadMultiplicador = 1f;
    public bool reflejaDanio = false;
    public bool regeneracion = false;
}
```
- `SlugArenaGame.SpawnEnemy`: con probabilidad baja (crece con nivel/oleada), el enemigo nace "élite": elegir 1-2 modificadores aleatorios, aplicarlos al `SlugActor` y `EnemySlugAI`, marcar con badge visual (anillo de color o etiqueta en HUD).
- Modificadores: "Vigor" (+HP), "Furia" (+daño), "Prisa" (+velocidad), "Espejo" (refleja daño al jugador), "Sangre" (regenera).

**Roster:** añadir SlugData de volador, tejedor, excavador en `Assets/Resources/Slugs/` o `SlugRoster`; asignar `Arquetipo` correspondiente.

**Criterio de aceptación S3:** volador ignora tiros <100 MPH y solo cae con skill shot; tejedor invoca aliado o buffea; excavador es invulnerable enterrado; élites aparecen con modificador visible y más difícil.

---

### 5.4 S4 — Dr. Blakk a 3 fases

**Definición** — `DuelistaMazo.Jefe = "dr-blakk"` ya existe. Se añade `Assets/Scripts/World/Jefe/DrBlakkFases.cs`:
```csharp
public enum FaseBlakk { Uno, Dos, Tres }
public class DrBlakkFases {
    public FaseBlakk FaseActual;
    public float HpBase;
    public bool AnilloInvertidoActivo; // fase 2
}
```

**Fases:**
- **Fase 1:** Dr. Blakk normal (HP base, ataques estándar, sin anillo invertido).
- **Fase 2 (anillo invertido):** al bajar a ~60% HP, activar `SlugActor.AnilloInvertido = true` (reusa la bandera del nivel 25). Los multiplicadores elementales se invierten (`ElementalChart.GetMultiplier(..., true)`). HP se rellena parcialmente. Patrón de ataque más agresivo.
- **Fase 3 (ira):** al bajar a ~30% HP, `AnilloInvertido = false`, velocidad y daño ×1.5, nuevo patrón (ráfaga de disparos), HP relleno pequeño. Última fase: al morir, victoria.

**Integración en `SlugArenaGame`:**
- Cuando `DuelistaMazo.Elegir` devuelve `dr-blakk` (último enemigo de campaña o cada 25 oleadas en infinito), instanciar con `DrBlakkFases`.
- `EnemySlugAI` para Dr. Blakk maneja transiciones de fase en `Update`: chequear `Hp/MaxHp`, disparar eventos de fase, aplicar modificadores.
- UI: mostrar "FASE 2 — ANILLO AL REVÉS" y "FASE 3 — IRA" en HUD/StoryPanel.

**Criterio de aceptación S4:** Dr. Blakk tiene 3 fases; fase 2 inverte el anillo (lo que vencía pierde); al matar la fase 3 se gana el nivel.

---

### 5.5 S5 — Momento de captura

**Hitstop + cámara lenta** — ya existe `GameFeel.Hitstop(duracion)` y `Trauma`. Se refuerza:
- Al capturar (`SlugActor.Capturar()` / `SlugShot.Impactar` cuando `ListaParaCapturar`): `GameFeel.Hitstop(0.12f)` + `GameFeel.Trauma(1.2f)` + `Time.timeScale = 0.35f` por 0.4 s (cámara lenta), luego volver a 1.

**Chime y VFX por elemento** — `SlugFx.Capture(elemento)` ya existe (arpegio + partículas del color elemental). Se añade un chime distinto por elemento (otro arpegio o tono) y burst de partículas más grande al capturar.

**Jingle al transformar el tiro** — en `PlayerShooter.Disparar`, al determinar `fase = FaseDe(...)`:
- fase 0 → tiro corto, sin jingle.
- fase 1 (velocimorph ≥100 MPH) → jingle de "transformación velocimorph" (tono ascendente).
- fase 2 (megamorph) → jingle de "megamorph" (tono más brillante + efecto elemental garantizado).
- Usar `SlugFx.Shot()` o un nuevo `SlugFx.Transformacion(fase)`.

**Integración:** `SlugActor.Capturar()` llama a los efectos; `PlayerShooter.Disparar()` llama al jingle de transformación.

**Criterio de aceptación S5:** capturar se siente con hitstop + cámara lenta + chime y partículas del elemento; transformar tiro (cargar a 100 MPH) suena distinto.

---

### 5.6 S6 — Afuera de la partida

**Escondite (hideout) de mejoras permanentes** — `Assets/Scripts/World/Meta/Escondite.cs` (menú fuera de partida):
- Moneda: `cristales` (ganados por capturar y completar niveles), guardado en PlayerPrefs `slugout.cristales`.
- Mejoras permanentes (comprables una vez o por nivel): daño base +10%, vida base +20, recarga base -5%, capture umbral -0.05, etc. Aplicadas al iniciar nivel (`SlugArenaGame.Iniciar` lee `Escondite` y ajusta `FijarVidaMaxima`, `SetRecargaPorTubo`, `SetPenalizacionDisparoPesado`, daño base de `PlayerShooter`).

**Logros** — `Assets/Scripts/World/Meta/Logros.cs`:
- Lista de logros con condición (capturar 10 babosas, ganar nivel 20, matar un volador solo con 100 MPH, completar Dr. Blakk fase 3, etc.).
- Estado desbloqueado + fecha, guardado en PlayerPrefs `slugout.logros`.
- Notificación al desbloquear (HUD/toast).

**Estrellas por nivel** — `Assets/Scripts/World/Meta/Estrellas.cs`:
- Al terminar nivel: 1 estrella por ganar, +1 si se capturan ≥ meta, +1 si se termina con vida > umbral o tiempo < par.
- Guardado por nivel en PlayerPrefs `slugout.estrellas_n{nivel}` (0-3).
- Mostrado en mapa de campaña (`CampanaMapa`).

**Desafío diario por semilla** — `Assets/Scripts/World/Meta/DesafioDiario.cs`:
- Semilla del día: `DateTime.UtcNow.Date` → hash → seed int.
- Genera un nivel especial (enemigos, objetivo, límite) determinista desde esa seed.
- Tabla de clasificación local (mejor tiempo/puntos) guardada en PlayerPrefs `slugout.diario_<fecha>`.
- Se resetea cada día UTC.

**Integración:** menú principal / mapa de campaña añade botones "Escondite", "Logros", "Diario". `GameFlow` expone los datos de PlayerPrefs.

**Criterio de aceptación S6:** menú fuera de partida con escondite (comprar mejora permanente), logros desbloqueables, estrellas 0-3 por nivel visibles en mapa, desafío diario con seed del día y tabla local.

---

## 6. Datos y persistencia

- Progreso de evolución: `slugout_progreso.json` en `persistentDataPath` (SlugProgreso).
- Escondite/logros/estrellas/diario: PlayerPrefs existentes (`slugout.*`).
- Todo se carga al iniciar menú y se guarda al cambiar.

---

## 7. Qué se deja al builder

- Prefabs de UI de ecos (si no se usa GameHUD fallback).
- Sonidos/chimes concretos (se puede reutilizar `SlugFx` audio procedural).
- Badge visual de élite y fases de Dr. Blakk (color/anillo, se puede reusar `DuelistaMarca`).
- Los campos `esVolador`/`esTejedor`/`esExcavador` en `SlugData` o detección por id de roster.

---

## 8. Criterios de aceptación globales (para aprobar la entrega completa)

1. Los 28 niveles siguen ganables (arnés gana 1..28).
2. Tests EditMode/PlayMode en verde (los 4 fallos preexistentes no son de este cambio).
3. Cada slice medido con `ArneltCampanaMedida.cs` antes y después; no hay regresión de balance.
4. Commit solo de archivos tocados; `.meta` conservados; árbol sucio de otra sesión intacto.

---

**Próximo paso tras aprobación:** implementar **S1 (Ecos de la cueva)** como primer slice, medir, commitear, y continuar con S2..S6 en orden.

¿Apruebas este diseño para comenzar a construir por slices? Si hay algo que quieras cambiar (orden, alcance de un slice, campos de SlugData), dímelo ahora y lo ajusto antes de tocar código.