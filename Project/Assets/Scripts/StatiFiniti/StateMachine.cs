using UnityEngine;
public class StateMachine : MonoBehaviour
{
    // Attributes
    private State currentState;

    // Method to set state
    public void SetState(State newState)
    {

        // If the machine is in a current state, gently exit the current state before going into a new state
        if (currentState != null)
        {
            currentState.ExitState();
        }

        // Set the new state
        currentState = newState;
        currentState.EnterState();
    }

    // Function to get the current state
    public State GetCurrentState()
    {
        return currentState;
    }

    private void Update()
    {
        if (currentState != null)
        {
            currentState.ExecuteState();
        }
    }
}