using UnityEngine;

/// <summary>
/// Fallback / alternative collection mechanism (attach to the agent).
/// If you can't or don't want to use physics triggers for markers, attach this to the pathfinding agent.
/// It will periodically check for nearby markers and remove them when within collectRadius.
/// 
/// Usage:
/// - Add this component to your agent GameObject (pathfinding agent).
/// - Set markerLayer to the Layer used by marker objects, or leave at default and mark markers with tag "Marker".
/// - If using layer detection, set marker objects to that layer, and give them a Collider (trigger or not).
/// - This approach uses Physics.OverlapSphere (fast) and avoids per-frame GameObject.Find calls.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MarkerCollector : MonoBehaviour
{
    [Tooltip("Radius (world units) within which a marker will be collected.")]
    public float collectRadius = 0.5f;

    [Tooltip("Layer mask to filter marker colliders. If left as 'Nothing', the script will fall back to tag-based detection.")]
    public LayerMask markerLayer;

    [Tooltip("Optional: tag used for markers when markerLayer is empty.")]
    public string markerTag = "Marker";

    // how often to check (seconds) - lowering frequency reduces cost
    [Tooltip("How often (seconds) to scan for nearby markers.")]
    public float checkInterval = 0.1f;

    float nextCheck = 0f;

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + Mathf.Max(0.01f, checkInterval);

        // Prefer layer-based detection if a mask is provided
        if (markerLayer != (LayerMask)0)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, collectRadius, markerLayer, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                var marker = hits[i].GetComponent<Marker>();
                if (marker != null)
                {
                    marker.Collect();
                }
                else
                {
                    // if marker script missing, still remove the object
                    Destroy(hits[i].gameObject);
                }
            }
        }
        else if (!string.IsNullOrEmpty(markerTag))
        {
            // Fallback: find by tag (cheaper than FindGameObjects every frame because we only call occasionally)
            var candidates = GameObject.FindGameObjectsWithTag(markerTag);
            for (int i = 0; i < candidates.Length; i++)
            {
                var go = candidates[i];
                if (go == null) continue;
                if ((go.transform.position - transform.position).sqrMagnitude <= collectRadius * collectRadius)
                {
                    var marker = go.GetComponent<Marker>();
                    if (marker != null)
                        marker.Collect();
                    else
                        Destroy(go);
                }
            }
        }
    }

    // visualize the collection radius in the editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.35f);
        Gizmos.DrawSphere(transform.position, collectRadius);
    }
}