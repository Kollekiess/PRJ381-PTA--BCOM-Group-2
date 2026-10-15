using UnityEngine;
using UnityEngine.InputSystem;

public class BreadcrumbSpawner : MonoBehaviour
{
    [Header("Breadcrumb Setup")]
    public GameObject breadcrumbPrefab;
    public float throwForce = 3.5f;
    public float throwUpwardForce = 1.2f;
    public float cooldown = 0.5f;

    private float lastThrowTime = -10f;
    private Material breadcrumbMat;

    void Start()
    {
        CreateDefaultMaterialIfNull();
    }

    void CreateDefaultMaterialIfNull()
    {
        if (breadcrumbMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            breadcrumbMat = new Material(shader);
            // Golden-brown bread crust color
            breadcrumbMat.color = new Color(0.82f, 0.58f, 0.28f, 1f);
            if (breadcrumbMat.HasProperty("_BaseColor"))
            {
                breadcrumbMat.SetColor("_BaseColor", new Color(0.82f, 0.58f, 0.28f, 1f));
            }
        }
    }

    public const int MAX_BREADCRUMBS = 4;

    /// <summary>
    /// Checks whether another breadcrumb can be summoned (under the 4 breadcrumb cap).
    /// </summary>
    public bool CanSpawnBreadcrumb()
    {
        return Breadcrumb.ActiveCrumbCount < MAX_BREADCRUMBS;
    }

    /// <summary>
    /// Current count of active breadcrumbs in the scene.
    /// </summary>
    public int ActiveCrumbCount => Breadcrumb.ActiveCrumbCount;

    void Update()
    {
        // Breadcrumbs are strictly spawned via mouse clicks (aimed into environment) 
        // and via the SASL VR gesture for 'Eat'.
        // Keyboard B and Space keys do NOT summon breadcrumbs.
    }

    /// <summary>
    /// Tosses a breadcrumb if under the maximum limit of 4 active breadcrumbs.
    /// Returns true if a breadcrumb was successfully spawned, false if blocked by cap or cooldown.
    /// </summary>
    public bool TossBreadcrumb()
    {
        if (Time.time < lastThrowTime + cooldown)
        {
            return false;
        }

        // Strictly enforce maximum of 4 breadcrumbs in the scene at any time
        if (Breadcrumb.ActiveCrumbCount >= MAX_BREADCRUMBS)
        {
            Debug.Log($"[BreadcrumbSpawner] Cannot spawn breadcrumb: Maximum limit of {MAX_BREADCRUMBS} reached ({Breadcrumb.ActiveCrumbCount}/{MAX_BREADCRUMBS}). Wait for birds to finish eating.");
            if (ScoreHUD.Instance != null)
            {
                ScoreHUD.Instance.ShowToast($"Max {MAX_BREADCRUMBS} breadcrumbs active! Birds are feeding.");
            }
            return false;
        }

        lastThrowTime = Time.time;

        Transform cam = Camera.main != null ? Camera.main.transform : transform;
        Vector3 spawnPos = cam.position + cam.forward * 0.6f + Vector3.down * 0.25f;
        Vector3 throwDir = (cam.forward * throwForce + Vector3.up * throwUpwardForce).normalized * throwForce;

        GameObject crumbObj;

        if (breadcrumbPrefab != null)
        {
            crumbObj = Instantiate(breadcrumbPrefab, spawnPos, Random.rotation);
        }
        else
        {
            // Dynamically construct a realistic breadcrumb cluster
            crumbObj = CreateProceduralBreadcrumb(spawnPos);
        }

        var rb = crumbObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = throwDir;
            rb.angularVelocity = Random.insideUnitSphere * 8f;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBreadToss(spawnPos);
        }

        Debug.Log($"[BreadcrumbSpawner] Tossed breadcrumb ({Breadcrumb.ActiveCrumbCount}/{MAX_BREADCRUMBS} active) at {spawnPos}");
        return true;
    }

    private GameObject CreateProceduralBreadcrumb(Vector3 pos)
    {
        GameObject root = new GameObject("Breadcrumb");
        root.transform.position = pos;

        CreateDefaultMaterialIfNull();

        // 3 tiny clustered crumbs
        Vector3[] offsets = new Vector3[]
        {
            Vector3.zero,
            new Vector3(0.04f, -0.01f, 0.03f),
            new Vector3(-0.03f, 0.01f, -0.04f)
        };
        Vector3[] scales = new Vector3[]
        {
            new Vector3(0.10f, 0.08f, 0.11f),
            new Vector3(0.06f, 0.05f, 0.07f),
            new Vector3(0.05f, 0.06f, 0.06f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject sub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sub.name = "CrumbPiece_" + i;
            sub.transform.SetParent(root.transform);
            sub.transform.localPosition = offsets[i];
            sub.transform.localScale = scales[i];
            sub.transform.localRotation = Random.rotation;

            var r = sub.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = breadcrumbMat;

            // Remove sub collider; root will have the main collider
            var c = sub.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        // Add main sphere collider
        var col = root.AddComponent<SphereCollider>();
        col.radius = 0.08f;

        // Add Rigidbody
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.15f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Add Breadcrumb logic
        root.AddComponent<Breadcrumb>();

        return root;
    }
}
