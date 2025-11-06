using System.Collections.Generic;
using UnityEngine;

// Simple boids/flocking demo with optional "spawn in front of camera" support.
public class FlockingAlgorithm : MonoBehaviour, IAlgorithm
{
    [Header("Agent")]
    public GameObject agentPrefab;
    public int agentCount = 100;
    public float spawnRadius = 10f;

    [Header("Spawn relative to camera")]
    public bool spawnInFrontOfCamera = true;      // if true, spawn around a point in front of Camera.main
    public float spawnDistanceFromCamera = 8f;    // distance forward from camera
    public float verticalOffsetFromCamera = -2f;  // vertical offset relative to camera (negative = below camera)

    [Header("Boids parameters")]
    public float neighborRadius = 2.5f;
    public float separationWeight = 1.5f;
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;
    public float maxSpeed = 6f;

    List<GameObject> agents = new List<GameObject>();

    public void StartAlgorithm()
    {
        // determine spawn center
        Vector3 spawnCenter = transform.position;
        if (spawnInFrontOfCamera && Camera.main != null)
        {
            var cam = Camera.main.transform;
            spawnCenter = cam.position + cam.forward * spawnDistanceFromCamera + Vector3.up * verticalOffsetFromCamera;
        }

        // instantiate agents around spawnCenter (keeps them on same Y plane as spawnCenter)
        for (int i = 0; i < agentCount; i++)
        {
            Vector3 pos = spawnCenter + Random.insideUnitSphere * spawnRadius;
            pos.y = spawnCenter.y; // keep on same plane
            var go = Instantiate(agentPrefab, pos, Quaternion.identity, transform);
            var ac = go.GetComponent<AgentController>();
            if (ac != null) ac.maxSpeed = maxSpeed;
            agents.Add(go);
        }
    }

    public void StopAlgorithm()
    {
        // clean up
        foreach (var g in agents)
            if (g != null) Destroy(g);
        agents.Clear();
    }

    void Update()
    {
        if (agents.Count == 0) return;

        for (int i = 0; i < agents.Count; i++)
        {
            var a = agents[i];
            if (a == null) continue;
            Vector3 pos = a.transform.position;
            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;
            int nearby = 0;

            for (int j = 0; j < agents.Count; j++)
            {
                if (i == j) continue;
                var b = agents[j];
                if (b == null) continue;
                Vector3 to = b.transform.position - pos;
                float d = to.magnitude;
                if (d < neighborRadius)
                {
                    // separation
                    if (d > 0.001f) separation -= to.normalized / d;
                    // alignment
                    var rb = b.GetComponent<AgentController>();
                    if (rb != null) alignment += rb.velocity;
                    // cohesion
                    cohesion += b.transform.position;
                    nearby++;
                }
            }

            Vector3 steer = Vector3.zero;
            if (nearby > 0)
            {
                alignment = (alignment / nearby).normalized;
                cohesion = ((cohesion / nearby) - pos).normalized;
                steer = separation * separationWeight + alignment * alignmentWeight + cohesion * cohesionWeight;
            }

            var ac = a.GetComponent<AgentController>();
            Vector3 newVel = ac.velocity + steer;
            ac.ApplyVelocity(Vector3.ClampMagnitude(newVel, ac.maxSpeed));
        }
    }
}