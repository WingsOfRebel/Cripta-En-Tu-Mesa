using UnityEngine;
using UnityEngine.InputSystem;
using Vuforia;

// Crea el marcador (Image Target) de Vuforia a partir de una imagen al iniciar,
// pone el escenario encima y lo muestra u oculta según si la cámara ve el marcador.
public class MarcadorAR : MonoBehaviour
{
    public Texture2D m_Imagen;              // la imagen del marcador (Resources/Marcador)
    public float m_AnchoMetros = 0.2f;      // ancho real del marcador impreso o en pantalla
    public Transform m_Escenario;           // la cripta que aparece encima
    public ShowCripta m_Show;

    [Header("Prueba en PC sin marcador (tecla P)")]
    public float m_DistanciaPrueba = 0.45f;

    public bool Visible { get; private set; }

    private ObserverBehaviour m_Objetivo;
    private Renderer[] m_Renderers;
    private Light[] m_Luces;

    private void Start()
    {
        m_Renderers = m_Escenario.GetComponentsInChildren<Renderer>(true);
        m_Luces = m_Escenario.GetComponentsInChildren<Light>(true);
        MostrarEscenario(false);
        VuforiaApplication.Instance.OnVuforiaStarted += CrearMarcador;
    }

    private void OnDestroy()
    {
        if (VuforiaApplication.Instance != null) VuforiaApplication.Instance.OnVuforiaStarted -= CrearMarcador;
        if (m_Objetivo != null) m_Objetivo.OnTargetStatusChanged -= CambioDeEstado;
    }

    private void CrearMarcador()
    {
        if (m_Objetivo != null) return;
        m_Objetivo = VuforiaBehaviour.Instance.ObserverFactory.CreateImageTarget(m_Imagen, m_AnchoMetros, "CriptaEnTuMesa");
        if (m_Objetivo == null)
        {
            Debug.LogError("AR: no se pudo crear el marcador. Revisa que la imagen tenga Read/Write activado.");
            return;
        }
        // El escenario queda pegado al marcador
        m_Escenario.SetParent(m_Objetivo.transform, false);
        m_Escenario.localPosition = Vector3.zero;
        m_Escenario.localRotation = Quaternion.identity;
        m_Objetivo.OnTargetStatusChanged += CambioDeEstado;
        if (m_Show != null) m_Show.MarcadorVisible(false);   // muestra "Apunta la cámara al marcador"
        Debug.Log("AR: marcador creado (" + m_AnchoMetros + " m de ancho)");
    }

    private void CambioDeEstado(ObserverBehaviour objetivo, TargetStatus estado)
    {
        bool ve = estado.Status == Status.TRACKED || estado.Status == Status.EXTENDED_TRACKED;
        if (ve == Visible) return;
        Visible = ve;
        MostrarEscenario(ve);
        if (m_Show != null) m_Show.MarcadorVisible(ve);
    }

    // Modo de prueba: con la tecla P la cripta aparece frente a la cámara sin necesitar el marcador
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.pKey.wasPressedThisFrame || Visible) return;
        var cam = Camera.main.transform;
        m_Escenario.SetParent(null, true);
        m_Escenario.position = cam.position + cam.forward * m_DistanciaPrueba - cam.up * 0.12f;
        m_Escenario.rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f) * Quaternion.Euler(-35f, 0f, 0f);
        Visible = true;
        MostrarEscenario(true);
        if (m_Show != null) m_Show.MarcadorVisible(true);
        Debug.Log("AR: modo de prueba sin marcador");
    }

    // Se ocultan los dibujos y luces (no el objeto) para que las animaciones no se reinicien
    private void MostrarEscenario(bool ver)
    {
        foreach (var r in m_Renderers) if (r != null) r.enabled = ver;
        foreach (var l in m_Luces) if (l != null) l.enabled = ver;
    }
}
