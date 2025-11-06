using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AgentController : MonoBehaviour
{
    public Vector3 velocity;
    public float maxSpeed = 5f;

    void Update()
    {
        // basic movement: move by velocity (no physics)
        transform.position += velocity * Time.deltaTime;
        if (velocity.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity.normalized, Vector3.up), 10f * Time.deltaTime);
    }

    public void ApplyVelocity(Vector3 v)
    {
        if (v.magnitude > maxSpeed)
            v = v.normalized * maxSpeed;
        velocity = v;
    }
}