using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;


public class RobotAgent : MonoBehaviour
{
    // Parametri di movimento e ricerca
    public float moveSpeed = 4f;
    public float verificationRange = 10f;

    // Variabili interne
    private new Rigidbody rigidbody;
    private KbManager kbManager;
    private Vector3[] hostagesToRescue;
    private Vector3 rescuePoint;
    private Vector3? enemyPosition;    // posizione comunicata dal drone
    private Vector3 dronePosition;

    // Stato dell'agente
    private enum RobotState { Idle, Navigating, Recalculating }
    private RobotState currentState = RobotState.Idle;
    private Vector3 targetDirection = Vector3.zero;
    private List<Vector3> currentPath = new List<Vector3>();

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

        // Inizializza la KB
        Task initKbTask = InitializeKnowledgeBase();
        yield return new WaitUntil(() => initKbTask.IsCompleted);

        // Inizializza le credenze
        Task initBeliefsTask = InitializeBeliefs();
        yield return new WaitUntil(() => initBeliefsTask.IsCompleted);

        StartCoroutine(UpdateBeliefsCoroutine());
        yield return StartCoroutine(WaitForDronePosition());
        StartCoroutine(RescueCoroutine());
    }


    private async Task InitializeKnowledgeBase()
    {
        // SetEnemyAgentPosition
        Task setEnemyPosTask = kbManager.SetHostageKnowledge("RobotAgent", "RobotAgent1");
        await setEnemyPosTask;
    }

    public async Task InitializeBeliefs()
    {
        // Recupera le posizioni degli ostaggi disponibili dalla KB
        Task<List<Vector3>> hostagesTask = kbManager.GetAvailableHostagePositionsByPriority("RobotAgent");
        await hostagesTask;

        List<Vector3> hostagePositions = hostagesTask.Result;
        if (hostagePositions != null && hostagePositions.Count > 0)
        {
            // Imposta i hostage points sulla base delle posizioni ottenute
            hostagesToRescue = hostagePositions.ToArray();
            Debug.Log($"RobotAgent conosce {hostagesToRescue.Length} ostaggi.");
        }
        else
        {
            Debug.Log("Tutti gli ostaggi sono liberi");
            Debug.Log("Game Over!");
            targetDirection = Vector3.zero;
            Task gameOverTask = kbManager.GameOver(isWin: false);
            await gameOverTask;
        }
    }


    IEnumerator WaitForDronePosition()
    {
        while (true)
        {
            Task<Vector3?> droneTask = kbManager.GetDronePosition();
            yield return new WaitUntil(() => droneTask.IsCompleted);

            if (droneTask.Result.HasValue)
            {
                // Drone position trovata!
                dronePosition = droneTask.Result.Value;
                yield break; // esci e prosegui
            }
            else
            {
                // Nessuna posizione disponibile, attendi un po’ e riprova
                yield return new WaitForSeconds(0.2f);
            }
        }
    }


    /// <summary>
    /// Aggiorna la conoscenza a intervalli regolari
    /// </summary>
    IEnumerator UpdateBeliefsCoroutine()
    {
        while (true)
        {
            // Controllo se il gioco è terminato
            Task<bool> isGameOverTask = kbManager.isGameOver("RobotAgent1");
            yield return new WaitUntil(() => isGameOverTask.IsCompleted);
            if (isGameOverTask.Result)
            {
                targetDirection = Vector3.zero;
                yield break;
            }

            // Aggiorna la conoscenza sulla posizione del nemico eventualmente comunicata dal drone
            Task<Vector3?> enemyTask = kbManager.GetEnemyPosition();
            yield return new WaitUntil(() => enemyTask.IsCompleted);
            if (enemyTask.Result.HasValue)
            {
                enemyPosition = enemyTask.Result.Value;
                Debug.Log("ep: " + enemyPosition);
            }


            /*if (enemyPosition != null)
                Debug.Log("Enemy position is: " + enemyPosition);
            */
            // Aggiorna la conoscenza sulla posizione del drone
            Task<Vector3?> droneTask = kbManager.GetDronePosition();
            yield return new WaitUntil(() => droneTask.IsCompleted);
            dronePosition = droneTask.Result.Value;
            // Debug.Log("Drone position is: " + dronePosition);

            // Recupera la lista degli ostaggi noti
            Task<List<Vector3>> hostagesTask = kbManager.GetAvailableHostagePositionsByPriority("RobotAgent");
            yield return new WaitUntil(() => hostagesTask.IsCompleted);

            if (hostagesTask.Result.Count > 0)
            {
                var oldLength = hostagesToRescue.Length;
                hostagesToRescue = hostagesTask.Result.ToArray();
                if (oldLength != hostagesToRescue.Length)
                    Debug.Log("RobotAgent knows " + hostagesToRescue.Length + " hostages remaining");
            }
            else
            {
                Debug.Log("Tutti gli ostaggi sono liberi");
                targetDirection = Vector3.zero;
                Task gameOverTask = kbManager.GameOver(isWin: true);
                yield return new WaitUntil(() => gameOverTask.IsCompleted);
                SceneManager.LoadScene("WinScene");
                yield break;
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    private Vector3 ChooseHostagesToRescue()
    {
        Vector3 targetPosition;
        if (enemyPosition == null)
        {
            // Nemico sconosciuto: scegli ostaggio più vicino al drone (zona presumibilmente controllata)
            targetPosition = ChooseClosestHostageToDrone();
        }
        else
        {
            // Nemico noto: scegli l'ostaggio in una zona sicura
            targetPosition = ChooseSafeHostage();
        }

        return targetPosition;
    }

    /// <summary>
    /// Seleziona l’ostaggio più vicino al drone.
    /// </summary>
    Vector3 ChooseClosestHostageToDrone()
    {
        if (hostagesToRescue.Length == 0) return transform.position;
        Vector3 chosen = hostagesToRescue[0];
        float minDist = Vector3.Distance(dronePosition, chosen);
        foreach (Vector3 hostage in hostagesToRescue)
        {
            float d = Vector3.Distance(dronePosition, hostage);
            //Debug.Log($"[RobotAgent] Distance from drone to hostage {hostage} = {d}");
            if (d < minDist)
            {
                chosen = hostage;
                minDist = d;
            }

        }
        //Debug.Log($"[RobotAgent] Chosen hostage near Drone = {chosen}");
        return chosen;
    }

    /// <summary>
    /// Seleziona un ostaggio “sicuro” in base alla distanza dal nemico.
    /// </summary>
    Vector3 ChooseSafeHostage()
    {
        if (hostagesToRescue.Length == 0) return transform.position;
        Vector3 chosen = hostagesToRescue[0];
        float bestScore = float.MinValue;
        foreach (Vector3 hostage in hostagesToRescue)
        {
            // Maggiore distanza dal nemico => minore rischio
            float riskScore = Vector3.Distance(enemyPosition.Value, hostage);
            if (riskScore > bestScore)
            {
                bestScore = riskScore;
                chosen = hostage;
            }
        }
        return chosen;
    }

    IEnumerator RescueCoroutine()
    {
        while (true)
        {
            // Controllo se il gioco è terminato
            Task<bool> isGameOverTask = kbManager.isGameOver("RobotAgent1");
            yield return new WaitUntil(() => isGameOverTask.IsCompleted);
            if (isGameOverTask.Result)
                yield break;

            if (rescuePoint == null || rescuePoint == Vector3.zero)
            {
                Debug.Log("RobotAgent state was: " + currentState);
                rescuePoint = ChooseHostagesToRescue();
                Debug.Log(rescuePoint);
                currentState = RobotState.Navigating;
            }

            // Se lo stato è Rescuing, calcola la direzione verso il hostage point corrente.
            if (currentState == RobotState.Navigating && rescuePoint != Vector3.zero)
            {

                if (currentPath == null || currentPath.Count == 0)
                {
                    // Calcola il percorso usando A*
                    Task<List<Vector3>> pathTask = kbManager.FindShortestPath("RobotAgent1", transform.position, rescuePoint);
                    yield return new WaitUntil(() => pathTask.IsCompleted);
                    currentPath = pathTask.Result;

                    // Se non viene trovato alcun percorso,
                    if (currentPath == null || currentPath.Count == 0)
                    {
                        Debug.LogWarning("Nessun percorso trovato.");
                    }
                }
                // Segui il percorso calcolato
                if (currentPath != null && currentPath.Count > 0)
                {
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

                // Durante il movimento, controlla se il nemico si avvicina troppo al percorso
                if (IsEnemyThreateningPath())
                {
                    currentState = RobotState.Recalculating;
                    //Debug.Log("ricalcolo");
                    // Ricalcola percorso e, eventualmente, target diverso
                    rescuePoint = Vector3.zero;
                    continue;
                }

                // Se enemy è abbastanza vicino al rescue point, passa al successivo.
                float horizontalDistance = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(rescuePoint.x, 0, rescuePoint.z)
                );

                if (horizontalDistance < 5f)
                {
                    //Debug.Log("EnemyAgent vicino al rescue point. Passaggio al prossimo.");
                    targetDirection = Vector3.zero;
                    yield return StartCoroutine(FreeHostage(rescuePoint));
                    rescuePoint = Vector3.zero;
                }
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    /// <summary>
    /// Verifica se il nemico è troppo vicino ad almeno un nodo del percorso corrente.
    /// </summary>
    bool IsEnemyThreateningPath()
    {
        if (enemyPosition == null) return false;

        float threatDistance = 25f; // soglia di pericolo
        foreach (Vector3 node in currentPath)
        {
            Debug.Log("enemy distance: " + Vector3.Distance(enemyPosition.Value, node));
            if (Vector3.Distance(enemyPosition.Value, node) < threatDistance)
            {
                Debug.Log("TRUE");
                return true;
            }
        }
        return false;
    }

    IEnumerator FreeHostage(Vector3 target)
    {
        // Controlla se l'agente è abbastanza vicino al target
        float distanceToTarget = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(target.x, 0, target.z)
        );

        if (distanceToTarget < verificationRange)
        {
            Debug.Log("Libero" + target);
            // Verifica se l'ostaggio è ancora presente nella KB
            Task<bool> isHostagePresentTask = kbManager.IsHostagePresent(target);
            yield return new WaitUntil(() => isHostagePresentTask.IsCompleted);
            bool isHostagePresent = isHostagePresentTask.Result;

            if (isHostagePresent)
            {
                Debug.Log($"Ostaggio a {target} liberato");
                // Rimuovi l'ostaggio dalla conoscenza dell'agente
                Task forgetHostageTask = kbManager.FreeHostage(target);
                yield return new WaitUntil(() => forgetHostageTask.IsCompleted);
            }
        }
        yield break;
    }

    void FixedUpdate()
    {
        // Leggi la velocità corrente del rigidbody
        Vector3 currentVelocity = rigidbody.velocity;

        // Ruota l'agente verso la direzione del target
        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, 0.1f);
        }

        // Imposta la velocità desiderata in base a targetDirection e a una velocità prefissata
        Vector3 desiredVelocity = targetDirection * moveSpeed;

        // Se siamo abbastanza vicini all'ostaggio, rallenta
        if (rescuePoint != null && rescuePoint != Vector3.zero)
        {

            float distanceToTarget = Vector3.Distance(transform.position, rescuePoint);
            if (distanceToTarget < 7f)
            {
                // Debug.Log("Rallenta!");
                desiredVelocity = targetDirection * moveSpeed * 0.5f;
            }
        }

        // Interpola la velocità corrente verso quella desiderata
        Vector3 smoothVelocity = Vector3.Lerp(currentVelocity, desiredVelocity, 0.1f);

        // Applica la nuova velocità
        rigidbody.velocity = smoothVelocity;
    }

}