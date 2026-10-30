/**
 * El nivel 1 como tutorial (issue #2).
 *
 * El nivel 1 es el primer contacto con el juego. Si encima viene con rocas y
 * con las habilidades del rival, el jugador aprende a esquivar y a leer el
 * techo antes que a tirar: es lo contrario de lo que el juego quiere enseñar.
 * El nivel 1 se queda con lo esencial —el tiro y la potencia— y el resto se
 * presenta despues, de a una.
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
console.log('tutorial: el nivel 1 sin rocas ni habilidades\n');

// --- 1. el nivel 1 es el unico tutorial ---
check('el nivel 1 esta marcado como tutorial',
  !!G.esTutorial && G.esTutorial(1) === true, '');
check('ningun otro nivel es tutorial',
  !!G.esTutorial && G.esTutorial(2) === false && G.esTutorial(28) === false, '');

// --- 2. en el nivel 1 ningun rival tiene arquetipo ---
if (G.arquetipoDe) {
  const arquetipos = [0, 1, 2].map(i => G.arquetipoDe(1, i, i === 2));
  check('en el nivel 1 no hay arquetipos: ni escudado, ni saltarin, ni frenetico',
    arquetipos.every(a => a === 'normal'), arquetipos.join(','));
} else {
  check('arquetipoDe existe', false);
}

// --- 3. el guardian del nivel 1 no esquiva ---
if (G.enemigoNivel) {
  const jefe1 = G.enemigoNivel(1, 2);   // el ultimo rival del nivel 1 es el jefe
  check('el guardian del nivel 1 no esquiva', jefe1.evasion === 0,
    `evasion=${jefe1.evasion}`);
  check('el guardian del nivel 1 no esta enfurecido por arquetipo',
    jefe1.arquetipo === 'normal', `arquetipo=${jefe1.arquetipo}`);

  // ... y en el nivel 2 el guardian sigue esquivando: el tutorial no cambio
  // las reglas, solo las postponed.
  const jefe2 = G.enemigoNivel(2, 3);
  check('el guardian del nivel 2 sigue esquivando', jefe2.evasion > 0,
    `evasion=${jefe2.evasion}`);
  check('el guardian del nivel 2 sigue siendo frenetico',
    jefe2.arquetipo === 'frenetico', `arquetipo=${jefe2.arquetipo}`);
} else {
  check('enemigoNivel existe', false);
}

// --- 4. en el nivel 1 no cae ninguna roca ---
if (G.iniciar && G.update) {
  G.iniciar('nivel', 1);
  const st = G.state;
  for (let i = 0; i < 900; i++) G.update(16);   // 14.4s de combate
  check('en el nivel 1 no cae ninguna roca en 14.4s',
    st.rocas.length === 0, `rocas=${st.rocas.length}`);

  // --- 5. los demas niveles siguen con rocas ---
  G.iniciar('nivel', 2);
  const st2 = G.state;
  let primera = -1;
  for (let i = 0; i < 900 && primera < 0; i++) {
    const antes = st2.rocas.length;
    G.update(16);
    if (st2.rocas.length > antes) primera = i;
  }
  check('el nivel 2 sigue teniendo rocas', primera >= 0,
    primera >= 0 ? `primera a los ${(primera + 1) * 16}ms` : 'ninguna en 14.4s');
} else {
  check('el juego se puede iniciar', false);
}

console.log('\n' + (fallos === 0
  ? '=== OK: el nivel 1 es un tutorial de verdad ==='
  : `=== ${fallos} FALLA(S) ===`));
process.exit(fallos ? 1 : 0);
