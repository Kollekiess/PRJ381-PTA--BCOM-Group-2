using System.Collections.Generic;
using UnityEngine;

public class Boid : MonoBehaviour
{
    public enum BoidState
    {
        Flocking,
        AttractedToFood,
        Landing,
        OnGround,
        Fleeing
    }

    [Header("Current State")]
    public BoidState currentState = BoidState.Flocking;

    [Header("Food / Ground Settings")]
    public float foodDetectionRadius = 45f;
    public float scareDistance = 4.2f;
    public float minFleeDuration = 3.5f;
    public float maxFleeDuration = 5.0f;

    private BoidManager manager;
    private Vector3 velocity;
    private Transform playerTransform;

    // Food target
    private Breadcrumb targetCrumb;
    private Vector3 targetGroundPos;
    private Transform activeBirdModel;
    private Quaternion baseModelRot = Quaternion.Euler(270f, 180f, 0f);
    private BirdWingAnimator wingAnimator;

    // Timers & animations
    private float stateTimer = 0f;
    private float fleeDuration = 4f;
    private Vector3 fleeDirection;
    private float peckTimer = 0f;
    private bool isPeckingDown = false;
    private float peckSubTimer = 0f;

    void Awake()
    {
        // Randomly pick between Bird1 (Pigeon) and Bird2 (Cockatoo) if both are present
        Transform b1 = transform.Find("Bird1");
        Transform b2 = transform.Find("Bird2");
        if (b1 != null && b2 != null)
        {
            bool showBird1 = Random.value > 0.5f;
            b1.gameObject.SetActive(showBird1);
            b2.gameObject.SetActive(!showBird1);
            activeBirdModel = showBird1 ? b1 : b2;
        }
        else
        {
            activeBirdModel = b1 != null ? b1 : (b2 != null ? b2 : transform);
        }

        // Cache the original base rotation of the bird model (typically (270, 180, 0))
        if (activeBirdModel != null)
        {
            baseModelRot = activeBirdModel.localRotation;
        }

        wingAnimator = GetComponentInChildren<BirdWingAnimator>();
    }

    public void Initialize(BoidManager boidManager)
    {
        manager = boidManager;
        velocity = Random.onUnitSphere * manager.minSpeed;
        FindPlayer();
    }

    void FindPlayer()
    {
        if (playerTransform == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) playerTransform = p.transform;
            else if (Camera.main != null) playerTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        if (manager == null) return;
        if (playerTransform == null) FindPlayer();
        if (wingAnimator == null) wingAnimator = GetComponentInChildren<BirdWingAnimator>();

        switch (currentState)
        {
            case BoidState.Flocking:
                UpdateFlocking();
                CheckForBreadcrumbs();
                break;

            case BoidState.AttractedToFood:
                UpdateAttractedToFood();
                break;

            case BoidState.Landing:
                UpdateLanding();
                break;

            case BoidState.OnGround:
                UpdateOnGround();
                break;

            case BoidState.Fleeing:
                UpdateFleeing();
                break;
        }
    }

    // ==========================================
    // 1. FLOCKING STATE
    // ==========================================
    void UpdateFlocking()
    {
        Vector3 acceleration = Vector3.zero;

        acceleration += CalculateFlockForces();
        acceleration += CalculateBoundaryForce() * manager.boundaryWeight;
        acceleration += CalculateObstacleAvoidance() * manager.avoidanceWeight;

        velocity += acceleration * Time.deltaTime;
        float speed = Mathf.Clamp(velocity.magnitude, manager.minSpeed, manager.maxSpeed);
        velocity = velocity.normalized * speed;

        transform.position += velocity * Time.deltaTime;

        if (velocity != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity, Vector3.up), 8f * Time.deltaTime);
        }

        // Keep model at base orientation
        if (activeBirdModel != null && activeBirdModel.localRotation != baseModelRot)
        {
            activeBirdModel.localRotation = baseModelRot;
        }
    }

    void CheckForBreadcrumbs()
    {
        if (Breadcrumb.allCrumbs.Count == 0) return;

        int crumbCount = Breadcrumb.allCrumbs.Count;
        for (int i = 0; i < crumbCount; i++)
        {
            if (i >= Breadcrumb.allCrumbs.Count) break;
            var crumb = Breadcrumb.allCrumbs[i];
            if (crumb == null || !crumb.CanAttractMoreBoids()) continue;

            float dist = Vector3.Distance(transform.position, crumb.transform.position);
            if (dist <= foodDetectionRadius)
            {
                // Spot the breadcrumb!
                targetCrumb = crumb;
                crumb.RegisterBoid(this);

                // Calculate landing spot around the breadcrumb
                Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(0.45f, 1.15f);
                Vector3 spot = crumb.transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

                // Raycast to find exact ground elevation
                RaycastHit hit;
                if (Physics.Raycast(spot + Vector3.up * 10f, Vector3.down, out hit, 25f))
                {
                    spot.y = hit.point.y + 0.05f; // Feet sit flush on ground surface
                }
                else
                {
                    spot.y = Mathf.Max(0.05f, crumb.transform.position.y);
                }

                targetGroundPos = spot;
                currentState = BoidState.AttractedToFood;
                if (wingAnimator != null) wingAnimator.SetWingsFolded(false);
                break;
            }
        }
    }

    // ==========================================
    // 2. ATTRACTED TO FOOD (SWOOPING DESCENT)
    // ==========================================
    void UpdateAttractedToFood()
    {
        if (targetCrumb == null)
        {
            ReturnToFlocking();
            return;
        }

        Vector3 toTarget = targetGroundPos - transform.position;
        float distance = toTarget.magnitude;

        // Smooth descent towards landing spot
        float approachSpeed = Mathf.Lerp(manager.minSpeed * 0.9f, manager.maxSpeed * 1.1f, Mathf.Clamp01(distance / 12f));
        Vector3 desiredVel = toTarget.normalized * approachSpeed;

        velocity = Vector3.RotateTowards(velocity, desiredVel, 4.5f * Time.deltaTime, 0f);
        transform.position += velocity * Time.deltaTime;

        if (velocity != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity, Vector3.up), 10f * Time.deltaTime);
        }

        // Close to ground & landing position -> Transition to Landing
        if (distance < 1.4f || (transform.position.y <= targetGroundPos.y + 0.45f && toTarget.magnitude < 2.0f))
        {
            currentState = BoidState.Landing;
            stateTimer = 0f;
        }
    }

    // ==========================================
    // 3. LANDING STATE (LEVELING OUT & TOUCHDOWN)
    // ==========================================
    void UpdateLanding()
    {
        stateTimer += Time.deltaTime;

        // Smoothly glide onto target ground position
        transform.position = Vector3.MoveTowards(transform.position, targetGroundPos, 2.2f * Time.deltaTime);

        // Calculate level, upright ground rotation facing the breadcrumb
        Vector3 lookDir = transform.forward;
        if (targetCrumb != null)
        {
            lookDir = targetCrumb.transform.position - transform.position;
        }
        lookDir.y = 0f; // Level pitch to horizontal ground plane
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion uprightRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            // Slerp to upright posture (pitch = 0, roll = 0)
            transform.rotation = Quaternion.Slerp(transform.rotation, uprightRot, 10f * Time.deltaTime);
        }

        // Fold wings as we touch down
        if (wingAnimator != null)
        {
            wingAnimator.SetWingsFolded(true);
        }

        // Ensure bird model stays in proper base orientation
        if (activeBirdModel != null)
        {
            activeBirdModel.localRotation = baseModelRot;
        }

        // Touchdown complete
        if (Vector3.Distance(transform.position, targetGroundPos) < 0.04f || stateTimer > 0.7f)
        {
            transform.position = targetGroundPos;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }
            if (activeBirdModel != null) activeBirdModel.localRotation = baseModelRot;
            if (wingAnimator != null) wingAnimator.SetWingsFolded(true);

            currentState = BoidState.OnGround;
            peckTimer = Random.Range(0.4f, 1.2f);
        }
    }

    // ==========================================
    // 4. ON GROUND (UPRIGHT POSTURE & PECKING)
    // ==========================================
    void UpdateOnGround()
    {
        // 1. Maintain correct ground elevation and level orientation
        transform.position = new Vector3(transform.position.x, targetGroundPos.y, transform.position.z);

        if (targetCrumb != null)
        {
            Vector3 toCrumb = targetCrumb.transform.position - transform.position;
            toCrumb.y = 0f;
            if (toCrumb.sqrMagnitude > 0.001f)
            {
                Quaternion uprightFace = Quaternion.LookRotation(toCrumb.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, uprightFace, 4f * Time.deltaTime);
            }
        }

        // 2. Procedural head pecking animation (relative to baseModelRot)
        peckTimer -= Time.deltaTime;
        if (peckTimer <= 0f)
        {
            isPeckingDown = !isPeckingDown;
            if (isPeckingDown)
            {
                peckTimer = 0.18f;
            }
            else
            {
                peckTimer = Random.Range(0.8f, 2.0f);
            }
        }

        // Smoothly blend head pitch
        if (activeBirdModel != null)
        {
            Quaternion targetRot = isPeckingDown 
                ? baseModelRot * Quaternion.Euler(-22f, 0f, 0f) 
                : baseModelRot;
            activeBirdModel.localRotation = Quaternion.Slerp(activeBirdModel.localRotation, targetRot, 18f * Time.deltaTime);
        }

        // 3. Proximity check -> Scares birds when player walks near
        if (playerTransform != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distToPlayer <= scareDistance)
            {
                ScareAwayAllNearbyBoids();
                return;
            }
        }

        // 4. If crumb is gone, wait a moment then fly back to flock
        if (targetCrumb == null)
        {
            peckSubTimer += Time.deltaTime;
            if (peckSubTimer > 2.5f)
            {
                StartFleeing(Vector3.up + Random.insideUnitSphere * 0.3f);
            }
        }
    }

    void ScareAwayAllNearbyBoids()
    {
        if (targetCrumb != null)
        {
            var boidsList = new List<Boid>(targetCrumb.attractedBoids);
            for (int i = 0; i < boidsList.Count; i++)
            {
                var boid = boidsList[i];
                if (boid != null && (boid.currentState == BoidState.OnGround || boid.currentState == BoidState.Landing || boid.currentState == BoidState.AttractedToFood))
                {
                    boid.TriggerScaredFromPlayer();
                }
            }
        }
        else
        {
            TriggerScaredFromPlayer();
        }
    }

    public void TriggerScaredFromPlayer()
    {
        if (currentState == BoidState.Fleeing) return;

        FindPlayer();
        Vector3 away = Vector3.up;
        if (playerTransform != null)
        {
            away = (transform.position - playerTransform.position).normalized;
            away.y = 0f;
        }
        else
        {
            away = Random.onUnitSphere;
            away.y = 0f;
        }

        // Scatter in separate outward directions!
        float scatterAngle = Random.Range(-65f, 65f);
        Vector3 scatter = Quaternion.Euler(0f, scatterAngle, 0f) * away;
        // Strong upward takeoff angle
        scatter.y = Random.Range(0.6f, 0.95f);
        scatter.Normalize();

        StartFleeing(scatter);
    }

    void StartFleeing(Vector3 direction)
    {
        currentState = BoidState.Fleeing;
        fleeDirection = direction;
        fleeDuration = Random.Range(minFleeDuration, maxFleeDuration);
        stateTimer = 0f;

        if (activeBirdModel != null) activeBirdModel.localRotation = baseModelRot;
        if (wingAnimator != null)
        {
            wingAnimator.SetWingsFolded(false);
            wingAnimator.SetPanicFlap(true);
        }

        velocity = fleeDirection * (manager.maxSpeed * 1.4f);

        if (targetCrumb != null)
        {
            targetCrumb.UnregisterBoid(this);
            targetCrumb = null;
        }
    }

    // ==========================================
    // 5. FLEEING STATE (SCATTERING UPWARD)
    // ==========================================
    void UpdateFleeing()
    {
        stateTimer += Time.deltaTime;

        velocity = Vector3.RotateTowards(velocity, fleeDirection * (manager.maxSpeed * 1.3f), 2.5f * Time.deltaTime, 0f);
        transform.position += velocity * Time.deltaTime;

        if (velocity != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity, Vector3.up), 12f * Time.deltaTime);
        }

        if (activeBirdModel != null && activeBirdModel.localRotation != baseModelRot)
        {
            activeBirdModel.localRotation = baseModelRot;
        }

        // After a few seconds, return to standard boid flocking
        if (stateTimer >= fleeDuration)
        {
            ReturnToFlocking();
        }
    }

    void ReturnToFlocking()
    {
        currentState = BoidState.Flocking;
        if (activeBirdModel != null) activeBirdModel.localRotation = baseModelRot;
        if (wingAnimator != null)
        {
            wingAnimator.SetPanicFlap(false);
            wingAnimator.SetWingsFolded(false);
        }
        if (targetCrumb != null)
        {
            targetCrumb.UnregisterBoid(this);
            targetCrumb = null;
        }
    }

    // ==========================================
    // FLOCKING CALCULATIONS
    // ==========================================
    Vector3 CalculateFlockForces()
    {
        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        Vector3 centerOfMass = Vector3.zero;
        int neighborCount = 0;

        int boidCount = manager.allBoids.Count;
        for (int i = 0; i < boidCount; i++)
        {
            if (i >= manager.allBoids.Count) break;
            Boid other = manager.allBoids[i];
            if (other == null || other == this || other.currentState != BoidState.Flocking) continue;
            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist < manager.neighborRadius)
            {
                if (dist < manager.separationRadius && dist > 0)
                {
                    separation += (transform.position - other.transform.position) / dist;
                }
                alignment += other.velocity;
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
        if (Physics.Raycast(transform.position, transform.forward, out hit, manager.obstacleCheckDistance, manager.obstacleMask))
        {
            Vector3 avoidDirection = Vector3.Reflect(transform.forward, hit.normal);
            return avoidDirection.normalized * manager.maxSpeed;
        }
        return Vector3.zero;
    }
}
