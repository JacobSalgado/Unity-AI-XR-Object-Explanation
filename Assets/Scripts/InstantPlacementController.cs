using Meta.XR;
using Meta.XR.MRUtilityKit;
using Oculus.Interaction.Samples;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public class InstantPlacementController : MonoBehaviour
{
    [Header("Controller/Placement")]
    [SerializeField] private Transform rightControllerAnchor; // origin of the raycast
    [SerializeField] private GameObject hoop; // prefab instantiated when raycast hits surface
    [SerializeField] private float maxPlacementDistance = 5f;

    [Header("Wall Clipping Offset")]
    [SerializeField] private float hoopBackOffset = 0.02f; // half the hoop's backboard thickness, in meters
    [SerializeField] private float clippingBuffer = 0.005f; // extra buffer to dodge depth-occlusion noise

    [Header("Raycast Visual")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private GameObject reticlePrefab;
    [SerializeField] private float maxRayDistance = 5f;
    [SerializeField] private Color validColor = Color.cyan;
    [SerializeField] private Color invalidColor = Color.red;

    [Header("Raycast Manager")]
    public EnvironmentRaycastManager raycastManager;

    [HideInInspector]
    public bool hoopPlaced = false;

    private GameObject reticleInstance;

    // cached from the last raycast this frame, consumed by TryPlace on trigger press
    private bool hasValidHit = false;
    private RaycastHit cachedHit;
    private MRUKAnchor cachedAnchor;
    private EnvironmentRaycastHit cachedEnvHit; // live-depth hit, used for actual placement

    // restricts raycasts to wall surfaces only
    private readonly LabelFilter wallFilter = new LabelFilter(MRUKAnchor.SceneLabels.WALL_FACE);

    /// <summary>
    //
    /// </summary>

    private void Awake()
    {
        if (reticlePrefab != null)
        {
            reticleInstance = Instantiate(reticlePrefab);
            reticleInstance.SetActive(false);
        }

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }
    }

    private void Update()
    {
        if (hoopPlaced)
        {
            HideVisual();
            return;
        }

        Ray ray = new Ray(
            rightControllerAnchor.position, 
            rightControllerAnchor.forward
            );

        // single raycast per frame against wall anchors only - result is cached
        // and reused by both the visual and TryPlace
        hasValidHit = false;
        var room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;
        if (room != null)
        {
            hasValidHit = room.Raycast(ray, maxPlacementDistance, wallFilter, out cachedHit, out cachedAnchor);
        }

        // environment (live depth) raycast gives the actual placement point that matches
        // what passthrough occlusion sees - only trust it when the room raycast agrees it's a wall
        bool hasValidEnvHit = false;
        if (hasValidHit && raycastManager != null)
        {
            hasValidEnvHit = raycastManager.Raycast(ray, out cachedEnvHit);
        }

        // final "can place here" state requires both: it's a wall AND live depth confirms a surface
        hasValidHit = hasValidHit && hasValidEnvHit;

        UpdateRayVisual(ray, hasValidHit, cachedHit, cachedEnvHit);

        if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger))
        {
            TryPlace();
        }
    }

    private void UpdateRayVisual(Ray ray, bool didHit, RaycastHit hit, EnvironmentRaycastHit envHit)
    {
        if (didHit)
        {
            // line from controller to hit point
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, ray.origin);
            lineRenderer.SetPosition(1, hit.point);
            lineRenderer.startColor = validColor;
            lineRenderer.endColor = validColor;

            // for the reticle to sit flush on the surface, oriented to its normal
            // uses envHit (live depth) so preview matches where hoop will land
            if (reticleInstance != null)
            {
                reticleInstance.SetActive(true);
                reticleInstance.transform.SetPositionAndRotation(
                    envHit.point,
                    Quaternion.LookRotation(hit.normal, Vector3.up)
                    );
            }
        }
        else
        {
            // no valid hit - show the ray at max length in "invalid" color, hide reticle
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, ray.origin);
            lineRenderer.SetPosition(1, ray.origin + ray.direction * maxRayDistance);
            lineRenderer.startColor = invalidColor;
            lineRenderer.endColor = invalidColor;

            if (reticleInstance != null)
                reticleInstance.SetActive(false); // no proper target, no reticle shown
        }
    }

    /**
     * @brief hides linerenderer and reticleinstance
     */
    private void HideVisual()
    {
        if (lineRenderer != null) lineRenderer.enabled = false;
        if (reticleInstance != null) reticleInstance.SetActive(false);
    }

    /**
     * @brief looks for proper location to place hoop
     */
    private void TryPlace()
    {
        if (!hasValidHit || hoopPlaced) return;

        // offset outward along the wall normal so the hoop's backboard sits flush
        // against the wall instead of clipping into it (pivot offset + depth-occlusion buffer)
        Vector3 placementPosition = cachedEnvHit.point + cachedEnvHit.normal * (hoopBackOffset + clippingBuffer);

        var hoopToPlace = Instantiate(hoop);
        hoopToPlace.transform.SetPositionAndRotation(
            placementPosition,
            Quaternion.LookRotation(cachedHit.normal, Vector3.up)
            );

        // if no MRUK component is present in the secene, an OVRSpatialAnchor component
        // to the instantiated prefab to anchor it in the physical space and prevent drift
        if (MRUK.Instance?.IsWorldLockActive != true)
        {
            hoopToPlace.AddComponent<OVRSpatialAnchor>();
        }
        hoopPlaced = true;
        HideVisual();
    }
}
