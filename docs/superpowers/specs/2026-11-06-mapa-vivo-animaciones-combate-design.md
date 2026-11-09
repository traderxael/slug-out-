# Diseño — SLUG OUT: mapa vivo, animaciones esqueleto y combate legible

**Proyecto:** `C:\Users\USUARIO\SLUG OUT` · Unity 6000.6.5f1 · URP · rama `templo-oriental`
**Fecha:** 2026-11-06
**Estado:** pendiente de aprobación antes de escribir producto.

---

## 1. Intent y por qué

El juego ya funciona: campaña de 28 niveles, cinco regiones, tres microhábitats por cueva, oleadas, captura, habilidades y HUD. Lo que hoy no está a la altura es lo que el jugador mira todo el tiempo:

- **El mapa** se lee como una arena marrón con losas oscuras, no como una cueva de Bajoterra: el fondo es vacío, no hay vida, y las tres zonas se distinguen poco.
- **El jugador** se anima sobre huesos, pero solo las piernas usan clips; el resto es procedural. Al disparar, recargar o recibir un golpe, el cuerpo apenas reacciona.
- **El combate** no tiene la respuesta visual que el sistema ya calcula (carga, umbral de 100 MPH, captura, impacto).

Las tres cosas comparten una causa: falta de señales. Este diseño agrega señales, no sistemas nuevos.

## 2. Lo que hay hoy (verificado en el proyecto)

- `SlugArenaGame.Awake` añade `Terrain3D`, `RegionCuevaSeca`, `LookCuevaPixar`, `RockShower`, `EcosCuevaManager` al objeto `Terreno` de `Mapa3D`. La cueva con hábitats, niebla y luces **sí se construye en runtime**.
- `RegionCuevaSeca` (1301 líneas) construye plaza, tres hábitats, caminos, bocas de túnel, coberturas, balizas y borde. `TunelPasadizo` permite caminar a otra región y reconstruye la cueva.
- `MundoAbierto` existe (NPCs por zonas) pero **no está en ninguna escena** y sus prefabs están sin asignar.
- El jugador es `shane_rig` + `DuelistaAnimator` (903 líneas). Clips horneados disponibles: `caminar`, `carrera`, `impacto`, `muerte`; **solo se aplican a las piernas**. `apuntar` es una pose fija; `ataque` es un gesto procedural de 0,34 s.
- `Jugador.glb` tiene skin y 76 animaciones, pero es **otro personaje** (un pícaro) y Unity lo importa como asset genérico. Se descarta.
- No hay Blender instalado. La animación se hace sobre el rig actual, en código.
- `PlayerShooter`: carga 0–0,6 s → 20–62 MPH, transforma a 44,7, 6 tubos, recarga 1,6 s/tubo, autoapuntado 30 m. `GameFeel` ya tiene hitstop y trauma.

## 3. Alcance — tres slices, en este orden

| Slice | Qué | Por qué primero |
|-------|-----|-----------------|
| **S1 — Cueva viva** | Techo, fondo, niebla, luz por zona, NPCs y props. Sin cambiar topología ni progresión. | Es lo primero que se ve y lo más barato de medir. |
| **S2 — Cuerpo entero** | Idle, apuntar con blending, disparo con retroceso, recarga, impacto y rodada sobre el rig de Shane. | Da sentido a S3: el combate necesita que el cuerpo responda. |
| **S3 — Combate legible** | Feedback de disparo, impacto, carga y captura, enganchado a los eventos reales. | Cierra el bucle: el jugador entiende qué pasó. |

**Regla de entrega:** un slice por cambio verificable. Cada uno se prueba en Play y se commitea solo lo tocado.

## 4. Metas / no-metas / anti-metas

**Metas**
- Al entrar a un nivel, la cueva se lee como cueva: techo, paredes, niebla y tres zonas con identidad.
- Hay NPCs caminando por la plaza y los caminos, sin coste perceptible de CPU.
- El jugador dispara, recarga, recibe daño y rueda con animación de cuerpo entero.
- Cada acción de combate tiene una señal visual/sonora coherente con su estado real.

**No-metas**
- Topología nueva (más cuevas, biomas, streaming) ni mundo continuo por chunks.
- Cambiar balance: vida 113, daño, cargador, curva de 28 niveles y `arenaLimit` quedan como están.
- Sustituir el modelo de Shane, pasar a `Animator`/Humanoid o importar el pícaro.
- Multijugador, economía, nuevas especies.

**Anti-metas (fracaso aunque compile)**
- Romper cualquiera de los 28 niveles o los tests EditMode/PlayMode.
- Bajar el rendimiento en la máquina objetivo (GT 640) por props o NPCs de más.
- Perder el trabajo que hoy está sin commitear (ver §5.1).

## 5. Restricciones

- Conservar `.meta`; no mover ni renombrar assets.
- Solo se commitea lo que toca cada slice.
- Todo se mide con el arnés del editor (`Tools/Arnelt*.cs`) cuando exista, y a ojo en Play cuando no.
- No cambiar la firma pública de `RegionCuevaSeca`, `DuelistaAnimator` ni `PlayerShooter` sin actualizar sus tests.

### 5.1 Punto de partida del repositorio (importante)

`HEAD` (`f0654e9`) **no contiene el juego actual**: el índice tiene ~4.400 líneas preparadas sobre 19 scripts, más assets sin seguimiento. El juego que se juega hoy vive en el árbol de trabajo. Por eso:

1. Antes de tocar nada se crea una rama de respaldo (`respaldo/antes-mapa-anim-combate`) apuntando al estado actual.
2. Se hace **un commit de línea base** con el estado real del juego (scripts + assets ya presentes), para que los diffs de cada slice sean solo nuestros.
3. Recién entonces empieza S1. Cada slice se commitea encima.

Sin ese paso, S2 y S3 no se pueden entregar: los archivos que hay que cambiar (`DuelistaAnimator`, `PlayerShooter`, `SlugActor`, `SlugFx`, `SlugShot`, `SlugArenaGame`) ya están modificados por la sesión anterior.

## 6. Diseño por slice

### S1 — Cueva viva

**Techo y fondo.** Ya existe: `LookCuevaPixar` construye `CuevaPixar_Techo` (cúpula de 60 m) y `MantoTecho` (bóveda a 12–14,6 m con 22 estalactitas y 16 cristales colgantes). **No se añade un segundo techo.** Se retoca el existente para cerrar el encuadre: bajar el manto, tapar el hueco del horizonte con un anillo de roca y ajustar la niebla. Se respeta el presupuesto de `RegionesLookTests` (210 renderers / 120k triángulos).

**Niebla y luz.** `LookCuevaPixar` ya aplica paleta, niebla y luces por región. Se ajusta para que la niebla cierre el fondo (fin de niebla ≤ 60 m) y para que cada hábitat tenga su luz de acento visible desde la plaza. El objetivo es contraste, no oscurecer.

**Vida.** `MundoAbierto` se añade desde `SlugArenaGame.Awake` y carga sus prefabs de `Resources/Characters/NpcSabio`, `NpcExplorador`, `NpcBarbaro` y `Duelista` en vez de exigir referencias serializadas. **Tres NPCs en total por región** (no por hábitat), sin física, con `NpcAmbulante` existente; se retiran al viajar de región. Como sus FBX no traen clips y no hay AnimatorController, caminan con un bob/giro procedural simple: no se toca el `.meta` ni se añaden controladores en S1. Los NPCs no entran al combate ni cuentan para oleadas.

**Props.** Se reutilizan los constructores existentes: balizas, coberturas y rocas. Se añade un cartel de chapa por camino con el nombre del hábitat (texto 3D simple), para que las zonas se nombren solas.

**Criterio de aceptación S1**
- Al iniciar un nivel se ven techo, paredes y fondo; no se ve el vacío negro entre las paredes.
- Hay NPCs caminando por la plaza y al menos un camino, y desaparecen/renacen sin errores.
- Las tres zonas se distinguen por color de luz y cartel.
- No hay errores nuevos en consola y el framerate se mantiene estable en la máquina objetivo.

### S2 — Cuerpo entero

Todo sobre `DuelistaAnimator`, sin clips nuevos ni Blender.

- **Apuntar con blending.** Hoy `apuntar` es un bool que pisa el brazo. Pasa a un peso 0..1 (`_pesoApuntar`) que interpola entre la pose de caminata y la de apuntar; el torso y la cabeza acompañan.
- **Disparo.** `AnimarDisparo(fuerza)` con retroceso de brazo, hombro y torso en 0,18 s, encadenable. `PlayerShooter` lo llama al soltar el tiro, con fuerza según la fase (corta / 100 MPH / megamorph).
- **Recarga.** `AnimarRecarga(duracion)`: la mano izquierda baja al cinturón y vuelve, mientras el brazo del bláster se mantiene. `PlayerShooter` la llama al empezar cada recarga de tubo.
- **Idle.** Respiración y deriva ya existen; se les suma un cambio de peso lento para que no parezca congelado.
- **Impacto y rodada.** Ya existen; se revisa que `impacto` no se pise con `apuntar` y que la rodada no deje el rig girado.
- **Caminar/correr.** Se mantiene el clip horneado de piernas y se mejora el contrapeso de brazos y cadera para que el paso se lea desde atrás.

**Criterio de aceptación S2**
- Disparar se ve: el brazo retrocede y vuelve, sin salto de pose.
- Recargar se ve y dura lo que dura la recarga real.
- Apuntar y caminar se mezclan sin tirones; soltar el apuntado no congela el cuerpo.
- Recibir un golpe y rodar se ven una sola vez y no dejan el rig en pose rara.

### S3 — Combate legible

- **Disparo:** fogonazo y trazador con el color del elemento (`SlugFx` existente), y sonido diferenciado por fase.
- **Carga:** no hay retículo en pantalla y el disparo es autoapuntado, así que **no se inventa una mira**. Se refuerza la barra de carga existente (`GameHUD.DibujarCarga`): la muesca de 100 MPH cambia de color al cruzarla y se añade una marca de megamorph. Además, un marcador en el mundo sobre el objetivo actual de `PlayerShooter`.
- **Impacto:** hitstop corto y trauma ya existentes; se añade marca de impacto en el punto real y número de daño solo si el golpe conectó.
- **Captura:** el FX y el hitstop actuales se mantienen. El gesto del jugador se reserva para capturas que completan el objetivo o estrenan especie, para no cortar el apuntado en cada captura.

**Criterio de aceptación S3**
- Un tiro acertado y uno fallido se distinguen sin mirar el HUD.
- La marca de 100 MPH coincide con el umbral real (44,7 MPH de carga).
- No se duplican VFX ni sonidos al encadenar tiros.
- La captura se siente distinta de un impacto normal.

## 7. Orden y verificación

1. S1 se implementa y se prueba en Play; debe quedar commiteado solo.
2. S2 se implementa sobre el rig; se verifica con `AimPoseCalibracionTests` y Play.
3. S3 se engancha a los eventos de `PlayerShooter` y `SlugActor`.
4. Al final: los 28 niveles siguen ganables y los tests EditMode/PlayMode siguen verdes.

## 8. Qué queda al builder

- La forma exacta del techo (anillo + bóveda) y cuántas estalactitas, según lo que aguante la máquina.
- Los tiempos exactos del retroceso, la recarga y el peso de apuntado.
- Si el cartel de zona va en 3D o en el HUD.
- El orden fino de S3 dentro de cada acción.

---

**Próximo paso tras aprobación:** `superpowers:writing-plans` con S1 como primer entregable.
