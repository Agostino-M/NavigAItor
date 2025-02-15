using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class NavigationState : State
{
    // Attributes
    private RobotController robotController;
    private bool destinationReached = false;
    private int currentCornerIndex = 0;
    public List<Cell> path;

    public NavigationState(StateMachine stateMachine, List<Cell> path) : base(stateMachine)
    {
        this.robotController = stateMachine.gameObject.GetComponent<RobotController>();
        this.path = path;
    }

    public override void EnterState()
    {
        Debug.Log("Entered the NAVIGATION state! Robot is moving to specified destination...");

        // Ottengo tutte le posizioni del percorso
        /*foreach (Cell cell in path)
        {
            Debug.Log(cell.GetWorldPosition());
        }*/

        // Imposta il comando di navigazione
        robotController.SetMoving(true);
    }

    public override void ExecuteState()
    {
        if (GridManager.Instance.checkEnemyInPath(path.GetRange(currentCornerIndex, path.Count - currentCornerIndex)))
        {

            stateMachine.SetState(new PlanningState(stateMachine));
        }
        Vector3 targetPosition = path[currentCornerIndex].GetWorldPosition();

        if (robotController.enemyDetected)
        {
            if (robotController.isRecalculating)
            {
                // Passa allo stato ESCAPE per gestire la collisione
                Debug.Log("Escape");
                stateMachine.SetState(new EscapeState(stateMachine));
                return;
            }
        }
        else if (currentCornerIndex < path.Count)
        {
            // Ottieni la prossima posizione target dal percorso


            // Aggiorna la posizione del robot
            bool rotatedToTarget = robotController.RotateToTarget(targetPosition);

            if (rotatedToTarget)
            {
                bool movedToTarget = robotController.MoveToTarget(targetPosition);

                if (movedToTarget)
                {
                    //Debug.Log("Posizione punto " + currentCornerIndex + ": " + targetPosition + " raggiunta.");

                    // Move to the next corner
                    currentCornerIndex++;
                }
            }

        }

        if (currentCornerIndex >= path.Count)
        {
            this.destinationReached = true;
        }

        if (destinationReached)
        {
            stateMachine.SetState(new ArrivalState(stateMachine));
        }
    }



    public override void ExitState()
    {
        // Disabilita il movimento
        robotController.SetMoving(false);

        Debug.Log("Exited the NAVIGATION state!");
    }
}
