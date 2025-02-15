using UnityEngine;

public class ArrivalState : State
{
    public ArrivalState(StateMachine stateMachine) : base(stateMachine) { }

    public override void EnterState()
    {
        Debug.Log("Arrivato alla destinazione! Stato di ARRIVAL.");
    }

    public override void ExecuteState()
    {
        // Get the robot controller
        RobotController robotController = stateMachine.gameObject.GetComponent<RobotController>();

        Debug.Log("Arrivato! Pronto per la prossima Destinazione");
        robotController.ClearDestination();
        stateMachine.SetState(new StandbyState(stateMachine));

        // Recupera il PathDrawer e disabilita la dissolvenza automatica
        PathDrawer pd = stateMachine.gameObject.GetComponent<PathDrawer>();
        if (pd != null)
        {
            pd.autoFadeOut = false;  // impedisce la dissolvenza automatica
            pd.ClearPath();          // cancella il percorso disegnato
        }

        // Destroy the current grid
        GridManager.Instance.GenerateGrid();
        GridManager.Instance.DetectBlockedCells();
    }

    public override void ExitState()
    {
        Debug.Log("Uscendo dallo stato ARRIVAL.");
    }
}