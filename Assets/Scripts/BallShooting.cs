using Meta.XR;
using Meta.XR.MRUtilityKit;
using Unity.VisualScripting;
using UnityEngine;

public class BallShooting : MonoBehaviour
{
    private enum ShotState { InHand, Aiming, InFlight }
    private ShotState state = ShotState.InHand;

    [Header("Controller & Shooting Features")]
    [SerializeField] private Transform rightController; // origin of the spawned basketball
    [SerializeField] private GameObject basketballPrefab; // prefab of basketball

    private Transform hoopTarget; // rim 

    [Header("Aiming")]
    [SerializeField] private float maxYawDegrees = 35f; // stick X -> left/right curve
    [SerializeField] private float minPitchDegrees = 0f; // stick at rest -> lowest arc
    [SerializeField] private float maxPitchDegrees = 50f; // stick Y at full push -> highest arc
    [SerializeField] private float minLaunchSpeed = 3f;
    [SerializeField] private float maxLaunchSpeed = 9f;
    [SerializeField] private float chargeDeadzone = 0.15f; // stick must exceed this to start charging
    // [SerializeField] private float releaseThreshold = 0.1f; // letting stick fall below this fires the shot

    [Header("Trajectory Preview")]
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private int trajectoryPoints = 30;
    [SerializeField] private float trajectoryTimeStep = 0.05f;

    [Header("Shot Lifecycle")]
    [SerializeField] private float maxFlightTime = 4f;
    [SerializeField] private float outOfPlayHeight = -2f; // relative to spawn height, ball below this is "gone"

    [Header("Physics Feel")]
    [SerializeField] private float ballMass = 0.6f; // kg, roughly equivalent to a real bball
    [SerializeField] private float ballDrag = 0.05f;
    [SerializeField] private float ballAngularDrag = 0.3f;
    [SerializeField] private float backspinAmount = 8f; // radians/sec applied at release


    [Header("Instant Placement Controller Reference")]
    public InstantPlacementController instantPlacementController;

    private bool activeBall = false;
    private GameObject basketballInstance;
    private Rigidbody basketballRb;
    private BallContactTracker basketballContactTracker_;

    // Aim States
    private float currentYaw;
    private float currentPitch;
    private float currentSpeed01; // 0-1, how "charged" the shot is

    private float flightTimer;

    private void Awake()
    {
        // spawn once, in the hand, and keep inactive until the hoop is placed
        basketballInstance = Instantiate(basketballPrefab, rightController.position, rightController.rotation, rightController);

        basketballRb = basketballInstance.GetComponent<Rigidbody>();
        basketballRb.isKinematic = true; // no gravity yet - controlled manually while in-hand

        ConfigureBallPhysics(basketballRb);
        basketballInstance.SetActive(false);

        basketballContactTracker_ = basketballInstance.GetComponent<BallContactTracker>();


        if (trajectoryLine != null)
        {
            trajectoryLine.positionCount = trajectoryPoints;
            trajectoryLine.enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (instantPlacementController.hoopPlaced && !activeBall)
        {
            hoopTarget = instantPlacementController.PlacedHoopTransform;
            basketballInstance.SetActive(true);
            activeBall = true;
        }

        switch (state)
        {
            case ShotState.InHand:
                CheckForChargeStart(); //stick exceeds deadzone -> move to Aiming
                break;
            case ShotState.Aiming:
                HandleAiming(); // read stick, update yaw/pitch/speed, draw trajectory, check release
                break;
            case ShotState.InFlight:
                HandleFlightTimeout();  // watch for miss (fell out of play / timed out)
                break;
        }
    }

    /**
     * @brief sets mass/drag for basketball
     */
    private void ConfigureBallPhysics(Rigidbody rb)
    {
        rb.mass = ballMass;
        rb.linearDamping = ballDrag;
        rb.angularDamping = ballAngularDrag;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // smoothens visual motion between physics steps
    }

    /**
     * @brief checks if right stick is enough to charge basketball aim
     */
    private void CheckForChargeStart()
    {
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        if (stick.sqrMagnitude > (chargeDeadzone * chargeDeadzone))
        {
            state = ShotState.Aiming;
            trajectoryLine.enabled = true;
        }
    }

    private void HandleAiming()
    {
        // keeps the ball anchored to hand while aiming
        basketballInstance.transform.SetPositionAndRotation(rightController.position, rightController.rotation);

        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        // stick X curves the shot left/right
        currentYaw = stick.x * maxYawDegrees;

        // lets the stick fall back to neutral WITHOUT firing cancels the aim entirely.
        if (stick.sqrMagnitude < (chargeDeadzone * chargeDeadzone))
        {
            CancelAiming();
            return;
        }

        // stick Y (forward push) drives both arc height and power together
        currentSpeed01 = Mathf.Clamp01(stick.y); // clamp since pulling stick back gives negative values
        currentPitch = Mathf.Lerp(minPitchDegrees, maxPitchDegrees, currentSpeed01);

        Vector3 launchVelocity = ComputeLaunchVelocity();
        DrawTrajectoryPreview(rightController.position, launchVelocity);

        // trigger press confirms the shot - separate from the stick
        if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger))
        {
            FireShot(launchVelocity);
        }
    }

    private void HandleFlightTimeout()
    {
        flightTimer += Time.deltaTime;

        bool fellOutOfPlay = basketballInstance.transform.position.y < rightController.position.y + outOfPlayHeight;
        bool tookTooLong = flightTimer > maxFlightTime;

        if (tookTooLong || fellOutOfPlay)
        {
            RespawnBall();
        }
    }

    private void CancelAiming()
    {
        state = ShotState.InHand;
        trajectoryLine.enabled = false;
    }

    private Vector3 ComputeLaunchVelocity()
    {
        Vector3 toHoop = hoopTarget.position - rightController.position;
        Vector3 horizontalDir = new Vector3(toHoop.x, 0f, toHoop.z).normalized;

        Vector3 aimedHorizontal = Quaternion.AngleAxis(currentYaw, Vector3.up) * horizontalDir;

        float pitchRad = currentPitch * Mathf.Deg2Rad;
        Vector3 launchDir = new Vector3(
            aimedHorizontal.x * Mathf.Cos(pitchRad),
            Mathf.Sin(pitchRad),
            aimedHorizontal.z * Mathf.Cos(pitchRad)
        ).normalized;

        float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, currentSpeed01);
        return launchDir * speed;
    }

    private void DrawTrajectoryPreview(Vector3 origin, Vector3 initialVelocity)
    {
        Vector3 gravity = Physics.gravity;
        for (int i = 0; i < trajectoryPoints; i++)
        {
            float t = i * trajectoryTimeStep;
            Vector3 point = origin + initialVelocity * t + 0.5f * gravity * t * t;
            trajectoryLine.SetPosition(i, point);
        }
    }

    private void FireShot(Vector3 launchVelocity)
    {
        // unparent before enabling physics - otherwise the Rigidbody keeps inheriting
        // the moving controller's transform on top of its own physics motion, which
        // causes erratic/compounded flight instead of a clean launch
        basketballInstance.transform.SetParent(null);

        basketballRb.isKinematic = false;
        basketballRb.linearVelocity = launchVelocity;

        trajectoryLine.enabled = false;
        flightTimer = 0f;
        state = ShotState.InFlight;
    }

    private void RespawnBall()
    {
        Destroy(basketballInstance);

        basketballInstance = Instantiate(basketballPrefab, rightController.position, rightController.rotation, rightController);
        basketballRb = basketballInstance.GetComponent<Rigidbody>();
        basketballRb.isKinematic = true; // back to hand-controlled, no gravity until next shot

        state = ShotState.InHand;
    }

    public void OnBallResult(bool madeShot)
    {
        if (state != ShotState.InFlight) return;
        RespawnBall();
    }
}
