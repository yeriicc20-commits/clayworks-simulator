using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ItemCardUI : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public Image iconImage;
    public Button buyButton;

    public void Setup(string itemName, int price, Sprite icon, Action onBuyClicked)
    {
        nameText.text = itemName;
        priceText.text = GameManager.Format(price);

        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuyClicked());
    }

    public void SetupGeneric(string title, string actionLabel, Action onClicked)
    {
        nameText.text = title;
        priceText.text = actionLabel;

        if (iconImage != null)
        {
            iconImage.enabled = false;
        }

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onClicked());
    }
}
