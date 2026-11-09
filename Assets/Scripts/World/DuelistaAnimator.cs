using System.Collections.Generic;
using UnityEngine;

namespace SlugOut.World
{
    /// Animacion procedural sobre huesos, en la misma linea que SlugAnimator (sin
    /// AnimationClips ni AnimatorController en el proyecto).
    ///
    /// Regla que sostiene todo esto: cada hueso llega de Blender con una rotacion de
    /// bind pose no identidad. Escribirle `localRotation` directo lo teletransporta a
    /// identidad y el personaje queda en cruz. Todo lo que se aplica aca es
    /// `bind * delta`, con el bind capturado una sola vez al armar.
    public class DuelistaAnimator : MonoBehaviour
    {
        public enum Modo { Duelista, Babosa }

        [Header("Modo")]
        [SerializeField] private Modo modo = Modo.Duelista;

        [Header("Clips horneados")]
        [Tooltip("Juego de clips propio del personaje: Resources/Animacion/<id>.json. " +
                 "Vacio = el global del heroe (shane_piernas).")]
        [SerializeField] private string idClips = "";
        public string IdClips { get { return idClips; } set { idClips = value; } }

        [Header("Apuntar")]
        [Tooltip("Postura de Shane: el brazo del blaster queda levantado siempre.")]
        [SerializeField] private bool apuntar = false;
        public bool Apuntar { get { return apuntar; } set { apuntar = value; } }

        [Header("Pose de apuntar (calibrada en Unity)")]
        [SerializeField] private Vector3 brazoDerAim = new Vector3(-74f, 0f, -8f);
        [SerializeField] private Vector3 antebrazoDerAim = new Vector3(-12f, 0f, 0f);
        [SerializeField] private Vector3 manoDerAim = new Vector3(133f, 96f, 22f);
        [SerializeField] private Vector3 brazoIzqAim = new Vector3(-95f, -67f, 69f);
        [SerializeField] private Vector3 antebrazoIzqAim = new Vector3(-16f, -20f, 12f);
        [SerializeField] private Vector3 manoIzqAim = new Vector3(74f, -26f, -56f);
        [SerializeField] private Vector3 columnaAim = new Vector3(-2f, 9f, 0f);
        [SerializeField] private Vector3 cabezaAim = new Vector3(-5f, 7f, 0f);

        [Header("Caminata")]
        [Tooltip("Grados de barrido del muslo hacia adelante y atras")]
        [SerializeField] private float pasoMuslo = 34f;
        [Tooltip("La rodilla solo dobla hacia atras: el maximo es el doblez, no el rango")]
        [SerializeField] private float doblaRodilla = 52f;
        [SerializeField] private float balanceoBrazo = 26f;
        [SerializeField] private float codoFijo = 24f;
        [Tooltip("Ciclos por segundo a 1 unidad de velocidad, con tope")]
        [SerializeField] private float frecuenciaPorVelocidad = 0.62f;
        [SerializeField] private float frecuenciaMax = 3.1f;
        [SerializeField] private float reboteCadera = 0.035f;
        [SerializeField] private float inclinacionCarrera = 8f;

        [Header("Idle")]
        [SerializeField] private float respirar = 2.2f;
        [SerializeField] private float giroCabeza = 6f;
        [SerializeField] private float derivaBrazos = 3.5f;

        [Header("Ataque")]
        [SerializeField] private float durAtaque = 0.34f;
        [SerializeField] private float levantarBrazo = 62f;
        [SerializeField] private float giroTorso = 16f;

        [Header("Impacto")]
        [SerializeField] private float durImpacto = 0.26f;
        [SerializeField] private float retrocesoCabeza = 16f;

        [Header("Muerte")]
        [SerializeField] private float durMuerte = 0.75f;
        [SerializeField] private float caidaCadera = 0.62f;

        [Header("Rodada")]
        [SerializeField] private float rodadaGiro = 360f;
        [SerializeField] private float rodadaElevacion = 0.25f;
        [SerializeField] private float rodadaTuck = 1f;

        [Header("Postura en el terreno")]
        [Tooltip("Inclinacion maxima del cuerpo para acompanar la ladera, en grados.")]
        [SerializeField] private float inclinacionMaxima = 12f;

        class Hueso
        {
            public Transform t;
            public Quaternion bind;
        }

        private readonly Dictionary<string, Hueso> _huesos = new Dictionary<string, Hueso>();
        private Transform _cadera;
        private Vector3 _caderaBind;
        private Transform _rig;
        private Vector3 _rigBindPos;
        private Quaternion _rigBindRot;
        private Transform _modeloVisual;
        private Vector3 _modeloBindPos;
        private Quaternion _modeloBindRot;
        private Vector3 _modeloBindOriginal;
        private Quaternion _modeloBindRotOriginal;
        private Vector3 _pivoteRodadaLocal;
        private class MallaSuela
        {
            public SkinnedMeshRenderer smr;
            public Mesh malla;
            public BoneWeight[] pesos;
            public int idxIzq;
            public int idxDer;
        }

        private readonly List<MallaSuela> _mallasSuela = new List<MallaSuela>();
        private float _calibrarEn = -1f;
        private Quaternion _posturaRot = Quaternion.identity;
        private SlugActor _actor;
        private Transform _raizActor;
        private PlayerDodge _dodge;
        private Vector3 _ultimaPos;
        private float _velocidadSuave;
        private float _fase;
        private float _distanciaLocomocion;
        private ClipsProcedurales.Clip _clipCaminar;
        private ClipsProcedurales.Clip _clipCarrera;
        private ClipsProcedurales.Clip _clipImpacto;
        private ClipsProcedurales.Clip _clipMuerte;
        private ClipsProcedurales.Clip _clipAvance;
        private bool _clipsBuscados;
        private float _tAtaque = -1f;
        private float _fuerzaAtaque = 1f;
        private float _tImpacto = -1f;
        private float _tMuerte = -1f;
        private float _t;
        private bool _listo;
        private bool _avisoSinHuesos;

        // Contador propio en vez de GetInstanceID(): en Unity 6 esa API esta marcada
        // obsolete-como-error y rompe la compilacion del ensamble entero.
        private static int _siguiente = 1;
        private readonly int _numero = _siguiente++;

        private void Awake()
        {
            Recolectar();
        }

        /// Separado de Awake porque AttachShane corre desde el flujo del menu, y alli Unity
        /// no llega a invocar Awake: el componente quedaba activo, sin errores, con cero
        /// huesos y el heroe rigido para siempre. Update lo reintentar si hace falta.
        /// Falso cuando el cuerpo que se linkeo ya no existe: AttachShane puede correr mas
        /// de una vez y la segunda pasada destruye el rig anterior, dejando los punteros
        /// cacheados en null. Ahi hay que volver a recolectar, no quedarse con el cache.
        private bool HuesosVivos()
        {
            if (_huesos.Count == 0) return false;
            foreach (KeyValuePair<string, Hueso> kv in _huesos)
                if (kv.Value.t == null) return false;
            return true;
        }

        private void Recolectar()
        {
            if (_listo && HuesosVivos()) return;
            _huesos.Clear();
            _listo = false;
            _actor = GetComponent<SlugActor>();
            _raizActor = transform;
            _ultimaPos = _raizActor.position;
            _distanciaLocomocion = 0f;
            _posturaRot = Quaternion.identity;
            SuelaCalibrada = false;
            // Fase distinta por instancia: sin esto, una oleada camina en comparsa exacta
            // y se lee como un fondo, no como personajes.
            _fase = (_numero % 977) * (Mathf.PI * 2f / 977f);

            foreach (Transform h in GetComponentsInChildren<Transform>(true))
            {
                if (EsHueso(h.name))
                    _huesos[h.name] = new Hueso { t = h, bind = h.localRotation };
            }
            _rig = null;
            foreach (KeyValuePair<string, Hueso> kv in _huesos)
            {
                if (kv.Key == "raiz") { _rig = kv.Value.t; break; }
            }
            if (_rig != null)
            {
                _rigBindPos = _rig.localPosition;
                _rigBindRot = _rig.localRotation;
            }
            if (!_clipsBuscados)
            {
                _clipsBuscados = true;
                ClipsProcedurales.Cargar();
                // Cada personaje puede traer su juego de clips horneado en Blender
                // (con su esqueleto y su zancada). Sin el, se usa el global.
                _clipCaminar = ClipsProcedurales.Buscar("caminar", idClips);
                _clipCarrera = ClipsProcedurales.Buscar("carrera", idClips);
                _clipImpacto = ClipsProcedurales.Buscar("impacto", idClips);
                _clipMuerte = ClipsProcedurales.Buscar("muerte", idClips);
                _clipAvance = ClipsProcedurales.Buscar("avance", idClips);
            }
            // El modelo visible es el hijo directo del actor que contiene el rig. La
            // raiz hereda una escala no uniforme del FBX (~100); girarla cizalla la
            // malla, asi que la rodada gira este wrapper, que si es uniforme.
            _modeloVisual = null;
            Transform visual = _rig;
            while (visual != null && visual.parent != null && visual.parent != _raizActor)
                visual = visual.parent;
            if (visual != null && visual.parent == _raizActor)
            {
                // El bind original se guarda una sola vez por modelo. Si se recapturara
                // en cada Recolectar, la altura ya calibrada se volvia a calibrar y el
                // modelo se hundia 0,6 m por nivel.
                if (visual != _modeloVisual)
                {
                    _modeloBindOriginal = visual.localPosition;
                    _modeloBindRotOriginal = visual.localRotation;
                }
                _modeloVisual = visual;
                _modeloBindPos = _modeloBindOriginal;
                _modeloBindRot = _modeloBindRotOriginal;
                if (_cadera != null)
                    _pivoteRodadaLocal = _raizActor.InverseTransformPoint(_cadera.position);
            }
            if (_huesos.TryGetValue("cadera", out Hueso c))
            {
                _cadera = c.t;
                _caderaBind = _cadera != null ? _cadera.localPosition : Vector3.zero;
            }
            _listo = _huesos.Count > 0;
            PrepararSuelas();
            // La altura se calibra cuando la pose de reposo ya se asento (0,25 s),
            // no en el cuadro del montaje: ahi el modelo puede venir a mitad de paso.
            _calibrarEn = 0.25f;
        }

        /// Una sola calibracion al montar el modelo: lo sube/baja para que la suela
        /// VISIBLE mas baja de cualquier malla del cuerpo quede en el piso. Se miran
        /// todas las SkinnedMeshRenderer con los huesos del pie: el cuerpo puede estar
        /// partido en varias mallas y calibrar una sola dejaba otra 0,6 m enterrada.
        private void CalibrarAltura()
        {
            if (_modeloVisual == null || _mallasSuela.Count == 0) return;
            // Un cadaver no se calibra: la pose de muerte aleja los pies del piso.
            if (_actor != null && !_actor.Alive) return;
            float menor = float.MaxValue;
            Vector3 punto = Vector3.zero;
            for (int m = 0; m < _mallasSuela.Count; m++)
            {
                MallaSuela ms = _mallasSuela[m];
                if (ms.smr == null) continue;
                ms.malla.Clear();
                ms.smr.BakeMesh(ms.malla, true);
                Vector3[] verts = ms.malla.vertices;
                Matrix4x4 aMundo = ms.smr.transform.localToWorldMatrix;
                for (int v = 0; v < ms.pesos.Length && v < verts.Length; v++)
                {
                    int dom = Dominante(ms.pesos[v]);
                    if (dom != ms.idxIzq && dom != ms.idxDer) continue;
                    Vector3 mundo = aMundo.MultiplyPoint3x4(verts[v]);
                    if (mundo.y < menor) { menor = mundo.y; punto = mundo; }
                }
            }
            if (menor == float.MaxValue) return;
            float error = Terrain3D.Altura(punto.x, punto.z) - menor;
            // Si el actor todavia esta cayendo (spawn alto, oleada), el error es de
            // metros: calibrar ahi hundia el modelo para siempre. Se reintenta al aterrizar.
            if (Mathf.Abs(error) > 0.35f)
            {
                _calibrarEn = 0.08f;
                return;
            }
            _modeloBindPos.y += error;
            _modeloVisual.localPosition = _modeloBindPos;
            SuelaCalibrada = true;
        }

        private static int Dominante(BoneWeight p)
        {
            int dom = p.boneIndex0;
            float mayor = p.weight0;
            if (p.weight1 > mayor) { mayor = p.weight1; dom = p.boneIndex1; }
            if (p.weight2 > mayor) { mayor = p.weight2; dom = p.boneIndex2; }
            if (p.weight3 > mayor) { mayor = p.weight3; dom = p.boneIndex3; }
            return mayor > 0.001f ? dom : -1;
        }

        /// Recolecta todas las mallas skinned que usan los huesos del pie. El mesh
        /// importado no es legible, pero BakeMesh entrega una copia legible.
        private void PrepararSuelas()
        {
            for (int i = 0; i < _mallasSuela.Count; i++)
                if (_mallasSuela[i].malla != null) Destroy(_mallasSuela[i].malla);
            _mallasSuela.Clear();
            Transform pieIzq = _huesos.TryGetValue("pie_izq", out Hueso hi) ? hi.t : null;
            Transform pieDer = _huesos.TryGetValue("pie_der", out Hueso hd) ? hd.t : null;
            if (_modeloVisual == null || pieIzq == null || pieDer == null) return;

            // Solo la malla del modelo visible: un cuerpo oculto colgado del actor
            // (menu, preview) puede tener pies a metros del piso y arruinaba el minimo.
            SkinnedMeshRenderer[] mallas = _modeloVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < mallas.Length; i++)
            {
                SkinnedMeshRenderer s = mallas[i];
                if (s.sharedMesh == null || s.bones == null) continue;
                int idxIzq = -1, idxDer = -1;
                for (int b = 0; b < s.bones.Length; b++)
                {
                    if (s.bones[b] == pieIzq) idxIzq = b;
                    else if (s.bones[b] == pieDer) idxDer = b;
                }
                if (idxIzq < 0 || idxDer < 0) continue;
                BoneWeight[] pesos = s.sharedMesh.boneWeights;
                if (pesos == null || pesos.Length != s.sharedMesh.vertexCount) continue;
                _mallasSuela.Add(new MallaSuela
                {
                    smr = s, malla = new Mesh(), pesos = pesos, idxIzq = idxIzq, idxDer = idxDer
                });
            }
        }

        private static bool EsHueso(string n)
        {
            switch (n)
            {
                case "raiz": case "cadera": case "columna": case "cuello": case "cabeza":
                case "brazo_izq": case "brazo_der":
                case "antebrazo_izq": case "antebrazo_der":
                case "mano_izq": case "mano_der":
                case "muslo_izq": case "muslo_der":
                case "pantorrilla_izq": case "pantorrilla_der":
                case "pie_izq": case "pie_der":
                case "cola": case "cuerpo": case "pecho":
                case "antena_0": case "antena_1":
                    return true;
            }
            return false;
        }

        /// Rota un hueso respecto de su bind pose. Angulos en grados locales.
        private void Girar(string hueso, float x, float y, float z)
        {
            if (!_huesos.TryGetValue(hueso, out Hueso h) || h.t == null) return;
            h.t.localRotation = h.bind * Quaternion.Euler(x, y, z);
        }

        /// Diagnostico de pose para pruebas: aplica una pose concreta y deja de
        /// actualizar para poder medir el arma sin que el Update la pise.
        public void ProbePose(Vector3 brazoDer, Vector3 antebrazoDer, Vector3 manoDer,
                              Vector3 brazoIzq, Vector3 antebrazoIzq, Vector3 manoIzq,
                              Vector3 columna, Vector3 cabeza)
        {
            Recolectar();
            enabled = false;
            Girar("brazo_der", brazoDer.x, brazoDer.y, brazoDer.z);
            Girar("antebrazo_der", antebrazoDer.x, antebrazoDer.y, antebrazoDer.z);
            Girar("mano_der", manoDer.x, manoDer.y, manoDer.z);
            Girar("brazo_izq", brazoIzq.x, brazoIzq.y, brazoIzq.z);
            Girar("antebrazo_izq", antebrazoIzq.x, antebrazoIzq.y, antebrazoIzq.z);
            Girar("mano_izq", manoIzq.x, manoIzq.y, manoIzq.z);
            Girar("columna", columna.x, columna.y, columna.z);
            Girar("cabeza", cabeza.x, cabeza.y, cabeza.z);
        }

        private void Resetear(string hueso)
        {
            if (!_huesos.TryGetValue(hueso, out Hueso h) || h.t == null) return;
            h.t.localRotation = h.bind;
        }

        private void ResetearTodo()
        {
            foreach (KeyValuePair<string, Hueso> kv in _huesos)
                if (kv.Value.t != null) kv.Value.t.localRotation = kv.Value.bind;
            if (_cadera != null) _cadera.localPosition = _caderaBind;
            if (_rig != null) _rig.localPosition = _rigBindPos;
            if (_modeloVisual != null)
            {
                _modeloVisual.localPosition = _modeloBindPos;
                _modeloVisual.localRotation = _modeloBindRot;
            }
        }

        /// Aplica la pose de apuntar de produccion y deja de actualizar: una prueba mide
        /// exactamente lo que se ve en el juego, no una copia de las constantes.
        public void ProbeApuntar()
        {
            Recolectar();
            enabled = false;
            _tAtaque = -1f;
            _tImpacto = -1f;
            _tMuerte = -1f;
            ResetearTodo();
            AplicarApuntar();
        }

        /// Variante de calibracion: pose de produccion con un brazo izquierdo candidato.
        public void ProbeApuntarIzquierda(Vector3 brazoIzq, Vector3 antebrazoIzq, Vector3 manoIzq)
        {
            ProbeApuntarCompleta(manoDerAim, brazoIzq, antebrazoIzq, manoIzq);
        }

        /// Variante de calibracion: pose de produccion con muneca derecha y brazo
        /// izquierdo candidatos. La muneca mueve el blaster; por eso se mide primero.
        public void ProbeApuntarCompleta(Vector3 manoDer, Vector3 brazoIzq,
            Vector3 antebrazoIzq, Vector3 manoIzq)
        {
            ProbeApuntar();
            Girar("mano_der", manoDer.x, manoDer.y, manoDer.z);
            Girar("brazo_izq", brazoIzq.x, brazoIzq.y, brazoIzq.z);
            Girar("antebrazo_izq", antebrazoIzq.x, antebrazoIzq.y, antebrazoIzq.z);
            Girar("mano_izq", manoIzq.x, manoIzq.y, manoIzq.z);
        }

        public void ProbeApuntarDerecha(Vector3 manoDer)
        {
            ProbeApuntarCompleta(manoDer, brazoIzqAim, antebrazoIzqAim, manoIzqAim);
        }

        /// Publico: la arena lo llama cuando el duelistar/dispara para que el brazo
        /// del arma acompañe el tiro.
        /// Quien monta el modelo decide el esqueleto que va a mover: un duelistar
        /// usa las extremidades, una babosa la columna ondulada.
        public Modo ModoActual { get { return modo; } set { modo = value; } }

        /// Angulo de la rodada aplicado este cuadro (0 fuera de la rodada). Lo lee la
        /// prueba de rodada para comprobar el giro completo sin ambiguedad del quaternion.
        public float AnguloRodada { get; private set; }

        /// Diagnostico: el modelo encontro los dos clips horneados.
        public bool UsandoClips { get { return _clipCaminar != null && _clipCarrera != null; } }

        /// Diagnostico: la altura de la suela ya se asento contra el terreno.
        public bool SuelaCalibrada { get; private set; }

        /// Diagnostico de la rodada para las pruebas: dice si el animador tiene raiz,
        /// si encontro el PlayerDodge y si el tiempo del cuadro no es cero.
        public string DiagnosticoRodada()
        {
            PlayerDodge d = _dodge != null ? _dodge : GetComponent<PlayerDodge>();
            string escala = _rig != null && _rig.parent != null ? _rig.parent.lossyScale.ToString("F3") : "sin-padre";
            string rumbo = d != null ? d.RumboRodada.ToString("F2") : "sin-dodge";
            return "rig=" + (_rig != null)
                + " dodge=" + (d != null)
                + " rodando=" + (d != null && d.Rodando)
                + " expuesto=" + (d != null && d.Expuesto)
                + " listo=" + _listo + " modo=" + modo
                + " dt=" + Time.deltaTime.ToString("F3")
                + " k=" + (d != null ? d.ProgresoRodada.ToString("F3") : "-")
                + " rumbo=" + rumbo + " escala=" + escala
                + " angulo=" + AnguloRodada.ToString("F1");
        }

        /// `fuerza` 0..1: un tiro de 20 m/s no puede sacudir el hombro igual que uno de 62.
        public void AnimarAtaque(float fuerza = 1f)
        {
            _tAtaque = 0f;
            _fuerzaAtaque = Mathf.Clamp01(fuerza);
        }
        public void AnimarImpacto() { _tImpacto = 0f; }

        /// Solo para la herramienta de capturas: arranca el colapso sin matar al actor.
        /// Matarlo abre el panel de resultado, que congela timeScale y corta la animacion
        /// justo cuando se quiere fotografiar.
        public void AnimarMuerteParaCaptura() { _tMuerte = 0f; }

        private void OnEnable()
        {
            // SlugActor ya publica un evento estatico por golpe recibido: suscribirse aca
            // evita tener que tocar SlugActor ni la IA para que el impacto se vea.
            SlugActor.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            SlugActor.Damaged -= OnDamaged;
        }

        private void OnDamaged(SlugActor golpeado, float final, float mult)
        {
            if (golpeado == _actor) AnimarImpacto();
        }

        /// Get-or-add. AttachShane corre cada vez que arranca un nivel y Reintentar vuelve
        /// a pasar por aqui: con AddComponent a secas el jugador acumulaba dos animadores y
        /// NotificarAtaque, que usa GetComponent, terminaba moviendo uno mientras el otro
        /// (el ultimo agregado) se quedaba quieto.
        public static DuelistaAnimator Obtener(GameObject actor, Modo modo, bool apuntar = false)
        {
            if (actor == null) return null;
            DuelistaAnimator a = actor.GetComponent<DuelistaAnimator>();
            if (a == null) a = actor.AddComponent<DuelistaAnimator>();
            a.ModoActual = modo;
            a.Apuntar = apuntar;
            // Reusar el componente sin limpiar el estado dejaba al heroe colapsado para
            // siempre: _tMuerte es una trampa en Update y nunca se levanta solo, asi que
            // tras morir en el nivel 3 llegaba al 4 ya en el suelo.
            a.Recolectar();      // por si el cuerpo de esta pasada no es el del cache
            a.ReiniciarEstado();
            return a;
        }

        /// Vuelve al estado de reposo (sin muerte, ataque ni rodada). Publico para el
        /// respawn y para que una prueba que reusa al jugador no mida un cadaver.
        public void ReiniciarEstado()
        {
            _tMuerte = -1f;
            _tAtaque = -1f;
            _tImpacto = -1f;
            _velocidadSuave = 0f;
            AnguloRodada = 0f;
            if (_dodge == null) _dodge = GetComponent<PlayerDodge>();
            if (_dodge != null) _dodge.Reiniciar();
            ResetearTodo();
        }

        /// Llamado desde la IA en el telegrafiado: el brazo del arma tiene que levantarse
        /// cuando el enemigo avisa, no cuando ya llego.
        public static void NotificarAtaque(GameObject actor, float fuerza = 1f)
        {
            if (actor == null) return;
            DuelistaAnimator a = actor.GetComponent<DuelistaAnimator>();
            if (a != null) a.AnimarAtaque(fuerza);
        }

        private void Update()
        {
            if (!_listo || !HuesosVivos()) Recolectar();
            if (!_listo)
            {
                // Una sola vez por personaje: si el modelo no trae armature hay que decirlo,
                // pero no una linea por cuadro.
                if (!_avisoSinHuesos)
                {
                    _avisoSinHuesos = true;
                    Debug.LogWarning("[DuelistaAnimator] en " + name + " no hay ningun hueso: "
                        + "el modelo no viene riggeado (falta el FBX) y queda rigido.");
                }
                return;
            }
            // SlugActor se agrega DESPUES de este componente en SpawnEnemy. Resolverlo
            // solo en Awake lo deja en null para siempre: ni el impacto (que compara el
            // golpeado contra _actor) ni la muerte (que lee _actor.Alive) se enteraban.
            if (_actor == null) _actor = GetComponent<SlugActor>();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _t += dt;

            MedirVelocidad(dt);

            bool muerto = _actor != null && !_actor.Alive;
            if (muerto && _tMuerte < 0f) _tMuerte = 0f;
            if (_tMuerte >= 0f)
            {
                _tMuerte += dt;
                AplicarMuerte();
                return;
            }

            if (_tAtaque >= 0f) { _tAtaque += dt; if (_tAtaque > durAtaque) _tAtaque = -1f; }
            if (_tImpacto >= 0f) { _tImpacto += dt; if (_tImpacto > durImpacto) _tImpacto = -1f; }

            ResetearTodo();
            if (modo == Modo.Babosa) AplicarBabosa();
            else AplicarHumanoide();
            if (apuntar) AplicarApuntar();
            if (_tAtaque >= 0f) AplicarAtaque();
            if (_tImpacto >= 0f) AplicarImpacto();
            if (modo == Modo.Duelista) AplicarRodada();
            if (_calibrarEn > 0f)
            {
                _calibrarEn -= dt;
                if (_calibrarEn <= 0f) CalibrarAltura();
            }
            if (modo == Modo.Duelista) AplicarPosturaTerreno(dt);
        }

        /// Apoya el modelo en el relieve sin tocar huesos: inclina el wrapper (escala
        /// uniforme) hasta `inclinacionMaxima` y lo sube/baja para que la suela mas
        /// hundida quede en el piso. Los pies son puntos de malla, no los origenes de
        /// los huesos (que en este FBX estan ~0,7 m por debajo y enganaban al IK).
        private void AplicarPosturaTerreno(float dt)
        {
            if (_modeloVisual == null) return;
            if (_dodge != null && (_dodge.Rodando || _dodge.Expuesto)) return;

            // Primero la inclinacion: cambia la posicion de las suelas y la medicion
            // tiene que ver la pose ya inclinada.
            Vector3 pend = Terrain3D.Pendiente(_raizActor.position.x, _raizActor.position.z, 0.8f);
            Vector3 normal = new Vector3(-pend.x, 1f, -pend.z);
            float angulo = Vector3.Angle(Vector3.up, normal);
            if (angulo > inclinacionMaxima && angulo > 0.0001f)
                normal = Vector3.Slerp(Vector3.up, normal, inclinacionMaxima / angulo);
            normal.Normalize();
            Quaternion qMundo = Quaternion.FromToRotation(Vector3.up, normal);
            Quaternion qLocal = Quaternion.Inverse(_raizActor.rotation) * qMundo * _raizActor.rotation;
            _posturaRot = Quaternion.Slerp(_posturaRot, qLocal, Mathf.Clamp01(dt * 10f));
            _modeloVisual.localRotation = _posturaRot * _modeloBindRot;

            // La altura quedo calibrada al montar. Solo se acompaña la inclinacion:
            // un lazo de altura por cuadro con la malla horneada oscilaba entre el
            // limite superior e inferior y terminaba flotando.
            _modeloVisual.localPosition = _modeloBindPos;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _mallasSuela.Count; i++)
                if (_mallasSuela[i].malla != null) Destroy(_mallasSuela[i].malla);
            _mallasSuela.Clear();
        }

        /// Pose de apuntar sostenida. Va DESPUES del idle/caminata a proposito: el brazo
        /// del arma no puede balancearse como el otro, y si se aplicara antes el paso
        /// lo pisaria y el personaje quedaria con el blaster colgando.
        private void AplicarApuntar()
        {
            // Constantes medidas con AimPoseCalibracionTests sobre el FBX real: los ejes
            // del modelo no son los de Blender, asi que no se adivinan.
            Girar("brazo_der", brazoDerAim.x, brazoDerAim.y, brazoDerAim.z);
            Girar("antebrazo_der", antebrazoDerAim.x, antebrazoDerAim.y, antebrazoDerAim.z);
            Girar("mano_der", manoDerAim.x, manoDerAim.y, manoDerAim.z);
            Girar("brazo_izq", brazoIzqAim.x, brazoIzqAim.y, brazoIzqAim.z);
            Girar("antebrazo_izq", antebrazoIzqAim.x, antebrazoIzqAim.y, antebrazoIzqAim.z);
            Girar("mano_izq", manoIzqAim.x, manoIzqAim.y, manoIzqAim.z);
            Girar("columna", columnaAim.x, columnaAim.y, columnaAim.z);
            Girar("cabeza", cabezaAim.x, cabezaAim.y, cabezaAim.z);
        }

        private void MedirVelocidad(float dt)
        {
            Vector3 pos = _raizActor.position;
            Vector3 delta = pos - _ultimaPos;
            float v = dt > 0f ? delta.magnitude / dt : 0f;
            // La fase del clip avanza con la distancia, no con el reloj: asi la
            // zancada coincide con el avance real y el pie de apoyo no patina.
            delta.y = 0f;
            _distanciaLocomocion += Mathf.Min(delta.magnitude, 1.5f);
            _ultimaPos = pos;
            // Suavizado: la posicion la fija Terrain3D.Altura cada cuadro y sale a saltos.
            _velocidadSuave = Mathf.Lerp(_velocidadSuave, v, Mathf.Clamp01(dt * 9f));
        }

        // ------------------------------------------------------------------ humanoide

        private void AplicarHumanoide()
        {
            float f = Mathf.Min(frecuenciaMax, _velocidadSuave * frecuenciaPorVelocidad);
            float w = _t * f * Mathf.PI * 2f + _fase;
            // Mezcla continua en vez de un umbral seco: antes el paso aparecia de golpe al
            // pasar 0,35 y al frenar en contacto melee las piernas quedaban congeladas.
            float paso = Mathf.Clamp01((f - 0.15f) / 0.5f);
            float quieto = 1f - paso;

            float s = Mathf.Sin(w);
            float c = Mathf.Cos(w);
            float r = Mathf.Sin(_t * 1.9f + _fase);

            // --- piernas: si hay clips horneados de Blender se muestrean; si no,
            // queda el ciclo procedural de siempre. Los brazos y el torso siguen
            // procedurales para no pisar la pose de apuntar.
            bool usoClip = paso > 0.01f && AplicarClipPiernas(paso);
            if (!usoClip)
            {
                float vueloI = Mathf.Clamp01(Mathf.Cos(w) * 0.5f + 0.5f);
                float vueloD = Mathf.Clamp01(-Mathf.Cos(w) * 0.5f + 0.5f);
                Girar("muslo_izq", pasoMuslo * s * paso, 0f, 0f);
                Girar("muslo_der", -pasoMuslo * s * paso, 0f, 0f);
                float rodI = -(paso * doblaRodilla * (0.12f + 0.88f * vueloI) + quieto * 7f);
                float rodD = -(paso * doblaRodilla * (0.12f + 0.88f * vueloD) + quieto * 7f);
                Girar("pantorrilla_izq", rodI, 0f, 0f);
                Girar("pantorrilla_der", rodD, 0f, 0f);
                Girar("pie_izq", -rodI * 0.55f - paso * 10f * s, 0f, 0f);
                Girar("pie_der", -rodD * 0.55f + paso * 10f * s, 0f, 0f);
            }

            // --- brazos en contrafase con la pierna del mismo lado
            Girar("brazo_izq", -balanceoBrazo * s * paso + derivaBrazos * r * quieto, 0f, 0f);
            Girar("brazo_der", balanceoBrazo * s * paso - derivaBrazos * r * quieto, 0f, 0f);
            Girar("antebrazo_izq",
                -codoFijo * (0.35f + 0.65f * paso) - balanceoBrazo * 0.5f * s * paso, 0f, 0f);
            Girar("antebrazo_der",
                -codoFijo * (0.35f + 0.65f * paso) + balanceoBrazo * 0.5f * s * paso, 0f, 0f);

            // --- cadera y columna giran al reves. Eso es lo que hace que se lea como paso
            // y no como un muñeco deslizandose; el peso ademas se corre de lado.
            if (_cadera != null)
            {
                // La cadera vive bajo un hueso con escala de FBX (~100): sumarle
                // 0,035 "metros" directo la levantaba 3,5 m. Los desplazamientos se
                // convierten de metros a unidades locales con la escala del padre.
                Vector3 escala = _cadera.parent != null ? _cadera.parent.lossyScale : Vector3.one;
                float ex = Mathf.Max(0.0001f, Mathf.Abs(escala.x));
                float ey = Mathf.Max(0.0001f, Mathf.Abs(escala.y));
                Vector3 p = _caderaBind;
                p.y += (Mathf.Abs(c) * reboteCadera * paso
                     + Mathf.Sin(_t * 0.9f + _fase) * 0.008f * quieto) / ey;
                p.x += s * 0.012f * paso / ex;
                _cadera.localPosition = p;
            }
            Girar("cadera", 0f, 7f * s * paso, 0f);
            float carrera = Mathf.Clamp01((_velocidadSuave - 3.5f) / 2.5f);
            Girar("columna", 3.5f * paso + respirar * r * quieto + inclinacionCarrera * carrera,
                -9f * s * paso, 2.6f * c * paso);
            // La cabeza contra-rota para que la mirada no baile con el torso.
            Girar("cabeza", -2f * paso - inclinacionCarrera * 0.45f * carrera,
                4.5f * s * paso + giroCabeza * Mathf.Sin(_t * 0.62f + _fase) * quieto,
                -1.5f * c * paso);
        }

        /// Muestrea el clip horneado de piernas y lo mezcla con el bind por `paso`.
        /// Si falta el JSON o el hueso, devuelve false y el procedural dibuja el paso.
        private bool AplicarClipPiernas(float paso)
        {
            ClipsProcedurales.Clip clip = _velocidadSuave < 3.2f ? _clipCaminar : _clipCarrera;
            if (clip == null) return false;
            // Zancada medida por el horneador (metros de avance por ciclo). 1,57 es
            // el valor del shane actual y queda como respaldo si un JSON viejo no la trae.
            float zancada = clip.stride > 0.1f ? clip.stride : 1.57f;
            float fase = Mathf.Repeat(_distanciaLocomocion / zancada, 1f);
            bool alguno = false;
            if (ClipBone(clip, fase, "muslo_izq", paso)) alguno = true;
            if (ClipBone(clip, fase, "pantorrilla_izq", paso)) alguno = true;
            if (ClipBone(clip, fase, "pie_izq", paso)) alguno = true;
            if (ClipBone(clip, fase, "muslo_der", paso)) alguno = true;
            if (ClipBone(clip, fase, "pantorrilla_der", paso)) alguno = true;
            if (ClipBone(clip, fase, "pie_der", paso)) alguno = true;
            if (_cadera != null && ClipsProcedurales.MuestrearHip(clip, fase, out float hip)
                && Mathf.Abs(hip) > 0.0001f)
            {
                Vector3 p = _caderaBind;
                float ey = _cadera.parent != null ? Mathf.Abs(_cadera.parent.lossyScale.y) : 1f;
                p.y += hip / Mathf.Max(0.0001f, ey);
                _cadera.localPosition = p;
            }
            return alguno;
        }

        private bool ClipBone(ClipsProcedurales.Clip clip, float fase, string nombre, float paso)
        {
            if (!_huesos.TryGetValue(nombre, out Hueso h) || h.t == null) return false;
            if (!ClipsProcedurales.Muestrear(clip, fase, nombre, out Quaternion q)) return false;
            Quaternion objetivo = h.bind * q;
            h.t.localRotation = Quaternion.Slerp(h.bind, objetivo, paso);
            return true;
        }

        /// Aplica un clip de una sola pasada mezclando desde la pose actual. El peso
        /// deja entrar/salir al procedural y el canal `hip` se convierte de metros a
        /// unidades locales con la escala del padre, igual que el ciclo de piernas.
        private void AplicarClipCompleto(ClipsProcedurales.Clip clip, float progreso,
            float peso, bool incluirHip)
        {
            if (clip == null || peso <= 0.0001f) return;
            float w = Mathf.Clamp01(peso);
            foreach (KeyValuePair<string, Hueso> kv in _huesos)
            {
                Hueso h = kv.Value;
                if (h.t == null) continue;
                if (!ClipsProcedurales.MuestrearUnaVez(clip, progreso, kv.Key, out Quaternion q))
                    continue;
                h.t.localRotation = Quaternion.Slerp(h.t.localRotation, h.bind * q, w);
            }
            if (!incluirHip || _cadera == null) return;
            if (!ClipsProcedurales.MuestrearHipUnaVez(clip, progreso, out float hip)) return;
            float ey = _cadera.parent != null ? Mathf.Abs(_cadera.parent.lossyScale.y) : 1f;
            Vector3 p = _cadera.localPosition;
            p.y += hip / Mathf.Max(0.0001f, ey) * w;
            _cadera.localPosition = p;
        }

        private void AplicarAtaque()
        {
            float k = Mathf.Clamp01(_tAtaque / durAtaque);
            // Sube rapido y vuelve con ease: si fuera lineal el brazo se ve empujado, no lanzado.
            float env = k < 0.32f ? k / 0.32f : 1f - (k - 0.32f) / 0.68f;
            env = Mathf.SmoothStep(0f, 1f, env);
            // El tiro cargado sacude de verdad; el corto apenas mueve el hombro.
            env *= Mathf.Lerp(0.42f, 1f, _fuerzaAtaque);
            if (apuntar)
            {
                // Retroceso de un tipo que ya apunta: el arma empuja hacia atras y el
                // hombro patina. Levantar el brazo desde aca se veria como un saludo.
                Girar("brazo_der", brazoDerAim.x + 14f * env, brazoDerAim.y, brazoDerAim.z);
                Girar("antebrazo_der", antebrazoDerAim.x - 20f * env, antebrazoDerAim.y, antebrazoDerAim.z);
                Girar("columna", columnaAim.x + 5f * env, columnaAim.y - 7f * env, columnaAim.z);
                Girar("cabeza", cabezaAim.x + 6f * env, cabezaAim.y, cabezaAim.z);
                return;
            }
            Girar("brazo_der", -levantarBrazo * env, 0f, 0f);
            Girar("antebrazo_der", -codoFijo * 0.5f * env, 0f, 0f);
            Girar("mano_der", -12f * env, 0f, 0f);
            Girar("columna", -4f * env, -giroTorso * env, 0f);
            Girar("cabeza", -3f * env, giroTorso * 0.6f * env, 0f);
        }

        private void AplicarImpacto()
        {
            float k = Mathf.Clamp01(_tImpacto / durImpacto);
            float env = Mathf.Sin(k * Mathf.PI);
            if (_clipImpacto != null)
            {
                // El clip ya trae su propio sobre/bajada: se entra y se sale con el
                // mismo envelope para no duplicar el pico ni cortar el gesto.
                AplicarClipCompleto(_clipImpacto, k, env, false);
                return;
            }
            Girar("cabeza", retrocesoCabeza * env, 0f, 0f);
            Girar("columna", retrocesoCabeza * 0.45f * env, 0f, 0f);
        }

        /// La rodada de Shift mueve la raiz 3,6 u sin ninguna lectura visual. Se gira el
        /// hueso `raiz` alrededor de la cadera (no el transform del actor, que mueve la
        /// camara y el collider) y se recoge el cuerpo. La recuperacion devuelve el bind.
        private void AplicarRodada()
        {
            if (_dodge == null) _dodge = GetComponent<PlayerDodge>();
            if (_dodge == null || _rig == null) return;
            bool rodando = _dodge.Rodando;
            bool recuperando = _dodge.Expuesto;
            if (!rodando && !recuperando) { AnguloRodada = 0f; return; }

            float k = rodando ? _dodge.ProgresoRodada : 1f;
            float tuck = rodando
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.25f))
                : 1f - Mathf.SmoothStep(0f, 1f, _dodge.ProgresoRecuperacion);

            if (rodando)
            {
                Vector3 rumbo = _dodge.RumboRodada;
                rumbo.y = 0f;
                if (rumbo.sqrMagnitude < 0.0001f) rumbo = _raizActor.forward;
                rumbo.Normalize();
                Vector3 eje = Vector3.Cross(Vector3.up, rumbo);
                if (eje.sqrMagnitude > 0.0001f && _modeloVisual != null)
                {
                    float theta = rodadaGiro * Mathf.SmoothStep(0f, 1f, k);
                    AnguloRodada = theta;
                    Quaternion rMundo = Quaternion.AngleAxis(theta, eje.normalized);
                    Quaternion rLocal = Quaternion.Inverse(_raizActor.rotation) * rMundo * _raizActor.rotation;
                    Vector3 pivote = _pivoteRodadaLocal;
                    Vector3 despegue = Vector3.up * (rodadaElevacion * Mathf.Sin(k * Mathf.PI));
                    _modeloVisual.localPosition = pivote + rLocal * (_modeloBindPos - pivote) + despegue;
                    _modeloVisual.localRotation = rLocal * _modeloBindRot;
                }
            }

            float t = tuck * rodadaTuck;
            Girar("muslo_izq", -78f * t, 0f, 0f);
            Girar("muslo_der", -70f * t, 0f, 0f);
            Girar("pantorrilla_izq", -105f * t, 0f, 0f);
            Girar("pantorrilla_der", -98f * t, 0f, 0f);
            Girar("columna", 46f * t, 0f, 0f);
            Girar("cabeza", 30f * t, 0f, 0f);
            Girar("brazo_izq", 20f * t, 0f, 0f);
            Girar("brazo_der", 18f * t, 0f, 0f);
            Girar("antebrazo_izq", -70f * t, 0f, 0f);
            Girar("antebrazo_der", -55f * t, 0f, 0f);
        }

        private void AplicarMuerte()
        {
            // Si el golpe llega a mitad de una rodada, la raiz quedo girada y con
            // posicion de pivote: se vuelve al bind antes de armar el colapso.
            AnguloRodada = 0f;
            ResetearTodo();
            float k = Mathf.Clamp01(_tMuerte / durMuerte);
            float env = Mathf.SmoothStep(0f, 1f, k);
            if (_clipMuerte != null)
            {
                AplicarClipCompleto(_clipMuerte, env, env, true);
                return;
            }
            Girar("columna", 34f * env, 8f * env, 0f);
            Girar("cabeza", 22f * env, 0f, 6f * env);
            Girar("muslo_izq", -46f * env, 0f, 0f);
            Girar("muslo_der", -38f * env, 0f, 0f);
            Girar("pantorrilla_izq", -62f * env, 0f, 0f);
            Girar("pantorrilla_der", -54f * env, 0f, 0f);
            Girar("brazo_izq", 26f * env, 0f, 18f * env);
            Girar("brazo_der", 20f * env, 0f, -22f * env);
            if (_cadera != null)
            {
                // Cae sobre su propia altura de cadera: si el modelo mide 1,9 la pierna
                // no tiene a donde ir, asi que la sentada es proporcional.
                Vector3 p = _caderaBind;
                p.y -= _caderaBind.y * caidaCadera * env;
                _cadera.localPosition = p;
            }
        }

        // ------------------------------------------------------------------ babosa

        private void AplicarBabosa()
        {
            float f = Mathf.Min(2.6f, 0.9f + _velocidadSuave * 0.8f);
            float w = _t * f + _fase;
            // Onda que viaja de la cola a la cabeza: cada tramo va retrasado del anterior.
            Girar("cola", Mathf.Sin(w) * 7f, Mathf.Cos(w) * 5f, 0f);
            Girar("cuerpo", Mathf.Sin(w - 0.9f) * 8f, Mathf.Cos(w - 0.9f) * 6f, 0f);
            Girar("pecho", Mathf.Sin(w - 1.8f) * 9f, Mathf.Cos(w - 1.8f) * 6f, 0f);
            Girar("cabeza", Mathf.Sin(w - 2.6f) * 11f, Mathf.Cos(w - 2.6f) * 8f, 0f);
            Girar("antena_0", Mathf.Sin(w * 1.7f) * 12f, 0f, Mathf.Cos(w * 1.3f) * 9f);
            Girar("antena_1", Mathf.Sin(w * 1.7f + 1.1f) * 12f, 0f, Mathf.Cos(w * 1.3f + 1.1f) * 9f);
        }
    }
}
