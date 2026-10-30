# 🗺️ Roadmap y guía de trabajo

Documento para seguir mejorando **Lanzador de Cavernas 2** sin volver a descubrir
todo de cero. El juego es un solo `index.html` vanilla (IIFE, sin frameworks),
así que *dónde está cada cosa* es la mitad del trabajo.

---

## Cómo se trabaja

**TDD estricto.** Ninguna línea de producción sin un test que falle primero por el
motivo correcto.

```bash
# 1. RED: el test falla por la razón esperada
node test-lo-nuevo.cjs

# 2. GREEN: el cambio mínimo en index.html

# 3. Regresión: las 16 suites
for f in test-*.cjs; do node $f >/dev/null 2>&1 || echo "FALLA: $f"; done

# 4. Sintaxis
node -e "const fs=require('fs');const m=fs.readFileSync('index.html','utf8').match(/<script>([\s\S]*)<\/script>/);fs.writeFileSync('_chk.js',m[1])" && node --check _chk.js && rm _chk.js

# 5. Push y esperar a que Pages reconstruya (~1-2 min)
git push origin main
```

### Las tres formas de probar

| Herramienta | Para qué | Cuándo |
|---|---|---|
| `node test-*.cjs` | Lógica y flujo, contra el juego real | Siempre. Rápido. |
| `recordDraw` en `harness.cjs` | Verificar **qué se dibuja** y de qué color | Cuando dos entidades comparten tono |
| `test-*-browser.py` (Edge) | End-to-end real: jugar, ganar, hacer clic | Al final, antes de commitear |

`harness.cjs` corre el `index.html` real en Node con un canvas y un DOM
*stub*. Por eso los tests de lógica son de verdad: ejecutan el código de
producción, no una copia. El stub registra los listeners de clic, así que un
test de Node puede recorrer `victoria → historia → siguiente nivel` completo.

---

## Mapa de `index.html` (~2960 líneas)

### Datos y constantes
| Línea | Qué |
|---|---|
| 121 | `NIVELES` — los **28** niveles de campana (nombre, nº de rivales, elementos, cofre; los del 25 al 28 llevan `anilloInvertido`) |
| 121 | `FREQS` — tono de cada elemento |
| 153 | `HISTORIA` — los **28** fragmentos (1-12 el viaje, 13-20 las consecuencias, 21-28 la fuente) |
| ~95 | `RING` (qué elemento vence a cuál) · **`anilloAlReves`, `ventaja`, `avisoAnillo`** (del 25 al 28 el anillo se invierte) |
| ~140 | `ELEMENT_INFO`, `ELKEYS` |

### Guardado y progresión
197 `loadSave` · **209 `baseSave` (incluye `equipo`, las 3 del combate)** · **216
`normalizeSave` (sanea el equipo: sin repetidas, sin las que no tenés, tope 3)** ·
229 `actualizarRacha` · 268 `checkLogros` · 292 `generarDesafios` · 306
`actualizarDesafios`

### Colección y equipo
667 `mejorRareza` · **674 `equipoActual` (las 3 elegidas, no la mochila)** ·
**688 `toggleEquipo` (la única puerta: marca/saca, y se niega a dejar 0 o pasar de
3)** · 706 `totalCofres`

### Enemigos y dificultad
650 `crearEnemigo` · 668 `statsNivel` (**el balance: HP y daño por nivel**) ·
676 `arquetipoDe` (qué arquetipo le toca a cada rival) · 682 `enemigoNivel` ·
695 `arquetipoInfinito` · 701 `enemigoInfinito` · 711 `nuevoEstado`

### Combate del jugador
805 `applyHit` (el daño) · 902 `lanzarUlti` · 960 `fire` (el candado de tiro) ·
984 `lanzarEscupitajo`

### Combate del rival
993 `curarEnemigo` · **1006 `lanzarHabilidad` (las 8 habilidades elementales)** ·
1060 `siguienteEnemigo` · **1075 `update` — la IA completa** ·
**1136-1141 la esquiva del rival** (radio `70 + evasion*200`, determinista) · 1251 el aturdimiento · 1264-1273 movimiento y límites

### Historia
670 `histVistas` · **703 `mostrarHistoria` (el fragmento entre combates)** ·
**733 `renderHistoria` (la ruta: los 20 en orden, solo lo vivido)**

### Obstáculos
874 `danoDeRoca` · 882 `intervaloRoca` · 890 `spawnRoca` · 1827 `drawRocas`

### Dibujo de entidades
1421 `drawEnemy` (arquetipos + aviso de habilidad) · 1534 `drawZonas` (charco) ·
1559 `drawEstadoJugador` (congelado/aturdido/ardiendo/marea) · 1610 `drawProjectile` ·
1621 `drawEnemyShot` · 1661 `drawLauncher` · 1696 `drawPowerMeter` ·
1720 `drawAimPreview` · 1750 `drawSelector` · 1827 `drawRocas`

### Estado, loop y pantallas
1872 `draw` · 1954 `loop` · 2010 `programarFin` · 2054 `iniciar` ·
**2067 `ganarNivel` (donde se engancha la mini historia)** · 2114 `endGame` ·
2197 `renderMenu` (botones **Equipo** y **Mejoras**) · 2226 `renderNiveles` ·
2237 `renderCofres` · **2425 `renderEquipo` (el selector de babosas)** · **2571
`renderMejoras` (la pantalla de comprar)** · 2381 `renderAyuda`

### Mejoras (las gemas como progreso)
`MEJORA_MAX` (5) · `costoMejora(clave, nivel)` (base 150 habilidades / 60 arma y
babosa, paso 110 / 45) · `armaDano(n)` (1+0.06n) · `armaCd(n)` (1−0.04n) ·
`babosaDano(elKey)` · `escudoDur` / `rapidDur` / `usosEscudo` / `usosRapid` (usos
por combate = nivel de la mejora) · `comprarMejora` (única puerta: valida, cobra y
sube) · **`danoAlJugador(n)`** — el único camino por donde entra el daño al
jugador (rocas y escupitajos van por acá); con el escudo activo gasta 250ms,
saca partícula 'BLOQUEADO' y **devuelve 0** · `usarEscudo` / `usarRapid` · los
timers `escudoLeft` / `rapidLeft` bajan en `update`; `cooldownFor` multiplica por
`armaCd()` y por 0.5 mientras `rapidLeft > 0`; `applyHit` multiplica por
`armaDano()` y por `babosaDano(elKey)`. Teclas `E` y `F`, HUD 🛡️/⚡ con usos y
segundos restantes. `save.mejoras` sobrevive el guardado; `normalizeSave` lo sanea
al tope y los saves viejos arrancan en 0.

### Modo infinito (la cueva sin fondo)
`anilloAlReves(L)` — en la campana lo dice el nivel; en infinito, del 25 en
adelante, se invierte por ciclos de 8 (`CICLO_ANILLO_DESDE=25`,
`CICLO_ANILLO_LARGO=8`). `PRESENTA` / `PRESENTA_TXT` (ola 3 escudado, 5
frenético, 7 saltarín) y `avisoArquetipoInfinito(wave)` · `avisoInfinito(wave)`
— **un** banner con prioridad: presentación de arquetipo > ciclo invertido. Se
avisa al entrar en el ciclo, no en cada ola. `arquetipoInfinito(wave)` (después
de la 7 mezcla por módulos) · `enemigoInfinito(wave)` (ráfaga `min(4,
1+⌊wave/10⌋)`, piso de intervalo 1100ms). `HISTORIA_INF` (8 fragmentos) ·
`OLAS_HISTORIA` derivado de `HIST_INF_PRIMERA=3` / `HIST_INF_DUR=7` ·
`fragmentoInfinito(ola)` **da vueltas** (`i % 8`) · `esOlaHistoria(ola)` ·
`mostrarHistoriaInfinito` / `renderHistoriaInfinito` (menú: "La cueva sin fondo")
· `histInfVistas()` (deriva de `save.mejorOla`, no guarda nada aparte) ·
`gemasPorOla(ola)` (`6+⌊ola×1.5⌋`, +15 en jefes) · `siguienteMejora(m)` y
`resumenMejoras(m)` para el cierre del lazo con las mejoras. La historia entra en
`siguienteEnemigo()` al **avanzar** y se puede saltar; logros `ola_40` y `ola_60`.

---

## Recetas

**Añadir un nivel de campana** → `NIVELES` (121) + un fragmento en `HISTORIA` (153). `test-niveles.cjs` exige que haya **exactamente uno por nivel** y que ninguno se repite.
**Añadir un nivel con el anillo al revés** → `anilloInvertido:true` en el nivel; `ventaja()` ya lo respeta y el selector lo anuncia solo.
El test `test-historia.cjs` falla si los dos no tienen el mismo largo: eso es a
propósito, así el arco no se queda atrás del contenido.

**Ajustar la dificultad** → `statsNivel` (668) para HP/daño, `intervaloRoca` (882)
para las rocas, `HAB_CD_BASE` para la frecuencia de las habilidades del rival.

**Añadir un arquetipo** → constantes + `arquetipoDe` (676) + `arquetipoInfinito` (695)
+ su ciclo en `update` (1075) + su dibujo en `drawEnemy` (1421). Regla: cada
arquetipo obliga a una respuesta distinta y **avisa** antes de actuar.

**Añadir una habilidad elemental** → constantes `HAB_*` + un caso en
`lanzarHabilidad` (1006) + su estado en el jugador + su dibujo en
`drawEstadoJugador` (1559).

---

## Pendientes

### 1. La esquiva del rival es por probabilidad ✅ HECHO

`index.html:1136-1137` (ya no existe)

```js
// ANTES
if(amenaza && en.dodgeCd<=0 && ... && Math.random()<en.evasion*dt/80){
```

El proyecto se sostiene sobre un principio: **todo avisa, se lee, no se adivina**.
Las rocas tiemblan antes de caer, el escudado cierra un anillo, el saltarín se
agacha. Pero el *disparo* de la esquiva era un dado: el jugador no podía prepararse
para algo que a veces pasa y a veces no, así que la única respuesta posible era
disparar más veces y rezar.

**Cómo se arregló:** la evasión dejó de ser probabilidad y pasó a ser **a qué
distancia ve venir el tiro**:

```js
const radioDeteccion = 70 + Math.max(0, en.evasion)*200;
if(amenaza && en.evasion>0 && dyAmenaza < radioDeteccion && ...)
```

Con la misma amenaza y el mismo estado, el rival reacciona **siempre igual**. Más
evasión = más radio = el tell aparece más temprano y cuesta más cortarlo, y la
progresión por nivel se mantiene. La fórmula está calzada para que a potencia
máxima el tiro llegue en `194 ms` contra los `200 ms` del tell: **interrumpe en
todos los niveles**, incluso contra el jefe. El counterplay de potencia no se tocó.

**Tests:** `test-esquiva-determinista.cjs` (60 de 60 reaccionan, antes 5 de 60) y
`test-esquiva-browser.py` (el tell se dibuja y la esquiva completa de verdad).
`test-esquiva-balance.cjs` se reescribió porque extraía el `dy<160` que se quitó.

### 2. Al combate entraban TODAS las babosas de la mochila ✅ HECHO

`equipoActual()` era `ELKEYS.filter(el=>mejorRareza(el)>=0)`: devolvía las 8 en
cuanto las desbloqueabas. El selector de abajo las mostraba todas y "cambiar de
babosa" era recorrer un inventario — **no había ninguna elección que hacer**.

**Cómo se arregló:** el equipo pasó al save (`save.equipo`, un array de a lo sumo
`EQUIPO_MAX = 3` claves de elemento) y `equipoActual()` devuelve *esas*, no la
mochila. `normalizeSave` las sanea al cargar: descarta las que ya no tenés, las
repetidas y las que sobren del tope, y si el save quedó vacío o corrupto cae a las
primeras 3 de la mochila — **nunca en 0**, porque el combate necesita al menos una.

`toggleEquipo(el)` es la única puerta de entrada y se niega a los tres casos que
romperían la regla: una babosa que no tenés, una cuarta cuando ya hay 3, y sacar
la última.

La pantalla (`renderEquipo`, botón **Equipo** en el menú) muestra solo la mochila;
la que está en el equipo lleva el borde de su color y un tilde, y con el equipo
lleno las demás se apagan. El estado se lee de un vistazo, igual que el resto del
juego.

**Tests:** `test-equipo.cjs` (22 checks: el tope, el guardado, el saneado, el
combate arrancando con las 3 elegidas y las reglas de marcar) y
`test-equipo-browser.py` (Edge real: abrir el selector, marcar y desmarcar con el
mouse, el equipo guardado en `localStorage`, y el selector de combate pintando
exactamente los 3 nombres elegidos). Ambos se escribieron antes que el código.

### 3. Candidatos (ideas, no compromisos)

- ~~**El nivel 1 como tutorial.**~~ ✅ HECHO — el nivel 1 ya no tiene rocas ni
  habilidades del rival: ni escudado, ni saltarín, ni frenético, y el guardián
  tampoco esquiva (`esTutorial`). Queda solo el tiro y la potencia, que es lo que
  hay que aprender primero; el resto se presenta después, de a una. Además el
  nivel se avisa a sí mismo al entrar: `TUTORIAL_AVISO` = "NIVEL 1 · sin rocas:
  probá el tiro". **Tests:** `test-tutorial-nivel-1.cjs` (el nivel 1 sin
  arquetipos, sin esquiva y sin ninguna roca en 14.4s; el nivel 2 sigue con las
  tres cosas) y `test-arquetipos-asignacion.cjs`, que ahora exige que el jefe
  frenético empiece en el 2 y no en el 1.
- **Un epílogo tras el nivel 12.** El arco cierra con "La verdad", pero el juego
  sigue. Un último fragmento que diga qué pasó después daría cierre real.
- ~~**El modo infinito tiene menos pulido que la campana.**~~ ✅ HECHO — ahora es
  un modo propio: arquetipos presentados de a uno con aviso, 8 fragmentos de
  historia que dan vueltas, el anillo invertido por ciclos del 25 en adelante, y
  el lazo con las mejoras (gemas crecientes + resumen final). Ver
  `test-infinito.cjs` (46 checks) y `test-infinito-browser.py`.

---

## Reglas de diseño (no negociables)

1. **Todo avisa.** Cualquier cosa que te pueda matar o bloquear se anuncia antes
   con un tell visible. Si no se puede leer, no entra.
2. **El jugador no se mueve.** `LX=180` es fijo. Las mecánicas se responden con
   *cuándo tirar*, *con qué elemento* y *cuánto cargar* — no esquivando.
3. **Un solo archivo.** `index.html` es el juego. Sin build, sin dependencias, sin
   exponer `gameState` ni `state` al exterior.
4. **Verificación honesta.** Si no se puede medir, no se afirma. Contar píxeles
   miente cuando dos entidades comparten tono: usar `recordDraw`.
