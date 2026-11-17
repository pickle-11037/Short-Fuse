using Unity.VisualScripting;
using UnityEngine;

public class ShortstuffRagdoll : MonoBehaviour
{
    //REFERENCES
    [SerializeField] private Rigidbody2D torso;
    [SerializeField] private Rigidbody2D[] bodyParts;

    [SerializeField] private float blastSpin = 250f;

    public Vector2 TorsoPosition => torso.position;

    public float TorsoSpeed => torso.linearVelocity.magnitude;

    //SETUP
    public void Activate(Vector2 initialVelocity, Vector2 explosionImpulse, Vector2 explosionOrigin)
    {
        foreach (Rigidbody2D part in bodyParts)
        {
            if (part != null)
            {
                part.linearVelocity = initialVelocity;
            }
        }

        torso.AddForce(explosionImpulse, ForceMode2D.Impulse);

        float spinDirection = torso.position.x >= explosionOrigin.x
            ? -1f
            : 1f;

        torso.angularVelocity = spinDirection * blastSpin;
    }

}
