using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public Camera[] cameras; // Array di telecamere
    private int currentCameraIndex = 0; // Indice della telecamera attiva

    public void SwitchCamera()
    {
        if (cameras.Length == 0) return;

        // Disabilita la telecamera corrente
        cameras[currentCameraIndex].gameObject.SetActive(false);

        // Passa alla telecamera successiva
        currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;

        // Abilita la nuova telecamera
        cameras[currentCameraIndex].gameObject.SetActive(true);
    }
}
