public abstract class State
{
    // Attributes
    protected StateMachine stateMachine;

    // Constructor
    public State(StateMachine sm)
    {
        // Set the state machine attribute
        this.stateMachine = sm;
    }

    // Method that handles actions when entering the state
    public abstract void EnterState();

    // Method that handles actions when the state is active
    public abstract void ExecuteState();

    // Method that handles actions when exiting the state
    public abstract void ExitState();
}