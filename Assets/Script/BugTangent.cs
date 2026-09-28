using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BugTangent : MonoBehaviour
{
    private enum State
    {
        Stop,
        Move,
        FollowWall
    }

    private enum RayKind
    {
        Clear,
        Hit,
        Singularity
    }

    private const int RayCount = 360;
    private const float RayAngleStep = 1f;
    private const float RayDistance = 4f;
    private const float DistanceEpsilon = 0.0001f;
    private const float GoalReachedDistance = 0.1f;

    [Header("Move")]
    public Transform goal;
    public float speed = 5f;
    public float targetSwitchMargin = 0.3f;

    [Header("Follow Wall")]
    public float wallClearance = 0.25f;

    [Header("Ray Sensor")]
    public LayerMask obstacleMask = ~0;
    public bool ignoreTriggers = true;

    [Header("Ray Visualization")]
    public bool drawRays = true;
    public float rayWidth = 0.015f;
    public Color clearRayColor = Color.green;
    public Color hitRayColor = Color.red;
    public Color singularityRayColor = Color.blue;
    public Color selectedRayColor = Color.yellow;
    public string raySortingLayer = "Default";
    public int raySortingOrder = 100;

    private readonly RaycastHit[] rayHits = new RaycastHit[RayCount];
    private readonly bool[] rayHitStates = new bool[RayCount];
    private readonly RayKind[] rayKinds = new RayKind[RayCount];
    private readonly Vector3[] rayPoints = new Vector3[RayCount];
    private LineRenderer[] rayRenderers;
    private Material rayMaterial;
    private State state = State.Stop;
    private State previousState = State.Move;
    private int selectedRayIndex = -1;

    private Vector3 followWallClosestPoint;
    private int followWallClosestRayIndex = -1;

    private Vector3 followWallApproachDirection;
    private bool followWallHasContacted;
    private Vector3 followWallEdgeDirection = Vector3.right;
    private readonly List<Collider> followedObstacles = new List<Collider>();

    // 리지드바디 회전을 고정하고 ray 시각화를 초기화한다
    private void Awake()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
            body.freezeRotation = true;

        CreateRuntimeRayVisuals();
        ScanRays();
        VisualizeRays();
    }

    // 매 프레임 상태에 맞는 동작을 실행한다
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (state == State.Stop)
                state = previousState;
            else
            {
                previousState = state;
                state = State.Stop;
            }
        }

        ScanRays();

        if (goal != null && DistanceXZ(transform.position, goal.position) <= GoalReachedDistance)
            state = State.Stop;

        if (state == State.Move){
            print("Move");
            UpdateMove();
        }
        else if (state == State.FollowWall){
            print("FallowWall");
            UpdateFollowWall();
        }

        VisualizeRays();
    }

    // 상태를 초기의 Stop 상태로 되돌린다
    public void ResetToStop()
    {
        state = State.Stop;
        previousState = State.Move;
        selectedRayIndex = -1;
        followWallHasContacted = false;
        followedObstacles.Clear();
    }

    // goal을 향해 이동하고, 거리가 오히려 멀어지면 FollowWall로 전환한다
    private void UpdateMove()
    {
        if (goal == null)
        {
            selectedRayIndex = -1;
            return;
        }

        int bestIndex = SelectMoveTargetRayIndex();
        selectedRayIndex = ResolveMoveDirection(selectedRayIndex, bestIndex);

        if (selectedRayIndex < 0)
            return;

        Vector3 goalPosition = Flatten(goal.position);
        float distanceBeforeMove = DistanceXZ(transform.position, goalPosition);

        MoveTowardSelectedRay();

        float distanceAfterMove = DistanceXZ(transform.position, goalPosition);
        if (distanceAfterMove > distanceBeforeMove)
            BeginFollowWall();
    }

    // FollowWall 진입 시점의 기준값들을 저장한다
    private void BeginFollowWall()
    {
        followWallClosestPoint = FindClosestRayPointToGoal(out followWallClosestRayIndex);
        followWallApproachDirection = GetRayDirection(selectedRayIndex);
        followWallHasContacted = false;
        state = State.FollowWall;
    }

    // 이탈 조건을 확인하고, 아니면 접근 또는 가장자리 이동을 수행한다
    private void UpdateFollowWall()
    {
        if (TryFindLeaveCandidate(out int leaveIndex))
        {
            selectedRayIndex = leaveIndex;
            state = State.Move;
            return;
        }

        if (!followWallHasContacted)
            ApproachAlongEntryDirection();
        else
            MoveAlongObstacleEdge();
    }

    // Move로 복귀할 ray(더 가깝고 충돌 없이 이동 가능한 ray)를 찾는다
    private bool TryFindLeaveCandidate(out int leaveIndex)
    {
        leaveIndex = -1;
        if (goal == null)
            return false;

        Vector3 goalPosition = Flatten(goal.position);
        float bestDistance = DistanceXZ(followWallClosestPoint, goalPosition);

        for (int index = 0; index < RayCount; index++)
        {
            if (!IsMoveCandidate(index))
                continue;

            float distance = DistanceXZ(rayPoints[index], goalPosition);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            leaveIndex = index;
        }

        return leaveIndex >= 0;
    }

    // 접촉 전: 진입 방향으로 직진하며 ray로도 근접 접촉을 확인한다
    private void ApproachAlongEntryDirection()
    {
        int nearestHitIndex = FindNearestHitRayIndex();
        if (nearestHitIndex >= 0 && rayHits[nearestHitIndex].distance <= wallClearance)
        {
            RaycastHit hit = rayHits[nearestHitIndex];
            OnWallContact(hit.collider, hit.point, hit.normal);
            return;
        }

        Vector3 movement = followWallApproachDirection * Mathf.Max(0f, speed) * Time.deltaTime;
        transform.position = Flatten(transform.position) + movement;
    }

    // 가장 가까운 Hit ray를 찾는다
    private int FindNearestHitRayIndex()
    {
        int nearestIndex = -1;
        float nearestDistance = float.PositiveInfinity;

        for (int index = 0; index < RayCount; index++)
        {
            if (!rayHitStates[index])
                continue;
            if (rayHits[index].distance >= nearestDistance)
                continue;

            nearestDistance = rayHits[index].distance;
            nearestIndex = index;
        }

        return nearestIndex;
    }

    // 물리 충돌로 접촉을 확정한다
    private void OnCollisionEnter(Collision collision)
    {
        if (state != State.FollowWall || followWallHasContacted)
            return;
        if (collision == null || collision.collider == null || collision.contactCount == 0)
            return;

        ContactPoint contact = collision.GetContact(0);
        OnWallContact(collision.collider, contact.point, contact.normal);
    }

    // 접촉 방향(시계/반시계)을 정하고 추적할 오브젝트를 등록한다
    private void OnWallContact(Collider hitCollider, Vector3 contactPoint, Vector3 contactNormal)
    {
        followWallHasContacted = true;

        followedObstacles.Clear();
        if (hitCollider != null)
        {
            Transform parent = hitCollider.transform.parent;
            if (parent != null)
            {
                foreach (Transform sibling in parent)
                {
                    if (sibling == transform || sibling.IsChildOf(transform))
                        continue;

                    Collider candidate = sibling.GetComponent<Collider>();
                    if (candidate == null)
                        continue;
                    if (ignoreTriggers && candidate.isTrigger)
                        continue;

                    followedObstacles.Add(candidate);
                }
            }
            else if (!(ignoreTriggers && hitCollider.isTrigger))
            {
                followedObstacles.Add(hitCollider);
            }
        }

        Vector3 normal = Flatten(contactNormal);
        if (normal.sqrMagnitude <= DistanceEpsilon)
            normal = Flatten(transform.position) - Flatten(contactPoint);
        if (normal.sqrMagnitude <= DistanceEpsilon)
            normal = Vector3.right;
        normal.Normalize();

        Vector3 clockwiseTangent = new Vector3(normal.z, 0f, -normal.x);
        followWallEdgeDirection = Vector3.Dot(clockwiseTangent, followWallApproachDirection) >= 0f
            ? clockwiseTangent
            : -clockwiseTangent;
    }

    // 접촉한 오브젝트의 가장자리를 따라 위치를 이동시킨다
    private void MoveAlongObstacleEdge()
    {
        if (!HasFollowedObstacles())
            return;

        Vector3 position = Flatten(transform.position);
        Vector3 closestPoint = GetClosestFollowedSurfacePoint(position);
        Vector3 outward = position - closestPoint;
        if (outward.sqrMagnitude <= DistanceEpsilon)
            outward = -followWallEdgeDirection;
        outward.Normalize();

        Vector3 tangent = new Vector3(outward.z, 0f, -outward.x);
        if (Vector3.Dot(tangent, followWallEdgeDirection) < 0f)
            tangent = -tangent;

        followWallEdgeDirection = tangent;
        Vector3 movement = tangent * Mathf.Max(0f, speed) * Time.deltaTime;
        transform.position = Flatten(transform.position + movement);

        MaintainWallDistance();
    }

    // 표면과의 거리를 wallClearance로 즉시 되돌린다
    private void MaintainWallDistance()
    {
        Vector3 position = Flatten(transform.position);
        Vector3 closestPoint = GetClosestFollowedSurfacePoint(position);
        Vector3 outward = position - closestPoint;
        if (outward.sqrMagnitude <= DistanceEpsilon)
            return;

        transform.position = closestPoint + outward.normalized * wallClearance;
    }

    // 추적 중인 오브젝트가 있는지 확인한다
    private bool HasFollowedObstacles()
    {
        followedObstacles.RemoveAll(collider => collider == null || !collider.enabled);
        return followedObstacles.Count > 0;
    }

    // 추적 중인 오브젝트들 중 가장 가까운 표면 점을 찾는다
    private Vector3 GetClosestFollowedSurfacePoint(Vector3 position)
    {
        Vector3 closestPoint = position;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (Collider obstacle in followedObstacles)
        {
            if (obstacle == null || !obstacle.enabled)
                continue;

            Vector3 candidate = Flatten(obstacle.ClosestPoint(position));
            float candidateSqrDistance = (candidate - position).sqrMagnitude;
            if (candidateSqrDistance < closestSqrDistance)
            {
                closestPoint = candidate;
                closestSqrDistance = candidateSqrDistance;
            }
        }

        return closestPoint;
    }

    // goal에 가장 가까운 ray 탐지점을 찾는다
    private Vector3 FindClosestRayPointToGoal(out int closestIndex)
    {
        Vector3 goalPosition = Flatten(goal.position);
        closestIndex = -1;
        float closestDistance = float.PositiveInfinity;

        for (int index = 0; index < RayCount; index++)
        {
            float distance = DistanceXZ(rayPoints[index], goalPosition);
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closestIndex = index;
        }

        return closestIndex >= 0 ? rayPoints[closestIndex] : Flatten(transform.position);
    }

    // ray 시각화용 오브젝트를 생성한다
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

    // LineRenderer의 기본 속성을 설정한다
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

    // ray를 쏘고 종류(Clear/Hit/Singularity)를 분류한다
    private void ScanRays()
    {
        Vector3 origin = transform.position;
        origin.y = 0f;

        for (int index = 0; index < RayCount; index++)
        {
            Vector3 direction = GetRayDirection(index);
            rayHitStates[index] = TryRaycast(origin, direction, out rayHits[index]);
            rayPoints[index] = rayHitStates[index]
                ? rayHits[index].point
                : origin + direction * RayDistance;
        }

        for (int index = 0; index < RayCount; index++)
        {
            if (!rayHitStates[index])
            {
                rayKinds[index] = RayKind.Clear;
                continue;
            }

            int previous = (index + RayCount - 1) % RayCount;
            int next = (index + 1) % RayCount;
            rayKinds[index] = rayHitStates[previous] && rayHitStates[next]
                ? RayKind.Hit
                : RayKind.Singularity;
        }
    }

    // goal에 가장 가까운 이동 목표 ray를 고른다
    private int SelectMoveTargetRayIndex()
    {
        if (goal == null)
            return -1;

        Vector3 goalPosition = Flatten(goal.position);
        int bestIndex = -1;
        RayKind bestKind = RayKind.Singularity;
        float bestDistance = float.PositiveInfinity;

        for (int index = 0; index < RayCount; index++)
        {
            RayKind kind = rayKinds[index];
            if (kind != RayKind.Clear && kind != RayKind.Singularity)
                continue;

            float distance = DistanceXZ(rayPoints[index], goalPosition);
            if (!IsBetterMoveTarget(distance, kind, bestDistance, bestKind))
                continue;

            bestDistance = distance;
            bestKind = kind;
            bestIndex = index;
        }

        return bestIndex;
    }

    // 더 나은 이동 목표인지 비교한다
    private bool IsBetterMoveTarget(float distance, RayKind kind, float bestDistance, RayKind bestKind)
    {
        if (distance < bestDistance - DistanceEpsilon)
            return true;
        if (distance > bestDistance + DistanceEpsilon)
            return false;

        return kind == RayKind.Clear && bestKind == RayKind.Singularity;
    }

    // 마진을 넘어설 때만 이동 목표 ray를 갈아탄다
    private int ResolveMoveDirection(int currentIndex, int bestIndex)
    {
        bool currentIsValid = IsMoveCandidate(currentIndex);

        if (bestIndex < 0)
            return currentIsValid ? currentIndex : -1;

        if (!currentIsValid)
            return bestIndex;

        Vector3 goalPosition = Flatten(goal.position);
        float currentDistance = DistanceXZ(rayPoints[currentIndex], goalPosition);
        float bestDistance = DistanceXZ(rayPoints[bestIndex], goalPosition);

        return bestDistance < currentDistance - targetSwitchMargin ? bestIndex : currentIndex;
    }

    // 이동 목표로 삼을 수 있는 ray(Clear/Singularity)인지 확인한다
    private bool IsMoveCandidate(int index)
    {
        return index >= 0 && (rayKinds[index] == RayKind.Clear || rayKinds[index] == RayKind.Singularity);
    }

    // 선택된 ray의 탐지점 방향으로 이동한다
    private void MoveTowardSelectedRay()
    {
        if (selectedRayIndex < 0)
            return;

        Vector3 position = Flatten(transform.position);
        Vector3 target = rayPoints[selectedRayIndex];
        transform.position = Vector3.MoveTowards(
            position,
            target,
            Mathf.Max(0f, speed) * Time.deltaTime);
    }

    // 인덱스에 해당하는 ray 방향을 계산한다
    private Vector3 GetRayDirection(int index)
    {
        return Quaternion.AngleAxis(index * RayAngleStep, Vector3.up) * Vector3.forward;
    }

    // 가장 가까운 장애물까지 raycast한다
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

    // y를 0으로 고정한다
    private Vector3 Flatten(Vector3 position)
    {
        position.y = 0f;
        return position;
    }

    // xz 평면 기준 거리를 계산한다
    private float DistanceXZ(Vector3 first, Vector3 second)
    {
        return Vector3.Distance(Flatten(first), Flatten(second));
    }

    // ray 시각화를 갱신한다
    private void VisualizeRays()
    {
        Vector3 origin = transform.position;
        origin.y = 0f;

        for (int index = 0; index < RayCount; index++)
            UpdateRuntimeRay(rayRenderers[index], origin, rayPoints[index], GetRayColor(index));
    }

    // ray 종류/선택 여부에 따른 색상을 결정한다
    private Color GetRayColor(int index)
    {
        if (index == selectedRayIndex)
            return selectedRayColor;

        switch (rayKinds[index])
        {
            case RayKind.Hit:
                return hitRayColor;
            case RayKind.Singularity:
                return singularityRayColor;
            default:
                return clearRayColor;
        }
    }

    // LineRenderer의 위치와 색상을 갱신한다
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

    // 씬 뷰에 ray를 그린다
    private void OnDrawGizmos()
    {
        if (!drawRays)
            return;

        Vector3 origin = transform.position;
        origin.y = 0f;

        for (int index = 0; index < RayCount; index++)
        {
            Vector3 end = rayHitStates[index]
                ? rayHits[index].point
                : origin + GetRayDirection(index) * RayDistance;
            Gizmos.color = index == selectedRayIndex ? selectedRayColor : GetRayColor(index);
            Gizmos.DrawLine(origin, end);
        }
    }

    // 생성한 머티리얼을 정리한다
    private void OnDestroy()
    {
        if (rayMaterial != null)
            Destroy(rayMaterial);
    }
}
