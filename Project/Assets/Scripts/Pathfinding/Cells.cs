using UnityEngine;

public class Cell
{
    private Vector3 worldPosition;
    private int x, z;
    private bool isWalkable;

    private float gCost;
    private float hCost;
   

    private Cell parent;

    public Cell(Vector3 position, int x, int z, bool isWalkable)
    {
        this.worldPosition = position;
        this.isWalkable = isWalkable;
       

        this.x = x;
        this.z = z;
        this.gCost = 1.0f;
        this.hCost = 0.0f;
        this.parent = null;

    }
    public override string ToString()
    {
        return $"Cell [x: {this.x}, y: {this.z}, pos: {this.worldPosition}, walkable: {this.isWalkable}]";
    }

    // Metodo per ottenere la posizione nel mondo della cella
    public Vector3 GetWorldPosition()
    {
        return this.worldPosition;
    }

    // Metodo per ottenere la posizione della cella nella griglia
    public Vector3 GetGridPosition()
    {
        return new Vector3(x, -0.01f, z);
    }

    // Metodo per determinare settare la cella come percorribile o meno
    public void SetWalkable(bool isWalkable)
    {
        this.isWalkable = isWalkable;
    }

    // Metodo per ottenere se la cella è percorribile
    public bool IsWalkable()
    {
        return this.isWalkable;
    }
  

  
    // Metodo per ottenere l'fCost della cella
    public float GetFCost()
    {
        return this.gCost + this.hCost;
    }

    // Metodo per ottenere X della cella
    public int GetX()
    {
        return this.x;
    }

    // Metodo per ottenere Z della cella
    public int GetZ()
    {
        return this.z;
    }

    // Metodo per ottenere il gCost della cella
    public float GetGCost()
    {
        return this.gCost;
    }

    // Metodo per settare il gCost della cella
    public void SetGCost(float gCost)
    {
        this.gCost = gCost;
    }

    // Metodo per calcolare l'hCost della cella
    public void CalculateHCost(Cell endCell)
    {
        this.hCost = Vector3.Distance(this.GetWorldPosition(), endCell.GetWorldPosition());
    }

    // Metodo per ottenere il parent della cella
    public Cell GetParent()
    {
        return this.parent;
    }

    // Metodo per settare il parent
    public void SetParent(Cell parent)
    {
        this.parent = parent;
    }

}
