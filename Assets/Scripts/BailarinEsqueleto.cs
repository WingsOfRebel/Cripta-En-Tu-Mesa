using System.Collections.Generic;
using UnityEngine;

// Controla las animaciones de un esqueleto. Cada movimiento dura lo que dura su animación
// y al terminar pasa al estado que se le indique (por defecto "Quieto").
[RequireComponent(typeof(Animator))]
public class BailarinEsqueleto : MonoBehaviour
{
    // Los llena la herramienta del editor con los estados del Animator y su duración
    public string[] m_Estados;
    public float[] m_Duraciones;

    public bool Ocupado => m_Reloj > 0f;
    public string Actual { get; private set; } = "Salir";

    private Animator m_Anim;
    private float m_Reloj;
    private string m_Siguiente;
    private readonly Dictionary<string, float> m_Tabla = new Dictionary<string, float>();

    private void Awake()
    {
        m_Anim = GetComponent<Animator>();
        for (int i = 0; i < m_Estados.Length; i++) m_Tabla[m_Estados[i]] = m_Duraciones[i];
    }

    private void OnEnable()
    {
        // Al aparecer empieza saliendo de la tierra
        Actual = "Salir";
        m_Reloj = Duracion("Salir");
        m_Siguiente = "Provocar";
    }

    public float Duracion(string estado) => m_Tabla.TryGetValue(estado, out float d) ? d : 1f;

    // Hace un movimiento y devuelve cuánto dura
    public float Hacer(string estado, string despues = "Quieto", float mezcla = 0.15f)
    {
        if (m_Anim == null) return 0f;
        m_Anim.CrossFadeInFixedTime(estado, mezcla);
        Actual = estado;
        m_Reloj = Duracion(estado);
        m_Siguiente = despues;
        return m_Reloj;
    }

    private void Update()
    {
        if (m_Reloj <= 0f) return;
        m_Reloj -= Time.deltaTime;
        if (m_Reloj <= 0f && !string.IsNullOrEmpty(m_Siguiente))
        {
            string s = m_Siguiente;
            m_Siguiente = s == "Provocar" ? "Quieto" : null;   // después de salir: provoca y luego queda quieto
            m_Anim.CrossFadeInFixedTime(s, 0.2f);
            Actual = s;
            m_Reloj = m_Siguiente != null ? Duracion(s) : 0f;
        }
    }
}
