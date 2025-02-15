using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class PlanningState : State
{
    private RobotController robotController;
    private bool planningComplete = false;
    private Vector3 destination;
    private Cell[,] grid;
    public List<Cell> path;

    // Riferimento al PathDrawer da riutilizzare
    private PathDrawer pathDrawer;

    public PlanningState(StateMachine stateMachine) : base(stateMachine)
    {
        this.robotController = stateMachine.gameObject.GetComponent<RobotController>();
        this.destination = GameObject.Find("GoalArea").transform.position;
        this.grid = GridManager.Instance.GetGrid();
    }

    public override void EnterState()
    {
        Debug.Log("Stato PLANNING: Pianificazione del percorso verso la destinazione...");
        Debug.Log("Destinazione: " + destination);

        // Cerca di riutilizzare il PathDrawer se già esiste
        pathDrawer = stateMachine.gameObject.GetComponent<PathDrawer>();
        if (pathDrawer == null)
        {
            pathDrawer = stateMachine.gameObject.AddComponent<PathDrawer>();
        }
        else
        {
            pathDrawer.ClearPath();
        }
        // In questo punto non abbiamo ancora il percorso calcolato, quindi DrawPath(path) va chiamato dopo
        stateMachine.StartCoroutine(ExecutePlanning());
    }

    public override void ExecuteState()
    {
        // Avvia la coroutine per eseguire la pianificazione
    }

    private IEnumerator ExecutePlanning()
    {
        // Otteniamo la posizione corrente del robot
        Vector3 robotPosition = stateMachine.gameObject.transform.position;

        // Otteniamo la cella di partenza e di destinazione del robot
        Cell startCell = GridManager.Instance.GetCellFromWorldPosition(robotPosition);
        Cell endCell = GridManager.Instance.GetCellFromWorldPosition(destination);

        Debug.Log("Cella di partenza: " + startCell);
        Debug.Log("Cella di destinazione: " + endCell);

        // Creiamo un'istanza di AStar
        AStar aStar = new AStar(grid, startCell, endCell);

        // Cerchiamo il percorso
        path = aStar.FindPath();

        if (path != null)
        {
            Debug.Log("Percorso trovato!");
            Debug.Log(path.Count + " celle nel percorso.");
            planningComplete = true;

            if (pathDrawer == null)
            {
                pathDrawer = stateMachine.gameObject.GetComponent<PathDrawer>() ?? stateMachine.gameObject.AddComponent<PathDrawer>();
            }
            else
            {
                pathDrawer.ClearPath();
            }
            pathDrawer.DrawPath(path);

            // Rimuove i waypoint troppo vicini al robot (evitando di modificare la lista mentre la si itera)
            while (path.Count > 0 && Vector3.Distance(robotPosition, path[0].GetWorldPosition()) < 1.0f)
            {
                path.RemoveAt(0);
            }

            yield return new WaitForSeconds(8);

            robotController.isRecalculating = false; // Resetta qui

            // Passa allo stato di navigazione
            stateMachine.SetState(new NavigationState(stateMachine, path));
            robotController.sensorEnabled = true; // Riattiva il sensore dopo il ricalcolo
        }
        else
        {
            yield return new WaitForSeconds(2);
            Debug.Log("Percorso non trovato!");

            // Torna alla visuale del robot in caso di errore
            robotController.isRecalculating = false;
            stateMachine.SetState(new StandbyState(stateMachine));
        }
    }

    public override void ExitState()
    {
        Debug.Log("Uscito dallo stato PLANNING.");
    }
}
