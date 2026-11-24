using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using System.Collections;
using UnityEditor.Experimental.GraphView;

public class ScreenManager : MonoBehaviour
{
    [Header("Transition settings")]
    [SerializeField] private ScreenBounds currentScreen;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Rigidbody2D playerBody;

    [Header("Travel zone settings")]
    [SerializeField] private float clearanceDistance = 0.05f;

    private ScreenTravelZone blockedZone;
    private Collider2D blockedCollider;
    private Bounds blockedBounds;

    private int physicsStepsRemaining;

    public ScreenBounds CurrentScreen => currentScreen;
    public bool IsTransitioning { get; private set; }


    private void FixedUpdate()
    {
        if (IsTransitioning || blockedZone == null)
            return;

        //allow physics to settle after the transition
        if (physicsStepsRemaining > 0)
        {
            physicsStepsRemaining--;
            return;
        }

        CheckZoneClearance();
    }

    public void SetCurrentScreen(ScreenBounds newScreen)
    {
        currentScreen = newScreen;
    }

    public void TryTravel(ScreenTravelZone sourceZone, Rigidbody2D enteringBody)
    {
        if (enteringBody != playerBody)
            return;

        if (sourceZone.OwnerScreen != currentScreen)
            return;

        ScreenTravelZone destinationZone = sourceZone.LinkedZone;

        //Teleport the player
        Vector2 arrivalPosition = destinationZone.ArrivalPoint.position;
        playerBody.position = arrivalPosition;

        //update the current screen
        SetCurrentScreen(destinationZone.OwnerScreen);

        //calc the new camera pos
        Vector2 cameraPosition = cameraFollow.ClampToScreen(arrivalPosition, CurrentScreen);

        //repos the cam
        cameraFollow.SetCameraPosition(cameraPosition);
    }

    public void BlockZone(ScreenTravelZone zone)
    {
        ReleaseBlockedZone();

        blockedZone = zone;
        blockedCollider = zone.GetComponent<Collider2D>();

        //store the bounds bf disabling the collider
        blockedBounds = blockedCollider.bounds;

        blockedCollider.enabled = false;

        Debug.Log("Unlocked travel zone: " + blockedZone.name);
    }

    private void ReleaseBlockedZone()
    {
        if (blockedCollider != null)
        {
            blockedCollider.enabled = true;
            Debug.Log("Unlocked travel zone: " + blockedZone.name);
        }

        blockedZone = null;
        blockedCollider = null;
    }

    private void CheckZoneClearance()
    {
        //direction pointing into the destination screen
        Vector2 inwardDirection = -blockedZone.OutwardDirection;

        Collider2D[] playerColliders = playerBody.GetComponentsInChildren<Collider2D>();

        //Sync colldier pos w/ theor transforms
        Physics2D.SyncTransforms();

        bool foundCollider = false;

        foreach (Collider2D playerCollider in playerColliders)
        {
            if (!playerCollider.enabled)
                continue;

            if (playerCollider.attachedRigidbody != playerBody)
                continue;

            foundCollider = true;

            Bounds playerBounds = playerCollider.bounds;

            //player arrived from west
            if (inwardDirection == Vector2.right)
            {
                if (playerBounds.min.x < blockedBounds.max.x + clearanceDistance)
                    return;
            }

            //player arrived from east
            else if (inwardDirection == Vector2.left)
            {
                if (playerBounds.max.x < blockedBounds.min.x + clearanceDistance)
                    return;
            }

            //player arrived from south
            else if (inwardDirection == Vector2.up)
            {
                if (playerBounds.min.y < blockedBounds.max.y + clearanceDistance)
                    return;
            }

            //player arrived from north
            else if (inwardDirection == Vector2.down)
            {
                if (playerBounds.max.y < blockedBounds.min.y + clearanceDistance)
                    return;
            }

        }

        //every player collider has cleared the passage
        if (foundCollider)
        {
            ReleaseBlockedZone();
        }

    }
}
