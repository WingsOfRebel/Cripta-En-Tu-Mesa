using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// El "show" de la cripta: el esqueleto sale de la tierra cuando aparece el marcador
// y luego baila, saluda, se duerme o reacciona si lo tocas.
public class ShowCripta : MonoBehaviour
{
    public BailarinEsqueleto m_Lider;
    public BailarinEsqueleto[] m_Coro;          // esqueletos que se unen al baile
    public AudioSource m_Musica;
    public AudioSource m_Efectos;
    public InterfazAR m_Interfaz;

    // Movimientos del baile, en orden (nombres de estados del Animator)
    public string[] m_Coreografia = { "Animar", "Saltar", "Provocar", "Saludar", "Lagartijas", "ProvocarLargo", "Saltar", "Animar" };

    private enum Modo { Esperando, Apareciendo, Libre, Bailando, Dormido }
    private Modo m_Modo = Modo.Esperando;
    private float m_Reloj;
    private int m_Paso;
    private bool m_CoroFuera;
    private float m_RelojCoro = -1f;
    private readonly Dictionary<string, AudioClip> m_Sonidos = new Dictionary<string, AudioClip>();

    public bool Dormido => m_Modo == Modo.Dormido;
    public bool Bailando => m_Modo == Modo.Bailando;
    public bool MusicaActiva => m_Musica != null && !m_Musica.mute;

    private void Awake()
    {
        foreach (var c in Resources.LoadAll<AudioClip>("Audio")) m_Sonidos[c.name] = c;
        m_Lider.gameObject.SetActive(false);
        foreach (var c in m_Coro) c.gameObject.SetActive(false);
        if (m_Musica != null && m_Sonidos.TryGetValue("musica_baile", out var m)) { m_Musica.clip = m; m_Musica.loop = true; }
    }

    public void Sonar(string nombre, float volumen = 1f)
    {
        if (m_Efectos != null && m_Sonidos.TryGetValue(nombre, out var c)) m_Efectos.PlayOneShot(c, volumen);
    }

    // Lo llama MarcadorAR
    public void MarcadorVisible(bool visible)
    {
        m_Interfaz.Estado(visible ? "" : "Apunta la cámara al marcador");
        if (visible && m_Modo == Modo.Esperando)
        {
            // Primera vez: el esqueleto sale de la tierra
            Sonar("sfx_encontrado");
            Sonar("sfx_tierra");
            m_Lider.gameObject.SetActive(true);
            m_Modo = Modo.Apareciendo;
            m_Reloj = m_Lider.Duracion("Salir") + m_Lider.Duracion("Provocar");
            m_Interfaz.Mensaje("¡El esqueleto despertó!");
        }
    }

    // ---------- Botones ----------

    public void Bailar()
    {
        if (m_Modo == Modo.Esperando || m_Modo == Modo.Apareciendo || m_Modo == Modo.Dormido) return;
        if (m_Modo == Modo.Bailando) { Parar(); return; }
        m_Modo = Modo.Bailando;
        m_Paso = 0;
        m_Reloj = 0f;
        if (m_Musica != null && !m_Musica.isPlaying) m_Musica.Play();
        if (!m_CoroFuera)
        {
            // Los esbirros salen de la tierra para acompañarlo
            m_CoroFuera = true;
            foreach (var c in m_Coro) c.gameObject.SetActive(true);
            Sonar("sfx_tierra", 0.7f);
            m_RelojCoro = m_Coro.Length > 0 ? m_Coro[0].Duracion("Salir") + 0.2f : -1f;
        }
        m_Interfaz.Actualizar();
    }

    private void Parar()
    {
        m_Modo = Modo.Libre;
        m_Lider.Hacer("Quieto");
        foreach (var c in m_Coro) if (c.gameObject.activeSelf && !EnSalida(c)) c.Hacer("Quieto");
        if (m_Musica != null) m_Musica.Pause();
        m_Interfaz.Actualizar();
    }

    public void Saludar()
    {
        if (m_Modo != Modo.Libre) return;
        m_Lider.Hacer("Saludar");
        Sonar("sfx_hueso", 0.5f);
    }

    public void DormirODespertar()
    {
        if (m_Modo == Modo.Bailando) Parar();
        if (m_Modo == Modo.Libre)
        {
            m_Modo = Modo.Dormido;
            m_Lider.Hacer("Morir", "Tumbado");
            foreach (var c in m_Coro) if (c.gameObject.activeSelf) c.Hacer("Morir", "Tumbado");
            Sonar("sfx_dormir");
            m_Interfaz.Mensaje("Zzz...");
        }
        else if (m_Modo == Modo.Dormido)
        {
            m_Modo = Modo.Apareciendo;
            m_Reloj = m_Lider.Hacer("Revivir");
            foreach (var c in m_Coro) if (c.gameObject.activeSelf) c.Hacer("Revivir");
            Sonar("sfx_tierra", 0.6f);
            m_Interfaz.Mensaje("¡Ha vuelto a la vida!");
        }
        m_Interfaz.Actualizar();
    }

    public void CambiarMusica()
    {
        if (m_Musica == null) return;
        m_Musica.mute = !m_Musica.mute;
        m_Interfaz.Actualizar();
    }

    // ---------- Bucle ----------

    private void Update()
    {
        if (m_Modo == Modo.Apareciendo)
        {
            m_Reloj -= Time.deltaTime;
            if (m_Reloj <= 0f) { m_Modo = Modo.Libre; m_Interfaz.MostrarBotones(true); m_Interfaz.Actualizar(); }
        }
        else if (m_Modo == Modo.Bailando)
        {
            // Espera a que los esbirros terminen de salir antes de que bailen juntos
            if (m_RelojCoro > 0f) m_RelojCoro -= Time.deltaTime;
            m_Reloj -= Time.deltaTime;
            if (m_Reloj <= 0f)
            {
                string paso = m_Coreografia[m_Paso % m_Coreografia.Length];
                m_Paso++;
                m_Reloj = m_Lider.Hacer(paso, null, 0.2f);
                if (m_RelojCoro <= 0f)
                    foreach (var c in m_Coro) c.Hacer(paso, null, 0.2f);
                if (paso == "Saltar") Sonar("sfx_hueso", 0.4f);
            }
        }
        RevisarToque();
    }

    private bool EnSalida(BailarinEsqueleto b) => b.Actual == "Salir" && b.Ocupado;

    // Tocar al esqueleto en la pantalla: se queja
    private void RevisarToque()
    {
        var p = Pointer.current;
        if (p == null || !p.press.wasPressedThisFrame || m_Modo != Modo.Libre) return;
        Vector2 pos = p.position.ReadValue();
        if (SobreUI(pos)) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (Physics.Raycast(cam.ScreenPointToRay(pos), out RaycastHit hit, 10f) && hit.collider.GetComponentInParent<BailarinEsqueleto>() == m_Lider)
        {
            m_Lider.Hacer("Golpeado");
            Sonar("sfx_hueso");
            m_Interfaz.Mensaje("¡Ay! ¡Mis huesos!");
        }
    }

    private static bool SobreUI(Vector2 pos)
    {
        if (EventSystem.current == null) return false;
        var datos = new PointerEventData(EventSystem.current) { position = pos };
        var resultados = new List<RaycastResult>();
        EventSystem.current.RaycastAll(datos, resultados);
        return resultados.Count > 0;
    }
}
