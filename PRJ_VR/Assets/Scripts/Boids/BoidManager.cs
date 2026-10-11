using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour
{
    [Header("Boid Spawning")]
    public GameObject boidPrefab;
    public int spawnCount = 20;
    public Vector3 spawnBounds = new Vector3(20f, 10f, 20f);

    [Header("Flock Rules & Weights")]
    public float minSpeed = 2f;
    public float maxSpeed = 5f;
    public float neighborRadius = 5f;
    public float separationRadius = 2f;

    [Header("Behavior Weights")]
    public float separationWeight = 1.5f;
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;
    public float boundaryWeight = 3.0f;
    public float avoidanceWeight = 5.0f;

    [Header("Boundary Box")]
    public Vector3 boundsCenter = Vector3.zero;
    public Vector3 boundsSize = new Vector3(40f, 20f, 40f);

    [Header("Obstacle Avoidance")]
    public LayerMask obstacleMask;
    public float obstacleCheckDistance = 5f;

    [Header("Boid List")]
    public List<Boid> allBoids = new List<Boid>();

    void Start()
    {
        SpawnBoids();
    }

    //Boid Spawning
    void SpawnBoids()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 randomPos = boundsCenter + new Vector3(
                Random.Range(-spawnBounds.x / 2, spawnBounds.x / 2),
                Random.Range(-spawnBounds.y / 2, spawnBounds.y / 2),
                Random.Range(-spawnBounds.z / 2, spawnBounds.z / 2)
            );

            GameObject newBoidObj = Instantiate(boidPrefab, randomPos, Quaternion.identity, transform);

            Boid boid = newBoidObj.GetComponent<Boid>();

            if (boid != null)
            {
                boid.Initialize(this);
                allBoids.Add(boid);
            }
        }
    }

    // Visualization of boundary in scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(boundsCenter, boundsSize);
    }
}