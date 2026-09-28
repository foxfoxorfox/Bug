using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Bug2 : MonoBehaviour
{
    [Header("Movement")]
    public Transform goal;
    public float speed = 5f;
    public float goalReachedDistance = 0.1f;

    [Header("Wall Following")]
    public float wallDistance = 0.25f;
    public float wallFollowSpeed = 5f;

    [Header("Bug2")]
    public float mLineTolerance = 0.08f;
    public float minimumWallTravel = 1f;
    public float entryPointTolerance = 0.1f;

    private enum State
    {
        MoveToGoal,
        FollowWall,
        Stop
    }

    private readonly List<Collider> obstacles = new List<Collider>();
    private State state = State.Stop;
    private State stateBeforeStop = State.MoveToGoal;
    private Vector3 startPoint;
    private Vector3 mLineDirection;
    private float hitDistanceToGoal;
    private float wallTravelDistance;
    private Vector3 entryPoint;
    private Rigidbody rigidBody;

    private void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        startPoint = Flatten(transform.position);
        UpdateMLineDirection();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            ToggleMovementState();

        if (goal == null)
            return;

        transform.position = Flatten(transform.position);

        if (DistanceXZ(transform.position, goal.position) <= goalReachedDistance)
            EnterStop();

        switch (state)
        {
            case State.MoveToGoal:
                MoveToGoal();
                break;
            case State.FollowWall:
                FollowWall();
                break;
            case State.Stop:
                break;
        }
    }

    private void ToggleMovementState()
    {
        if (state == State.Stop)
        {
            state = stateBeforeStop;
            return;
        }
        stateBeforeStop = state;
        EnterStop();
    }

    // Stop으로 전환하면서 Rigidbody에 남은 속도를 지운다.
    // 안 지우면 물리 충돌로 쌓인 속도 때문에 스크립트가 멈춘 뒤에도 조금씩 계속 움직인다.
    private void EnterStop()
    {
        state = State.Stop;
        if (rigidBody == null)
            return;

        rigidBody.linearVelocity = Vector3.zero;
        rigidBody.angularVelocity = Vector3.zero;
    }

    public void ResetToStop()
    {
        EnterStop();
        stateBeforeStop = State.MoveToGoal;
        obstacles.Clear();
        wallTravelDistance = 0f;
        startPoint = Flatten(transform.position);
        UpdateMLineDirection();
    }

    // 현재 위치에서 goal을 향해 이동한다. m_line에서 벗어난 지점에서 출발해도
    // goal로 정확히 수렴하도록, 고정된 m_line 방향이 아니라 매 프레임 방향을 다시 계산한다.
    private void MoveToGoal()
    {
        Vector3 currentPosition = Flatten(transform.position);
        Vector3 direction = Flatten(goal.position) - currentPosition;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.position = currentPosition + direction.normalized * speed * Time.deltaTime;
    }

    // 오브젝트의 외벽을 따라 이동하는 부분만 남긴 것
    void FollowWall()
    {
        //현재 위치
        Vector3 currentPosition = transform.position;
        currentPosition.y = 0f;
        //다음 예상 위치
        Vector3 closestPoint = ClosestObstaclePoint(currentPosition);
        closestPoint.y = 0f;
        //다음 위치로 향하는 벡터
        Vector3 normal = currentPosition - closestPoint;
        normal.y = 0f;
        // 너무 가까우면 임시 방향
        if (normal.sqrMagnitude < 0.0001f) { normal = Vector3.right; }
        normal.Normalize();
        //시계방향
        Vector3 clockwise = new Vector3(normal.z, 0f, -normal.x).normalized;
        //다음 위치
        Vector3 movement = clockwise * wallFollowSpeed * Time.deltaTime;
        transform.position += movement;//이동
        // ========================================
        // 장애물에서 일정한 거리 유지
        Vector3 newPosition = transform.position;
        newPosition.y = 0f;
        Vector3 newClosest = ClosestObstaclePoint(newPosition);
        newClosest.y = 0f;
        Vector3 outward = newPosition - newClosest;
        outward.y = 0f;
        if (outward.sqrMagnitude > 0.0001f)
        {
            outward.Normalize();
            newPosition = newClosest + outward * wallDistance;
        }
        newPosition.y = 0f;
        transform.position = newPosition;//거리 유지

        // 최소 거리를 벽을 따라 이동하기 전에는 이탈 판정을 하지 않는다.
        // 이게 없으면 충돌 직후 한 스텝만에 이탈 조건이 걸려 MoveToGoal과 FollowWall을
        // 매 프레임 오가며 오브젝트에 계속 부딪히다가 결국 뚫고 지나가버린다.
        wallTravelDistance += movement.magnitude;
        if (wallTravelDistance < minimumWallTravel)
            return;

        Vector3 currentPos = Flatten(transform.position);

        // 이탈하기 전에 진입 지점을 다시 만나면(한 바퀴 돌아 제자리로 돌아오면) Stop으로 전환한다.
        if (DistanceXZ(currentPos, entryPoint) <= entryPointTolerance)
        {
            EnterStop();
            return;
        }

        // 오브젝트 겉을 따라 이동 중 m_line을 만났을 때, 그 지점이 충돌 지점보다 goal에
        // 더 가까우면 이탈지점으로 삼고 MoveToGoal로 전환한다.
        if (IsOnMLine(currentPos) && DistanceXZ(currentPos, goal.position) < hitDistanceToGoal)
            state = State.MoveToGoal;
    }

    // m_line은 시작점에서 goal까지의 선분이다. 그 범위를 벗어난 연장선은 포함하지 않는다.
    private bool IsOnMLine(Vector3 point)
    {
        Vector3 fromStart = point - startPoint;
        float lineDistance = Vector3.Cross(mLineDirection, fromStart).magnitude;
        float progress = Vector3.Dot(fromStart, mLineDirection);
        float segmentLength = DistanceXZ(startPoint, goal.position);
        return lineDistance <= mLineTolerance && progress >= 0f && progress <= segmentLength;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (state != State.MoveToGoal || collision == null || collision.collider == null)
            return;

        obstacles.Clear();
        Transform obstacleRoot = collision.collider.transform.parent != null
            ? collision.collider.transform.parent
            : collision.collider.transform;
        obstacles.AddRange(obstacleRoot.GetComponentsInChildren<Collider>());

        if (!HasObstacles())
            return;

        if (goal != null)
            hitDistanceToGoal = DistanceXZ(transform.position, goal.position);

        entryPoint = Flatten(transform.position);
        wallTravelDistance = 0f;
        state = State.FollowWall;
    }

    private bool HasObstacles()
    {
        obstacles.RemoveAll(collider => collider == null || !collider.enabled);
        return obstacles.Count > 0;
    }

    private Vector3 ClosestObstaclePoint(Vector3 position)
    {
        Vector3 closestPoint = obstacles[0].ClosestPoint(position);
        float closestSqrDistance = (closestPoint - position).sqrMagnitude;

        for (int index = 1; index < obstacles.Count; index++)
        {
            Vector3 candidate = obstacles[index].ClosestPoint(position);
            float candidateSqrDistance = (candidate - position).sqrMagnitude;
            if (candidateSqrDistance < closestSqrDistance)
            {
                closestPoint = candidate;
                closestSqrDistance = candidateSqrDistance;
            }
        }

        return closestPoint;
    }

    private void UpdateMLineDirection()
    {
        if (goal == null)
            return;

        Vector3 direction = Flatten(goal.position) - startPoint;
        if (direction.sqrMagnitude > 0.0001f)
            mLineDirection = direction.normalized;
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
}
