using UnityEngine;

// Hace que la luz de una antorcha parpadee un poco, como fuego de verdad.
[RequireComponent(typeof(Light))]
public class Parpadeo : MonoBehaviour
{
    public float m_Variacion = 0.35f;

    private Light m_Luz;
    private float m_Base;
    private float m_Semilla;

    private void Awake()
    {
        m_Luz = GetComponent<Light>();
        m_Base = m_Luz.intensity;
        m_Semilla = Random.value * 100f;
    }

    private void Update()
    {
        float ruido = Mathf.PerlinNoise(m_Semilla, Time.time * 4f);
        m_Luz.intensity = m_Base * (1f - m_Variacion + ruido * m_Variacion * 2f);
    }
}
