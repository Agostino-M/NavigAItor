using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Environment : MonoBehaviour
{
    // The list of all enemy in this enemy area (enemy areas have multiple enemies)
    private List<GameObject> enemyAreas;

    public int AreaDiameter { get; private set; }

    // A lookup dictionary for looking up an enemy from a collider
    private Dictionary<Collider, Enemy> enemyDictionary;

    /// <summary>
    /// The list of all enemies in the enemy area
    /// </summary>
    public List<Enemy> Enemies { get; private set; }

    /// <summary>
    /// Reset the enemies and enemy areas
    /// </summary>
    public void ResetEnemies(bool randomPosition = false)
    {
        if (randomPosition)
        {
            // Reset each enemy area to a random rotation
            foreach (GameObject enemyArea in enemyAreas)
            {
                float xRotation = UnityEngine.Random.Range(-5f, 5f);
                float yRotation = UnityEngine.Random.Range(-180f, 180f);
                float zRotation = UnityEngine.Random.Range(-5f, 5f);

                enemyArea.transform.localRotation = Quaternion.Euler(xRotation, yRotation, zRotation);
            }
        }

        // Reset each enemy
        foreach (Enemy enemy in Enemies)
        {
            enemy.ResetEnemy();
        }
    }

    /// <summary>
    /// Gets the enemy from a collider
    /// </summary>
    /// <param name="collider"></param>
    /// <returns></returns>
    internal Enemy GetEnemyFromCollider(Collider collider)
    {
        return enemyDictionary[collider];
    }

    /// <summary>
    /// Called when the area wakes up
    /// </summary>
    private void Awake()
    {
        // Initialize variables
        enemyAreas = new List<GameObject>();
        enemyDictionary = new Dictionary<Collider, Enemy>();
        Enemies = new List<Enemy>();
        AreaDiameter = (int)this.transform.Find("Boundaries").localScale.x;
    }

    /// <summary>
    /// Called when the game starts
    /// </summary>
    private void Start()
    {
        // Debug.Log("Start environment");
        // Getting area diameter from boundary trasform dimentions
        // Find all enemies that are children of this GameObject/Transform
        FindChildEnemies(transform);


        //StartCoroutine(InitializeNeo4jGrid());
    }

    /// <summary>
    /// Recursively finds all enemies and enemy areas that are children of a parent transform
    /// </summary>
    /// <param name="parent">The parent of the children to check</param>
    private void FindChildEnemies(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.CompareTag("EnemyArea"))
            {
                // Found a enemy area, add it to the enemyAreas list
                //Debug.Log("Found a enemy area, add it to the enemyAreas list");
                enemyAreas.Add(child.gameObject);

                // Look for enemies within the enemy area
                FindChildEnemies(child);
            }
            else
            {
                // Not a enemy area, look for a Enemy component
                Enemy enemy = child.GetComponent<Enemy>();
                if (enemy != null && enemy.isActiveAndEnabled)
                {
                    // Found a enemy, add it to the Enemies list
                    //Debug.Log("Found a enemy, add it to the Enemies list");
                    Enemies.Add(enemy);

                    // Add the collider to the lookup dictionary if it's not null
                    if (enemy.cCollider != null)
                    {
                        enemyDictionary.Add(enemy.cCollider, enemy);
                    }
                    else
                    {
                        Debug.LogError("Enemy: " + enemy.name + " does not have a collider");
                    }

                    // Note: there are no enemies that are children of other enemies
                }
                else
                {
                    // Enemy component not found, so check children
                    FindChildEnemies(child);
                }
            }
        }
    }

}
