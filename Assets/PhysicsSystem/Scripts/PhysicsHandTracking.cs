using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR.OpenXR.Input;
using static UnityEngine.GraphicsBuffer;

public class PhysicsHandTracking : MonoBehaviour
{
    public Transform trackingHand;

    [Header("Î»ÖÃPD")]
    public float posKp = 500f;
    public float posKd = 50f;

    private Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // ===== Î»ÖÃPD =====
        Vector3 posError = trackingHand.position - rb.position;
        Vector3 posVelError = -rb.linearVelocity;
        Vector3 posForce = posError * posKp + posVelError * posKd;
        rb.AddForce(posForce);

        // ===== Ðý×ªPD =====
        //Quaternion rotationDifference = trackingHand.rotation * Quaternion.Inverse(transform.rotation);
        //rotationDifference.ToAngleAxis(out float angleInDegree, out Vector3 rotationAxis);
        
        //Vector3 rotationDifferenceInDegree = angleInDegree* rotationAxis;
        //rb.angularVelocity = (rotationDifferenceInDegree * Mathf.Deg2Rad / Time.deltaTime) * 1;
    }
}
