using UnityEngine;
using System.Collections.Generic;

public class PlaceableObject : MonoBehaviour
{
    public static readonly List<PlaceableObject> AllPlaced = new List<PlaceableObject>();

    public GameObject sourcePrefab;

    void OnEnable()
    {
        AllPlaced.Add(this);
    }

    void OnDisable()
    {
        AllPlaced.Remove(this);
    }
}