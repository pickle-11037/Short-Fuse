using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using System.Collections;
using UnityEditor.Experimental.GraphView;
using UnityEditor;
using UnityEditorInternal;

public class ScreenManager : MonoBehaviour
{
   
    
    [Header("Screen settings")]
    [SerializeField] private ScreenBounds currentScreen;

    [Header("Transition settings")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private MonoBehaviour[] playerControls;
    [SerializeField] private float transitionDuration = 0.5f;

    [Header("Travel zone settings")]
    [SerializeField] private float clearanceDistance = 0.05f;

    private ScreenTravelZone blockedZone;
    private Collider2D blockedCollider;
    private Bounds blockedBounds;

    private int physicsStepsRemaining;


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

        if (IsTransitioning)
            return;

        if (sourceZone == blockedZone)
            return;

        if (sourceZone.OwnerScreen != currentScreen)
            return;

        ScreenTravelZone destination = sourceZone.LinkedZone;

        if (destination == null)
        {
            Debug.LogWarning("Travel zone has no connection.");
            return;
        }

        if (destination.OwnerScreen == null || destination.ArrivalPoint == null)
        {
            Debug.LogWarning("Destinaion is not configured.");
            return;
        }

        if (destination.OwnerScreen == currentScreen)
            return;

        if (sourceZone.OutwardDirection != -destination.OutwardDirection)
        {
            Debug.LogWarning("Travel zone directions do not match.");
            return;
        }

        Collider2D destinationCollider = destination.GetComponent<Collider2D>();

        if (destinationCollider == null || !destinationCollider.isTrigger || !destinationCollider.enabled)
        {
            Debug.LogWarning("Destination trigger is not configured");
            return;
        }

        Debug.Log("Travelling from " + sourceZone.OwnerScreen.name + " to " + destination.OwnerScreen.name);

        StartCoroutine(TravelRoutine(sourceZone));

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


    private IEnumerator TravelRoutine(ScreenTravelZone sourceZone)
    {
        IsTransitioning = true;

        ScreenTravelZone destinationZone = sourceZone.LinkedZone;

        //disable the destination trigger immediately
        BlockZone(destinationZone);

        //Store player momentum
        Vector2 storedVelocity = playerBody.linearVelocity;
        float storedAngularVelocity = playerBody.angularVelocity;

        //store and diable player controls
        bool[] controlStates = new bool[playerControls.Length];

        for (int i = 0; i < playerControls.Length; i++)
        {
            if (playerControls[i] == null)
                continue;

            controlStates[i] = playerControls[i].enabled;
            playerControls[i].enabled = false;
        }

        //Suspend player physics
        playerBody.simulated = false;

        //calc cam pos
        Vector2 startPosition = cameraFollow.transform.position;
        Vector2 arrivalPosition = destinationZone.ArrivalPoint.position;

        Vector2 endPosition = cameraFollow.ClampToScreen(arrivalPosition, destinationZone.OwnerScreen);

        float elapsedTime = 0f;

        //transitioning the cam
        while (elapsedTime < transitionDuration)
        {
            elapsedTime = Time.deltaTime;

            float progress = elapsedTime / transitionDuration;
            progress = Mathf.SmoothStep(0f, 1f, progress);

            Vector2 cameraPosition = Vector2.Lerp(startPosition, endPosition, progress);

            cameraFollow.SetCameraPosition(cameraPosition);

            yield return null;
        }

        cameraFollow.SetCameraPosition(endPosition);

        //move the player into the destination screen
        playerBody.position = arrivalPosition;

        SetCurrentScreen(destinationZone.OwnerScreen);

        //Restore player physics
        playerBody.simulated = true;

        Physics2D.SyncTransforms();

        //restore player controls
        for (int i = 0; i < playerControls.Length;  i++)
        {
            if (playerControls[i] == null)
                continue;

            playerControls[i].enabled = controlStates[i];
        }

        //restores player momentum
        playerBody.linearVelocity = storedVelocity;
        playerBody.angularVelocity = storedAngularVelocity;

        //keep the destination blocked through initial physics updates
        physicsStepsRemaining = 2;

        IsTransitioning = false;

    }

}
