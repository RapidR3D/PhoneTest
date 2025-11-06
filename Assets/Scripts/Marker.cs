using System.Collections;
using UnityEngine;

/// <summary>
/// Simple marker script. When an agent passes through the marker (via trigger) it will disappear.
/// Attach this to the marker prefab (or scene marker objects).
/// Requirements:
/// - Marker GameObject must have a Collider. Set "Is Trigger" = true (recommended).
/// - Agent must have a Collider (not trigger) and a Rigidbody (can be kinematic).
/// - Either tag the agent with agentTag (default "Agent") OR the agent must have a component named "PathfindingAgent".
/// 
/// Usage:
/// - Add this script to your marker prefab, enable "Is Trigger" on its Collider.
/// - Tag your pathfinding agent GameObject with "Agent" (or change agentTag to match).
/// - Optionally set disappearDelay to leave a short visual before destroying.
/// </summary>
public class Marker : MonoBehaviour
{
    [Tooltip("If non-zero, the marker will wait this many seconds before actually destroying after being triggered.")]
    public float disappearDelay = 0f;

    [Tooltip("Tag used to recognize the pathfinding agent. If the colliding object has this tag OR contains a PathfindingAgent component, it will collect this marker.")]
    public string agentTag = "Agent";

    // Called by physics when another collider enters this trigger
    void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        // If the collider belongs to an object with the expected tag, collect
        if (!string.IsNullOrEmpty(agentTag) && other.CompareTag(agentTag))
        {
            Collect();
            return;
        }

        // Otherwise, check for a component named "PathfindingAgent" (common name for custom agent scripts).
        // This check is safe if you don't want to require a tag.
        var agentComp = other.GetComponentInParent(typeof(MonoBehaviour));
        if (agentComp != null)
        {
            // Check by type-name to avoid forcing a compile-time dependency on a specific pathfinding script name.
            var mb = agentComp as MonoBehaviour;
            if (mb != null && mb.GetType().Name == "PathfindingAgent")
            {
                Collect();
                return;
            }
        }
    }

    // Public collect method in case you want to call it from other code
    public void Collect()
    {
        if (disappearDelay <= 0f)
        {
            Destroy(gameObject);
        }
        else
        {
            StartCoroutine(DelayedDestroy());
        }
    }

    IEnumerator DelayedDestroy()
    {
        // optional visual/audio could go here
        yield return new WaitForSeconds(disappearDelay);
        Destroy(gameObject);
    }
}