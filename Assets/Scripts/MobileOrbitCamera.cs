using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

[RequireComponent(typeof(Camera))]
public class MobileOrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 targetOffset = Vector3.zero;

    [Header("Distance")]
    public float distance = 10f;
    public float minDistance = 2f;
    public float maxDistance = 50f;

    [Header("Speeds")]
    public float rotateSpeed = 0.25f;  // degrees per pixel
    public float zoomSpeed = 0.05f;
    public float panSpeed = 0.002f;

    [Header("Rotation limits")]
    public float minPitch = -30f;
    public float maxPitch = 85f;

    [Header("Behavior")]
    public bool allowRotate = true;
    public bool allowZoom = true;
    public bool allowPan = true;
    public bool useSmoothing = true;
    public float smoothTime = 0.08f;

    [Header("Debug")]
    public bool showDebugOverlay = true;
    public Font debugFont;

    // internal state
    float yaw = 0f;
    float pitch = 25f;
    Vector3 smoothPosVelocity;
    Vector3 desiredTargetWorldPos;
    float desiredDistance;
    Camera cam;

#if ENABLE_INPUT_SYSTEM
    PointerEventData pointerEventData;
    List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
#endif

    // debug UI
    GameObject debugCanvas;
    Text debugText;

    // --- Touch transition helpers (prevents one-frame bogus delta after multi-touch) ---
    int prevTouchCount = 0;
    Vector2 lastSingleTouchPos = Vector2.zero;
    bool lastSingleTouchValid = false;
    int activeSingleFingerId = -1;
    // small hysteresis storage for recent horizontal movement
    float lastAppliedHorizontal = 0f;
    // tuning thresholds
    const float kTinySqr = 0.000001f;
    const float kSuspiciousHorizontalThreshold = 1.5f; // pixels; tune if needed
    const float kSuspiciousVerticalThreshold = 0.7f;   // pixels
    // -------------------------------------------------------------------------------

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (target == null)
        {
            var go = new GameObject("CameraTarget");
            go.transform.position = Vector3.zero;
            target = go.transform;
        }

        desiredTargetWorldPos = target.position + targetOffset;
        desiredDistance = Mathf.Clamp(distance, minDistance, maxDistance);

        Vector3 dir = (transform.position - desiredTargetWorldPos).normalized;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion initial = Quaternion.LookRotation(-dir, Vector3.up);
            Vector3 e = initial.eulerAngles;
            yaw = e.y;
            pitch = e.x;
        }

#if ENABLE_INPUT_SYSTEM
        // Try to enable EnhancedTouch early; additional guards exist for safety when reading activeTouches.
        try { EnhancedTouchSupport.Enable(); } catch { /* ignore */ }
        pointerEventData = new PointerEventData(EventSystem.current);
#endif

        if (showDebugOverlay) CreateDebugOverlay();
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        try { EnhancedTouchSupport.Disable(); } catch { }
#endif
    }

    void CreateDebugOverlay()
    {
        debugCanvas = new GameObject("CameraDebugCanvas");
        var canvas = debugCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        debugCanvas.AddComponent<CanvasScaler>();
        debugCanvas.AddComponent<GraphicRaycaster>();

        var go = new GameObject("DebugText");
        go.transform.SetParent(debugCanvas.transform, false);
        debugText = go.AddComponent<Text>();

        if (debugFont != null)
        {
            debugText.font = debugFont;
        }
        else
        {
            Font chosen = null;
            try { chosen = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { chosen = null; }
            if (chosen == null)
            {
                try { chosen = Font.CreateDynamicFontFromOSFont("Arial", 14); } catch { chosen = null; }
            }
            if (chosen != null) debugText.font = chosen;
            else debugText.font = null;
        }

        debugText.fontSize = 14;
        debugText.alignment = TextAnchor.UpperLeft;
        debugText.color = Color.yellow;
        var rt = debugText.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(8f, -8f);
        rt.sizeDelta = new Vector2(600f, 300f);
    }

#if ENABLE_INPUT_SYSTEM
    // Safely return a list of enhanced touches. If EnhancedTouch isn't enabled,
    // attempt to enable it and try again. Always returns a non-null list (possibly empty).
    List<UnityEngine.InputSystem.EnhancedTouch.Touch> GetActiveEnhancedTouchesSafe()
    {
        var outList = new List<UnityEngine.InputSystem.EnhancedTouch.Touch>();
        try
        {
            var at = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            for (int i = 0; i < at.Count; i++)
                outList.Add(at[i]);
            return outList;
        }
        catch (System.InvalidOperationException)
        {
            // Try to enable it and try again once
            try { EnhancedTouchSupport.Enable(); } catch { }
            try
            {
                var at2 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
                for (int i = 0; i < at2.Count; i++)
                    outList.Add(at2[i]);
                return outList;
            }
            catch { /* fall through to return empty */ }
        }
        catch { /* any other error -> return empty */ }

        return outList;
    }
#endif

    void LateUpdate()
    {
        try
        {
            bool touchesOverUI = false;

#if ENABLE_LEGACY_INPUT_MANAGER
            int currentTouchCount = Input.touchCount;
            if (EventSystem.current != null && Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    try
                    {
                        if (EventSystem.current.IsPointerOverGameObject(Input.touches[i].fingerId))
                        {
                            touchesOverUI = true;
                            break;
                        }
                    }
                    catch { /* ignore pointer id exceptions */ }
                }
            }
#elif ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var enhancedTouches = GetActiveEnhancedTouchesSafe();
            int currentTouchCount = enhancedTouches != null ? enhancedTouches.Count : 0;
            if (EventSystem.current != null && currentTouchCount > 0)
            {
                foreach (var t in enhancedTouches)
                {
                    pointerEventData.position = t.screenPosition;
                    uiRaycastResults.Clear();
                    EventSystem.current.RaycastAll(pointerEventData, uiRaycastResults);
                    if (uiRaycastResults.Count > 0)
                    {
                        touchesOverUI = true;
                        break;
                    }
                }
            }
#else
            int currentTouchCount = 0;
#endif

            // --- Multi-touch: pinch zoom and two-finger pan ---
#if ENABLE_LEGACY_INPUT_MANAGER
            if (currentTouchCount >= 2)
            {
                // clear single-touch tracking while in multi-touch
                lastSingleTouchValid = false;
                activeSingleFingerId = -1;
                lastAppliedHorizontal = 0f;

                Touch t0 = Input.touches[0];
                Touch t1 = Input.touches[1];

                if (allowZoom)
                {
                    Vector2 prevPos0 = t0.position - t0.deltaPosition;
                    Vector2 prevPos1 = t1.position - t1.deltaPosition;
                    float prevDist = Vector2.Distance(prevPos0, prevPos1);
                    float currDist = Vector2.Distance(t0.position, t1.position);
                    float diff = currDist - prevDist;

                    desiredDistance -= diff * zoomSpeed * Mathf.Max(0.02f, desiredDistance * 0.02f);
                    desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
                }

                if (allowPan)
                {
                    Vector2 delta0 = t0.deltaPosition;
                    Vector2 delta1 = t1.deltaPosition;
                    Vector2 avgDelta = (delta0 + delta1) * 0.5f;

                    if (delta0.sqrMagnitude > 0.0001f && delta1.sqrMagnitude > 0.0001f &&
                        Vector2.Dot(delta0.normalized, delta1.normalized) > 0.3f)
                    {
                        Vector3 right = transform.right;
                        Vector3 up = transform.up;
                        float scale = panSpeed * desiredDistance;
                        Vector3 pan = -(right * avgDelta.x + up * avgDelta.y) * scale;
                        desiredTargetWorldPos += pan;
                    }
                }
            }
#elif ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (currentTouchCount >= 2)
            {
                lastSingleTouchValid = false;
                activeSingleFingerId = -1;
                lastAppliedHorizontal = 0f;

                var t0 = enhancedTouches[0];
                var t1 = enhancedTouches[1];

                if (allowZoom)
                {
                    Vector2 prevPos0 = t0.screenPosition - t0.delta;
                    Vector2 prevPos1 = t1.screenPosition - t1.delta;
                    float prevDist = Vector2.Distance(prevPos0, prevPos1);
                    float currDist = Vector2.Distance(t0.screenPosition, t1.screenPosition);
                    float diff = currDist - prevDist;

                    desiredDistance -= diff * zoomSpeed * Mathf.Max(0.02f, desiredDistance * 0.02f);
                    desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
                }

                if (allowPan)
                {
                    Vector2 delta0 = t0.delta;
                    Vector2 delta1 = t1.delta;
                    Vector2 avgDelta = (delta0 + delta1) * 0.5f;

                    if (delta0.sqrMagnitude > 0.0001f && delta1.sqrMagnitude > 0.0001f &&
                        Vector2.Dot(delta0.normalized, delta1.normalized) > 0.3f)
                    {
                        Vector3 right = transform.right;
                        Vector3 up = transform.up;
                        float scale = panSpeed * desiredDistance;
                        Vector3 pan = -(right * avgDelta.x + up * avgDelta.y) * scale;
                        desiredTargetWorldPos += pan;
                    }
                }
            }
#endif

            // --- Single-touch rotation with robust id/delta tracking and bogus-delta guard ---
            if (currentTouchCount == 1 && !touchesOverUI && allowRotate)
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                var t = Input.touches[0];
                Vector2 touchPos = t.position;
                int fid = t.fingerId;

                // If the active finger changed or we just transitioned, reinitialize baseline and skip rotation this frame
                if (!lastSingleTouchValid || activeSingleFingerId != fid || prevTouchCount != 1)
                {
                    activeSingleFingerId = fid;
                    lastSingleTouchPos = touchPos;
                    lastSingleTouchValid = true;
                }
                else
                {
                    // prefer platform-provided delta if available (more reliable across transitions)
                    Vector2 delta = t.deltaPosition;
                    if (delta.sqrMagnitude < kTinySqr)
                        delta = touchPos - lastSingleTouchPos;

                    // suspicious single-frame horizontal-zero after recent horizontal movement?
                    if (Mathf.Abs(delta.x) < 0.01f &&
                        Mathf.Abs(delta.y) > kSuspiciousVerticalThreshold &&
                        Mathf.Abs(lastAppliedHorizontal) > kSuspiciousHorizontalThreshold)
                    {
                        // skip this frame and re-baseline
                        lastSingleTouchPos = touchPos;
                        lastSingleTouchValid = true;
                        activeSingleFingerId = fid;
                        lastAppliedHorizontal = 0f;
                    }
                    else
                    {
                        yaw += delta.x * rotateSpeed;
                        pitch -= delta.y * rotateSpeed;
                        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

                        lastSingleTouchPos = touchPos;
                        lastAppliedHorizontal = delta.x;
                    }
                }
#elif ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                var touches = GetActiveEnhancedTouchesSafe();
                if (touches != null && touches.Count == 1)
                {
                    var t = touches[0];
                    Vector2 touchPos = t.screenPosition;
                    int fid = t.finger.index;

                    if (!lastSingleTouchValid || activeSingleFingerId != fid || prevTouchCount != 1)
                    {
                        activeSingleFingerId = fid;
                        lastSingleTouchPos = touchPos;
                        lastSingleTouchValid = true;
                    }
                    else
                    {
                        Vector2 delta = t.delta;
                        if (delta.sqrMagnitude < kTinySqr)
                            delta = touchPos - lastSingleTouchPos;

                        if (Mathf.Abs(delta.x) < 0.01f &&
                            Mathf.Abs(delta.y) > kSuspiciousVerticalThreshold &&
                            Mathf.Abs(lastAppliedHorizontal) > kSuspiciousHorizontalThreshold)
                        {
                            // skip and re-baseline
                            lastSingleTouchPos = touchPos;
                            lastSingleTouchValid = true;
                            activeSingleFingerId = fid;
                            lastAppliedHorizontal = 0f;
                        }
                        else
                        {
                            yaw += delta.x * rotateSpeed;
                            pitch -= delta.y * rotateSpeed;
                            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

                            lastSingleTouchPos = touchPos;
                            lastAppliedHorizontal = delta.x;
                        }
                    }
                }
#else
                // no input system enabled - do nothing
#endif
            }
            else if (currentTouchCount == 0)
            {
                lastSingleTouchValid = false;
                activeSingleFingerId = -1;
                lastAppliedHorizontal = 0f;
            }

            // --- Editor mouse controls preserved (unchanged) ---
#if UNITY_EDITOR
    #if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.mousePresent)
            {
                if (allowRotate && Input.GetMouseButton(0))
                {
                    Vector2 delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                    yaw += delta.x * 8f * rotateSpeed;
                    pitch -= delta.y * 8f * rotateSpeed;
                    pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                }

                if (allowPan && Input.GetMouseButton(1))
                {
                    float dx = -Input.GetAxis("Mouse X");
                    float dy = -Input.GetAxis("Mouse Y");
                    Vector3 right = transform.right;
                    Vector3 up = transform.up;
                    float scale = panSpeed * desiredDistance * 50f;
                    Vector3 pan = (right * dx + up * dy) * scale;
                    desiredTargetWorldPos += pan;
                }

                if (allowZoom)
                {
                    float scroll = Input.GetAxis("Mouse ScrollWheel");
                    if (Mathf.Abs(scroll) > 0.0001f)
                    {
                        desiredDistance -= scroll * 10f * Mathf.Max(0.02f, desiredDistance * 0.02f);
                        desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
                    }
                }
            }
    #elif ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                var delta = Mouse.current.delta.ReadValue();

                if (allowRotate && Mouse.current.leftButton != null && Mouse.current.leftButton.isPressed)
                {
                    yaw += delta.x * 8f * rotateSpeed;
                    pitch -= delta.y * 8f * rotateSpeed;
                    pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                }

                if (allowPan && Mouse.current.rightButton != null && Mouse.current.rightButton.isPressed)
                {
                    float dx = -delta.x;
                    float dy = -delta.y;
                    Vector3 right = transform.right;
                    Vector3 up = transform.up;
                    float scale = panSpeed * desiredDistance * 50f;
                    Vector3 pan = (right * dx + up * dy) * scale;
                    desiredTargetWorldPos += pan;
                }

                if (allowZoom)
                {
                    var scroll = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.0001f)
                    {
                        desiredDistance -= scroll * 10f * Mathf.Max(0.02f, desiredDistance * 0.02f);
                        desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
                    }
                }
            }
    #endif
#endif

            // compute desired camera transform
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredPosition = desiredTargetWorldPos + rot * (Vector3.back * desiredDistance);

            if (useSmoothing)
            {
                transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref smoothPosVelocity, smoothTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-30f * Time.deltaTime));
            }
            else
            {
                transform.position = desiredPosition;
                transform.rotation = rot;
            }

            // keep internal tracked target position aligned with real target + offset if the target moves externally
            desiredTargetWorldPos = Vector3.Lerp(desiredTargetWorldPos, target.position + targetOffset, 1f); // immediate but keeps consistent with target movement

            // update prevTouchCount for next frame
            prevTouchCount = currentTouchCount;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"MobileOrbitCamera LateUpdate exception: {ex}\n{ex.StackTrace}");
        }

        // update debug overlay (non-essential)
        if (showDebugOverlay && debugText != null)
        {
            string backend = "Unknown";
#if ENABLE_LEGACY_INPUT_MANAGER
            backend = "Legacy Input Manager";
#endif
#if ENABLE_INPUT_SYSTEM
            backend = backend == "Unknown" ? "New Input System" : backend + " + New Input System";
#endif
            int touchCount = 0;
#if ENABLE_LEGACY_INPUT_MANAGER
            touchCount = Input.touchCount;
#elif ENABLE_INPUT_SYSTEM
            touchCount = GetActiveEnhancedTouchesSafe().Count;
#endif
            debugText.text = $"Backend:{backend}\nTouches:{touchCount} prev:{prevTouchCount}\nactiveFinger:{activeSingleFingerId}\nlastAppliedH:{lastAppliedHorizontal:F2}\nYaw:{yaw:F1} Pitch:{pitch:F1} Dist:{desiredDistance:F2}";
        }
    }

    // Public helper to reset touch state when external code switches algorithms or teleports the target.
    // Call this after you change algorithms or modify the camera/target transform from other scripts.
    public void ResetTouchState()
    {
        prevTouchCount = 0;
        lastSingleTouchValid = false;
        lastSingleTouchPos = Vector2.zero;
        activeSingleFingerId = -1;
        lastAppliedHorizontal = 0f;
    }

    // Optional: let other code set the camera's orbit target cleanly
    public void SetTarget(Transform newTarget)
    {
        if (newTarget == null) return;
        target = newTarget;
        desiredTargetWorldPos = target.position + targetOffset;
        ResetTouchState();
    }
}