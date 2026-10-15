using System.Collections.Generic;
using UnityEngine;

public class Breadcrumb : MonoBehaviour
{
    public static List<Breadcrumb> allCrumbs = new List<Breadcrumb>();

    [Header("Breadcrumb Settings")]
    public bool isLanded = false;
    public float maxLifeTime = 25f;
    public int maxAttractedBoids = 4;
    public float eatDuration = 14f;

    [Header("State")]
    public List<Boid> attractedBoids = new List<Boid>();

    private float lifeTimer = 0f;
    private float eatTimer = 0f;
    private Vector3 initialScale;
    private Rigidbody rb;

    void Awake()
    {
        initialScale = transform.localScale;
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        if (!allCrumbs.Contains(this)) allCrumbs.Add(this);
    }

    void OnDisable()
    {
        allCrumbs.Remove(this);
    }

    void OnCollisionEnter(Collision col)
    {
        if (!isLanded)
        {
            // Settle on ground
            isLanded = true;
            if (rb != null)
            {
                rb.linearDamping = 5f;
                rb.angularDamping = 5f;
            }
        }
    }

    void Update()
    {
        lifeTimer += Time.deltaTime;

        // Auto-detect ground if velocity is low even if collision event was skipped
        if (!isLanded && rb != null && rb.linearVelocity.magnitude < 0.2f && lifeTimer > 0.4f)
        {
            isLanded = true;
        }

        // Count how many birds are currently on ground eating
        int eatingCount = 0;
        for (int i = attractedBoids.Count - 1; i >= 0; i--)
        {
            if (attractedBoids[i] == null)
            {
                attractedBoids.RemoveAt(i);
                continue;
            }
            if (attractedBoids[i].currentState == Boid.BoidState.OnGround)
            {
                eatingCount++;
            }
        }

        if (eatingCount > 0)
        {
            eatTimer += Time.deltaTime;
            // Slowly shrink breadcrumb as birds eat it
            float progress = Mathf.Clamp01(eatTimer / eatDuration);
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, progress);

            if (eatTimer >= eatDuration)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (lifeTimer >= maxLifeTime)
        {
            Destroy(gameObject);
        }
    }

    public bool CanAttractMoreBoids()
    {
        return isLanded && attractedBoids.Count < maxAttractedBoids;
    }

    public void RegisterBoid(Boid boid)
    {
        if (!attractedBoids.Contains(boid))
        {
            attractedBoids.Add(boid);
        }
    }

    public void UnregisterBoid(Boid boid)
    {
        attractedBoids.Remove(boid);
    }
}
