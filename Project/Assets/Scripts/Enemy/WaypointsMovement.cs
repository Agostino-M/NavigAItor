using System.Collections;
using UnityEngine;

public class WaypointsMovement : MonoBehaviour
{
    public float speed = 3f; // Velocità del nemico
    public float avoidanceRadius = 1f; // Raggio per evitare ostacoli
    public float pauseDuration = 4f; // Tempo di pausa al waypoint
    public Transform[] waypoints; // Array dei waypoints
    private int currentWaypointIndex = 0; // Indice del waypoint corrente
    private bool isPaused = false; // Indica se il nemico è in pausa
    public Transform turret;
    public float turretRotationSpeed = 1f; // Velocità di rotazione del cannone
    public float turretRotationInterval = 5f; // Intervallo tra i cambi di direzione del cannone
    private float turretTargetAngle;
    private float turretRotationTimer;
    private void Start()
    {
        // Avvio casuale tra 1 e 5 secondi
        float randomStartDelay = Random.Range(1f, 5f);
        StartCoroutine(StartMovingAfterDelay(randomStartDelay));
    }

    private IEnumerator StartMovingAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isPaused = false;
    }

    private void Update()
    {
        if (isPaused) return; // Non muovere durante la pausa

        if (waypoints.Length == 0) return; // Se non ci sono waypoint, non muovere

        Transform targetWaypoint = waypoints[currentWaypointIndex];

        // Calcola la direzione verso il target
        Vector3 direction = (targetWaypoint.position - transform.position).normalized;
        direction.y = 0; // Ignora l'asse Y per mantenere il movimento solo sul piano XZ

        // Evita gli ostacoli lungo il percorso
        direction += AvoidObstacles();

        // Muovi il nemico verso il target
        transform.position += direction * speed * Time.deltaTime;

        // Ruota il nemico verso il target (solo sull'asse Y)
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed);
        }

        // Controlla se il nemico ha raggiunto il waypoint
        if (Vector3.Distance(transform.position, targetWaypoint.position) < 0.5f)
        {
            StartCoroutine(PauseAtWaypoint());
        }

        RotateTurret();
    }

    private IEnumerator PauseAtWaypoint()
    {
        isPaused = true;
        yield return new WaitForSeconds(pauseDuration);
        isPaused = false;
        MoveToNextWaypoint();
    }

    private void MoveToNextWaypoint()
    {
        currentWaypointIndex++;
        if (currentWaypointIndex >= waypoints.Length)
        {
            currentWaypointIndex = 0; // Ricomincia dal primo waypoint
        }
    }

    private Vector3 AvoidObstacles()
    {
        Vector3 avoidance = Vector3.zero;

        Collider[] hits = Physics.OverlapSphere(transform.position, avoidanceRadius);
        foreach (Collider hit in hits)
        {
            if (hit.gameObject != gameObject && hit.gameObject.tag != "Boundary")
            {
                Debug.Log("Ostacolo trovato: " + hit.gameObject.name);
                Vector3 directionAway = transform.position - hit.transform.position;
                directionAway.y = 0;
                avoidance += directionAway.normalized;
            }
        }

        return avoidance.normalized; // Normalizzare la direzione di evitamento
    }

    private void RotateTurret()
    {
        if (turret == null)
            return;

        // Timer per cambiare l'angolo di rotazione del cannone
        turretRotationTimer += Time.deltaTime;
        if (turretRotationTimer >= turretRotationInterval)
        {
            turretTargetAngle = Random.Range(-90f, 90f); // Nuovo angolo target casuale
            turretRotationTimer = 0f;
        }

        // Rotazione graduale del cannone verso l'angolo target
        Quaternion targetRotation = Quaternion.Euler(0, turretTargetAngle, 0);
        turret.localRotation = Quaternion.Slerp(turret.localRotation, targetRotation, turretRotationSpeed * Time.deltaTime);
    }

}
