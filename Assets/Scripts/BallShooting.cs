using Meta.XR;
using Meta.XR.MRUtilityKit;
using UnityEngine;

public class BallShooting : MonoBehaviour
{
    [Header("Controller & Shooting Features")]
    [SerializeField] private Transform rightController; // origin of the spawned basketball
    [SerializeField] private GameObject basketballPrefab; // prefab of basketball

    public InstantPlacementController instantPlacementController;

    private bool activeBall = false;
    private GameObject basketballInstance;
    private Rigidbody basketballRb;

    private void Awake()
    {
        // spawn once, in the hand, and keep inactive until the hoop is placed
        basketballInstance = Instantiate(basketballPrefab, rightController.position, rightController.rotation, rightController);

        basketballRb = basketballInstance.GetComponent<Rigidbody>();
        basketballRb.isKinematic = true; // no gravity yet - controlled manually while in-hand
        basketballInstance.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (instantPlacementController.hoopPlaced && !activeBall)
        {
            basketballInstance.SetActive(true);
            activeBall = true;
        }
    }
}
