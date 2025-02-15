using System;
using System.Collections.Generic;
using UnityEngine;

public class AStar : MonoBehaviour
{
    public static AStar Instance { get; private set; }
    public List<Cell> openList;
    public HashSet<Cell> closedList;
    public List<Cell> currentPath;
    private Cell[,] grid;
    private Cell startCell;
    private Cell endCell;
    public List<Cell> lastCalculatedPath;  // Lista per conservare l'ultimo percorso calcolato
    public event Action<List<Cell>> PathUpdated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            lastCalculatedPath = new List<Cell>();
            openList = new List<Cell>();
            closedList = new HashSet<Cell>();
            currentPath = new List<Cell>();
        }
        else
        {
            Destroy(gameObject);
        }
    }
   
    public AStar(Cell[,] grid, Cell startCell, Cell endCell)
    {
        this.grid = grid;
        this.startCell = startCell;
        this.endCell = endCell;
        openList = new List<Cell>();
        closedList = new HashSet<Cell>();
        currentPath = new List<Cell>();
        lastCalculatedPath = new List<Cell>();
    }  // Inizializzazione della lista del percorso

   
    public List<Cell> FindPath()
    {
        if (!endCell.IsWalkable())
        {
            Debug.LogError("La cella di destinazione non è percorribile.");
            return null;
        }
        openList.Clear();
        closedList.Clear();
        currentPath.Clear();

        Debug.Log("Liste pulite");

        openList.Add(startCell);

        while (openList.Count > 0)
        {
            Cell currentCell = GetCellWithLowestFCost(openList);
            if (currentCell == endCell)
            {
                currentPath = RetracePath();
                lastCalculatedPath = new List<Cell>(currentPath);  // Aggiorna l'ultimo percorso calcolato
              
                return currentPath;
            }
            openList.Remove(currentCell);
            closedList.Add(currentCell);

            foreach (Cell neighbor in GetNeighbors(currentCell))
            {
                if (!neighbor.IsWalkable() || closedList.Contains(neighbor))
                    continue;

                float tentativeGCost = currentCell.GetGCost() + GetManhattanDistance(currentCell.GetWorldPosition(), neighbor.GetWorldPosition());
                if (!openList.Contains(neighbor) || tentativeGCost < neighbor.GetGCost())
                {
                    neighbor.SetGCost(tentativeGCost);
                    neighbor.CalculateHCost(endCell);
                    neighbor.SetParent(currentCell);

                    if (!openList.Contains(neighbor))
                        openList.Add(neighbor);
                }
            }
        }

        Debug.Log("Non esiste un percorso valido!");
        return null;
    }

    private float GetManhattanDistance(Vector3 a, Vector3 b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.z - b.z);
    }

    private Cell GetCellWithLowestFCost(List<Cell> cells)
    {
        Cell lowestFCostCell = cells[0];
        foreach (Cell cell in cells)
        {
            if (cell.GetFCost() < lowestFCostCell.GetFCost())
                lowestFCostCell = cell;
        }
        return lowestFCostCell;
    }

    private List<Cell> GetNeighbors(Cell cell)
    {
        List<Cell> neighbors = new List<Cell>();
        int currentX = (int)cell.GetGridPosition().x;
        int currentZ = (int)cell.GetGridPosition().z;
        int[] dx = { -1, 1, 0, 0, -1, -1, 1, 1 };
        int[] dz = { 0, 0, -1, 1, -1, 1, -1, 1 };

        for (int i = 0; i < 8; i++)
        {
            int neighborX = currentX + dx[i];
            int neighborZ = currentZ + dz[i];
            if (IsValidCell(neighborX, neighborZ))
            {
                neighbors.Add(grid[neighborX, neighborZ]);
            }
        }

        return neighbors;
    }

    private bool IsValidCell(int x, int z)
    {
        return x >= 0 && x < grid.GetLength(0) &&
               z >= 0 && z < grid.GetLength(1) &&
               grid[x, z].IsWalkable();
    }

    public List<Cell> RetracePath()
    {
        List<Cell> path = new List<Cell>();
        Cell currentCell = endCell;

        while (currentCell != startCell)
        {
            path.Add(currentCell);
            currentCell = currentCell.GetParent();
        }

        path.Add(startCell);
        path.Reverse();
        return path;
    }
}
