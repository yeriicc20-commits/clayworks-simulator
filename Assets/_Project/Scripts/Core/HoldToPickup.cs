using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HoldToPickup : MonoBehaviour
{
    public Camera playerCamera;
    public Image progressCircle;
    public float holdDuration = 1.5f;
    public float maxDistance = 6f;
    public LayerMask placeableLayer;

    private float holdTimer = 0f;
    private PlaceableObject currentTarget;

    void Update()
    {
        DetectTarget();

        if (currentTarget != null && Input.GetMouseButton(1))
        {
            holdTimer += Time.deltaTime;
            progressCircle.gameObject.SetActive(true);
            progressCircle.fillAmount = holdTimer / holdDuration;

            if (holdTimer >= holdDuration)
            {
                PickupPlacedObject();
            }
        }
        else
        {
            holdTimer = 0f;
            progressCircle.fillAmount = 0f;
            progressCircle.gameObject.SetActive(false);
        }
    }

    void DetectTarget()
    {
        // Con una caja o una maquina ya en las manos no se puede coger otra.
        if (PlayerCarry.Busy)
        {
            currentTarget = null;
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxDistance, placeableLayer))
        {
            PlaceableObject found = hit.collider.GetComponentInParent<PlaceableObject>();
            currentTarget = found;
        }
        else
        {
            currentTarget = null;
        }
    }

    void PickupPlacedObject()
    {
        if (currentTarget == null) return;

        if (currentTarget.sourcePrefab == null)
        {
            Debug.LogError("El objeto " + currentTarget.gameObject.name + " tiene Source Prefab vacio. No se puede recoger.");
            holdTimer = 0f;
            progressCircle.fillAmount = 0f;
            progressCircle.gameObject.SetActive(false);
            currentTarget = null;
            return;
        }

        GameObject prefabToRestore = currentTarget.sourcePrefab;

        // PlaceableObject vive en un hijo, asi que la maquina entera es la raiz.
        Transform machineRoot = currentTarget.transform.root;

        currentTarget = null;
        holdTimer = 0f;
        progressCircle.fillAmount = 0f;
        progressCircle.gameObject.SetActive(false);

        PlacementManager.Instance.CarryWithNextPlacement(CollectToysInside(machineRoot), machineRoot);

        Destroy(machineRoot.gameObject);
        PlacementManager.Instance.StartPlacement(prefabToRestore);
    }

    // Los juguetes son objetos sueltos de la escena, no hijos de la maquina, asi
    // que los buscamos por posicion dentro de la caja que ocupa la maquina.
    List<GameObject> CollectToysInside(Transform machineRoot)
    {
        List<GameObject> found = new List<GameObject>();

        Renderer[] renderers = machineRoot.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(machineRoot.position, Vector3.zero);
        bool any = false;

        foreach (Renderer rend in renderers)
        {
            if (rend == null || rend is LineRenderer) continue;

            if (!any)
            {
                bounds = rend.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(rend.bounds);
            }
        }

        if (!any) return found;

        PlushItem[] plushes = Object.FindObjectsByType<PlushItem>(FindObjectsInactive.Exclude);

        foreach (PlushItem plush in plushes)
        {
            if (plush == null) continue;
            if (plush.transform.IsChildOf(machineRoot)) continue;
            if (!bounds.Contains(plush.transform.position)) continue;

            found.Add(plush.gameObject);
        }

        return found;
    }
}