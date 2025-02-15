using UnityEngine;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class EnemyAgent : MonoBehaviour
{
    // Parametri di movimento e pattugliamento
    public float patrolSpeed = 3f;
    public float interceptSpeed = 4f;
    public float detectionRange = 10f;
    public float verificationRange = 10f;

    // Variabili interne
    private new Rigidbody rigidbody;
    private KbManager kbManager;
    private Vector3[] patrolPoints;
    private int currentPatrolIndex = 0;

    // Stato dell'agente
    private enum EnemyState { Patrolling, Intercepting }
    private EnemyState currentState = EnemyState.Patrolling;
    Vector3[] directions = new Vector3[1] { Vector3.zero };

    private GameObject robotAgent;
    private Vector3 targetDirection = Vector3.zero;
    private List<Vector3> currentPath = new List<Vector3>();
    private List<Vector3> interceptPath = new List<Vector3>();
    private Vector3? interceptDestination = null;
    private Vector3 currentPatrolPoint;
    private Vector3 lastRobotPos;
    private float interceptTimer;
    private bool checkingHostage = false;

    void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

    void Start()
    {
        kbManager = KbManager.Instance;

        // Inizializza i punti di pattugliamento sulla base della KB
        StartCoroutine(InitializeAndStart());
    }

    IEnumerator InitializeAndStart()
    {
        yield return new WaitUntil(() => kbManager.IsReady);
        yield return StartCoroutine(InitializeAgent());
        StartCoroutine(PatrolCoroutine());
        StartCoroutine(UpdateBeliefsCoroutine());
    }

    IEnumerator InitializeAgent()
    {
        // Inizializza la KB
        Task initKbTask = InitializeKnowledgeBase();
        yield return new WaitUntil(() => initKbTask.IsCompleted);

        // Inizializza le credenze
        Task initBeliefsTask = InitializeBeliefs();
        yield return new WaitUntil(() => initBeliefsTask.IsCompleted);

        // Inizializza le intenzioni
        InitializeIntentions();
    }

    private async Task InitializeKnowledgeBase()
    {
        // SetEnemyAgentPosition
        Task setEnemyPosTask = kbManager.SetHostageKnowledge("EnemyAgent", "EnemyAgent1");
        await setEnemyPosTask;
    }

    public async Task InitializeBeliefs()
    {
        // Recupera le posizioni degli ostaggi disponibili dalla KB
        Task<List<Vector3>> hostagesTask = kbManager.GetAvailableHostagePositionsByPriority("EnemyAgent");
        await hostagesTask;

        List<Vector3> hostagePositions = hostagesTask.Result;

        if (hostagePositions != null && hostagePositions.Count > 0)
        {
            // Imposta i patrol points sulla base delle posizioni ottenute
            patrolPoints = hostagePositions.ToArray();
            Debug.Log($"EnemyAgent conosce {patrolPoints.Length} ostaggi.");
        }
        else
        {
            Debug.LogWarning("Nessun ostaggio disponibile dalla KB. Utilizzo di patrol points predefiniti.");
            // Se la KB non restituisce posizioni, puoi usare dei punti di default
            patrolPoints = new Vector3[]
            {
            new Vector3(2, 0, 2),
            new Vector3(10, 0, 2),
            new Vector3(10, 0, 10),
            new Vector3(2, 0, 10)
            };
        }
    }

    public void InitializeIntentions()
    {
        // Imposta lo stato iniziale a Patrolling
        currentState = EnemyState.Patrolling;
        Debug.Log("EnemyAgent inizializzato con intenzione di pattugliare.");
    }


    /// <summary>
    /// Aggiorna le credenze in base alle informazioni presenti nella KB.
    /// </summary>
    IEnumerator UpdateBeliefsCoroutine()
    {
        while (true)
        {
            // Controllo se il gioco è terminato
            Task<bool> isGameOverTask = kbManager.isGameOver("EnemyAgent1");
            yield return new WaitUntil(() => isGameOverTask.IsCompleted);
            if (isGameOverTask.Result)
            {
                targetDirection = Vector3.zero;
                yield break;
            }


            // Recupera la lista degli ostaggi disponibili dalla KB
            Task<List<Vector3>> hostagesTask = kbManager.GetAvailableHostagePositionsByPriority("EnemyAgent");
            yield return new WaitUntil(() => hostagesTask.IsCompleted);

            if (hostagesTask.Result.Count > 0)
            {
                var oldLength = patrolPoints.Length;
                patrolPoints = hostagesTask.Result.ToArray();
                if (oldLength != patrolPoints.Length)
                    Debug.Log("EnemyAgent knows " + patrolPoints.Length + " hostages remaining");
                if (currentPatrolIndex >= patrolPoints.Length)
                    currentPatrolIndex = 0;
            }

            // Controlla il rilevamento tramite sensori
            GameObject _robotAgent = DetectRobotWithFrontSensors();
            if (_robotAgent != null)
            {
                robotAgent = _robotAgent;

                float distanceToTarget = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(robotAgent.transform.position.x, 0, robotAgent.transform.position.z)
                );
                if (distanceToTarget < 10f)
                {
                    Debug.Log("Game Over!");
                    targetDirection = Vector3.zero;
                    Task gameOverTask = kbManager.GameOver(isWin: false);
                    yield return new WaitUntil(() => gameOverTask.IsCompleted);
                    SceneManager.LoadScene("GameOverScene");

                    yield break;
                }
                // Aggiorna la KB
                Task updateDetection = kbManager.DetectedRobotAgent(robotAgent.transform.position);
                yield return new WaitUntil(() => updateDetection.IsCompleted);

                currentState = EnemyState.Intercepting;
                yield return new WaitForSeconds(5f);

                Task forgetDetection = kbManager.ResetDetectedRobotAgent();
                yield return new WaitUntil(() => forgetDetection.IsCompleted);
                robotAgent = null;
            }
            else
            {
                currentState = EnemyState.Patrolling;
            }
            yield return new WaitForSeconds(0.2f);
        }
    }

    /// <summary>
    /// Coroutine per la pattuglia continua.
    /// </summary>
    IEnumerator PatrolCoroutine()
    {
        while (true)
        {
            // Controllo se il gioco è terminato
            Task<bool> isGameOverTask = kbManager.isGameOver("EnemyAgent1");
            yield return new WaitUntil(() => isGameOverTask.IsCompleted);
            if (isGameOverTask.Result)
                yield break;

            // Se lo stato è Patrolling, calcola la direzione verso il patrol point corrente.
            if (currentState == EnemyState.Patrolling && patrolPoints.Length > 0)
            {
                // Reset interceptDestination quando si torna in pattuglia
                interceptDestination = null;
                currentPatrolPoint = patrolPoints[currentPatrolIndex];
                //Debug.Log(currentPatrolPoint);

                if (currentPath == null || currentPath.Count == 0)
                {
                    // Calcola il percorso usando A*
                    Task<List<Vector3>> pathTask = kbManager.FindShortestPath("EnemyAgent1", transform.position, currentPatrolPoint);
                    yield return new WaitUntil(() => pathTask.IsCompleted);
                    currentPath = pathTask.Result;

                    // Se non viene trovato alcun percorso,
                    if (currentPath == null || currentPath.Count == 0)
                    {
                        Debug.LogWarning("Nessun percorso trovato");
                    }
                }
                // Segui il percorso calcolato
                if (currentPath != null && currentPath.Count > 0)
                {
                    // Prendi il primo punto del percorso
                    Vector3 nextPoint = currentPath[0];
                    targetDirection = (nextPoint - transform.position).normalized;
                    targetDirection.y = 0;

                    // Se siamo vicini al punto, passa al successivo del percorso
                    float nextPointHDistance = Vector3.Distance(
                        new Vector3(transform.position.x, 0, transform.position.z),
                        new Vector3(nextPoint.x, 0, nextPoint.z));

                    if (nextPointHDistance < 2f)
                    {
                        currentPath.RemoveAt(0);
                    }
                }
                // Se enemy è abbastanza vicino al patrol point, passa al successivo.
                float horizontalDistance = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(currentPatrolPoint.x, 0, currentPatrolPoint.z)
                );

                if (horizontalDistance < 5f)
                {
                    targetDirection = Vector3.zero;
                    yield return StartCoroutine(VerifyHostagePresence(currentPatrolPoint));
                    currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                    currentPath = null; // Reset per calcolare un nuovo percorso al prossimo ciclo
                    interceptDestination = null; // Assicuriamoci di ricalcolare in intercept se necessario

                }
            }

            else if (currentState == EnemyState.Intercepting && robotAgent != null)
            {
                // Se non abbiamo già calcolato una destinazione intercept
                if (!interceptDestination.HasValue)
                {
                    targetDirection = Vector3.zero;
                    Debug.Log("Calcolo nuova destinazione intercept.");
                    Task<Vector3?> nearestHostageTask = kbManager.GetNearestHostage(transform.position);
                    yield return new WaitUntil(() => nearestHostageTask.IsCompleted);
                    Vector3? nearestHostagePos = nearestHostageTask.Result;
                    if (nearestHostagePos.HasValue)
                    {
                        interceptDestination = nearestHostagePos.Value;
                        lastRobotPos = robotAgent.transform.position;
                        interceptTimer = Time.time;  // memorizza il tempo del calcolo
                        Debug.Log($"Destinazione di intercettazione calcolata: {interceptDestination.Value}");
                        //patrolPoints[currentPatrolIndex] = interceptDestination.Value;

                    }
                }

                // Ricalcola il percorso solo se necessario: se interceptPath è null o se il tempo trascorso supera un certo limite
                if (interceptPath == null || interceptPath.Count == 0)
                {
                    Task<List<Vector3>> interceptTask = kbManager.FindShortestPath("EnemyAgent1", transform.position, interceptDestination.Value);
                    yield return new WaitUntil(() => interceptTask.IsCompleted);
                    interceptPath = interceptTask.Result;
                }

                if (interceptPath != null && interceptPath.Count > 0)
                {
                    Vector3 nextPoint = interceptPath[0];
                    targetDirection = (nextPoint - transform.position).normalized;
                    targetDirection.y = 0;

                    // Se siamo vicini al punto, passa al successivo del percorso
                    float horizontalDistance = Vector3.Distance(
                        new Vector3(transform.position.x, 0, transform.position.z),
                        new Vector3(nextPoint.x, 0, nextPoint.z));

                    if (horizontalDistance < 2f)
                    {
                        interceptPath.RemoveAt(0);
                    }
                }

                // Se l'EnemyAgent raggiunge la destinazione o se è trascorso un tempo minimo, resetta la destinazione per ricalcolare il percorso
                if (Vector3.Distance(transform.position, interceptDestination.Value) < 5f || (Time.time - interceptTimer > 30f))
                {
                    Debug.Log("Destinazione intercept raggiunta.");
                    if (Time.time - interceptTimer > 30f)
                    {
                        Debug.Log("Destinazione intercept non raggiunta per tempo scaduto, ricalcolo se necessario.");
                    }
                    targetDirection = Vector3.zero;
                    yield return StartCoroutine(VerifyHostagePresence(interceptDestination.Value));
                    interceptDestination = null;
                    currentPath = null;
                    interceptPath = null;
                    currentState = EnemyState.Patrolling;
                }
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator VerifyHostagePresence(Vector3 target)
    {
        // Controlla se l'agente è abbastanza vicino al target
        float distanceToTarget = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(target.x, 0, target.z)
        );

        if (distanceToTarget < verificationRange)
        {
            // Verifica se l'ostaggio è ancora presente nella KB
            Task<bool> isHostagePresentTask = kbManager.IsHostagePresent(target);
            yield return new WaitUntil(() => isHostagePresentTask.IsCompleted);
            bool isHostagePresent = isHostagePresentTask.Result;

            if (!isHostagePresent)
            {
                Debug.Log($"Ostaggio a {target} non è più presente.");
                // Rimuovi l'ostaggio dalla conoscenza dell'agente
                Task forgetHostageTask = kbManager.ForgetHostage(target);
                yield return new WaitUntil(() => forgetHostageTask.IsCompleted);
            }
            else
            {
                yield return StartCoroutine(RandomRotationRoutine());
            }
            Debug.Log("Verification: " + isHostagePresent + ", " + target);
        }
    }

    IEnumerator RandomRotationRoutine()
    {
        Debug.Log("Inizio rotazione casuale");

        checkingHostage = true;

        // Resetta la direzione del movimento
        targetDirection = Vector3.zero;

        // Genera parametri casuali
        float rotationTime = Random.Range(1f, 2f);
        float randomRotationAngle = Random.Range(90f, 360f);
        float rotationDirection = Random.Range(0, 2) * 2 - 1; // -1 o 1

        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0, randomRotationAngle * rotationDirection, 0);

        float elapsedTime = 0f;

        while (elapsedTime < rotationTime)
        {
            transform.rotation = Quaternion.Slerp(
                startRotation,
                endRotation,
                elapsedTime / rotationTime
            );

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        checkingHostage = false;
        Debug.Log("Check completato");
    }

    GameObject DetectRobotWithFrontSensors()
    {
        foreach (var direction in directions)
        {
            if (Physics.Raycast(transform.position, direction.normalized, out RaycastHit hit, detectionRange))
            {
                if (hit.collider.CompareTag("RobotAgent"))
                {
                    Debug.Log("EnemyAgent: trovato RobotAgent");
                    Debug.DrawRay(transform.position, direction.normalized * detectionRange, Color.red);
                    robotAgent = hit.collider.gameObject;
                    return robotAgent;
                }
            }
        }
        return null;
    }

    void FixedUpdate()
    {
        UpdateSensorDirections();
        if (checkingHostage)
        {
            // Blocca qualsiasi movimento fisico durante la rotazione
            rigidbody.velocity = Vector3.zero;
            return;
        }
        // Leggi la velocità corrente del rigidbody
        Vector3 currentVelocity = rigidbody.velocity;

        // Ruota l'agente verso la direzione del target
        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, 0.1f);
        }

        // Imposta la velocità desiderata in base a targetDirection e a una velocità prefissata
        float speed = (currentState == EnemyState.Intercepting) ? interceptSpeed : patrolSpeed;
        Vector3 desiredVelocity = targetDirection * speed;

        // Se siamo abbastanza vicini all'ostaggio, rallenta
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            //Debug.Log("Patrol is " + targetHostage);

            float distanceToTarget = Vector3.Distance(transform.position, currentPatrolPoint);
            if (distanceToTarget < 7f)
            {
                // Debug.Log("Rallenta!");
                desiredVelocity = targetDirection * speed * 0.5f;
            }
        }

        // Interpola la velocità corrente verso quella desiderata
        Vector3 smoothVelocity = Vector3.Lerp(currentVelocity, desiredVelocity, 0.1f);

        // Applica la nuova velocità
        rigidbody.velocity = smoothVelocity;
    }


    void UpdateSensorDirections()
    {
        directions = new Vector3[]
        {
        transform.forward,                  // Frontale
        transform.forward + transform.right,  // Diagonale avanti-destra
        transform.forward - transform.right,   // Diagonale avanti-sinistra
        transform.forward + 0.5f *transform.right,  // Diagonale avanti-destra
        transform.forward - 0.5f *transform.right,   // Diagonale avanti-sinistra

        };
    }

    public void OnDrawGizmos()
    {
        if (directions == null) return;

        foreach (var direction in directions)
        {
            Debug.DrawRay(transform.position, direction.normalized * 10, Color.yellow);
        }
        if (robotAgent != null)
        {
            Gizmos.DrawSphere(new Vector3(robotAgent.transform.position.x, 1, transform.position.z), 0.5f);
        }


    }
}
