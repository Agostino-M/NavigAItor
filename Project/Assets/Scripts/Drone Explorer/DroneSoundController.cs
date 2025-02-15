using UnityEngine;

public class DroneSoundController : MonoBehaviour
{
    private AudioSource rotorSound;  // Assegna l'AudioSource nell'Inspector
    public Rigidbody droneRigidbody;  // Assegna il Rigidbody del drone per calcolare la velocità

    public float minPitch = 0.8f;  // Tonalità a riposo
    public float maxPitch = 1.5f;  // Tonalità alla massima velocità
    public float maxVelocity = 10f;  // Velocità massima del drone
    public bool trainingMode = false;

    void Start()
    {
        rotorSound = GetComponent<AudioSource>();
        rotorSound.loop = true;
    }

    void Update()
    {
        if (trainingMode) return;
        rotorSound.Play();
        AdjustRotorSound();
    }
 
    void AdjustRotorSound()
    {
        if (droneRigidbody != null)
        {
            // Calcola il pitch in base alla velocità
            float speed = droneRigidbody.velocity.magnitude;
            float pitch = Mathf.Lerp(minPitch, maxPitch, speed / maxVelocity);
            rotorSound.pitch = pitch;

            // Modifica il volume in base alla velocità
            rotorSound.volume = Mathf.Clamp01(speed / maxVelocity);
        }
    }
}
