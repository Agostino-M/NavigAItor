using UnityEngine;

public class EscapeState : State
{
    private RobotController robotController;
    private Vector3 escapeDirection;
    private bool escapeComplete = false;
    private float safeDistance = 5f;
    private float escapeStep = 2f; // Distanza da percorrere ad ogni aggiornamento

    public EscapeState(StateMachine stateMachine) : base(stateMachine)
    {
        robotController = stateMachine.gameObject.GetComponent<RobotController>();
    }

    public override void EnterState()
    {
        Debug.Log("Entered the ESCAPE state! Robot is escaping from enemy...");

        // Calcola la direzione iniziale di fuga
        if (robotController.enemyDetected)
        {
            escapeDirection = (robotController.transform.position - robotController.enemyPosition).normalized;
        }
        else
        {
            escapeDirection = Vector3.back;
        }

        robotController.SetMoving(true);
    }

    public override void ExecuteState()
    {
        // Se il nemico è ancora rilevato, ricalcola la direzione
        if (robotController.enemyDetected)
        {
            escapeDirection = (robotController.transform.position - robotController.enemyPosition).normalized;
        }

        // Calcola la posizione target moltiplicando l'escapeDirection per escapeStep
        Vector3 targetPosition = robotController.transform.position + escapeDirection * escapeStep;

        // Ruota verso il target
        bool rotated = robotController.RotateToTarget(targetPosition);
        if (rotated)
        {
            // Muove il robot verso il target
            bool moved = robotController.MoveToTarget(targetPosition);
            if (moved)
            {
                Debug.Log("Escaping... moved to position: " + targetPosition);
            }
        }

        // Verifica se il robot è a distanza di sicurezza dal nemico oppure se il nemico non è più rilevato
        if (robotController.enemyDetected)
        {
            float distanceFromEnemy = Vector3.Distance(robotController.transform.position, robotController.enemyPosition);
            if (distanceFromEnemy > safeDistance)
            {
                escapeComplete = true;
            }
        }
        else
        {
            escapeComplete = true;
        }

        // Se la fuga è completata, passa a un altro stato (ad esempio, ricalcola il percorso)
        if (escapeComplete)
        {
            stateMachine.SetState(new PlanningState(stateMachine));
        }
    }

    public override void ExitState()
    {
        robotController.SetMoving(false);
        Debug.Log("Exited the ESCAPE state!");
        robotController.enemyDetected = false;
    }
}
