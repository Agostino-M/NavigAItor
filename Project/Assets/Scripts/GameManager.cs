using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject boundaryObject;
    public static GameManager Instance { get; private set; }

    public List<Vector3> enemyPositions { get; private set; } = new List<Vector3>();
    public Environment env { get; private set; }
    public GameObject startingPosition { get; private set; }
    // The target location of the agent
    public GameObject goalPosition { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        env = GameObject.Find("env").GetComponent<Environment>();
    }

    void Start()
    {
        InvertMesh(boundaryObject);
        GenerateStartPosition();
    }

    public static Vector3 SharedStartPosition { get; private set; }

    public void GenerateStartPosition()
    {
        // Getting start and target positions from gameobjects in envArea
        startingPosition = env.transform.Find("StartArea").gameObject;
        goalPosition = env.transform.Find("GoalArea").gameObject;

        // Generazione posizione di partenza sicura
        startingPosition.transform.position = GetSafePosition();
        // Debug.Log("Starting position: " + startingPosition.transform.position);

        SharedStartPosition = startingPosition.transform.position;

        // Trova la posizione del goal ad almeno 140 unità di distanza
        goalPosition.transform.position = GetDistantSafePosition(startingPosition.transform.position, 140);
        // Debug.Log("Goal position: " + goalPosition.transform.position);

        // Debug.Log(Vector3.Distance(startingPosition.transform.position, goalPosition.transform.position));
    }


    private Vector3 GetSafePosition()
    {
        float areaRadius = env.AreaDiameter / 2.0f;
        int maxAttempts = 200;
        float innerRadius = 50f; // Escludiamo un'area centrale di raggio

        for (int i = 0; i < maxAttempts; i++)
        {
            // Genera un angolo casuale tra 0 e 360 gradi (convertito in radianti)
            float angle = Random.Range(0f, Mathf.PI * 2);

            // Genera una distanza casuale tra innerRadius e areaRadius
            float radius = Random.Range(innerRadius, areaRadius);

            // Converti in coordinate cartesiane
            Vector3 potentialPosition = new Vector3(
                radius * Mathf.Cos(angle),
                0,
                radius * Mathf.Sin(angle)
            );

            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 4f);
            // Debug.Log("Colliders: " + colliders.Length);
            // foreach (var collider in colliders)
            // {
            //     Debug.Log(collider.name);
            // }
            // Controlla se la posizione è libera o contiene solo il terreno
            if (colliders.Length == 0 || (colliders.Length == 1 && colliders[0].name == "Boundaries"))
            {
                return potentialPosition;
            }
        }

        Debug.LogWarning("Could not find a safe position after maximum attempts.");
        return new Vector3(66, 1, 44);
    }



    private Vector3 GetDistantSafePosition(Vector3 startPos, float minDistance)
    {
        int maxAttempts = 200;
        for (int i = 0; i < maxAttempts; i++)
        {
            // Genera un angolo casuale tra 0 e 360 gradi
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;

            // Genera una distanza casuale tra minDistance e una frazione dell'area
            float distance = Random.Range(minDistance, env.AreaDiameter);
            // Debug.Log("Distance: " + distance);
            // Calcola le coordinate della nuova posizione utilizzando coordinate polari
            Vector3 potentialPosition = new Vector3(
                startPos.x + Mathf.Cos(angle) * distance,
                0f, // Manteniamo Y a 0 per la mappa piatta
                startPos.z + Mathf.Sin(angle) * distance
            );

            // Assicurati che la posizione sia all'interno dei limiti dell'area di gioco
            if (IsPositionSafe(potentialPosition) && IsInsideBounds(potentialPosition))
            {
                return potentialPosition;
            }
        }

        Debug.LogWarning("Could not find a distant safe position, returning default.");
        return new Vector3(-73, 1, 0);
    }

    private bool IsInsideBounds(Vector3 position)
    {
        // Verifica se la posizione è all'interno dei limiti dell'area
        float halfArea = env.AreaDiameter / 2;
        return position.x >= -halfArea && position.x <= halfArea && position.z >= -halfArea && position.z <= halfArea;
    }


    private bool IsPositionSafe(Vector3 position)
    {
        float checkRadius = 5f; // Raggio di controllo per evitare ostacoli
        Collider[] colliders = Physics.OverlapSphere(position, checkRadius);

        // Se non ci sono collisori oppure c'è solo il terreno, la posizione è sicura
        return colliders.Length == 0 || (colliders.Length == 1 && colliders[0].name == "Boundaries");
    }

    public void RegisterEnemy(Vector3 position)
    {
        enemyPositions.Add(position);
    }

    public List<Vector3> GetEnemyPositions()
    {
        return new List<Vector3>(enemyPositions);
    }

    // Metodo per resettare le posizioni dei nemici
    public void ResetEnemies()
    {
        enemyPositions.Clear();  // Rimuove tutte le posizioni salvate dei nemici
    }

    // Metodo per invertire la mesh di un GameObject
    static void InvertMesh(GameObject obj)
    {
        // Ottieni il componente MeshFilter.
        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogError("Il GameObject non ha un MeshFilter!");
            return;
        }

        // Ottieni la mesh originale.
        Mesh mesh = meshFilter.mesh;

        // Inverti le normali.
        Vector3[] normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = -normals[i];
        }
        mesh.normals = normals;

        // Inverti i triangoli per mantenere l'orientamento corretto.
        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int temp = triangles[i];
            triangles[i] = triangles[i + 1];
            triangles[i + 1] = temp;
        }
        mesh.triangles = triangles;

        // Aggiorna il Mesh Collider.
        MeshCollider meshCollider = obj.GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null; // Forza l'aggiornamento del collisore.
            meshCollider.sharedMesh = mesh;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        //Gizmos.DrawWireSphere(Vector3.zero, 75f); // Raggio minimo da escludere (evitare il centro));
    }
}
