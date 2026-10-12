/**
 * Arquetipos de enemigo — SLICE 4: el FRENETICO (se enfurece a media vida).
 *
 * Al cruzar el 50% de vida se enfurece: se mueve mas rapido. La decision que
 * agrega es de tempo — bajarlo de golpe o bancarse la andanada — y por eso el
 * test afirma que NO se enfurece arriba del umbral: sin ese control, un
 * "frenetico" que ya arranca enfurecido no seria una mecanica sino un numero.
 *
 * Corre contra el index.html real via harness.cjs.
 */
const { buildGame } = require('./harness.cjs');

let fallos = 0;
const check = (nombre, ok, detalle) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${nombre}` + (detalle ? `   [${detalle}]` : ''));
  if (!ok) fallos++;
};

const G = buildGame();
console.log('arquetipo frenetico: se enfurece a media vida\n');

// Un tiro tarda ~480ms en llegar pero el cooldown a potencia 1 es 300+320*1 =
// 620ms: esperar solo el vuelo deja el cooldown activo y el SIGUIENTE disparo
// no sale, con lo que el caso 2 no mediria nada. Esperar los dos.
const dispararYEsperar = () => {
  G.fire(0, -100, 1);
  for (let i = 0; i < 80; i++) G.update(16);   // 1280ms > 620ms de cooldown
};

G.iniciar('nivel', 6);
const st = G.state, en = st.enemy;
en.atkInterval = 1e9; en.evasion = 0; en.dodgeCd = 1e9;
en.arquetipo = 'frenetico';
en.vx = 0.05; en.dir = 1; en.furia = false;
const UMBRAL = en.maxHp * 0.5;

check('no esta enfurecido con vida completa', en.furia === false);

// --- Caso 1: un tiro que lo deja ARRIBA del 50% no debe enfurecerlo ---
en.x = 180; en.hp = Math.round(en.maxHp * 0.8);
dispararYEsperar();
check('un tiro que lo deja por encima del 50% NO lo enfurece',
      en.furia === false && en.hp > UMBRAL,
      `hp ${en.hp}/${en.maxHp}, umbral ${UMBRAL}`);

// --- Caso 2: un tiro que lo CRUZA por debajo del 50% si debe enfurecerlo ---
en.x = 180; en.hp = Math.round(UMBRAL + 4);       // a un tiro de distancia
dispararYEsperar();
check('el tiro que lo cruza por debajo del 50% lo ENFURECE',
      en.furia === true && en.hp < UMBRAL, `hp ${en.hp}/${en.maxHp}`);

// --- Caso 3: enfurecido se mueve mas rapido (efecto observable) ---
const medirAvance = () => {
  en.x = 120; en.dir = 1;                        // lejos de las paredes
  const x0 = en.x;
  for (let i = 0; i < 40; i++) G.update(16);     // 640 ms
  return en.x - x0;
};
const dxFurioso = medirAvance();
en.furia = false;
const dxNormal = medirAvance();
check('enfurecido se mueve mas rapido que normal',
      dxFurioso > dxNormal * 1.2,
      `furioso ${dxFurioso.toFixed(1)}px vs normal ${dxNormal.toFixed(1)}px en 640ms`);

console.log('\n' + (fallos === 0
  ? '=== OK: el frenetico se enfurece ==='
  : `=== ${fallos} FALLA(S): el frenetico no se enfurece ===`));
process.exit(fallos ? 1 : 0);
