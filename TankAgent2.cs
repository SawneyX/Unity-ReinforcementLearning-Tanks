using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;


public class TankAgent2 : Agent
{
    private float moveSpeed = 20f;  // Movement speed multiplier
    float shootCooldown = 0.2f;  // Time between shots
    private float lastShotTime = 0.0f;
    public int score = 0;

    float maxRange = 30f;
    public GameObject bulletPrefab;  // Prefab for the player's bullets

    private LineRenderer lineRenderer;
    public float lineDuration = 0.1f;  // Time the line will be visible

    public GameObject Explosion;

    public bool effects = false;

    public float timeElapsed = 0; // Tracks time elapsed in seconds
    public float topTime;

    private List<GameObject> allTanks = new List<GameObject>();

    private float rotationSpeed = 90f; // Degrees per second


    public AudioClip boomSound;  // Assign this in the Inspector
    private AudioSource audioSource;  // The AudioSource component  

    void Start()
    {   
        audioSource = GetComponent<AudioSource>();


        if (effects) Time.timeScale = 3.0f;
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;  // A line has two points: start and end
        lineRenderer.enabled = false;    // Initially disable it
        topTime = 10000;
    }

    void Update() 
    {
        timeElapsed += Time.deltaTime;
    }

    public override void OnEpisodeBegin()
    {
        // Reset agent's position and rotation
        transform.localPosition = new Vector3(0f, 0f, -35f);
        transform.localRotation = Quaternion.Euler(0f, 0f, 0f); // Reset rotation to 0 degrees
        Debug.Log("Episode Begin");

        if (score >= 20 && timeElapsed != 0 && timeElapsed < topTime) topTime = timeElapsed;
        
        score = 0;
        timeElapsed = 0;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent position (x-axis only, no vertical movement)
        sensor.AddObservation(transform.localPosition.x);

        // Agent rotation (angle on y-axis, normalized)
        sensor.AddObservation(transform.localEulerAngles.y / 90f);  // Normalize to [-1, 1]

        // Observations for each nearby tank (enemy or friendly)
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("EnemyAI");
        GameObject[] friends = GameObject.FindGameObjectsWithTag("Friendly");

        allTanks.Clear();

        allTanks.AddRange(enemies);
        allTanks.AddRange(friends);

        List<GameObject> nearestTanks = allTanks
            .OrderBy(obj => Vector3.Distance(transform.position, obj.transform.localPosition)) // Sort by distance to player
            .Take(3) // Take the closest 3 objects
            .ToList();

        foreach (GameObject tank in nearestTanks)
        {
            Vector3 tankPosition = tank.transform.localPosition;
            bool isEnemy = tank.CompareTag("EnemyAI");

            sensor.AddObservation(tankPosition.x);
            sensor.AddObservation(tankPosition.z);
            sensor.AddObservation(isEnemy ? 1.0f : 0.0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Branch 0: Movement actions (0: Stay, 1: Move Left, 2: Move Right)
        int moveAction = actionBuffers.DiscreteActions[0];

        // Branch 1: Shooting actions (0: Don't Shoot, 1: Shoot)
        int shootAction = actionBuffers.DiscreteActions[1];

        // Branch 2: Rotation actions (0: Don't Rotate, 1: Rotate Left, 2: Rotate Right)
        int rotateAction = actionBuffers.DiscreteActions[2];

        // Handle movement
        Vector3 newPosition = transform.localPosition;

        if (moveAction == 1)  // Move left
        {
            newPosition.x -= moveSpeed * Time.deltaTime;
        }
        else if (moveAction == 2)  // Move right
        {
            newPosition.x += moveSpeed * Time.deltaTime;
        }
        newPosition.x = Mathf.Clamp(newPosition.x, -36f, 36f);
        transform.localPosition = newPosition;

        // Handle rotation
        float newRotation = transform.localEulerAngles.y;
        
        // Convert the angle to a range between -180 and 180
        if (newRotation > 180f) newRotation -= 360f;

        if (rotateAction == 1)  // Rotate left
        {
            newRotation -= rotationSpeed * Time.deltaTime;
        }
        else if (rotateAction == 2)  // Rotate right
        {
            newRotation += rotationSpeed * Time.deltaTime;
        }
        newRotation = Mathf.Clamp(newRotation, -90f, 90f);  // Limit rotation to -90 to +90 degrees
        transform.localRotation = Quaternion.Euler(0f, newRotation, 0f);

        // Shooting action
        if (shootAction == 1 && Time.time - lastShotTime > shootCooldown)
        {
            Shoot();
            lastShotTime = Time.time;
        }
        else
        {
            if (Time.time - lastShotTime > 2) Invoke("HideLine", lineDuration);
        }

        // Handle rewards/punishments
        HandleRewards();
    }



    void Shoot()
    {
        // Cast a ray forward from the gun tip position
        RaycastHit hit;


        
        
        //if (Physics.Raycast(transform.position + new Vector3(0,0,0.25f), Vector3.forward, out hit, maxRange))
        if (Physics.SphereCast(transform.position + new Vector3(0,0,0.25f), 1f, transform.forward, out hit, maxRange))
        {
            // Check what the ray hit
            if (hit.collider != null)
            {
                // Example: Assuming tanks have tags "EnemyTank" or "FriendlyTank"
                if (hit.collider.CompareTag("EnemyAI"))
                {
                    Debug.Log("Enemy tank hit!");
                    // Award points for hitting the enemy tank
                    score += 2;
                    SetReward(2f);  // ML-Agents reward
                    Destroy(hit.collider.gameObject);  // Destroy enemy tank
                    ShowShot(hit.collider.transform.position, true);
                    if (effects) Instantiate(Explosion, hit.transform.position, Quaternion.identity);
                    if (effects) audioSource.PlayOneShot(boomSound);
                }
                else if (hit.collider.CompareTag("Friendly"))
                {
                    Debug.Log("Friendly tank hit!");
                    // Penalize for hitting friendly tank
                    score -= 1;
                    SetReward(-1.5f);  // ML-Agents penalty
                    Destroy(hit.collider.gameObject);  // Destroy friendly tank
                    ShowShot(hit.collider.transform.position, false);
                    if (effects) Instantiate(Explosion, hit.transform.position, Quaternion.identity);
                    if (effects) audioSource.PlayOneShot(boomSound);
                }
                
            }
        }
        else
        {
            Debug.Log("Missed! No tanks hit.");
            Vector3 shotEndPosition = transform.position + transform.forward * maxRange;
            ShowShot(shotEndPosition, false);
            SetReward(-0.1f); 
        }
    }
    void ShowShot(Vector3 hitPosition, bool hitEnemy)
    {
        lineRenderer.SetPosition(0, transform.position + new Vector3(0, 1, 0));   // Start position at the gun tip
        lineRenderer.SetPosition(1, hitPosition);       // End position where the shot hit or max range
        lineRenderer.enabled = true;

        lineRenderer.startColor = hitEnemy ? Color.green : Color.red;
        lineRenderer.endColor = hitEnemy ? Color.green : Color.red;
        
    }

    // Hide the LineRenderer after the shot is shown
    void HideLine()
    {
        lineRenderer.enabled = false;
    }

    private void HandleRewards()
    {
        // Check nearby tanks and apply rewards/penalties

        List<GameObject> toDelete = new List<GameObject>();

        foreach (GameObject tank in allTanks)
        {
            if (tank != null && tank.transform.localPosition.z <= -35f)  // Tank has reached the bottom
            {
                if (tank.CompareTag("EnemyAI"))
                {
                    score -= 1;
                    AddReward(-0.8f);  // Enemy reached the bottom, punishment    
                    
                    toDelete.Add(tank);
                    
                }
                else
                {
                    score += 2;
                    AddReward(0.4f);  // Friendly reached the bottom
                    
                    toDelete.Add(tank);
                    
                }
            }
        }
        foreach (GameObject tank in toDelete)
        {
            allTanks.Remove(tank);
            Destroy(tank);
        }

        //Check position
        if (transform.localPosition.x >= 34.5f || transform.localPosition.x <= -34.5f) {
            AddReward(-0.5f);
            Debug.Log("Close to border");
        }
        
        if (StepCount >= 15000) 
        {
            EndEpisode();  // End episode after max steps to avoid very long episodes
        }
 

        if (score >= 20)
        {
            SetReward(5.0f);  // Big reward for reaching goal
            EndEpisode();     // End the episode
        }
       
        // Penalty given each step to encourage agent to finish task quickly.
        AddReward(-1f / MaxStep);

    }




    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;

        // Movement heuristic (left: 1, right: 2, stay: 0)
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            discreteActionsOut[0] = 1;  // Move left
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActionsOut[0] = 2;  // Move right
        }
        else
        {
            discreteActionsOut[0] = 0;  // Stay
        }

        // Shooting heuristic (shoot: 1, don't shoot: 0)
        discreteActionsOut[1] = Input.GetKey(KeyCode.Space) ? 1 : 0;

        // Rotation heuristic (rotate left: 1, rotate right: 2, no rotation: 0)
        if (Input.GetKey(KeyCode.A))
        {
            discreteActionsOut[2] = 1;  // Rotate left
        }
        else if (Input.GetKey(KeyCode.D))
        {
            discreteActionsOut[2] = 2;  // Rotate right
        }
        else
        {
            discreteActionsOut[2] = 0;  // No rotation
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.transform.tag == "EnemyAI")
        {
            
            SetReward(-1.0f);  // Big penalty
            EndEpisode();  
        }
    }


}