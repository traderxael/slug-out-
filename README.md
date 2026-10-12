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

## 🛡️ Arquetipos de enemigo: variedad que obliga a responder distinto

No son más babosas del mismo molde: son tres comportamientos que fuerzan una
decisión distinta. Cada uno **avisa** antes de actuar.

| Arquetipo | Desde | Qué hace | Tu decisión |
|---|---|---|---|
| 🛡️ **Escudado** | nivel 4 (ola 6) | Levanta un escudo que **absorbe el tiro**. El anillo cian se cierra 480 ms antes de subir | Esperar a que baje, cambiar de elemento, o gastar el tiro a propósito |
| 🦗 **Saltarín** | nivel 7 (ola 10) | Se agacha 260 ms y **salta** ~54 px | Leer la altura antes de tirar |
| 😤 **Frenético** | todos los jefes | Al 50% de vida **se enfurece**: aura roja, +40% velocidad, ataca cada 0.65× el intervalo | Bajarlo de golpe o bancarse la andanada |

El escudo dura 900 ms y cicla cada 2.6 s; el salto cada 2.2 s. La furia es
irreversible — es la fase 2 del jefe.

Lo que agrega esto no es contenido: son tres decisiones más. Con las rocas, cada
tiro ahora se responde a *cuánto cargo*, *por dónde* y *cuándo* — y contra cada
enemigo, *qué*.

## 🎨 Cada villano usa la habilidad de su elemento

La simetría que faltaba: **tus babosas tienen habilidades elementales, y los
villanos también** — las usan contra vos. El anillo de carga se pinta del color
del elemento, así que leés **cuál** viene antes de que salga.

| Elemento | Habilidad del villano | Cómo se responde |
|---|---|---|
| 🔥 **Fuego** | Charco ardiente bajo tus pies que te quema (−5 cada 0.5s, 4s) | Matarlo rápido o bancarse el ardor |
| 💧 **Agua** | Marea que te alarga la recarga **1.6×** | Planear el próximo tiro sabiendo que llega tarde |
| 🪨 **Tierra** | Te **aturde** 0.9s — no disparás | No comprometer una carga completa justo antes |
| 🌪️ **Aire** | Abanico de **3 tiros perforantes** | Cambiar de elemento para cubrir |
| ❄️ **Hielo** | Te **congela** 1s — no disparás **y te baja la carga** | Distinto a tierra: acá perdés la carga |
| ⚡ **Rayo** | **Descarga doble** rápida | El primero te avisa que viene el segundo |
| 🌿 **Planta** | Se **regenera** (+14) y te enreda | Bajarlo de golpe o te gana por desgaste |
| 🌑 **Sombra** | Te **roba vida** (−6) y se cura con ella | Matarlo antes de que el dreno se acumule |

El jugador no se mueve, así que **ninguna se esquiva**: se responden con *cuándo*
tirar, *con qué elemento* y *cuánto cargar*. Aturdir al villano cancela su
carga — aturdir tiene recompensa.

Con rocas y arquetipos, cada tiro ahora se decide por *cuánto cargo*, *por dónde*
y *contra quién* — y además hay que leer *qué* te va a largar.

## 📖 La mini historia de campana

Doce niveles son doce combates sueltos. La mini historia es lo que los vuelve un
viaje: **un fragmento corto tras cada combate ganado**, contado en primera persona,
que avanza con el elemento del nivel.

El arco, en una línea: *las fuentes de la cueva se secaron, los guardianes que
quedan intentan conservar lo último de su elemento, y en el fondo alguien los
está juntando.*

| Nivel | Fragmento |
|---|---|
| 1 · Umbral | **La última fuente secó** — "Las fuentes de la cueva cantaban. Ahora son piedra seca." |
| 3 · Veta de roca | **No piden permiso** — "No hay que esquivarlos: hay que decidir cuándo tirar." |
| 6 · Guardián de escarcha | **El primero que no era basura** — el jefe no ataca al verte: espera que decidas. |
| 8 · Velo de sombras | **Lo que se lleva la sombra** — la sombra drena, y alguien junta los elementos. |
| 12 · Corazón de la cueva | **La verdad** — no hay un monstruo: hay un rey que selló los elementos, y tiene razón. |

El fragmento se puede **saltar**; la campana sigue igual. Solo aparece al *avanzar*
— repetir un nivel o volver a Niveles va directo, porque no estás avanzando el viaje.

Cada fragmento se pinta con el color del elemento del nivel, así que la historia
se lee con el mismo lenguaje que el combate.

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

# 10-15. Arquetipos: absorcion del escudo, su ciclo telegrafiado, el salto, la
#        furia, la asignacion por nivel y LO QUE SE DIBUJA de cada uno
node test-arquetipo-escudado.cjs
node test-arquetipo-escudado-ciclo.cjs
node test-arquetipo-saltarin.cjs
node test-arquetipo-frenetico.cjs
node test-arquetipos-asignacion.cjs
node test-arquetipos-visual.cjs

# 16. End-to-end en Edge real: el juego corre sin errores y la furia se ve jugando
"C:/Users/USUARIO/AppData/Local/Programs/Python/Python312/python.exe" test-arquetipos-browser.py

# 17-22. Habilidades elementales del villano: que las 8 se activen solas y
#        avisadas, que SE SIENTAN sobre el jugador, y que se vean
node test-habilidades.cjs
node test-habilidades-efectos.cjs
node test-habilidades-visual.cjs

# 23. End-to-end en Edge real: salen solas jugando y se ven por su color
"C:/Users/USUARIO/AppData/Local/Programs/Python/Python312/python.exe" test-habilidades-browser.py

# 24-25. Mini historia: datos del arco y el flujo completo con clics simulados
#         (victoria -> historia -> siguiente nivel, y repetir NO muestra historia)
node test-historia.cjs

# 26. End-to-end en Edge real: GANAR de verdad el nivel 1 y leer la historia
"C:/Users/USUARIO/AppData/Local/Programs/Python/Python312/python.exe" test-historia-browser.py

# 27. El juego YA PUBLICADO, cargado desde la URL de produccion
"C:/Users/USUARIO/AppData/Local/Programs/Python/Python312/python.exe" qa-live.py
```

La suite 15 no cuenta píxeles: usa el modo de grabación del harness
(`buildGame({ recordDraw: true })`), que anota el estilo vigente en cada
`stroke()`/`fill()`. Era la única forma honesta de verificar el dibujo, porque el
escudo cian `(127,212,255)` y el proyectil de hielo `#6fd6e6` son casi el mismo
tono: un conteo por color habría dado un falso positivo permanente.

Las suites 3-6, 10-15, 17-22 y 24 se escribieron **primero que el código** (TDD): cada una se
vio fallar por el motivo correcto antes de implementar. La 9 mide el **delta**
contra la línea base porque la cornisa y el fondo ya son marrones y suman píxeles
aunque no haya ninguna roca: medir absolutos habría dado un falso positivo.

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
