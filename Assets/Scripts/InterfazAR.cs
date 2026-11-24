using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Interfaz en pantalla: título, nombre y matrícula arriba a la derecha,
// aviso del marcador, mensajes y los botones del show.
public class InterfazAR : MonoBehaviour
{
    public ShowCripta m_Show;

    private Font m_Fuente;
    private Text m_Estado, m_Mensaje;
    private GameObject m_Botones;
    private Text m_TxtBailar, m_TxtDormir, m_TxtMusica;
    private Button m_BtnSaludar;
    private float m_RelojMensaje;

    private void Awake()
    {
        m_Fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasGO = new GameObject("Interfaz");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var escala = canvasGO.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);
        escala.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        var t = canvasGO.transform;

        // Título y nombre (esquina superior derecha, como pide la tarea)
        Texto(t, "LA CRIPTA EN TU MESA", 46, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30, -25), new Vector2(900, 70)).color = new Color(1f, 0.82f, 0.3f);
        Texto(t, "Edwin Flete - 1-22-1805", 40, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-30, -25), new Vector2(800, 60)).color = new Color(1f, 0.85f, 0.35f);

        m_Estado = Texto(t, "Iniciando cámara...", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1400, 80));
        m_Mensaje = Texto(t, "", 52, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1400, 80));
        m_Mensaje.color = new Color(0.75f, 1f, 0.75f);

        // Botones abajo
        m_Botones = new GameObject("Botones", typeof(RectTransform));
        m_Botones.transform.SetParent(t, false);
        var rb = m_Botones.GetComponent<RectTransform>();
        rb.anchorMin = rb.anchorMax = new Vector2(0.5f, 0f);
        rb.anchoredPosition = new Vector2(0, 90);
        m_TxtBailar = Boton(m_Botones.transform, "BAILAR", new Vector2(-645, 0), () => m_Show.Bailar());
        m_BtnSaludar = Boton(m_Botones.transform, "SALUDAR", new Vector2(-215, 0), () => m_Show.Saludar()).GetComponentInParent<Button>();
        m_TxtDormir = Boton(m_Botones.transform, "DORMIR", new Vector2(215, 0), () => m_Show.DormirODespertar());
        m_TxtMusica = Boton(m_Botones.transform, "MÚSICA: SÍ", new Vector2(645, 0), () => m_Show.CambiarMusica());
        m_Botones.SetActive(false);

        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void Update()
    {
        if (m_RelojMensaje > 0f)
        {
            m_RelojMensaje -= Time.deltaTime;
            if (m_RelojMensaje <= 0f) m_Mensaje.text = "";
        }
    }

    // ---------- API ----------

    public void Estado(string texto) => m_Estado.text = texto;

    public void Mensaje(string texto, float duracion = 2.5f)
    {
        m_Mensaje.text = texto;
        m_RelojMensaje = duracion;
    }

    public void MostrarBotones(bool ver) => m_Botones.SetActive(ver);

    // Cambia el texto de los botones según lo que esté pasando
    public void Actualizar()
    {
        m_TxtBailar.text = m_Show.Bailando ? "PARAR" : "BAILAR";
        m_TxtDormir.text = m_Show.Dormido ? "DESPERTAR" : "DORMIR";
        m_TxtMusica.text = m_Show.MusicaActiva ? "MÚSICA: SÍ" : "MÚSICA: NO";
        m_BtnSaludar.interactable = !m_Show.Bailando && !m_Show.Dormido;
    }

    // ---------- Ayudantes ----------

    private Text Texto(Transform padre, string texto, int tamano, TextAnchor alineacion, Vector2 ancla, Vector2 pos, Vector2 caja)
    {
        var go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = ancla;
        rt.pivot = ancla;
        rt.anchoredPosition = pos;
        rt.sizeDelta = caja;
        var t = go.AddComponent<Text>();
        t.font = m_Fuente; t.text = texto; t.fontSize = tamano; t.fontStyle = FontStyle.Bold;
        t.alignment = alineacion; t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var sombra = go.AddComponent<Outline>();
        sombra.effectColor = new Color(0, 0, 0, 0.85f); sombra.effectDistance = new Vector2(3, -3);
        return t;
    }

    private Text Boton(Transform padre, string texto, Vector2 pos, UnityEngine.Events.UnityAction accion)
    {
        var go = new GameObject("Boton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(400, 120);
        go.GetComponent<Image>().color = new Color(0.35f, 0.1f, 0.1f, 0.9f);
        var b = go.GetComponent<Button>();
        var col = b.colors; col.highlightedColor = new Color(1f, 0.7f, 0.7f); col.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); b.colors = col;
        b.onClick.AddListener(accion);
        b.onClick.AddListener(() => m_Show.Sonar("sfx_boton", 0.6f));
        var txt = Texto(go.transform, texto, 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, rt.sizeDelta);
        txt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        return txt;
    }
}
