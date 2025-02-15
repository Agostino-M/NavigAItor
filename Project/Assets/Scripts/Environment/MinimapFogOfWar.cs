using UnityEngine;
using UnityEngine.UI;
public class MinimapFogOfWar : MonoBehaviour
{
    public Transform drone;
    public Transform robot;
    public RawImage fogImage;
    public RawImage droneMarker;
    public RawImage robotMarker;
    public int fogResolution = 256; // Risoluzione della texture della nebbia
    public float revealRadius = 10f; // Raggio di visibilità
    private Texture2D fogTexture;
    private Color[] fogPixels;
    private int texSize;
    private float AreaDiameter = 150f;

    private bool[,] exploredMap;  // Mappa delle celle esplorate
    public float cellSize = 50f; // Dimensione di ogni cella esplorabile

    public float mapSize = 200f;
    public float offset = 150f;


    void Start()
    {
        // Creazione della texture della nebbia
        texSize = fogResolution;
        fogTexture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        fogPixels = new Color[texSize * texSize];

        // Inizialmente la minimappa è tutta coperta di nero
        for (int i = 0; i < fogPixels.Length; i++) 
            fogPixels[i] = Color.black;

        fogTexture.SetPixels(fogPixels);
        fogTexture.Apply();

        // Assegna la texture alla minimappa
        fogImage.texture = fogTexture;

        // Inizializza la mappa delle aree esplorate
        int cellsPerAxis = Mathf.CeilToInt(AreaDiameter / cellSize);
        exploredMap = new bool[cellsPerAxis, cellsPerAxis];
    }

    public void ResetFog()
    {
        // Resetta la mappa delle aree esplorate
        for (int x = 0; x < exploredMap.GetLength(0); x++)
        {
            for (int y = 0; y < exploredMap.GetLength(1); y++)
            {
                exploredMap[x, y] = false;
            }
        }

        // Resetta la texture della nebbia
        for (int i = 0; i < fogPixels.Length; i++)
        {
            fogPixels[i] = Color.black; // Rende di nuovo la minimappa tutta coperta
        }

        fogTexture.SetPixels(fogPixels);
        fogTexture.Apply();
    }

    void Update()
    {
        RevealArea(drone.position);
        UpdateDroneMarkerPosition(drone.position);
        UpdateRobotMarkerPosition(robot.position);
    }

    public void RevealArea(Vector3 playerPos)
    {
        // Calcola le coordinate normalizzate
        float normalizedX = (playerPos.x + AreaDiameter / 2) / AreaDiameter;
        float normalizedY = (playerPos.z + AreaDiameter / 2) / AreaDiameter;

        // Converti alle coordinate della griglia
        int gridX = Mathf.RoundToInt(normalizedX * fogResolution);
        int gridY = Mathf.RoundToInt(normalizedY * fogResolution);

        // Verifica i limiti della griglia
        if (gridX < 0 || gridX >= fogResolution || gridY < 0 || gridY >= fogResolution)
            return;

        // Rivela l'area attorno al drone nella texture
        int revealSize = Mathf.RoundToInt(revealRadius * fogResolution / AreaDiameter);
        for (int x = -revealSize; x <= revealSize; x++)
        {
            for (int y = -revealSize; y <= revealSize; y++)
            {
                int texX = gridX + x;
                int texY = gridY + y;

                if (texX >= 0 && texX < fogResolution && texY >= 0 && texY < fogResolution)
                {
                    fogPixels[texY * fogResolution + texX] = Color.clear; // Rende trasparente
                }
            }
        }

        fogTexture.SetPixels(fogPixels);
        fogTexture.Apply();
    }

    public bool IsAreaExplored(Vector3 position)
    {
        // Calcola le coordinate della cella basate su cellSize
        int gridX = Mathf.FloorToInt((position.x + AreaDiameter / 2) / cellSize);
        int gridY = Mathf.FloorToInt((position.z + AreaDiameter / 2) / cellSize);

        // Calcola il numero totale di celle
        int cellsPerAxis = Mathf.CeilToInt(AreaDiameter / cellSize);

        // Verifica i limiti
        if (gridX < 0 || gridX >= cellsPerAxis || gridY < 0 || gridY >= cellsPerAxis)
            return false;

        // Verifica se la cella è già esplorata
        if (!exploredMap[gridX, gridY])
        {
            exploredMap[gridX, gridY] = true; // Marca come esplorata
            //Debug.Log($"Cella esplorata ({gridX}, {gridY})");
            return true; // Appena esplorata
        }

        return false; // Già esplorata
    }

    /// <summary>
    /// Update the position of the drone marker on the minimap
    /// </summary>
    /// <param name="playerPos"></param>
    void UpdateDroneMarkerPosition(Vector3 playerPos)
    {
        // Normalize the player position to the area diameter
        float normalizedX = playerPos.x / AreaDiameter;
        float normalizedY = playerPos.z / AreaDiameter;

        // Convert to minimap coordinates
        float minimapX = normalizedX * mapSize;
        float minimapY = normalizedY * mapSize;

        // Update the position of the drone marker on the minimap
        droneMarker.rectTransform.anchoredPosition = new Vector2(minimapX - offset, minimapY - offset);
    }

    /// <summary>
    /// Update the position of the robot marker on the minimap
    /// </summary>
    /// <param name="playerPos"></param>
    void UpdateRobotMarkerPosition(Vector3 playerPos)
    {
        // Normalize the player position to the area diameter
        float normalizedX = playerPos.x / AreaDiameter;
        float normalizedY = playerPos.z / AreaDiameter;

        // Convert to minimap coordinates
        float minimapX = normalizedX * mapSize;
        float minimapY = normalizedY * mapSize;

        // Update the position of the drone marker on the minimap
        robotMarker.rectTransform.anchoredPosition = new Vector2(minimapX - offset, minimapY - offset);
    }

    /// <summary>
    /// Calcolates the percentage of explored cells
    /// </summary>
    /// <returns>The percentage</returns>
    public float GetExplorationPercentage()
    {
        int exploredCount = 0;

        for (int x = 0; x < exploredMap.GetLength(0); x++)
        {
            for (int y = 0; y < exploredMap.GetLength(1); y++)
            {
                if (exploredMap[x, y])
                    exploredCount++;
            }
        }

        int totalCells = exploredMap.GetLength(0) * exploredMap.GetLength(1);
        //Debug.Log($"Explored: {exploredCount} - Total: {totalCells}");
        return (float)exploredCount / totalCells;
    }

    // sono arrivato qui, ottengo l'errore IndexOutOfRangeException: Index was outside the bounds of the array.
    // da verificare cosa succede ma serve nel collect observation per osservare se c'è un'area non esplorata vicino
    public bool HasUnexploredAreaNearby(Vector3 position, float checkRadius = 10f)
    {
        int cellsPerAxis = Mathf.CeilToInt(AreaDiameter / cellSize);

        int gridX = Mathf.FloorToInt((position.x + AreaDiameter / 2) / cellSize);
        int gridY = Mathf.FloorToInt((position.z + AreaDiameter / 2) / cellSize);

        int radiusInCells = Mathf.CeilToInt(checkRadius / cellSize);

        for (int x = -radiusInCells; x <= radiusInCells; x++)
        {
            for (int y = -radiusInCells; y <= radiusInCells; y++)
            {
                int checkX = gridX + x;
                int checkY = gridY + y;

                if (checkX >= 0 && checkX < cellsPerAxis && checkY >= 0 && checkY < cellsPerAxis)
                {
                    // Debug delle celle controllate
                    //Debug.Log($"Checking cell ({checkX}, {checkY}) - Explored: {exploredMap[checkX, checkY]}");

                    if (!exploredMap[checkX, checkY])
                    {
                        //Debug.Log($"Unexplored cell found at ({checkX}, {checkY})!");
                        return true;
                    }
                }
            }
        }

        Debug.Log("No unexplored areas nearby.");
        return false;
    }

    public float GetDistanceToNearestUnexplored(Vector3 position)
    {
        int cellsPerAxis = Mathf.CeilToInt(AreaDiameter / cellSize);

        float minDistance = float.MaxValue;
        for (int x = 0; x < cellsPerAxis; x++)
        {
            for (int y = 0; y < cellsPerAxis; y++)
            {
                if (!exploredMap[x, y]) // Se la cella è inesplorata
                {
                    Vector3 cellPos = new Vector3(
                        (x + 0.5f) * cellSize - AreaDiameter / 2,
                        position.y,
                        (y + 0.5f) * cellSize - AreaDiameter / 2
                    );

                    float dist = Vector3.Distance(position, cellPos);
                    minDistance = Mathf.Min(minDistance, dist);
                }
            }
        }

        return minDistance == float.MaxValue ? -1f : minDistance; // Se non ci sono celle inesplorate, restituisce -1
    }

    public Vector3 GetDirectionToNearestUnexplored(Vector3 position)
    {
        int cellsPerAxis = Mathf.CeilToInt(AreaDiameter / cellSize);

        float minDistance = float.MaxValue;
        Vector3 nearestCellPosition = Vector3.zero;
        bool found = false;

        for (int x = 0; x < cellsPerAxis; x++)
        {
            for (int y = 0; y < cellsPerAxis; y++)
            {
                if (!exploredMap[x, y]) // Se la cella è inesplorata
                {
                    Vector3 cellPos = new Vector3(
                        (x + 0.5f) * cellSize - AreaDiameter / 2,
                        position.y, // Manteniamo la stessa altezza del drone
                        (y + 0.5f) * cellSize - AreaDiameter / 2
                    );

                    float dist = Vector3.Distance(position, cellPos);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearestCellPosition = cellPos;
                        found = true;
                    }
                }
            }
        }

        if (!found) return Vector3.zero; // Se non ci sono celle inesplorate

        // Direzione normalizzata verso la cella inesplorata
        Vector3 direction = (nearestCellPosition - position).normalized;
        return direction;
    }


    void OnDrawGizmos()
    {
        if (drone == null || exploredMap == null)
            return;

        int cellsPerAxis = Mathf.CeilToInt(AreaDiameter / cellSize);

        //Draw all the cells of the map
        for (int x = 0; x < cellsPerAxis; x++)
        {
            for (int y = 0; y < cellsPerAxis; y++)
            {
                Vector3 cellPos = new Vector3(
                    (x + 0.5f) * cellSize - AreaDiameter / 2,
                    1,
                    (y + 0.5f) * cellSize - AreaDiameter / 2
                );


                if (exploredMap[x, y])
                    Gizmos.color = Color.green; // Celle esplorate in verde
                else
                    Gizmos.color = Color.red; // Celle inesplorate in rosso

                Gizmos.DrawWireCube(cellPos, new Vector3(cellSize, 0.1f, cellSize));
            }
        }
    }

}
