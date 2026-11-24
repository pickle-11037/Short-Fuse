using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public class ScreenManager : MonoBehaviour
{
    [Header("Screen Settings")]
    [SerializeField] private ScreenBounds currentScreen;

    [Header("Player and Camera")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Rigidbody2D playerBody;

    public ScreenBounds CurrentScreen => currentScreen;
    public bool IsTransitioning => false;

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
}
