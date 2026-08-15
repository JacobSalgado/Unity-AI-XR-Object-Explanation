using UnityEngine;

/// <summary>
/// Tracks whether the ball has touched the
/// backboard/rim (tagged "HoopBoard") since it was last fired, so
/// ScoreZone can tell a clean swish apart from a bank/rim shot
/// </summary>

public class BallContactTracker : MonoBehaviour
{
    [SerializeField] private string boardTag = "HoopBoard";

    public bool HasTouchedBoard { get; private set; }

    /*
     * @brief when a new shot is fired, contact from the previous shot doesn't carry over
     */
    public void ResetContact()
    {
        HasTouchedBoard = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag(boardTag))
        {
            HasTouchedBoard = true;
        }
    }
}
