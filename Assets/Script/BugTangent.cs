using UnityEngine;

public class BugTangent : MonoBehaviour
{
    private const int RayCount = 90;
    private const float RayAngleStep = 4f;
    private const float RayDistance = 4f;

    [Header("Ray Sensor")]
    public Transform goal;
    public float speed = 5f;
    public LayerMask obstacleMask = ~0;
    public bool ignoreTriggers = true;

    [Header("Overlap Separation")]
    public float separationPadding = 0.01f;

    [Header("Ray Visualization")]
    public bool drawRays = true;
    public float rayWidth = 0.015f;
    public Color clearRayColor = Color.green;
    public Color hitRayColor = Color.red;
    public string raySortingLayer = "Default";
    public int raySortingOrder = 100;

    private readonly RaycastHit[] rayHits = new RaycastHit[RayCount];
    private readonly bool[] rayHitStates = new bool[RayCount];
    private readonly System.Collections.Generic.List<Vector3> singularities =
        new System.Collections.Generic.List<Vector3>();
    private LineRenderer[] rayRenderers;
    private Material rayMaterial;
    private Vector3 movementTarget;

    private void Awake()
    {
        CreateRuntimeRayVisuals();
    }

    private void Update()
    {
        ScanAndVisualizeRays();
        MoveUsingShortestRoute();
        SeparateFromOverlappingObjects();
    }
    // 화면에 ray 생성
    private void CreateRuntimeRayVisuals()
    {
        rayMaterial = new Material(Shader.Find("Sprites/Default"));
        rayRenderers = new LineRenderer[RayCount];

        for (int index = 0; index < RayCount; index++)
        {
            GameObject rayObject = new GameObject("Ray_" + index);
            rayObject.transform.SetParent(transform, false);
            rayRenderers[index] = CreateLineRenderer(rayObject.AddComponent<LineRenderer>());
        }
    }
    // ray 시각화
    private LineRenderer CreateLineRenderer(LineRenderer lineRenderer)
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.material = rayMaterial;
        lineRenderer.startWidth = rayWidth;
        lineRenderer.endWidth = rayWidth;
        lineRenderer.sortingLayerName = raySortingLayer;
        lineRenderer.sortingOrder = raySortingOrder;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        return lineRenderer;
    }
    // ray 탐지
    private void ScanAndVisualizeRays()
    {
        Vector3 origin = transform.position;
        origin.y = 0f;

        for (int index = 0; index < RayCount; index++)
        {
            Vector3 direction = Quaternion.AngleAxis(index * RayAngleStep, Vector3.up) * Vector3.forward;
            rayHitStates[index] = TryRaycast(origin, direction, out rayHits[index]);

            Vector3 end = rayHitStates[index]
                ? rayHits[index].point
                : origin + direction * RayDistance;
            Color color = rayHitStates[index] ? hitRayColor : clearRayColor;

            Debug.DrawRay(origin, end - origin, color);
            UpdateRuntimeRay(rayRenderers[index], origin, end, color);
        }

        FindSingularities();
    }

    private void FindSingularities()
    {
        singularities.Clear();

        for (int index = 0; index < RayCount; index++)
        {
            if (!rayHitStates[index])
                continue;

            int previous = (index + RayCount - 1) % RayCount;
            int next = (index + 1) % RayCount;
            if (rayHitStates[previous] && rayHitStates[next])
                continue;

            Vector3 point = rayHits[index].point;
            bool isDuplicate = false;
            foreach (Vector3 singularity in singularities)
            {
                if ((singularity - point).sqrMagnitude <= 0.04f)
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
                singularities.Add(point);
        }
    }

    private void MoveUsingShortestRoute()
    {
        if (goal == null)
            return;

        Vector3 position = Flatten(transform.position);
        Vector3 goalPosition = Flatten(goal.position);
        Vector3 directVector = goalPosition - position;
        if (directVector.sqrMagnitude <= 0.01f)
            return;

        movementTarget = goalPosition;
        bool directRouteBlocked = IsGoalDirectionBlocked(directVector);
        float shortestCost = directRouteBlocked ? float.PositiveInfinity : directVector.magnitude;

        foreach (Vector3 singularity in singularities)
        {
            float candidateCost = DistanceXZ(position, singularity) + DistanceXZ(singularity, goalPosition);
            if (candidateCost >= shortestCost)
                continue;

            shortestCost = candidateCost;
            movementTarget = singularity;
        }

        Vector3 direction = movementTarget - position;
        if (direction.sqrMagnitude <= 0.01f)
            return;

        transform.position = Flatten(Vector3.MoveTowards(
            position,
            movementTarget,
            Mathf.Max(0f, speed) * Time.deltaTime));
    }

    private bool IsGoalDirectionBlocked(Vector3 toGoal)
    {
        Vector3 goalDirection = toGoal.normalized;
        float closestAngle = float.PositiveInfinity;

        for (int index = 0; index < RayCount; index++)
        {
            if (!rayHitStates[index])
                continue;

            Vector3 rayDirection = Quaternion.AngleAxis(index * RayAngleStep, Vector3.up) * Vector3.forward;
            float angle = Vector3.Angle(rayDirection, goalDirection);
            if (angle <= RayAngleStep * 0.5f && angle < closestAngle && rayHits[index].distance <= toGoal.magnitude)
                closestAngle = angle;
        }

        return closestAngle < float.PositiveInfinity;
    }

    private void SeparateFromOverlappingObjects()
    {
        Collider[] ownColliders = GetComponentsInChildren<Collider>();
        if (ownColliders.Length == 0)
            return;

        Bounds bounds = ownColliders[0].bounds;
        foreach (Collider ownCollider in ownColliders)
            bounds.Encapsulate(ownCollider.bounds);

        Collider[] nearbyColliders = Physics.OverlapSphere(
            bounds.center,
            bounds.extents.magnitude + separationPadding,
            obstacleMask,
            ignoreTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);

        for (int iteration = 0; iteration < 3; iteration++)
        {
            bool separated = false;
            foreach (Collider ownCollider in ownColliders)
            {
                foreach (Collider otherCollider in nearbyColliders)
                {
                    if (otherCollider == null || otherCollider.transform == transform || otherCollider.transform.IsChildOf(transform))
                        continue;

                    if (Physics.ComputePenetration(
                        ownCollider,
                        ownCollider.transform.position,
                        ownCollider.transform.rotation,
                        otherCollider,
                        otherCollider.transform.position,
                        otherCollider.transform.rotation,
                        out Vector3 separationDirection,
                        out float separationDistance))
                    {
                        transform.position = Flatten(transform.position +
                            separationDirection * (separationDistance + separationPadding));
                        separated = true;
                    }
                }
            }

            if (!separated)
                break;
        }
    }
    private bool TryRaycast(Vector3 origin, Vector3 direction, out RaycastHit closestHit)
    {
        closestHit = default;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            RayDistance,
            obstacleMask,
            ignoreTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);

        float closestDistance = float.PositiveInfinity;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private Vector3 Flatten(Vector3 position)
    {
        position.y = 0f;
        return position;
    }

    private float DistanceXZ(Vector3 first, Vector3 second)
    {
        return Vector3.Distance(Flatten(first), Flatten(second));
    }
    // ray 갱신
    private void UpdateRuntimeRay(LineRenderer lineRenderer, Vector3 start, Vector3 end, Color color)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.enabled = drawRays;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = rayWidth;
        lineRenderer.endWidth = rayWidth;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }
    // 유니티 씬에서 ray 표시
    private void OnDrawGizmos()
    {
        if (!drawRays)
            return;

        Vector3 origin = transform.position;
        origin.y = 0f;

        for (int index = 0; index < RayCount; index++)
        {
            Vector3 direction = Quaternion.AngleAxis(index * RayAngleStep, Vector3.up) * Vector3.forward;
            Vector3 end = rayHitStates[index]
                ? rayHits[index].point
                : origin + direction * RayDistance;
            Gizmos.color = rayHitStates[index] ? hitRayColor : clearRayColor;
            Gizmos.DrawLine(origin, end);
        }
    }
    // bug 삭제 -> ray 삭제
    private void OnDestroy()
    {
        if (rayMaterial != null)
            Destroy(rayMaterial);
    }
}
