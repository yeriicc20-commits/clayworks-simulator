using UnityEngine;

public class PlushMachine : MonoBehaviour
{
    public int cost = 1;
    public float winChance = 0.4f;

    private bool playerInRange = false;

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            TryPlay();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Algo ha entrado en el trigger: " + other.name);

        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            InteractionUI.Prompt("Pulsa E para jugar (" + cost + "€)");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            InteractionUI.Hide();
        }
    }

    void TryPlay()
    {
        bool paid = GameManager.Instance.SpendMoney(cost);

        if (!paid)
        {
            Debug.Log("No tienes suficiente dinero");
            return;
        }

        float roll = Random.Range(0f, 1f);

        if (roll <= winChance)
        {
            Debug.Log("Has ganado un peluche!");
        }
        else
        {
            Debug.Log("No ha caido nada esta vez");
        }
    }
}