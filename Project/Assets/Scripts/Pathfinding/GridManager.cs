using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid settings")]
    public float cellSize = 1;
    public int gridWidth = 150;
    public int gridHeight = 150;
    private Cell[,] grid;
    private RobotController robotController;
    public List<Cell> path;
    public static GridManager Instance;
    GameObject[] blockedObjects;
    GameObject[] enemyObjects;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {

        Debug.Log("Generazione della griglia iniziata...");
        GenerateGrid();
        Debug.Log("Griglia generata con successo.");
        DetectBlockedCells();
    }

    public Cell[,] GetGrid()
    {
        return this.grid;
    }
    public void GenerateGrid()
    {
        float offsetX = gridWidth * cellSize * 0.5f - cellSize * 0.5f;
        float offsetZ = gridHeight * cellSize * 0.5f - cellSize * 0.5f;
        grid = new Cell[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                Vector3 relativePosition = new Vector3(x * cellSize - offsetX, -14f, z * cellSize - offsetZ);
                Vector3 worldPosition = transform.position + RotatePosition(relativePosition);
                grid[x, z] = new Cell(worldPosition, x, z, true);
            }
        }
    }

    public void DetectBlockedCells()
    {
        blockedObjects = GameObject.FindGameObjectsWithTag("Obstacle");
        enemyObjects = GameObject.FindGameObjectsWithTag("Enemy");
        Debug.Log($"Trovati {blockedObjects.Length} oggetti con il tag 'Obstacles' e {enemyObjects.Length} oggetti con il tag 'Enemy'.");

        MarkObjectsAsBlocked(blockedObjects, 2);
        MarkObjectsAsBlocked(enemyObjects, 1);
    }

    public void MarkObjectsAsBlocked(GameObject[] objects, float radius)
    {
        foreach (var obj in objects)
        {
            Collider objCollider = obj.GetComponent<Collider>();
            if (objCollider != null)
            {
                Bounds objBounds = objCollider.bounds;
                objBounds.Expand(radius);

                for (int x = 0; x < gridWidth; x++)
                {
                    for (int z = 0; z < gridHeight; z++)
                    {
                        Vector3 cellPosition = grid[x, z].GetWorldPosition();
                        if (objBounds.Contains(cellPosition))
                        {
                            grid[x, z].SetWalkable(false);
                            // Aggiungi log per verificare se la cella � nel percorso

                        }
                    }
                }
            }
        }
    }

    public bool checkEnemyInPath(List<Cell> cells)
    {

        foreach (var cell in cells)
        {

            if (!grid[cell.GetX(), cell.GetZ()].IsWalkable())
            {
                return true;
            }

        }
        return false;
    }

    public bool CheckEnemyInPosition(Vector3 position)
    {
        Cell cell = GetCellFromWorldPosition(position);

        if (!grid[cell.GetX(), cell.GetZ()].IsWalkable())
        {
            return true;
        }
        return false;
    }
    Vector3 RotatePosition(Vector3 position)
    {
        return transform.rotation * position;
    }

    private void OnDrawGizmos()
    {
        if (Camera.current == Camera.main) return; // Controlla la telecamera corrente

        if (grid != null)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    Vector3 cellPosition = grid[x, z].GetWorldPosition();
                    Gizmos.color = grid[x, z].IsWalkable() ? Color.green : Color.red;
                    Gizmos.DrawCube(cellPosition, new Vector3(cellSize, -0.1f, cellSize));
                }
            }
        }
    }



    public Cell GetCellFromWorldPosition(Vector3 position)
    {
        Cell closestCell = null;
        float minDistance = float.MaxValue;
        foreach (Cell cell in grid)
        {
            float distance = Vector3.Distance(cell.GetWorldPosition(), position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestCell = cell;
            }
        }

        return closestCell;
    }
}
