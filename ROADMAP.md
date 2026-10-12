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

## Mapa de `index.html` (~2450 líneas)

### Datos y constantes
| Línea | Qué |
|---|---|
| 107 | `NIVELES` — los 12 niveles de campana (nombre, nº de rivales, elementos) |
| 121 | `FREQS` — tono de cada elemento |
| 129 | `HISTORIA` — los 12 fragmentos de la mini historia |
| ~140 | `ELEMENT_INFO`, `ELKEYS`, `RING` (qué elemento vence a cuál) |

### Guardado y progresión
197 `loadSave` · 209 `normalizeSave` · 229 `actualizarRacha` · 268 `checkLogros` ·
292 `generarDesafios` · 306 `actualizarDesafios`

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
**1136-1141 la esquiva del rival** · 1251 el aturdimiento · 1264-1273 movimiento y límites

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
2197 `renderMenu` · 2226 `renderNiveles` · 2237 `renderCofres` · 2381 `renderAyuda`

---

## Recetas

**Añadir un nivel de campana** → `NIVELES` (107) + un fragmento en `HISTORIA` (129).
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

### 1. La esquiva del rival es por probabilidad 🔴

`index.html:1136-1137`

```js
if(amenaza && en.dodgeCd<=0 && ... && Math.random()<en.evasion*dt/80){
```

El proyecto se sostiene sobre un principio: **todo avisa, se lee, no se adivina**.
Las rocas tiemblan antes de caer, el escudado cierra un anillo, el saltarín se
agacha. Pero el *disparo* de la esquiva es un dado: el jugador no puede prepararse
para algo que a veces pasa y a veces no, así que la única respuesta posible es
disparar más veces y rezar.

**El arreglo:** esquivar siempre que un proyectil sea amenaza real (dentro de un
cono de llegada, a menos de N píxeles), nunca por azar. El tell de 200ms ya
existe; lo que falta es que sea *determinista*. Así el jugador aprende una regla
legible: *"si tiro mientras el tell está puesto, gasto el tiro"*.

**Test primero (RED):** con el mismo proyectil y el mismo estado, el rival esquiva
siempre — o nunca. Ninguna de las dos depende de `Math.random()`.

### 2. Candidatos (ideas, no compromisos)

- **El nivel 1 como tutorial.** Es el primer contacto y ya tiene rocas y
  habilidades del rival desde el arranque. Podría ser el único nivel sin ninguna
  de las dos, para enseñar de a una.
- **Un epílogo tras el nivel 12.** El arco cierra con "La verdad", pero el juego
  sigue. Un último fragmento que diga qué pasó después daría cierre real.
- **El modo infinito tiene menos pulido que la campana.** No tiene historia ni
  progresión de arquetipos tan clara; se siente como un extra.

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
