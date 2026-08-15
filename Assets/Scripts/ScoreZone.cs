using UnityEngine;

/// <summary>
/// Detects the ball passing downward through the net as a made shot, distinguises a swish
/// from a bank/rim shot via BallContactTracker, and reports the result to ScoreManager and BallShooting
/// </summary>
public class ScoreZone : MonoBehaviour
{
    [SerializeField] private string ballTag = "Basketball";
    [SerializeField] private BallShooting ballShooting_;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(ballTag)) return;

        Rigidbody rb = other.attachedRigidbody;

        // only count the ball moving downward through the zone - guards against
        // a ball the bounces upward through it
        // from being counted as a make
        if (rb == null || rb.linearVelocity.y >= 0f) return;

        BallContactTracker tracker = other.GetComponent<BallContactTracker>();
        bool wasSwish = tracker == null || !tracker.HasTouchedBoard;

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterScore(wasSwish);

        if (ballShooting_ != null)
            ballShooting_.OnBallResult(true);

    }
}
