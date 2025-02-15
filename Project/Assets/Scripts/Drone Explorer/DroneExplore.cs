using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
using System.Collections.Generic;

public class DroneExplore : Agent
{
    [Tooltip("Force to apply when moving")]
    public float moveForce = 30f;

    [Tooltip("Speed to pitch up or down")]
    public float pitchSpeed = 10f;

    [Tooltip("Speed to rotate around the up axis")]
    public float yawSpeed = 10f;

    [Tooltip("Speed to rotate around the forward axis")]
    public float rollSpeed = 10f;

    [Tooltip("Whether this is training mode or gameplay mode")]
    public bool trainingMode;

    // The rigidbody of the agent
    new private Rigidbody rigidbody;

    // The enemy area that the agent is in
    private Environment envArea;

    // Allows for smoother pitch changes
    private float smoothPitchChange = 0f;

    // Allows for smoother yaw changes
    private float smoothYawChange = 0f;

    // Allows for smoother roll changes
    private float smoothRollChange = 0f;

    private const float maxTorque = 50f;

    private const float maxStabilizeTorque = 0.2f;

    // The distance of the raycast
    public float LidarViewDistance = 40f;
    // The distance of the raycast
    //public float UltrasonicViewDistance = 10f;

    public float nLidarRays = 36f;
    //public float ultrasonicRaysDegrees = 45f;

    public float DownwardAngle = 30f;

    // The number of enemies found
    private int enemiesFound = 0;

    private MinimapFogOfWar fogOfWar;

    private bool endEpisode = false;
    private Vector3[] rayDirections;

    private AudioSource audioSource;
    public AudioClip collisionSound;
    public AudioClip droneStart;
    public ParticleSystem collisionEffectPrefab;
    public GridManager gridManager;
    private bool frozen = true;
    private bool isReturningToBase = false;
    private KalmanFilter kalmanFilter;


    /// <summary>
    /// Initialize the agent
    /// </summary>
    public override void Initialize()
    {
        endEpisode = false;

        // If not training mode, no max step, play forever
        if (!trainingMode) MaxStep = 0;

        Transform childTransform = transform.Find("ExtraSound");
        if (childTransform != null)
        {
            audioSource = childTransform.GetComponent<AudioSource>();
        }

        gridManager = FindObjectOfType<GridManager>();
        rigidbody = GetComponent<Rigidbody>();
        envArea = GetComponentInParent<Environment>();
        fogOfWar = FindObjectOfType<MinimapFogOfWar>();
        kalmanFilter = new KalmanFilter(new Vector3(transform.position.x, transform.position.y, transform.position.z));
    }

    /// <summary>
    /// Reset the agent when an episode begins
    /// </summary>
    public override void OnEpisodeBegin()
    {
        endEpisode = false;
        transform.Find("FireComplex").gameObject.SetActive(false);

        // Reset the enemies
        if (trainingMode)
        {
            // Logga la percentuale di esplorazione dell'episodio precedente
            float explorationPercentage = fogOfWar.GetExplorationPercentage();
            Debug.Log($"Exploration Percentage: {explorationPercentage * 100f}%");
            Debug.Log($"Enemies found: {enemiesFound}");
            Debug.Log("OnEpisodeBegin - Agent: " + gameObject.name);

            // Reset
            envArea.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);
            GameManager.Instance.GenerateStartPosition();
            envArea.ResetEnemies(randomPosition: true);
            GameManager.Instance.ResetEnemies();
            fogOfWar.ResetFog();
        }

        enemiesFound = 0;
        // Zero out velocities so that movement stops before a new episode begins
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        // Move the agent to a new random position

        if (trainingMode)
        {
            SetDronePositionForTraining();
        }
        else
        {
            FreezeAgent();
        }

    }

    public void FreezeAgent()
    {
        frozen = true;
        transform.Find("FireComplex").gameObject.SetActive(false);
        rigidbody.Sleep();
    }

    public void StarMapping()
    {
        envArea.ResetEnemies();
        GameManager.Instance.ResetEnemies();
        fogOfWar.ResetFog();
        isReturningToBase = false;
        PlaySound(droneStart);
        rigidbody.WakeUp();

        Vector3 targetPosition = GameManager.SharedStartPosition;
        targetPosition.y = 10f;
        Debug.Log(targetPosition);
        Debug.DrawLine(transform.position, transform.position + targetPosition * 20f, Color.green);
        StartCoroutine(GoToPositionRoutine(targetPosition));
    }

    /// <summary>
    /// Move the agent to a safe random position (i.e. does not collide with anything)
    /// </summary>
    private void SetDronePositionForTraining()
    {
        bool safePositionFound = false;
        int attemptsRemaining = 100; // Prevent an infinite loop
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = new Quaternion();

        // Loop until a safe position is found or we run out of attempts
        while (!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;

            // Pick a random height from the ground
            float height = UnityEngine.Random.Range(2f, 15f);

            // Pick a random radius from the center of the area
            float radius = UnityEngine.Random.Range(2f, 20f);

            // Pick a random direction rotated around the y axis
            Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);

            // Combine height, radius, and direction to pick a potential position
            potentialPosition = GameManager.SharedStartPosition + Vector3.up * height + direction * Vector3.forward * radius;

            // Choose and set random starting pitch and yaw
            float pitch = UnityEngine.Random.Range(-20f, 20f);
            float yaw = UnityEngine.Random.Range(-180f, 180f);
            potentialRotation = Quaternion.Euler(pitch, yaw, 0f);

            // Check to see if the agent will collide with anything
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 4f);
            // Check if the agent is whitin the boundaries
            if (potentialPosition.x < envArea.transform.position.x - envArea.AreaDiameter / 2 ||
                potentialPosition.x > envArea.transform.position.x + envArea.AreaDiameter / 2 ||
                potentialPosition.z < envArea.transform.position.z - envArea.AreaDiameter / 2 ||
                potentialPosition.z > envArea.transform.position.z + envArea.AreaDiameter / 2)
            {
                colliders = new Collider[1];
            }

            // Debug.Log("colliders.Length: " + colliders.Length);
            // Safe position has been found if no colliders are overlapped
            safePositionFound = colliders.Length == 0;
        }

        Debug.Assert(safePositionFound, "Could not find a safe position to spawn");

        // Set the position and rotation
        transform.SetPositionAndRotation(potentialPosition, potentialRotation);
    }

    /// <summary>
    /// Called when and action is received from either the player input or the neural network
    /// 
    /// actionBuffers[i] represents:
    /// Index 0: move vector x (+1 = right, -1 = left)
    /// Index 1: move vector y (+1 = up, -1 = down)
    /// Index 2: move vector z (+1 = forward, -1 = backward)
    /// Index 3: pitch angle (+1 = pitch up, -1 = pitch down)
    /// Index 4: yaw angle (+1 = turn right, -1 = turn left)
    /// Index 5: roll angle (+1 = roll clockwise, -1 = roll counterclockwise)
    /// </summary>
    /// <param name="actionBuffers">The actions to take</param>
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (frozen) return;

        if (endEpisode)
        {
            EndEpisode();
            return;
        }

        ApplyActionMovement(actionBuffers);

        // Controlla se il drone ha esplorato una nuova area
        if (fogOfWar.IsAreaExplored(transform.position))
        {
            if (trainingMode && fogOfWar != null)
            {
                AddReward(0.05f);
            }

        }
    }

    private void ApplyActionMovement(ActionBuffers actionBuffers)
    {
        var vectorAction = actionBuffers.ContinuousActions;
        // Calculate movement vector
        Vector3 move = new Vector3(vectorAction[0], vectorAction[1], vectorAction[2]);

        // Add force in the direction of the move vector
        rigidbody.AddForce(move * moveForce);

        // Calculate and apply a stabilization force to counter gravity
        float liftForce = rigidbody.mass * Mathf.Abs(Physics.gravity.y);
        rigidbody.AddForce(Vector3.up * liftForce);

        // Calculate pitch and yaw rotation
        float pitchChange = vectorAction[3];
        float yawChange = vectorAction[4];
        float rollChange = vectorAction[5];

        // Calculate smooth rotation changes
        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
        smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);
        smoothRollChange = Mathf.MoveTowards(smoothRollChange, rollChange, 2f * Time.fixedDeltaTime);

        // Calcola il torque da applicare
        Vector3 torque = pitchSpeed * smoothPitchChange * transform.right; // Pitch (asse X locale)
        torque += smoothYawChange * yawSpeed * Vector3.up; // Yaw (asse Y globale)
        torque += smoothRollChange * rollSpeed * transform.forward; // Roll (asse Z locale)
        torque = Vector3.ClampMagnitude(torque, maxTorque);

        // Check current pitch and roll angles
        float currentPitch = transform.eulerAngles.x;
        if (currentPitch > 180f) currentPitch -= 360f; // Convert to range [-180, 180]
        float currentRoll = transform.eulerAngles.z;
        if (currentRoll > 180f) currentRoll -= 360f; // Convert to range [-180, 180]

        Vector3 stabilizationTorque = Vector3.zero;
        if (Mathf.Abs(currentPitch) > 1f)
            stabilizationTorque -= transform.right * Mathf.Sign(currentPitch) * Mathf.Abs(currentPitch) * maxStabilizeTorque;

        if (Mathf.Abs(currentRoll) > 1f)
            stabilizationTorque -= transform.forward * Mathf.Sign(currentRoll) * Mathf.Abs(currentRoll) * maxStabilizeTorque;

        Vector3 combinedTorque = Vector3.ClampMagnitude(torque + stabilizationTorque, maxTorque);

        // Applica il torque al rigidbody
        rigidbody.AddTorque(combinedTorque);
    }

    /// <summary>
    /// Collect vector observations from the environment
    /// </summary>
    /// <param name="sensor">The vector sensor</param>
    public override void CollectObservations(VectorSensor sensor)
    {
        // Observe the agent's local rotation (4 observations)
        sensor.AddObservation(transform.localRotation.normalized);

        // Observe the agent's velocity (3 observations), normalized by max speed
        sensor.AddObservation(rigidbody.velocity.normalized);

        // Observe the agent's angular velocity (3 observations), normalized by max angular speed
        sensor.AddObservation(rigidbody.angularVelocity.normalized);

        // Observe the agent's position (3 observations), normalized relative to the environment
        sensor.AddObservation(transform.position / envArea.AreaDiameter);

        // Observe the number of enemies found / total enemies (1 observation)
        sensor.AddObservation((float)enemiesFound / envArea.Enemies.Count);

        if (fogOfWar != null)
        {
            Vector3 directionToUnexplored = fogOfWar.GetDirectionToNearestUnexplored(transform.position);
            float distanceToUnexplored = fogOfWar.GetDistanceToNearestUnexplored(transform.position);

            // Observe the normalized direction to the nearest unexplored area (3 observations)
            sensor.AddObservation(directionToUnexplored);

            // Observe the distance to the nearest unexplored area (1 observation), normalized by the map size
            sensor.AddObservation(distanceToUnexplored / envArea.AreaDiameter);

            // Debug visualization of the direction to the unexplored area
            Debug.DrawLine(transform.position, transform.position + directionToUnexplored * distanceToUnexplored, Color.red);

            // Observe the percentage of the map explored (1 observation)
            float explorationPercentage = fogOfWar.GetExplorationPercentage();
            sensor.AddObservation(explorationPercentage);

            // Reward for full exploration
            if (explorationPercentage > 0.99f && !endEpisode && !frozen)
            {
                Debug.Log("All areas explored");
                if (trainingMode)
                {
                    endEpisode = true;
                    AddReward(1f);
                }
                else
                {
                    GotoHeliport();
                }
            }
        }
    }


    public void GotoHeliport()
    {
        frozen = true;
        Transform heliport = GameObject.Find("Heliport").transform;
        if (heliport != null && !isReturningToBase && Vector3.Distance(transform.position, heliport.position) > 1f)
        {
            Debug.Log("Returning to base");
            Vector3 targetPosition = heliport.position + new Vector3(0, 3f, 0);
            StartCoroutine(ReturnToBaseRoutine(targetPosition));
        }
    }

    private IEnumerator ReturnToBaseRoutine(Vector3 basePosition)
    {
        isReturningToBase = true;
        bool landing = false;
        gameObject.layer = LayerMask.NameToLayer("DroneEntering");
        WaitForFixedUpdate waitForPhysics = new WaitForFixedUpdate();


        while (true)
        {
            float liftForce = rigidbody.mass * Mathf.Abs(Physics.gravity.y);
            rigidbody.AddForce(Vector3.up * liftForce);

            // Direzione verso la base
            Vector3 directionToBase = (basePosition - transform.position).normalized;

            // Evitamento ostacoli
            Vector3 avoidanceDirection = AvoidObstacles(directionToBase);

            // Controlla la distanza orizzontale dalla base
            float horizontalDistance = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(basePosition.x, 0, basePosition.z)
            );

            // Se il drone è sopra la base, inizia l'atterraggio
            if (horizontalDistance < 1.0f && !landing)
            {
                //Debug.Log("Drone is above the base, starting landing");
                landing = true;
            }

            if (landing)
            {
                // Riduci gradualmente l'altitudine per atterrare
                rigidbody.AddForce(Vector3.down * moveForce * 0.3f);

                // Controlla se ha toccato la base
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 0.6f))
                {
                    //Debug.Log("Drone landed at the base");
                    FreezeAgent(); // Blocca il drone all'arrivo
                    isReturningToBase = false; // Reset dello stato
                    gameObject.layer = LayerMask.NameToLayer("Default");
                    break; // Esci dalla Coroutine

                }
            }
            else
            {
                // Se non è ancora in fase di atterraggio, muoviti verso la base normalmente
                Vector3 force = avoidanceDirection * moveForce;
                rigidbody.AddForce(force);
            }

            // Rotazione graduale verso la base
            Quaternion targetRotation = Quaternion.LookRotation(avoidanceDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, yawSpeed * Time.deltaTime);

            // Correzione di pitch e roll
            float pitch = -transform.eulerAngles.x;
            if (pitch < -180f) pitch += 360f;
            float roll = -transform.eulerAngles.z;
            if (roll < -180f) roll += 360f;

            rigidbody.AddTorque(transform.right * pitch * pitchSpeed * Time.deltaTime);
            rigidbody.AddTorque(transform.forward * roll * rollSpeed * Time.deltaTime);

            yield return waitForPhysics;
        }
    }

    private IEnumerator GoToPositionRoutine(Vector3 targetPosition)
    {
        WaitForFixedUpdate waitForPhysics = new WaitForFixedUpdate();
        gameObject.layer = LayerMask.NameToLayer("DroneEntering");

        while (true)
        {
            if (isReturningToBase) break;
            float liftForce = rigidbody.mass * Mathf.Abs(Physics.gravity.y);
            rigidbody.AddForce(Vector3.up * liftForce);

            // Direzione verso il target
            Vector3 directionToTarget = (targetPosition - transform.position).normalized;

            // Evitamento ostacoli
            Vector3 avoidanceDirection = AvoidObstacles(directionToTarget);

            // Applicazione della forza verso la direzione desiderata
            Vector3 force = avoidanceDirection * moveForce;
            rigidbody.AddForce(force);

            // Rotazione graduale verso il target o direzione evitamento
            Quaternion targetRotation = Quaternion.LookRotation(avoidanceDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, yawSpeed * Time.deltaTime);

            // Correzione di pitch e roll
            float pitch = -transform.eulerAngles.x;
            if (pitch < -180f) pitch += 360f;
            float roll = -transform.eulerAngles.z;
            if (roll < -180f) roll += 360f;

            rigidbody.AddTorque(transform.right * pitch * pitchSpeed * Time.deltaTime);
            rigidbody.AddTorque(transform.forward * roll * rollSpeed * Time.deltaTime);


            // Controlla se il drone ha raggiunto il target
            float horizontalDistance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                            new Vector3(targetPosition.x, 0, targetPosition.z));
            if (horizontalDistance < 2f && rigidbody.velocity.magnitude < 1f)
            {
                Debug.Log("Drone reached the target point");
                frozen = false;
                gameObject.layer = LayerMask.NameToLayer("Default");
                break;
            }

            yield return waitForPhysics;
        }
    }

    private Vector3 AvoidObstacles(Vector3 desiredDirection)
    {
        float rayDistance = 10f; // Distanza massima di rilevamento
        float avoidanceStrength = 10f; // Intensità della deviazione
        Vector3 avoidanceForce = Vector3.zero;

        // Direzioni principali da controllare
        Vector3[] directions = {
        transform.forward,                  // Frontale
        transform.right,                   // Destra
        -transform.right,                  // Sinistra
        Vector3.up,                        // Sopra
        transform.forward + transform.right,  // Diagonale avanti-destra
        transform.forward - transform.right,   // Diagonale avanti-sinistra
        Vector3.down                        // Basso
    };

        foreach (var direction in directions)
        {
            if (Physics.Raycast(transform.position, direction.normalized, out RaycastHit hit, rayDistance))
            {
                if (!hit.collider.CompareTag("Boundary"))
                {
                    Debug.DrawRay(transform.position, direction.normalized * rayDistance, Color.red);

                    // Aggiungi forza repulsiva inversamente proporzionale alla distanza
                    Vector3 repulsion = (transform.position - hit.point).normalized / Mathf.Max(hit.distance, 0.1f);
                    avoidanceForce += repulsion;
                }

            }
        }

        // Se il drone si trova bloccato vicino a un ostacolo, forza una salita
        if (avoidanceForce.magnitude > 0 && Physics.Raycast(transform.position, Vector3.up, out RaycastHit upHit, rayDistance))
        {
            if (upHit.distance > 5f) // Solo se c'è spazio sopra il drone
            {
                avoidanceForce += Vector3.up * avoidanceStrength;
            }
        }

        // Combina la direzione desiderata con la forza di evitamento
        Vector3 finalDirection = (desiredDirection + avoidanceForce * avoidanceStrength).normalized;
        return finalDirection;
    }

    /// <summary>
    /// When Behavior Type is set to "Heuristic Only" on the agent's Behavior Parameters,
    /// this function will be called. Its return values will be fed into
    /// <see cref="OnActionReceived(float[])"/> instead of using the neural network
    /// </summary>
    /// <param name="actionsOut">And output action array</param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Create placeholders for all movement/turning
        Vector3 forward = Vector3.zero;
        Vector3 left = Vector3.zero;
        Vector3 up = Vector3.zero;
        float pitch = 0f;
        float yaw = 0f;
        float roll = 0f;

        // Convert keyboard inputs to movement and turning
        // All values should be between -1 and +1

        // Forward/backward
        if (Input.GetKey(KeyCode.W)) forward = transform.forward;
        else if (Input.GetKey(KeyCode.S)) forward = -transform.forward;

        // Left/right
        if (Input.GetKey(KeyCode.A)) left = -transform.right;
        else if (Input.GetKey(KeyCode.D)) left = transform.right;

        // Up/down
        if (Input.GetKey(KeyCode.R)) up = transform.up;
        else if (Input.GetKey(KeyCode.F)) up = -transform.up;

        // Pitch up/down
        if (Input.GetKey(KeyCode.UpArrow)) pitch = 1f;
        else if (Input.GetKey(KeyCode.DownArrow)) pitch = -1f;

        // Turn left/right
        if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f;
        else if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f;

        // Roll clockwise/counterclockwise
        if (Input.GetKey(KeyCode.E)) roll = -1f;
        else if (Input.GetKey(KeyCode.Q)) roll = 1f;

        // Combine the movement vectors and normalize
        Vector3 combined = (forward + left + up).normalized;

        // Add the 3 movement values, pitch, and yaw to the actionsOut array
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = combined.x;
        continuousActions[1] = combined.y;
        continuousActions[2] = combined.z;
        continuousActions[3] = pitch;
        continuousActions[4] = yaw;
        continuousActions[5] = roll;
    }

    /// <summary>
    /// Called when the agent collides with something solid
    /// </summary>
    /// <param name="collision">The collision info</param>
    private void OnCollisionEnter(Collision collision)
    {
        if (trainingMode && collision.collider.CompareTag("Boundary"))
        {
            Debug.Log("Boundary collision");
            // Collided with the area boundary, give a negative reward
            AddReward(-1f);
        }
        else if (trainingMode && collision.collider.CompareTag("Enemy"))
        {
            Debug.Log("Enemy collision");
            // Collided with the enemy, give a negative reward
            AddReward(-0.5f);
        }
        else if (trainingMode)
        {
            Debug.Log("Other collision");
            // Collided with something that isn't the boundary or an enemy, give a small negative reward
            AddReward(-0.5f);
        }

        if (!trainingMode)
        {
            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed > 5f)
            {
                ShowCollisionEffect(collision.contacts[0].point);
                PlaySound(collisionSound);
                GotoHeliport();
            }
        }
    }

    private void ShowCollisionEffect(Vector3 position)
    {
        if (collisionEffectPrefab != null)
        {
            ParticleSystem effect = Instantiate(collisionEffectPrefab, position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, 2f);  // Elimina l'effetto dopo 2 secondi
            transform.Find("FireComplex").gameObject.SetActive(true);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (trainingMode) return;

        if (audioSource.isPlaying)
        {
            audioSource.Stop(); // Stop the current sound if playing
        }
        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>
    /// Called every frame
    /// </summary>
    private void Update()
    {
        EnemyDetection();
    }

    void InitializeRayDirections()
    {
        rayDirections = new Vector3[(int)nLidarRays];
        // Numero di raggi per coprire 360 gradi
        float angleStep = 360f / nLidarRays;
        for (int i = 0; i < nLidarRays; i++)
        {
            float angle = i * angleStep;
            rayDirections[i] = Quaternion.Euler(DownwardAngle, angle, 0) * Vector3.forward;
        }
    }

    /// <summary>
    /// Dedect enemies within the field of view using raycasting 360 degrees
    /// </summary>
    void EnemyDetection()
    {
        if (rayDirections == null)
            InitializeRayDirections();

        List<Vector3> detectedEnemyPositions = new List<Vector3>();
        List<GameObject> enemiesDetected = new List<GameObject>();

        // Proietta raggi radialmente e verso il basso
        foreach (Vector3 direction in rayDirections)
        {
            RaycastHit hit;
            if (Physics.Raycast(this.transform.position, direction, out hit, LidarViewDistance))
            {
                if (hit.collider.CompareTag("Enemy"))
                {
                    Vector3 noisyPosition = hit.point;
                    //Debug.Log("Detected enemy at: " + noisyPosition + "Without Noise");

                    // Aggiungi rumore alla posizione rilevata
                    noisyPosition.x += Random.Range(-0.5f, 0.5f); // Simula errore di misura
                    noisyPosition.z += Random.Range(-0.5f, 0.5f);
                    //Debug.Log("Detected enemy at: " + noisyPosition + "with noise");

                    // Kalman filter
                    Vector3 filteredPosition = kalmanFilter.UpdatePosition(noisyPosition);
                    //Debug.Log("Detected enemy at: " + filteredPosition + "With Kalman");
                    detectedEnemyPositions.Add(filteredPosition);
                    enemiesDetected.Add(hit.collider.gameObject);

                    //GameManager.Instance.RegisterEnemy(filteredPosition);

                    Enemy enemy = envArea.GetEnemyFromCollider(hit.collider);
                    if (enemy != null && !enemy.found)
                    {
                        enemy.Found();
                        // Debug.Log("enemy found: " + enemy.gameObject);
                        enemiesFound++;

                        if (trainingMode)
                        {
                            AddReward(0.01f);
                        }
                    }
                }
            }
        }
        if (enemiesDetected.Count > 0)
        {
            if (gridManager != null)
            {
                gridManager.MarkObjectsAsBlocked(enemiesDetected.ToArray(), 1.0f);
            }
            else
            {
                Debug.Log("GridManager is null");
            }
        }

    }


    /// <summary>
    /// Show the agent's field of view in the scene view for debugging
    /// </summary>
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        // Numero di raggi
        float angleStep = 360f / nLidarRays;

        for (int i = 0; i < nLidarRays; i++)
        {
            float angle = i * angleStep;

            // Calcola la direzione del raggio
            Vector3 rayDirection = Quaternion.Euler(DownwardAngle, angle, 0) * Vector3.forward;

            // Disegna il raggio
            //Gizmos.DrawLine(transform.position, transform.position + rayDirection * LidarViewDistance);
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(new Vector3(transform.position.x, 1, transform.position.z), 0.5f);

    }


}
