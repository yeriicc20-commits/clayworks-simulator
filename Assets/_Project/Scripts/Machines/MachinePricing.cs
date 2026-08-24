using UnityEngine;
using System.Collections.Generic;

// El precio por jugada de una maquina. Es la fuente unica: lo leen el cartel de
// la maquina, el ordenador y los NPC cuando pagan.
public class MachinePricing : MonoBehaviour
{
    public static readonly List<MachinePricing> All = new List<MachinePricing>();

    [Header("Precio")]
    public float price = 5f;
    public float minPrice = 0.5f;
    public float maxPrice = 30f;

    [Header("Referencias del mercado")]
    [Tooltip("Lo que costaria de forma sensata segun el premio que da.")]
    public float recommendedPrice = 5f;
    [Tooltip("Lo que cobran los locales de al lado. Varia cada dia.")]
    public float competitionPrice = 5f;
    public float competitionSwing = 2f;

    private ClawController machine;

    public ClawController Machine
    {
        get
        {
            if (machine == null) machine = GetComponentInParent<ClawController>();

            return machine;
        }
    }

    public string MachineName
    {
        get
        {
            Transform root = transform.root;

            return root != null ? root.name : gameObject.name;
        }
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);

        RollCompetition();
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    // Cada maquina de la calle pone un precio parecido, no identico.
    public void RollCompetition()
    {
        float swing = Random.Range(-competitionSwing, competitionSwing);

        // A multiplos de 5 centimos, como los precios de verdad.
        competitionPrice = Round(Mathf.Max(minPrice, recommendedPrice + swing));
    }

    public void SetPrice(float value)
    {
        price = Round(Mathf.Clamp(value, minPrice, maxPrice));
    }

    public static float Round(float value)
    {
        return Mathf.Round(value * 20f) / 20f;
    }

    [Header("Demanda")]
    [Tooltip("Cuanta clientela pierdes por cada euro de mas sobre la competencia.")]
    [Range(0f, 0.5f)] public float lossPerExtraEuro = 0.15f;

    // Probabilidad de que un cliente acepte pagar. Por debajo de la competencia
    // juegan todos; a partir de ahi se va cayendo.
    public float AcceptanceChance
    {
        get
        {
            float over = price - competitionPrice;

            if (over <= 0f) return 1f;

            return Mathf.Clamp01(1f - over * lossPerExtraEuro);
        }
    }

    // Que tal esta el precio comparado con lo que cobran al lado. Sirve para
    // avisar al jugador y para que los NPC decidan si juegan.
    public string PriceOpinion
    {
        get
        {
            if (price <= competitionPrice - 2f) return "<color=#4CAF50>Barato</color>";
            if (price >= competitionPrice + 3f) return "<color=#E53935>Caro</color>";

            return "<color=#FFB300>En mercado</color>";
        }
    }

    // Busca el precio de una maquina concreta, con un valor por defecto sensato.
    public static MachinePricing For(ClawController target)
    {
        if (target == null) return null;

        foreach (MachinePricing pricing in All)
        {
            if (pricing != null && pricing.Machine == target) return pricing;
        }

        return null;
    }
}
