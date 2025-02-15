using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
using System.Collections.Generic;
using Neo4j.Driver;
using System.Threading.Tasks;

public class DroneAgent : MonoBehaviour
{

    private new Rigidbody rigidbody;
    private AudioSource audioSource;
    public GameObject enemyAgent;
    private KbManager kbManager;

    public AudioClip droneStart;
    public int explorationSpeed { get; private set; } = 10;
    public int followSpeed { get; private set; } = 20;
    private Vector3 targetDirection = Vector3.zero;
    private bool enemyDetected = false;
    private float baseSize = 20f;
    private Vector3? currentTarget;

    void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

    void Start()
    {
        transform.Find("FireComplex").gameObject.SetActive(false);
        enemyAgent = GameObject.FindGameObjectsWithTag("EnemyAgent")[0];
        kbManager = KbManager.Instance;

        StartCoroutine(InitializeAndStart());
    }

    IEnumerator InitializeAndStart()
    {
        yield return new WaitUntil(() => kbManager.IsReady);
        yield return StartCoroutine(InitializeKnowledgeBase());
        yield return StartCoroutine(ExploreAndTrack());
    }


    IEnumerator InitializeKnowledgeBase()
    {
        // SetEnemyAgentPosition
        Task setEnemyPosTask = kbManager.ConnectToRobot("DroneAgent1", "RobotAgent1");
        yield return new WaitUntil(() => setEnemyPosTask.IsCompleted);

        Task updatePositionTask = kbManager.UpdateDronePosition("DroneAgent1", transform.position);
        yield return new WaitUntil(() => updatePositionTask.IsCompleted);

    }

    /// <summary>
    /// Coroutine che rileva l'enemy e aggiorna la KB a intervalli brevi.
    /// </summary>
    IEnumerator ExploreAndTrack()
    {
        while (true)
        {
            // Controllo se il gioco è terminato
            Task<bool> isGameOverTask = kbManager.isGameOver("DroneAgent1");
            yield return new WaitUntil(() => isGameOverTask.IsCompleted);
            if (isGameOverTask.Result)
            {
                targetDirection = Vector3.zero;
                yield break;
            }

            // Rileva il nemico
            enemyDetected = EnemyDetection();

            if (enemyDetected)
            {
                // Aggiorna la KB con la posizione del nemico
                Task updateTask = kbManager.UpdateEnemyAgentPosition("EnemyAgent", "EnemyAgent1", enemyAgent.transform.position);
                yield return new WaitUntil(() => updateTask.IsCompleted);

                // Segui il nemico
                targetDirection = (enemyAgent.transform.position - transform.position).normalized;
                targetDirection.y = 0; // Mantieni la direzione orizzontale

                // Segnala la posizione del nemico alla KB
                Task reportEnemyTask = kbManager.ReportEnemyPosition("DroneAgent1", enemyAgent.transform.position);
                yield return new WaitUntil(() => reportEnemyTask.IsCompleted);
            }
            else
            {
                if (!currentTarget.HasValue)
                {
                    // Prende l'ultima posizione nota del nemico
                    Task<Vector3?> inferredTargetTask = kbManager.InferBestSearchZone();
                    yield return new WaitUntil(() => inferredTargetTask.IsCompleted);
                    Vector3? inferredTarget = inferredTargetTask.Result;

                    if (inferredTarget.HasValue)
                    {
                        currentTarget = inferredTarget.Value;
                        Debug.Log("Dirigendosi verso zona di interesse: " + inferredTarget.Value);
                    }
                    else
                    {
                        // Se nessuna zona interessante è trovata, esplora aree meno visitate
                        Task<Vector3?> unexploredZoneTask = kbManager.GetLeastVisitedZone("DroneAgent", "DroneAgent_01");
                        yield return new WaitUntil(() => unexploredZoneTask.IsCompleted);
                        Vector3? unexploredZone = unexploredZoneTask.Result;

                        if (unexploredZone.HasValue)
                        {
                            currentTarget = unexploredZone.Value;
                            Debug.Log("Esplorando area poco visitata: " + unexploredZone.Value);
                        }
                        else
                        {
                            // Se non ci sono suggerimenti dalla KB, esplora casualmente
                            Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                            currentTarget = randomDirection.normalized;
                        }
                        // Calcola la direzione solo quando viene assegnato un nuovo target
                        targetDirection = (currentTarget.Value - transform.position).normalized;
                        targetDirection.y = 0;
                    }
                }

                float horizontalDistance = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(currentTarget.Value.x, 0, currentTarget.Value.z)
                );

                if (horizontalDistance < 5f)
                {
                    targetDirection = Vector3.zero;
                    currentTarget = null;
                }


            }
            // Aggiorna la posizione del drone nella KB
            Task updatePositionTask = kbManager.UpdateDronePosition("DroneAgent1", transform.position);
            yield return new WaitUntil(() => updatePositionTask.IsCompleted);
            yield return new WaitForSeconds(0.2f);
        }
    }


    /// <summary>
    /// Rileva enemyAgent all'interno del campo visivo
    /// </summary>
    bool EnemyDetection()
    {
        // Vettore dalla posizione del drone al nemico
        Vector3 toEnemy = enemyAgent.transform.position - transform.position;

        // Calcola la distanza lungo la direzione down
        float d = Vector3.Dot(toEnemy, Vector3.down);

        // Se il nemico non è sotto il drone o è oltre l'altezza della piramide, esci
        if (d < 0 || d > transform.position.y)
            return false;

        // Calcola la componente orizzontale (proiezione sul piano perpendicolare a Vector3.down)
        Vector3 horizontal = toEnemy - Vector3.down * d;

        // Calcola la distanza massima consentita orizzontalmente a distanza d:
        // all'apice (d=0) deve essere 0, alla base (d=height) deve essere baseSize/2.
        float allowedHalfSize = (baseSize / 2f) * (d / transform.position.y);

        // Verifica se le componenti orizzontali (x e z) sono all'interno dei limiti
        if (Mathf.Abs(horizontal.x) <= allowedHalfSize && Mathf.Abs(horizontal.z) <= allowedHalfSize)
            return true;

        return false;

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

        // Utilizziamo una Lerp per smussare le variazioni.
        Vector3 desiredVelocity = targetDirection * (enemyDetected ? followSpeed : explorationSpeed);
        Vector3 smoothVelocity = Vector3.Lerp(currentVelocity, desiredVelocity, 0.1f);
        rigidbody.velocity = smoothVelocity;

    }


    /// <summary>
    /// Show the agent's field of view in the scene view for debugging
    /// </summary>
    void OnDrawGizmos()
    {

        // Imposta il colore per i raggi (ad esempio, rosso)
        Gizmos.color = Color.yellow;

        // L'apice della piramide è la posizione del drone
        Vector3 apex = transform.position;

        // Definiamo l'altezza della piramide: qui usiamo la y del drone.
        // Assumendo che il drone sia sopra il terreno (y > 0) e che la piramide si estenda fino a y=0.
        float height = transform.position.y;

        // Calcoliamo la metà della dimensione della base
        float halfBase = baseSize / 2f;

        // Il centro della base si trova "height" unità sotto l'apice lungo Vector3.down
        Vector3 baseCenter = apex + Vector3.down * height;

        // Calcoliamo i quattro angoli della base (assumendo che la base sia allineata agli assi mondiali)
        Vector3 corner1 = baseCenter + new Vector3(halfBase, 0, halfBase);
        Vector3 corner2 = baseCenter + new Vector3(halfBase, 0, -halfBase);
        Vector3 corner3 = baseCenter + new Vector3(-halfBase, 0, -halfBase);
        Vector3 corner4 = baseCenter + new Vector3(-halfBase, 0, halfBase);

        // Disegna il perimetro della base
        Gizmos.DrawLine(corner1, corner2);
        Gizmos.DrawLine(corner2, corner3);
        Gizmos.DrawLine(corner3, corner4);
        Gizmos.DrawLine(corner4, corner1);

        // Disegna le linee che collegano l'apice con i quattro angoli della base
        Gizmos.DrawLine(apex, corner1);
        Gizmos.DrawLine(apex, corner2);
        Gizmos.DrawLine(apex, corner3);
        Gizmos.DrawLine(apex, corner4);
    }

}
