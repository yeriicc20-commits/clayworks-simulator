using UnityEngine;
using TMPro;
using System.Collections;

public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance;

    public TextMeshProUGUI notificationText;
    public float displayDuration = 2f;

    private Coroutine currentRoutine;

    void Awake()
    {
        Instance = this;
    }

    public void ShowMessage(string message)
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
        }

        currentRoutine = StartCoroutine(DisplayRoutine(message));
    }

    IEnumerator DisplayRoutine(string message)
    {
        notificationText.text = message;
        notificationText.gameObject.SetActive(true);

        yield return new WaitForSeconds(displayDuration);

        notificationText.gameObject.SetActive(false);
    }
}