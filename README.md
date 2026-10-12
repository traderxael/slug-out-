# 🐌 Lanzador de Cavernas 2

Juego de disparos parabólicos con babosas elementales. 8 elementos, 40 babosas coleccionables, 5 rarezas, cofres, habilidades definitivas, **potencia de tiro por arrastre** y enemigos que esquivan.

## 🎮 Cómo jugar

Abrí `index.html` en tu navegador (doble click) o jugá online con GitHub Pages.

- **Arrastrá** para apuntar y **soltá** para disparar
- **Cuanto más lejos arrastres, más potente es el disparo**: más daño y el efecto
  elemental **garantizado** a carga máxima (marca `MÁX`) — pero la recarga se alarga
- **Tocá** una babosa abajo para cambiar de elemento
- **Tocá la barra de ULTI** cuando esté cargada para usar la habilidad definitiva
- **Interceptá** los escupitajos del rival con tus disparos

### Teclado (PC)

| Tecla | Acción |
|---|---|
| `←` `→` | Apuntar |
| `↑` `↓` | Subir / bajar la potencia |
| `Espacio` | Disparar |
| `1` – `8` | Cambiar de elemento |
| `U` | ULTI |
| `P` / `Esc` | Pausa |
| `M` | Silenciar |
| `R` | Reiniciar partida |

## ⚡ Potencia de tiro: la decisión central

El arrastre controla la potencia, y la potencia tiene dos caras:

| Potencia | Daño | Recarga | DPS | Efecto elemental |
|---|---|---|---|---|
| 0.55 (mínima) | 13 | 476 ms | 27.3 | 35-40% de probabilidad |
| 1.00 (normal) | 16 | 620 ms | 25.8 | 35-40% de probabilidad |
| 1.60 (máxima) | 25 | 812 ms | 30.8 | **garantizado** y +50% más fuerte |

Espamear tiros flojos te da más cadencia y más chances de interceptar; comprometerte
a un tiro pesado rinde más daño por impacto y asegura aturdir / congelar / encadenar,
pero si el rival **esquiva** perdés 812 ms. El combo caduca a los 5 segundos sin acertar,
así que mantenerlo también empuja a no quedarse cargando.

## 🎯 Esquiva telegrafiada: azar convertido en lectura

El rival **no se teletransporta al esquivar**. Antes de moverse se agacha `200 ms` y
**avisa con un anillo naranja** que se va cerrando, acompañado de un pitido. Durante
esa ventana está quieto y vulnerable.

| Qué hacés | Qué pasa |
|---|---|
| Le pegás **durante el anillo** | La esquiva se **cancela**: el tiro pega, el rival queda **aturdido 420 ms**, y ganás `+15` puntos y `+5` de ULTI |
| Disparás flojo y **provocás** el anillo | El rival gasta su esquiva; cuando el anillo cierra ya se movió |
| Esperás a que cierre y disparás después | Perdés la ventana: se movió y el tiro falla |

Por eso la potencia importa también contra la evasión. El rival detecta la amenaza a
`160 px`, y el proyectil tarda en cubrir esa distancia:

| Potencia | Llega en | Resultado |
|---|---|---|
| 1.60 (máxima) | ~182 ms | **Interrumpe** durante el viento, con el rival quieto |
| 1.20 | ~302 ms | Llega mientras el rival se mueve: corta la esquiva |
| 0.90 | ~480 ms | Tarde: el anillo ya cerró y se movió |
| 0.68 | ~662 ms | Tarde |

Más carga = más fiable contra la evasión. La potencia decide **cuánto gastás** y
**cuándo llegás**: dos decisiones con el mismo gatillo.

### El bug que había: el tell nunca aparecía

La primera versión detectaba la amenaza con los dos signos invertidos
(`if(p.vy<=0) continue` y `dy = en.y - p.y`): solo consideraba proyectiles que
**caían desde arriba**. Los tiros del jugador suben, así que el rival reaccionaba
a sus propios escupitajos y **nunca al jugador**. La mecánica compilaba, corría
sin errores, y el anillo no se dibujaba jamás en juego normal — un error de signo
deja una feature muerta sin avisar. Corregido a `if(p.vy>=0) continue` y
`dy = p.y - en.y`.

## 🪨 Rocas que caen: el escenario pasa a ser un problema de timing

El techo suelta rocas. Antes de caer se marcan con un triángulo que tiembla sobre
la columna, con la línea de aviso punteada: **el obstáculo se lee, no se adivina**.

| Qué hacés | Qué pasa |
|---|---|
| Disparás por una columna con roca | La roca **consume el tiro** y recibe el daño; a los pocos golpes se rompe |
| La roca cae por tu columna | Te **golpea** al cruzar tu línea (−12 de vida) |
| Leés el tell y cambiás de columna | El tiro llega limpio al rival |

El intervalo entre rocas baja con el nivel (`5200 − nivel*320`, mínimo `2200 ms`),
así que más adelante el escenario presiona más seguido. Una roca tarda ~1.5 s en
cruzar la pantalla y aguanta 40 de daño base.

Lo que agrega esto no es contenido: es una decisión más por tiro. Antes la única
pregunta era *cuánto cargo*; ahora es *cuánto cargo* y *por dónde*.

## ⚔️ Elementos

| Elemento | Habilidad | ULTI |
|---|---|---|
| 🔥 Fuego | Quema (daño por tiempo) | Lluvia de meteoritos |
| 💧 Agua | Ralentiza al rival | Ola sanadora |
| 🪨 Tierra | Puede aturdir | Terremoto brutal |
| 💨 Aire | Perfora resistencias | Triple disparo |
| ❄️ Hielo | Puede congelar | Congelación total |
| ⚡ Rayo | Rápido, golpea 2 veces | Tormenta eléctrica |
| 🌿 Planta | Te cura al impactar | Enredo 5s |
| 🌑 Sombra | Roba vida | Eclipse (daño x3) |

## 🏆 Sistemas

- **12 niveles** con Guardianes jefes
- **Modo infinito** con dificultad progresiva
- **5 cofres**: madera, hierro, dorado, cristal y mítico
- **25 logros** desbloqueables (incluye *Francotirador* por críticos y *A toda carga*
  por disparos a carga máxima)
- **3 desafíos diarios** con recompensa en gemas
- **Racha diaria** de días consecutivos jugando
- **Pausa** con reanudar / reiniciar / silenciar, y pausa automática al cambiar de pestaña

## 🛠️ Tech

Un solo archivo HTML + CSS + JS vanilla. Sin dependencias, sin build. Todo el arte es
procedural (Canvas 2D) y el audio es sintetizado (Web Audio API).

## ✅ Verificación

Suites Node que corren contra el `index.html` real, sin servidor. `harness.cjs`
extrae el `<script>`, le inyecta un hook de exportación dentro del IIFE y lo
evalúa en Node con el DOM y el audio stubeados: se prueba el **comportamiento**
del juego (estado, colisiones, daño), no una reimplementación.

```bash
# 1. Balance y tradeoffs: extrae las funciones de potencia DEL archivo y las ejecuta
node test-potencia.cjs

# 2. El tradeoff carga-vs-evasion, por CALCULO con las constantes reales del archivo
node test-esquiva-balance.cjs

# 3-6. Obstaculos (rocas), contra el juego real via harness.cjs
node test-obstaculos.cjs              # la roca bloquea el tiro y recibe el dano
node test-obstaculos-caida.cjs        # la roca cae, acelera y se deshace en el piso
node test-obstaculos-spawn.cjs        # aparecen solas y telegrafiadas antes de caer
node test-obstaculos-amenaza.cjs      # la roca golpea al jugador solo en su columna

# 7. Juego real en Edge headless: mide la fisica por pixels y ejercita los controles
"C:/Users/USUARIO/AppData/Local/Programs/Python/Python312/python.exe" qa-juego.py

# 8. La esquiva telegrafiada EN JUEGO: control sin evasion, el tell visible como anillo
"C:/Users/USUARIO/AppData/Local/Programs/Python/Python312/python.exe" test-esquiva.py

# 9. Las rocas EN JUEGO: que el tell y la roca se VEAN (delta contra la linea base)
"C:/Users/USUARIO/AppData/Local/Programs/Python/Python312/python.exe" test-obstaculos-visual.py

# 10. El juego YA PUBLICADO, cargado desde la URL de produccion
"C:/Users/USUARIO/AppData/Local/Programs/Python/Python312/python.exe" qa-live.py
```

Las suites 3-6 se escribieron **primero que el código** (TDD): cada una se vio
fallar por el motivo correcto antes de implementar. La 9 mide el **delta** contra
la línea base porque la cornisa y el fondo ya son marrones y suman píxeles aunque
no haya ninguna roca: medir absolutos habría dado un falso positivo.

La suite 2 mide, entre otras cosas, que un tiro a potencia baja se detiene en `y≈368`
y uno a potencia máxima en `y≈280` del canvas (predicción teórica: 375 y 287), que el
canvas se congela en pausa (0 px cambiados) y que el medidor de potencia aparece al
apuntar (0 → 1082 px brillantes) sin ensuciar la pantalla en reposo.

La suite 3 resuelve la cadena de tiempos que sostiene la esquiva con las constantes
sacadas del propio `index.html` (detección a `160 px`, viento `200 ms`, esquiva
`220 ms`): a potencia máxima el proyectil llega en `182 ms` e interrumpe el viento
con el rival quieto; a mínima llega en `662 ms`, cuando la esquiva ya completó. Si la
curva se invirtiera, el tradeoff sería al revés y la mecánica no significaría nada.

La suite 4 corre el juego de verdad: un **control en nivel 1** (evasión negativa,
el rival nunca esquiva) para confirmar que el apuntado y la medición funcionan, y
después verifica que el anillo del tell aparece como **anillo** (extensión
horizontal ≥ 40 px, no la estela vertical del proyectil).

**Qué esta suite NO puede medir, y por qué.** La tasa de acierto por potencia
queda invalidada por dos propiedades del propio juego: `powerFromDrag` satura en
`1.60` para cualquier objetivo lejano (las cuatro potencias producen el mismo
tiro), y el rival camina a `0.049 px/ms` en nivel 6, así que deriva `27–61 px`
mientras el proyectil viaja — justo por encima del margen de colisión (`±38.6 px`).
Un test que dispara a la posición leída hace rato mide **deriva**, no evasión. El
tradeoff queda cubierto por el cálculo de la suite 2, que usa las constantes
reales; la suite conductual cubre el tell.

La suite 5 corre lo mismo contra `https://traderxael.github.io/slug-out-/`: arranca el
nivel, dispara 3 pares de tiros con nivel reiniciado entre cada uno (así ningún
escupitajo del rival baja por la banda de medición) y comprueba que el cargado sube
más alto en las 3 repeticiones (21 / 28 / 27 px).
