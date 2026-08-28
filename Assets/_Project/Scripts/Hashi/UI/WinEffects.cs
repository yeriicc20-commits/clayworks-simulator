using System.Collections;
using TMPro;
using UnityEngine;

namespace Hashi
{
    // La fiesta al ganar: confeti, luces y el cartel latiendo.
    //
    // Va aparte de la interfaz porque son cosas distintas: la interfaz tiene que
    // funcionar siempre y esto es adorno. Si un dia molesta, se apaga el objeto
    // entero y el juego sigue jugandose igual.
    public class WinEffects : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] GameManager juego;

        [Tooltip("Confeti. Se dispara una vez por victoria.")]
        [SerializeField] ParticleSystem confeti;

        [Tooltip("El cartel de YOU WIN. Late mientras dura la celebracion.")]
        [SerializeField] TMP_Text cartel;

        [Tooltip("Luces del interior que se ponen a parpadear.")]
        [SerializeField] Light[] luces;

        [Header("Ajustes")]
        [Range(0.5f, 10f)] [SerializeField] float duracion = 3.5f;

        [Range(0.5f, 10f)] [SerializeField] float latidosPorSegundo = 2.5f;

        [Range(1f, 2f)] [SerializeField] float escalaLatido = 1.15f;

        [Tooltip("Cuanto suben las luces durante la fiesta.")]
        [Range(1f, 6f)] [SerializeField] float multiplicadorLuz = 2.2f;

        float[] intensidadOriginal;
        Vector3 escalaOriginal = Vector3.one;
        Coroutine fiesta;

        void Awake()
        {
            if (cartel != null) escalaOriginal = cartel.transform.localScale;

            if (luces != null)
            {
                intensidadOriginal = new float[luces.Length];
                for (int i = 0; i < luces.Length; i++)
                {
                    if (luces[i] != null) intensidadOriginal[i] = luces[i].intensity;
                }
            }
        }

        void OnEnable()
        {
            if (juego != null) juego.AlGanar += Celebrar;
        }

        void OnDisable()
        {
            if (juego != null) juego.AlGanar -= Celebrar;

            // Si se apaga a mitad de fiesta, las luces se quedarian al doble
            // para siempre.
            Restaurar();
        }

        public void Celebrar()
        {
            if (fiesta != null) StopCoroutine(fiesta);
            fiesta = StartCoroutine(Fiesta());
        }

        IEnumerator Fiesta()
        {
            if (confeti != null)
            {
                confeti.Clear();
                confeti.Play();
            }

            float fin = Time.time + duracion;

            while (Time.time < fin)
            {
                float t = Mathf.PingPong(Time.time * latidosPorSegundo, 1f);

                if (cartel != null)
                {
                    cartel.transform.localScale =
                        escalaOriginal * Mathf.Lerp(1f, escalaLatido, t);
                }

                if (luces != null && intensidadOriginal != null)
                {
                    for (int i = 0; i < luces.Length; i++)
                    {
                        if (luces[i] == null) continue;

                        luces[i].intensity = Mathf.Lerp(
                            intensidadOriginal[i],
                            intensidadOriginal[i] * multiplicadorLuz, t);
                    }
                }

                yield return null;
            }

            Restaurar();
            fiesta = null;
        }

        void Restaurar()
        {
            if (cartel != null) cartel.transform.localScale = escalaOriginal;

            if (luces == null || intensidadOriginal == null) return;

            for (int i = 0; i < luces.Length && i < intensidadOriginal.Length; i++)
            {
                if (luces[i] != null) luces[i].intensity = intensidadOriginal[i];
            }
        }
    }
}
