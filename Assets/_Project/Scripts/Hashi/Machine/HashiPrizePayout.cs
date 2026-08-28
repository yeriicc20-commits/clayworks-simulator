using System.Collections;
using UnityEngine;

namespace Hashi
{
    // Lo que pasa cuando la caja cae, pero dentro del local: cobrar el premio y
    // reponer la maquina.
    //
    // Va aparte de DropZone a proposito. DropZone solo sabe de fisica: mira si
    // la caja ha caido de verdad y lo dice. Todo lo que tenga que ver con dinero
    // vive aqui, y por eso la misma maquina puede jugarse en su escena de
    // pruebas sin que exista ninguna economia.
    public class HashiPrizePayout : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] DropZone bandeja;
        [SerializeField] PrizeSpawner generador;

        [Header("Premio")]
        [Tooltip("Lo que vale la caja que cae. Con el precio por partida a 5 y "
                 + "esto a 15, la maquina sale rentable si se gana una de cada "
                 + "tres, que es mas o menos lo que cuesta.")]
        [SerializeField] float valorPremio = 15f;

        [Header("Reponer")]
        [Tooltip("Pone otra caja pasado este rato.")]
        [SerializeField] bool reponer = true;

        [Range(1f, 20f)] [SerializeField] float esperaReponer = 5f;

        void OnEnable()
        {
            if (bandeja != null) bandeja.AlRecogerPremio += Cobrar;
        }

        void OnDisable()
        {
            if (bandeja != null) bandeja.AlRecogerPremio -= Cobrar;
        }

        void Cobrar(PrizeController premio)
        {
            // El del local, no el de la maquina de puente: los dos se llaman
            // GameManager.
            if (global::GameManager.Instance != null)
            {
                global::GameManager.Instance.AddMoney(valorPremio);
            }

            LevelManager niveles = LevelManager.EnsureExists();
            if (niveles != null) niveles.Add(niveles.xpPrizeSold);

            if (NotificationManager.Instance != null)
            {
                NotificationManager.Instance.ShowMessage(
                    "Premio conseguido: " + global::GameManager.Format(valorPremio));
            }

            if (reponer) StartCoroutine(Reponer());
        }

        IEnumerator Reponer()
        {
            yield return new WaitForSeconds(esperaReponer);

            if (generador == null) yield break;

            generador.Quitar();
            generador.GenerarAleatorio();
        }
    }
}
