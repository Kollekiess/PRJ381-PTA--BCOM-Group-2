using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class CloudManager : MonoBehaviour
{
    [System.Serializable]
    public class CloudInstance
    {
        public GameObject gameObject;
        public Transform transform;
        public float speed;
        public Vector3 targetScale;
    }

    [Header("Cloud Prefabs")]
    public GameObject[] cloudPrefabs;

    [Header("Pool Configuration")]
    [Range(10, 60)]
    public int cloudCount = 30;

    [Header("Movement Bounds & Direction (X Axis: 200 -> -200)")]
    public float startX = 200f;
    public float endX = -200f;

    [Tooltip("Distance over which clouds grow from 0 at startX and shrink to 0 at endX")]
    public float fadeDistance = 35f;

    [Header("Altitude & Lateral Spread")]
    public float minHeight = 35f;
    public float maxHeight = 65f;
    public float minZ = -100f;
    public float maxZ = 100f;

    [Header("Speed & Scale Variation")]
    public float minSpeed = 4f;
    public float maxSpeed = 8.5f;
    public float minScale = 280f;
    public float maxScale = 620f;

    [Header("Edit Mode Preview")]
    public bool animateInEditMode = true;

    private List<CloudInstance> pool = new List<CloudInstance>();
    private bool isInitialized = false;

    void Start()
    {
        InitializePool();
    }

    void OnEnable()
    {
        if (!isInitialized || pool.Count == 0 || pool[0].gameObject == null)
        {
            InitializePool();
        }
    }

    public void InitializePool()
    {
        // Clear existing pooled children
        ClearPool();

        if (cloudPrefabs == null || cloudPrefabs.Length == 0)
        {
            LoadDefaultPrefabs();
            if (cloudPrefabs == null || cloudPrefabs.Length == 0)
            {
                Debug.LogWarning("[CloudManager] No cloud prefabs assigned.");
                return;
            }
        }

        pool.Clear();
        Random.InitState(4242);

        // Pre-distribute 30 clouds evenly across the journey from startX to endX
        for (int i = 0; i < cloudCount; i++)
        {
            GameObject prefab = cloudPrefabs[Random.Range(0, cloudPrefabs.Length)];
            GameObject cloudObj = Instantiate(prefab, transform);
            cloudObj.name = $"Cloud_{i + 1}";

            // Keep clean transform defaults
            cloudObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            // Stagger position across whole travel distance
            float fraction = (float)i / cloudCount;
            float initialX = Mathf.Lerp(startX, endX, fraction) + Random.Range(-5f, 5f);
            initialX = Mathf.Clamp(initialX, endX, startX);

            float initialY = Random.Range(minHeight, maxHeight);
            float initialZ = Random.Range(minZ, maxZ);

            cloudObj.transform.position = new Vector3(initialX, initialY, initialZ);

            CloudInstance instance = new CloudInstance
            {
                gameObject = cloudObj,
                transform = cloudObj.transform,
                speed = Random.Range(minSpeed, maxSpeed),
                targetScale = GenerateRandomScale()
            };

            // Set initial scale based on starting X
            UpdateCloudScale(instance);
            pool.Add(instance);
        }

        isInitialized = true;
    }

    public void ClearPool()
    {
        pool.Clear();
        // Remove child clouds safely in edit and play mode
        var toDestroy = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
        {
            toDestroy.Add(transform.GetChild(i).gameObject);
        }
        foreach (var go in toDestroy)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
        isInitialized = false;
    }

    void Update()
    {
        if (!Application.isPlaying && !animateInEditMode) return;

        if (!isInitialized || pool.Count == 0 || pool[0].gameObject == null)
        {
            InitializePool();
            if (!isInitialized) return;
        }

        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

        for (int i = 0; i < pool.Count; i++)
        {
            CloudInstance cloud = pool[i];
            if (cloud == null || cloud.transform == null) continue;

            Vector3 pos = cloud.transform.position;
            pos.x -= cloud.speed * dt;

            // Recycled when reaching endX (-200)
            if (pos.x <= endX)
            {
                pos.x = startX;
                pos.y = Random.Range(minHeight, maxHeight);
                pos.z = Random.Range(minZ, maxZ);
                cloud.speed = Random.Range(minSpeed, maxSpeed);
                cloud.targetScale = GenerateRandomScale();
            }

            cloud.transform.position = pos;
            UpdateCloudScale(cloud);
        }
    }

    void UpdateCloudScale(CloudInstance cloud)
    {
        float posX = cloud.transform.position.x;
        float scaleFactor = 1f;

        // Growing from 0 to full scale at start (200 -> 200 - fadeDistance)
        if (posX > startX - fadeDistance)
        {
            float t = (startX - posX) / fadeDistance;
            scaleFactor = Mathf.Clamp01(t);
        }
        // Shrinking from full scale down to 0 at end (-200 + fadeDistance -> -200)
        else if (posX < endX + fadeDistance)
        {
            float t = (posX - endX) / fadeDistance;
            scaleFactor = Mathf.Clamp01(t);
        }

        // Smooth cubic easing for organic fade in/out effect
        scaleFactor = Mathf.SmoothStep(0f, 1f, scaleFactor);
        cloud.transform.localScale = cloud.targetScale * scaleFactor;
    }

    Vector3 GenerateRandomScale()
    {
        float baseScale = Random.Range(minScale, maxScale);
        float varY = Random.Range(0.85f, 1.15f);
        float varZ = Random.Range(0.9f, 1.2f);
        return new Vector3(baseScale, baseScale * varY, baseScale * varZ);
    }

    void LoadDefaultPrefabs()
    {
#if UNITY_EDITOR
        var p1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/3D Models/Cloud.fbx");
        var p2 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/3D Models/Cloud2.fbx");
        var p3 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/3D Models/Cloud3.fbx");

        var list = new List<GameObject>();
        if (p1 != null) list.Add(p1);
        if (p2 != null) list.Add(p2);
        if (p3 != null) list.Add(p3);
        cloudPrefabs = list.ToArray();
#endif
    }
}
