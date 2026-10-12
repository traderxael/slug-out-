/**
 * La esquiva del rival deja de ser un dado.
 *
 * El disparo de la esquiva era `Math.random() < en.evasion*dt/80`: el rival a
 * veces esquivaba y a veces no, y el jugador no podia prepararse para NADA. Eso
 * rompe el principio sobre el que se apoya el juego — todo avisa, se lee, no se
 * adivina. Las rocas tiemblan, el escudado cierra un anillo, el saltarin se
 * agacha: todo eso se puede LEER. La esquiva no.
 *
 * El arreglo no es borrar la evasion (que ademas escala con el nivel y sostiene
 * la progresion) sino cambiar QUE SIGNIFICA: deja de ser PROBABILIDAD y pasa a
 * ser A QUE DISTANCIA ve venir el tiro. Mas evasion = mas radio de deteccion =
 * el tell aparece mas temprano y cuesta mas cortarlo. Es determinista: con la
 * misma amenaza y el mismo estado, el rival reacciona SIEMPRE igual.
 *
 * El counterplay de potencia (a maxima carga el tiro llega dentro del tell) lo
 * cubre test-esquiva-balance.cjs; aca se cubre que la reaccion sea LEGIBLE.
 */
const fs = require('fs');
const { buildGame } = require('./harness.cjs');
const src = fs.readFileSync(__dirname + '/index.html', 'utf8');

let fallos = 0;
const check = (nombre, ok, detalle) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${nombre}` + (detalle ? `   [${detalle}]` : ''));
  if (!ok) fallos++;
};

// Escenario: un rival con `evasion` dada y UN proyectil subiendo por su columna
// a `dy` px de distancia. Todo lo autonomo silenciado para que solo la esquiva
// pueda mover al rival. El orden de `update` importa: la deteccion corre ANTES
// de que el proyectil avance, asi que en el primer paso `dy` es exactamente el
// que se pasa.
const G = buildGame({});
function escenario(evasion, dy){
  G.iniciar('nivel', 6);                       // nivel 6, rival 0: sin arquetipo
  const s = G.state;
  s.enemy.atkInterval = 1e9; s.rocaTimer = 1e9; s.rocas = []; s.enemyShots = [];
  s.enemy.evasion = evasion;
  s.enemy.dodgeCd = 0; s.enemy.dodgeT = 0; s.enemy.dodgeWindup = 0; s.enemy.stunT = 0;
  const y0 = s.enemy.y + dy;
  s.projectiles.length = 0;
  s.projectiles.push({ x: s.enemy.x, y: y0, x0: s.enemy.x, y0: y0,
                       vx0: 0, vy0: -0.56, vx: 0, vy: -0.56, age: 0,
                       el: 'fuego', r: 0, trail: [], power: 1 });
  return s;
}
// Un paso de simulacion de 16ms. Devuelve si el rival empezo a reaccionar.
const paso = s => { G.update(16); return s.enemy.dodgeWindup > 0; };

console.log('la esquiva del rival: de dado a lectura\n');
{
  const s = escenario(0.4, 110);
  console.log(`  geometria: rival en y=${s.enemy.y.toFixed(0)}, proyectil en y=${(s.enemy.y+110).toFixed(0)}\n`);
}

// ---------- 1. Determinismo: la misma amenaza, siempre la misma reaccion ----------
{
  // Con el codigo viejo cada corrida tenia ~10% de reaccionar (el dado), asi que
  // el conteo real era la evidencia: no pasaba nunca "casi todas". Con el nuevo
  // tiene que ser el total.
  let aciertos = 0;
  const N = 60;
  for (let i = 0; i < N; i++){
    const s = escenario(0.50, 110);           // amenaza bien dentro del radio
    if (paso(s)) aciertos++;
  }
  check('con la misma amenaza el rival reacciona SIEMPRE (sin suerte)',
    aciertos === N, `${aciertos}/${N} reaccionaron`);
}

// ---------- 2. La evasion pasa a ser el radio de deteccion ----------
// La MISMA amenaza, a distinta evasion, da distinto resultado: eso es lo que
// hace que la evasion sea legible en vez de oculta.
{
  const sLejos  = escenario(0.10, 110);        // radio ~84  -> 110 queda afuera
  const lejosEsquiva  = paso(sLejos);
  const sCerca  = escenario(0.40, 110);        // radio ~156 -> 110 queda adentro
  const cercaEsquiva  = paso(sCerca);
  check('la misma amenaza reacciona segun la evasion (radio, no azar)',
    !lejosEsquiva && cercaEsquiva,
    `evasion 0.10 -> ${lejosEsquiva ? 'reacciona' : 'no'}; evasion 0.40 -> ${cercaEsquiva ? 'reacciona' : 'no'}`);
}

// ---------- 3. evasion 0: los niveles 1-2 quedan igual que antes ----------
{
  const s = escenario(0, 40);                 // amenaza encima: radio seria 60
  check('con evasion 0 el rival no reacciona (niveles 1-2 intactos)',
    !paso(s), 'sin evasion, sin esquiva');
}

// ---------- 4. Al reaccionar aparece el tell ----------
{
  const s = escenario(0.40, 110);
  paso(s);
  check('al reaccionar aparece el tell, que es lo que se puede leer',
    s.enemy.dodgeWindup > 0, `${Math.round(s.enemy.dodgeWindup)}ms de aviso`);
}

// ---------- 5. Tras reaccionar hay que esperar el cooldown ----------
{
  const s = escenario(0.40, 110);
  paso(s);                                    // reacciona -> dodgeCd = 1300
  s.enemy.dodgeWindup = 0; s.enemy.dodgeT = 0; // el tell ya paso
  s.projectiles.length = 0;                   // y llega OTRO tiro
  const y0 = s.enemy.y + 110;
  s.projectiles.push({ x: s.enemy.x, y: y0, x0: s.enemy.x, y0: y0,
                       vx0: 0, vy0: -0.56, vx: 0, vy: -0.56, age: 0,
                       el: 'fuego', r: 0, trail: [], power: 1 });
  const otra = paso(s);
  check('con el cooldown puesto no vuelve a reaccionar (hay ventana para tirar)',
    !otra && s.enemy.dodgeCd > 0, `cooldown ${Math.round(s.enemy.dodgeCd)}ms`);
}

// ---------- 6. La condicion que decide SI esquiva ya no es azar ----------
{
  const bloque = src.match(/if\s*\(amenaza[\s\S]{0,220}?dodgeWindupMax/);
  check('la condicion que decide si esquiva no depende de Math.random()',
    !!bloque && !bloque[0].includes('Math.random'),
    !bloque ? 'no encontre el bloque del disparo'
            : (bloque[0].includes('Math.random') ? 'todavia hay azar en el disparo' : 'determinista'));
}

console.log('\n' + (fallos === 0
  ? '=== OK: la esquiva se lee, no se adivina ==='
  : `=== ${fallos} FALLA(S): la esquiva sigue siendo suerte ===`));
process.exit(fallos ? 1 : 0);
