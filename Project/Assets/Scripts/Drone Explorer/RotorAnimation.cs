using UnityEngine;

public class RotorAnimation : MonoBehaviour
{
    public float baseSpeed = 1000f;        // Velocità di base dei rotori
    public float maxSpeedMultiplier = 2f; // Moltiplicatore massimo della velocità
    public float wingTiltAngle = 90f;     // Angolo massimo di inclinazione delle ali
    public float tiltSmoothness = 2f;     // Velocità di transizione dell'inclinazione

    private Transform[] rotors;
    private Transform[] wings;
    private Rigidbody rb;

    void Start()
    {
        // Trova i rotori
        rotors = GetComponentsInChildren<Transform>();
        rotors = System.Array.FindAll(rotors, rotor => rotor.name.StartsWith("Rotor"));

        // Trova le ali
        wings = GetComponentsInChildren<Transform>();
        wings = System.Array.FindAll(wings, wing => wing.name.StartsWith("Wing"));

        rb = GetComponentInParent<Rigidbody>();
    }

    void Update()
    {
        // Calcola la velocità dinamica dei rotori
        float targetSpeed = Mathf.Max(baseSpeed, rb.velocity.magnitude * maxSpeedMultiplier * 100);

        // Ruota i rotori con direzioni alternate
        for (int i = 0; i < rotors.Length; i++)
        {
            float direction = (i == 1 || i == 2) ? 1f : -1f;  // Alterna il senso di rotazione
            rotors[i].Rotate(direction * targetSpeed * Time.deltaTime * Vector3.forward);
        }

        // Inclinazione delle ali in base a velocità e rotazioni
        TiltWings();
    }

    void TiltWings()
    {
        // Calcola l'inclinazione basata su pitch (X) e yaw (Y)
        float pitchTilt = Mathf.Clamp(-rb.angularVelocity.x * wingTiltAngle, -wingTiltAngle, wingTiltAngle);
        float yawTilt = Mathf.Clamp(rb.angularVelocity.y * wingTiltAngle, -wingTiltAngle, wingTiltAngle);
        //Debug.Log(rb.angularVelocity);
        // Applica inclinazione solo sugli assi X e Y
        foreach (Transform wing in wings)
        {
            Quaternion targetRotation = Quaternion.Euler(yawTilt, -pitchTilt, 0f);  // Z bloccato
            wing.localRotation = targetRotation;
        }
    }

}
