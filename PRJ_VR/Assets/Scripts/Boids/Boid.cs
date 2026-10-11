using UnityEngine;

public class Boid : MonoBehaviour
{
    private BoidManager manager;
    private Vector3 velocity;

    public void Initialize(BoidManager boidManager)
    {
        manager = boidManager;

        // Random initial direction
        velocity = Random.onUnitSphere * manager.minSpeed;
    }

    void Update()
    {
        if (manager == null) return;

        Vector3 acceleration = Vector3.zero;

        // Calculate flocking forces
        acceleration += CalculateFlockForces();

        // Calculate boundary containment force
        acceleration += CalculateBoundaryForce() * manager.boundaryWeight;

        // Calculate obstacle avoidance force
        acceleration += CalculateObstacleAvoidance() * manager.avoidanceWeight;

        // Update velocity & position
        velocity += acceleration * Time.deltaTime;
        float speed = velocity.magnitude;

        speed = Mathf.Clamp(speed, manager.minSpeed, manager.maxSpeed);
        velocity = velocity.normalized * speed;

        transform.position += velocity * Time.deltaTime;

        // Rotate bird towards movement direction
        if (velocity != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
        }
    }

    Vector3 CalculateFlockForces()
    {
        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        Vector3 centerOfMass = Vector3.zero;
        int neighborCount = 0;

        foreach (Boid other in manager.allBoids)
        {
            if (other == this) continue;
            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist < manager.neighborRadius)
            {
                // Separation
                if (dist < manager.separationRadius && dist > 0) separation += (transform.position - other.transform.position) / dist;

                // Alignment
                alignment += other.velocity;

                // Cohesion
                centerOfMass += other.transform.position;
                neighborCount++;
            }
        }
        if (neighborCount > 0)
        {
            alignment = (alignment / neighborCount).normalized * manager.maxSpeed - velocity;
            centerOfMass /= neighborCount;
            cohesion = (centerOfMass - transform.position).normalized * manager.maxSpeed - velocity;
        }
        return (separation * manager.separationWeight) + (alignment * manager.alignmentWeight) + (cohesion * manager.cohesionWeight);
    }

    Vector3 CalculateBoundaryForce()
    {
        Vector3 force = Vector3.zero;
        Vector3 minBounds = manager.boundsCenter - (manager.boundsSize / 2f);
        Vector3 maxBounds = manager.boundsCenter + (manager.boundsSize / 2f);

        // Turn back if exceeding X, Y, or Z boundaries
        if (transform.position.x < minBounds.x) force.x = 1;
        else if (transform.position.x > maxBounds.x) force.x = -1;

        if (transform.position.y < minBounds.y) force.y = 1;
        else if (transform.position.y > maxBounds.y) force.y = -1;

        if (transform.position.z < minBounds.z) force.z = 1;
        else if (transform.position.z > maxBounds.z) force.z = -1;

        return force.normalized * manager.maxSpeed;
    }

    Vector3 CalculateObstacleAvoidance()
    {
        RaycastHit hit;

        // Cast ray forward
        if (Physics.Raycast(transform.position, transform.forward, out hit, manager.obstacleCheckDistance, manager.obstacleMask))
        {
            // Steer away from the hit surface normal
            Vector3 avoidDirection = Vector3.Reflect(transform.forward, hit.normal);
            return avoidDirection.normalized * manager.maxSpeed;
        }
        return Vector3.zero;
    }
    
    
}