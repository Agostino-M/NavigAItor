using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    private StateMachine stateMachine;
    private Vector3 destination;
    private bool isMoving = false;
    private Environment envArea;
    [SerializeField] private float detectionRadius = 5f;
    private List<Vector3> detectedEnemiesPositions = new List<Vector3>();
    public bool enemyDetected = false;
    public bool isRecalculating = false;
    public bool sensorEnabled = true;
    public bool needToRecalculatePath;
    private bool destinationSet = false;
    private Vector3 previousPosition;
    public Vector3 enemyPosition;
    public int speed = 5;

    void Start()
    {
        stateMachine = this.gameObject.AddComponent<StateMachine>();
        envArea = GetComponentInParent<Environment>();

        if (stateMachine != null)
        {
            stateMachine.SetState(new StandbyState(stateMachine));
        }

        previousPosition = transform.position; // Inizializza la posizione precedente
        SetRobotPosition();
    }

    private void SetRobotPosition()
    {
        bool safePositionFound = false;
        int attemptsRemaining = 100;
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = new Quaternion();

        // Loop until a safe position is found or we run out of attempts
        while (!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;

            // Pick a random radius from the center of the area
            float radius = UnityEngine.Random.Range(2f, 10f);

            // Pick a random direction rotated around the y axis
            Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);

            // Combine height, radius, and direction to pick a potential position
            potentialPosition = GameManager.SharedStartPosition + Vector3.up * 0.2f + direction * Vector3.forward * radius;

            // Choose and set random starting pitch and yaw
            float yaw = UnityEngine.Random.Range(-180f, 180f);
            potentialRotation = Quaternion.Euler(0f, yaw, 0f);

            // Check to see if the agent will collide with anything
            int layerMask = ~LayerMask.GetMask("Boundaries");
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 2f, layerMask);
            safePositionFound = colliders.Length == 0;

            // Check if the agent is whitin the boundaries
            if (potentialPosition.x < envArea.transform.position.x - envArea.AreaDiameter / 2 ||
                potentialPosition.x > envArea.transform.position.x + envArea.AreaDiameter / 2 ||
                potentialPosition.z < envArea.transform.position.z - envArea.AreaDiameter / 2 ||
                potentialPosition.z > envArea.transform.position.z + envArea.AreaDiameter / 2)
            {
                safePositionFound = false;
            }

        }

        Debug.Assert(safePositionFound, "Could not find a safe position to spawn");

        // Set the position and rotation
        transform.SetPositionAndRotation(potentialPosition, potentialRotation);
    }

    private void Update()
    {
        if (sensorEnabled)
            DetectDynamicEnemies();

        if (needToRecalculatePath)
        {
            RecalculatePath();
            needToRecalculatePath = false;
        }
    }

    public bool DetectDynamicEnemies()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy"))
            {
                Vector3 detectedEnemyPosition = hitCollider.transform.position;


                // Aggiorna la posizione del nemico rilevato
                enemyPosition = detectedEnemyPosition;

                if (!detectedEnemiesPositions.Contains(detectedEnemyPosition) && !GridManager.Instance.CheckEnemyInPosition(enemyPosition))
                {
                    detectedEnemiesPositions.Add(detectedEnemyPosition);
                    GridManager.Instance.MarkObjectsAsBlocked(new GameObject[] { hitCollider.gameObject }, 2);
                    enemyDetected = true;
                    isRecalculating = true;
                    sensorEnabled = false;
                    return true;
                }
            }
        }
        return false;
    }
    private void EndGame()
    {
        // Assicurati che "GameOverScene" sia il nome esatto della scena da caricare
        SceneManager.LoadScene("GameOverScene");
    }
    public void SetMoving(bool moving)
    {
        this.isMoving = moving;
    }

    public bool IsMoving()
    {
        return this.isMoving;
    }

    public bool RotateToTarget(Vector3 targetPosition)
    {
        if (isMoving)
        {
            Vector3 targetDirection = targetPosition - transform.position;
            targetDirection.y = 0;
            targetDirection.Normalize();

            float angleToTarget = Vector3.Angle(transform.forward, targetDirection);
            if (angleToTarget < 1.0f) // Tolleranza angolare per evitare rotazioni continue
            {
                return true; // Non è più necessario ruotare
            }

            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 90.0f * Time.deltaTime);

            float error = Quaternion.Angle(transform.rotation, targetRotation);
            return error < 1.0f;
        }

        return false;
    }

    public bool MoveToTarget(Vector3 targetPosition)
    {
        if (isMoving)
        {
            Vector3 directionToTarget = targetPosition - transform.position;
            directionToTarget.y = 0;
            float distanceToTarget = directionToTarget.magnitude;

            Vector3 desiredMovement = directionToTarget.normalized * speed * Time.deltaTime;

            transform.position += desiredMovement;

            if (distanceToTarget <= 0.5f)
            {
                return true; // Raggiunto il target
            }

            return false; // Non ancora arrivati
        }

        return false;
    }

    public bool IsDestinationSet()
    {
        return this.destinationSet;
    }

    public void SetDestination(Vector3 destination)
    {
        Vector3 gridDestination = new Vector3(destination.x, -0.01f, destination.z);
        this.destination = gridDestination;
        destinationSet = true;
        Debug.Log("Destinazione impostata: " + destination);
    }

    public void ClearDestination()
    {
        this.destination = Vector3.zero;
        destinationSet = false;
    }

    public Vector3 GetDestination()
    {
        return this.destination;
    }

    public StateMachine GetStateMachine()
    {
        return this.stateMachine;
    }
    public void RecalculatePath()
    {
        // Qui logica per richiedere un nuovo percorso basato sulla griglia aggiornata
        stateMachine.SetState(new PlanningState(stateMachine));
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Enemy"))
        {
            Debug.Log("Robot Catturato");
            EndGame();
        }
    }

    void OnDrawGizmos()
    {
        // Disegna una sfera nel Gizmo per visualizzare il raggio di rilevamento
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
