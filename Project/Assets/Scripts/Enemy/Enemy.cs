using UnityEngine;

/// <summary>
/// Manages a single enemy
/// </summary>
public class Enemy : MonoBehaviour
{
    [Tooltip("The color when the enemy is found")]
    public Material originalMaterial;

    [Tooltip("The color when the enemy is not found")]
    public Material highlightMaterial;

    [Tooltip("Colore dell'emissione quando il nemico è evidenziato")]
    public Color highlightEmissionColor = Color.yellow;
    private Renderer enemyRenderer;

    /// <summary>
    /// The trigger collider representing the enemy
    /// </summary>
    [HideInInspector]
    public Collider cCollider { get; private set; }
    public bool found { get; private set; } = false;

    /// <summary>
    /// Called when the enemy wakes up 
    /// </summary>
    private void Awake()
    {
        // Find the enemy's renderer and get the main material
        enemyRenderer = GetComponent<Renderer>();
        cCollider = this.transform.GetComponent<Collider>();

        if (enemyRenderer != null)
        {
            originalMaterial = enemyRenderer.material; // Primo materiale attuale
            /*highlightMaterial = new Material(originalMaterial); // Crea una copia per evidenziare
            highlightMaterial.EnableKeyword("_EMISSION");
            highlightMaterial.SetColor("_EmissionColor", highlightEmissionColor);*/
        }
    }
    public void Found()
    {
        // Debug.Log("Enemy found");
        // Highlight the enemy color
        HighlightEnemy();
        cCollider.gameObject.SetActive(true);
        found = true;
    }

    /// <summary>
    /// Resets the enemy
    /// </summary>
    public void ResetEnemy()
    {
        //Debug.Log("reset enemy");
        found = false;
        // Restore highlight
        RemoveHighlight();
    }


    public void HighlightEnemy()
    {
        if (enemyRenderer != null && highlightMaterial != null)
        {
            enemyRenderer.material = highlightMaterial;
        }
    }

    public void RemoveHighlight()
    {
        if (enemyRenderer != null && originalMaterial != null)
        {
            enemyRenderer.material = originalMaterial;
        }
    }

}
