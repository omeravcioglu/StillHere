using System.Collections;
using UnityEngine;

/// <summary>
/// Moves an object along a series of waypoints with configurable speed and wait times.
/// The object will rotate to face the direction it's traveling.
/// </summary>
public class WaypointMover : MonoBehaviour
{
    [Header("Waypoints")]
    [Tooltip("List of points the object will travel to")]
    public Transform[] waypoints;

    [Header("Movement Settings")]
    [Tooltip("Movement speed in units per second")]
    [Range(0.1f, 50f)]
    public float speed = 5f;

    [Tooltip("Time to wait at each waypoint in seconds")]
    [Range(0f, 30f)]
    public float waitTime = 1f;

    [Tooltip("How fast the object rotates to face the target direction")]
    [Range(1f, 20f)]
    public float rotationSpeed = 10f;

    [Header("Behavior")]
    [Tooltip("Should the object loop back to the first waypoint after reaching the last?")]
    public bool loop = true;

    [Tooltip("Should the object ping-pong between waypoints instead of looping?")]
    public bool pingPong = false;

    [Tooltip("Start moving automatically when the scene starts")]
    public bool startOnAwake = true;

    [Header("Debug")]
    [Tooltip("Draw lines between waypoints in the Scene view")]
    public bool showPath = true;

    [Tooltip("Color of the path lines")]
    public Color pathColor = Color.yellow;

    // Private variables
    private int currentWaypointIndex = 0;
    private bool isMoving = false;
    private bool isWaiting = false;
    private int direction = 1; // 1 = forward, -1 = backward (for ping-pong)
    private Coroutine movementCoroutine;

    private void Start()
    {
        if (startOnAwake && waypoints.Length > 0)
        {
            StartMoving();
        }
    }

    /// <summary>
    /// Starts the waypoint movement
    /// </summary>
    public void StartMoving()
    {
        if (waypoints.Length == 0)
        {
            Debug.LogWarning($"[WaypointMover] No waypoints assigned to {gameObject.name}");
            return;
        }

        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }

        isMoving = true;
        movementCoroutine = StartCoroutine(MoveAlongWaypoints());
    }

    /// <summary>
    /// Stops the waypoint movement
    /// </summary>
    public void StopMoving()
    {
        isMoving = false;
        isWaiting = false;

        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }
    }

    /// <summary>
    /// Pauses or resumes movement
    /// </summary>
    public void SetPaused(bool paused)
    {
        isMoving = !paused;
    }

    /// <summary>
    /// Resets the object to the first waypoint
    /// </summary>
    public void ResetToStart()
    {
        StopMoving();
        currentWaypointIndex = 0;
        direction = 1;

        if (waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
        }
    }

    private IEnumerator MoveAlongWaypoints()
    {
        while (isMoving)
        {
            if (waypoints.Length == 0) yield break;

            Transform targetWaypoint = waypoints[currentWaypointIndex];

            // Move and rotate towards the target
            while (Vector3.Distance(transform.position, targetWaypoint.position) > 0.01f)
            {
                if (!isMoving) yield break;

                // Calculate direction to target
                Vector3 directionToTarget = (targetWaypoint.position - transform.position).normalized;

                // Move towards target
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetWaypoint.position,
                    speed * Time.deltaTime
                );

                // Rotate to face movement direction (only if we have a direction)
                if (directionToTarget != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        rotationSpeed * Time.deltaTime
                    );
                }

                yield return null;
            }

            // Snap to exact position
            transform.position = targetWaypoint.position;

            // Wait at waypoint
            if (waitTime > 0)
            {
                isWaiting = true;
                yield return new WaitForSeconds(waitTime);
                isWaiting = false;
            }

            // Move to next waypoint
            MoveToNextWaypoint();
        }
    }

    private void MoveToNextWaypoint()
    {
        if (pingPong)
        {
            // Ping-pong behavior
            currentWaypointIndex += direction;

            if (currentWaypointIndex >= waypoints.Length)
            {
                direction = -1;
                currentWaypointIndex = waypoints.Length - 2;
                if (currentWaypointIndex < 0) currentWaypointIndex = 0;
            }
            else if (currentWaypointIndex < 0)
            {
                direction = 1;
                currentWaypointIndex = 1;
                if (currentWaypointIndex >= waypoints.Length) currentWaypointIndex = 0;
            }
        }
        else if (loop)
        {
            // Loop behavior
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
        else
        {
            // One-way behavior
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Length)
            {
                isMoving = false;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showPath || waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = pathColor;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            // Draw waypoint sphere
            Gizmos.DrawWireSphere(waypoints[i].position, 0.3f);

            // Draw line to next waypoint
            if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
            else if (loop && waypoints[0] != null && i == waypoints.Length - 1)
            {
                // Draw line back to start if looping
                Gizmos.color = new Color(pathColor.r, pathColor.g, pathColor.b, 0.5f);
                Gizmos.DrawLine(waypoints[i].position, waypoints[0].position);
                Gizmos.color = pathColor;
            }
        }
    }

    // Public getters for state
    public bool IsMoving => isMoving;
    public bool IsWaiting => isWaiting;
    public int CurrentWaypointIndex => currentWaypointIndex;
}
