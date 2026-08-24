using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NPCSpawner : MonoBehaviour
{
    public GameObject npcPrefab;
    public Transform[] spawnPoints;
    public Transform entranceWaypoint;

    [Header("Ritmo de llegada")]
    public float firstSpawnDelay = 3f;
    public float minInterval = 5f;
    public float maxInterval = 10f;

    [Header("Limites")]
    public int maxActiveNPCs = 6;

    [Tooltip("Solo entran clientes con el local abierto.")]
    public bool onlyDuringOpenHours = true;

    [Tooltip("Explica por consola por que no entra nadie. Solo para depurar.")]
    public bool diagnostico = false;

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(firstSpawnDelay);

        while (true)
        {
            if (CanSpawn())
            {
                SpawnOne();
                yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
            }
            else
            {
                yield return new WaitForSeconds(3f);
            }
        }
    }

    bool CanSpawn()
    {
        if (npcPrefab == null) return Blocked("no hay prefab de NPC asignado");
        if (spawnPoints == null || spawnPoints.Length == 0) return Blocked("no hay puntos de aparicion");
        if (NPCClawPlayer.ActiveCount >= maxActiveNPCs) return Blocked("ya hay " + maxActiveNPCs + " clientes dentro");

        if (onlyDuringOpenHours && DayCycleManager.Instance != null && !DayCycleManager.Instance.DayRunning)
        {
            return Blocked("el local esta cerrado: pulsa O para abrir");
        }

        // Sin maquinas se entra igual: el cliente da una vuelta por el local y se
        // va. Un local vacio donde ni siquiera pasa gente parece roto.

        if (motivo != null)
        {
            if (diagnostico) Debug.Log("[NPCSpawner] Ya puedo soltar clientes.", this);
            motivo = null;
        }

        return true;
    }

    private string motivo;
    private float siguienteAviso = 0f;

    // Dice por consola por que no entra nadie, pero solo cuando cambia el motivo
    // o cada diez segundos: si no, llenaria el log igual que lo llenaba TestInput.
    bool Blocked(string razon)
    {
        if (razon != motivo || Time.time >= siguienteAviso)
        {
            if (diagnostico) Debug.Log("[NPCSpawner] No entran clientes porque " + razon + ".", this);

            motivo = razon;
            siguienteAviso = Time.time + 10f;
        }

        return false;
    }

    void SpawnOne()
    {
        Transform point = PickSpawnPoint();
        if (point == null) return;

        GameObject npc = Instantiate(npcPrefab, point.position, point.rotation);

        NPCClawPlayer brain = npc.GetComponentInChildren<NPCClawPlayer>();
        if (brain != null)
        {
            // Los hijos del punto de aparicion son waypoints opcionales: sirven
            // para rodear el edificio cuando el punto no esta al lado de la puerta.
            List<Transform> path = new List<Transform>();

            foreach (Transform waypoint in point)
            {
                path.Add(waypoint);
            }

            brain.ConfigureVisit(point.position, entranceWaypoint, path);
        }
    }

    Transform PickSpawnPoint()
    {
        int tries = 0;

        while (tries < 8)
        {
            Transform candidate = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (candidate != null) return candidate;
            tries++;
        }

        return null;
    }
}
