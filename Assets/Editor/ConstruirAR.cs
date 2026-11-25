using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Vuforia;

// Herramienta del editor: menú "AR > Construir todo".
// Configura los modelos, crea materiales y animaciones, arma el escenario de la cripta
// sobre el marcador, crea la escena "01 Cripta AR" y prepara los ajustes de Android.
public static class ConstruirAR
{
    const string Modelos = "Assets/Modelos";
    const string Generado = "Assets/Generado";
    const string Plantilla = "Assets/Scenes/SampleScene.unity";
    const string Escena = "Assets/Scenes/01 Cripta AR.unity";
    const string Marcador = "Assets/Resources/Marcador.png";
    const float Escala = 0.03f;          // 1 metro del juego = 3 cm sobre la mesa (la cripta mide unos 24 cm)
    const float AnchoMarcador = 0.2f;    // el marcador mide 20 cm de ancho

    static readonly StringBuilder s_Reporte = new StringBuilder();
    static Material s_MatMazmorra, s_MatEsqueleto;
    static AnimatorController s_Ctrl;
    static string[] s_Estados;
    static float[] s_Duraciones;

    [MenuItem("AR/Construir todo")]
    public static void ConstruirTodo()
    {
        s_Reporte.Clear();
        if (!File.Exists(Plantilla))
        {
            EditorUtility.DisplayDialog("AR", "No encuentro " + Plantilla + " (la escena de ejemplo de la plantilla).", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Carpeta(Generado);
        ConfigurarModelos();
        ConfigurarMarcador();
        CrearMateriales();
        CrearAnimaciones();
        CrearEscena();
        ConfigurarAndroid();

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Escena, true) };
        AssetDatabase.SaveAssets();
        File.WriteAllText("AR_construccion.txt", s_Reporte.ToString());
        Log("LISTO. Abre la escena 01 Cripta AR y dale a Play (muestra el marcador a la webcam).");
    }

    static void Log(string t) { s_Reporte.AppendLine(t); Debug.Log("AR: " + t); }

    static void Carpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        Carpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    // ---------------- Modelos ----------------

    static bool EsBucle(string clip)
    {
        string c = clip.ToLowerInvariant();
        return c.Contains("idle") || c.Contains("pose");
    }

    static void ConfigurarModelos()
    {
        int cambiados = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Modelos }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var mi = AssetImporter.GetAtPath(ruta) as ModelImporter;
            if (mi == null) continue;
            bool animado = ruta.Contains("/Esqueletos/") || ruta.Contains("/Animaciones/");
            bool conClips = ruta.Contains("/Animaciones/");
            bool cambio = false;

            if (mi.importCameras || mi.importLights) { mi.importCameras = false; mi.importLights = false; cambio = true; }
            var tipo = animado ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            if (mi.animationType != tipo) { mi.animationType = tipo; cambio = true; }
            if (mi.importAnimation != conClips) { mi.importAnimation = conClips; cambio = true; }
            if (cambio) { mi.SaveAndReimport(); cambiados++; mi = AssetImporter.GetAtPath(ruta) as ModelImporter; }

            // Las animaciones de quieto y las poses se repiten en bucle
            if (conClips)
            {
                var clips = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
                bool tocar = mi.clipAnimations.Length == 0;
                foreach (var c in clips)
                {
                    bool bucle = EsBucle(c.name);
                    if (c.loopTime != bucle) { c.loopTime = bucle; tocar = true; }
                }
                if (tocar) { mi.clipAnimations = clips; mi.SaveAndReimport(); cambiados++; }
            }
        }
        Log("Modelos configurados ahora: " + cambiados);
    }

    // Vuforia necesita poder leer los píxeles de la imagen del marcador
    static void ConfigurarMarcador()
    {
        var ti = AssetImporter.GetAtPath(Marcador) as TextureImporter;
        if (ti == null) { Log("AVISO: falta la imagen del marcador " + Marcador); return; }
        if (!ti.isReadable || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled || ti.npotScale != TextureImporterNPOTScale.None)
        {
            ti.isReadable = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.SaveAndReimport();
        }
        Log("Marcador configurado (lectura activada, sin compresión)");
    }

    static GameObject M(string carpeta, string nombre)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Modelos + "/" + carpeta + "/" + nombre + ".fbx");
        if (go == null) Log("AVISO: falta el modelo " + carpeta + "/" + nombre);
        return go;
    }

    // ---------------- Materiales ----------------

    static Material Mat(string nombre, string textura)
    {
        string ruta = Generado + "/" + nombre + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, ruta); }
        m.shader = shader;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(textura);
        if (tex == null) Log("AVISO: falta la textura " + textura);
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void CrearMateriales()
    {
        s_MatMazmorra = Mat("Mazmorra", Modelos + "/Mazmorra/dungeon_texture.png");
        s_MatEsqueleto = Mat("Esqueleto", Modelos + "/Esqueletos/skeleton_texture.png");
        Log("Materiales creados");
    }

    static void PonerMaterial(GameObject go, Material m)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.On;
        }
    }

    // ---------------- Animaciones ----------------

    static AnimationClip Clip(string archivo, string nombre)
    {
        string ruta = Modelos + "/Animaciones/" + archivo + ".fbx";
        var clips = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
        var clip = clips.FirstOrDefault(c => c.name == nombre) ?? clips.FirstOrDefault(c => c.name.EndsWith(nombre));
        if (clip == null) Log("AVISO: no encuentro la animación " + nombre + " en " + archivo + " (hay: " + string.Join(", ", clips.Select(c => c.name)) + ")");
        return clip;
    }

    static void CrearAnimaciones()
    {
        var estados = new (string estado, AnimationClip clip)[]
        {
            ("Salir", Clip("Rig_Medium_Special", "Skeletons_Spawn_Ground")),     // sale de la tierra (estado inicial)
            ("Quieto", Clip("Rig_Medium_Special", "Skeletons_Idle")),
            ("Provocar", Clip("Rig_Medium_Special", "Skeletons_Taunt")),
            ("ProvocarLargo", Clip("Rig_Medium_Special", "Skeletons_Taunt_Longer")),
            ("Animar", Clip("Rig_Medium_Simulation", "Cheering")),
            ("Saludar", Clip("Rig_Medium_Simulation", "Waving")),
            ("Lagartijas", Clip("Rig_Medium_Simulation", "Push_Ups")),
            ("Saltar", Clip("Rig_Medium_MovementBasic", "Jump_Full_Short")),
            ("Golpeado", Clip("Rig_Medium_General", "Hit_A")),
            ("Morir", Clip("Rig_Medium_Special", "Skeletons_Death")),
            ("Tumbado", Clip("Rig_Medium_Special", "Skeletons_Death_Pose")),
            ("Revivir", Clip("Rig_Medium_Special", "Skeletons_Death_Resurrect")),
        };

        string ruta = Generado + "/Esqueleto.controller";
        AssetDatabase.DeleteAsset(ruta);
        s_Ctrl = AnimatorController.CreateAnimatorControllerAtPath(ruta);
        var sm = s_Ctrl.layers[0].stateMachine;
        foreach (var (estado, clip) in estados)
        {
            var st = sm.AddState(estado);
            st.motion = clip;
            if (estado == "Salir") sm.defaultState = st;
        }
        s_Estados = estados.Select(e => e.estado).ToArray();
        s_Duraciones = estados.Select(e => e.clip != null ? e.clip.length : 1f).ToArray();
        Log("Animator Controller creado con " + estados.Length + " estados");
    }

    // ---------------- Escenario ----------------

    static GameObject Poner(GameObject modelo, Vector3 pos, float rotY, Transform padre, float escala = 1f)
    {
        if (modelo == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        go.transform.localScale = Vector3.one * escala;
        PonerMaterial(go, s_MatMazmorra);
        return go;
    }

    // Vela con una luz pequeña que parpadea (el alcance de la luz no se escala solo)
    static void Vela(string modelo, Vector3 pos, Transform padre)
    {
        var v = Poner(M("Mazmorra", modelo), pos, 0f, padre);
        if (v == null) return;
        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.transform.SetParent(v.transform, false);
        luz.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.6f, 0.3f);
        luz.range = 4f * Escala;
        luz.intensity = 0.25f;
        luz.shadows = LightShadows.None;
        luz.gameObject.AddComponent<Parpadeo>();
    }

    static BailarinEsqueleto Esqueleto(string modelo, Vector3 pos, float escala, Transform padre, bool tocable)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(M("Esqueletos", modelo));
        go.name = modelo;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // mira hacia quien sostiene el celular
        go.transform.localScale = Vector3.one * escala;
        PonerMaterial(go, s_MatEsqueleto);
        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();   // (?? no sirve con objetos de Unity)
        anim.runtimeAnimatorController = s_Ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var b = go.AddComponent<BailarinEsqueleto>();
        b.m_Estados = s_Estados;
        b.m_Duraciones = s_Duraciones;
        if (tocable)
        {
            // Para poder tocarlo en la pantalla
            var c = go.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0f, 1f, 0f); c.height = 2.2f; c.radius = 0.6f;
        }
        return b;
    }

    static GameObject CrearEscenario(out BailarinEsqueleto lider, out BailarinEsqueleto[] coro)
    {
        // Todo se arma en metros "del juego" y la raíz se encoge para caber sobre el marcador
        var raiz = new GameObject("Escenario");
        raiz.transform.localScale = Vector3.one * Escala;
        var t = raiz.transform;

        // Piso de 8 x 8 m: losas y tierra donde está la tumba
        Poner(M("Mazmorra", "floor_tile_large"), new Vector3(-2, 0, 2), 0, t);
        Poner(M("Mazmorra", "floor_tile_large"), new Vector3(2, 0, 2), 90, t);
        Poner(M("Mazmorra", "floor_dirt_large"), new Vector3(-2, 0, -2), 0, t);
        Poner(M("Mazmorra", "floor_dirt_large_rocky"), new Vector3(2, 0, -2), 0, t);

        // Fondo: muro bajo con columnas y estandartes
        Poner(M("Mazmorra", "wall_half"), new Vector3(-2, 0, 4), 180, t);
        Poner(M("Mazmorra", "wall_broken"), new Vector3(2, 0, 4), 180, t);
        Poner(M("Mazmorra", "pillar_decorated"), new Vector3(-4, 0, 4), 0, t);
        Poner(M("Mazmorra", "pillar_decorated"), new Vector3(4, 0, 4), 0, t);
        Poner(M("Mazmorra", "pillar"), new Vector3(0, 0, 4), 0, t);
        Poner(M("Mazmorra", "banner_patternA_red"), new Vector3(-2, 0.4f, 3.75f), 180, t);

        // Tesoro a la derecha, barriles y escombros a la izquierda
        Poner(M("Mazmorra", "chest_gold"), new Vector3(3f, 0, 1.6f), -110, t);
        Poner(M("Mazmorra", "coin_stack_large"), new Vector3(2.3f, 0, 2.8f), 30, t);
        Poner(M("Mazmorra", "coin_stack_small"), new Vector3(3.4f, 0, 0.2f), 0, t);
        Poner(M("Mazmorra", "barrel_small"), new Vector3(-3.3f, 0, 1.5f), 20, t);
        Poner(M("Mazmorra", "box_small"), new Vector3(-3.2f, 0, 0.1f), -15, t);
        Poner(M("Mazmorra", "rubble_large"), new Vector3(-2.6f, 0, 3.1f), 40, t);
        Poner(M("Mazmorra", "rubble_half"), new Vector3(1.4f, 0, 3.2f), 0, t);

        // Velas con luz
        Vela("candle_triple", new Vector3(-3.4f, 0, 3.3f), t);
        Vela("candle_triple", new Vector3(3.4f, 0, 3.3f), t);
        Vela("candle_lit", new Vector3(-1.2f, 0, -3.2f), t);
        Vela("candle_lit", new Vector3(1.3f, 0, -3.3f), t);

        // El bailarín principal y su coro de esbirros
        lider = Esqueleto("Skeleton_Warrior", new Vector3(0f, 0f, -0.3f), 1.15f, t, true);
        coro = new[]
        {
            Esqueleto("Skeleton_Minion", new Vector3(-2.2f, 0f, 1.3f), 0.9f, t, false),
            Esqueleto("Skeleton_Minion", new Vector3(2.0f, 0f, 1.0f), 0.9f, t, false),
        };
        return raiz;
    }

    // ---------------- Escena ----------------

    static void CrearEscena()
    {
        AssetDatabase.DeleteAsset(Escena);
        AssetDatabase.CopyAsset(Plantilla, Escena);
        var escena = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);

        // Cámara AR de Vuforia
        var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        cam.gameObject.name = "ARCamera";
        cam.tag = "MainCamera";
        cam.transform.position = Vector3.zero;
        cam.transform.rotation = Quaternion.identity;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 20f;
        if (cam.GetComponent<VuforiaBehaviour>() == null) cam.gameObject.AddComponent<VuforiaBehaviour>();
        if (cam.GetComponent<DefaultInitializationErrorHandler>() == null) cam.gameObject.AddComponent<DefaultInitializationErrorHandler>();

        foreach (var l in Object.FindObjectsByType<Light>())
            if (l.type == LightType.Directional) { l.intensity = 1.1f; l.transform.rotation = Quaternion.Euler(55f, -30f, 0f); l.shadows = LightShadows.Soft; }
        RenderSettings.fog = false;

        var raiz = CrearEscenario(out var lider, out var coro);

        // Objeto que controla todo
        var show = new GameObject("Show");
        var s = show.AddComponent<ShowCripta>();
        var interfaz = show.AddComponent<InterfazAR>();
        var marcador = show.AddComponent<MarcadorAR>();
        s.m_Lider = lider;
        s.m_Coro = coro;
        s.m_Interfaz = interfaz;
        s.m_Musica = show.AddComponent<AudioSource>();
        s.m_Musica.playOnAwake = false; s.m_Musica.volume = 0.6f;
        s.m_Efectos = show.AddComponent<AudioSource>();
        s.m_Efectos.playOnAwake = false;
        interfaz.m_Show = s;
        marcador.m_Imagen = AssetDatabase.LoadAssetAtPath<Texture2D>(Marcador);
        marcador.m_AnchoMetros = AnchoMarcador;
        marcador.m_Escenario = raiz.transform;
        marcador.m_Show = s;

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Log("Escena 01 Cripta AR creada (escenario a escala " + Escala + ", marcador de " + AnchoMarcador + " m)");
    }

    // ---------------- Android ----------------

    static void ConfigurarAndroid()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.companyName = "WingsOfRebel";
        PlayerSettings.productName = "Cripta en tu Mesa";
        PlayerSettings.SetApplicationIdentifier(android, "com.wingsofrebel.criptaentumesa");
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;   // Vuforia pide Android 10 o superior
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        Log("Android: IL2CPP, ARM64, Android 10+, OpenGL ES 3, horizontal");
    }
}
