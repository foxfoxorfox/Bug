using System.Collections.Generic;
using UnityEngine; 
 
public class Bug1 : MonoBehaviour 
{ 
    [Header("Movement")] 
    public Transform goal; 
    public float speed = 5f; 
 
    [Header("Wall Following")] 
    public float wallDistance = 0.25f; 
    public float rayDistance = 1.0f; 
    public float wallFollowSpeed = 5f; 
    // ========================================
    private enum State 
    {
        Stop,
        MoveToGoal, 
        FollowWall, 
        ToWallBestPoint 
    } 
 
    private State state = State.MoveToGoal; 
    // ========================================
    private readonly List<Collider> obstacles = new List<Collider>();
    private Vector3 ContactPoint;//충돌 위치
    private Vector3 bestPoint;//이탈 지점
    private float bestDistance; 
 
    private float wallTravelDistance;//충돌 지점 도착 판정
    private Vector3 initialTangent;//충돌 지점에서 시계방향
    private float returnDistance = 0.25f;//충돌 지점까지 거리 조건

    void Start() 
    { 
         
    }  
    void Update() 
    { 
        if (goal == null) 
            return; 
 
        Vector3 pos = transform.position;
        pos.y = 0f;
        transform.position = pos;
 
        switch (state) 
        { 
            case State.Stop:
                break;
            case State.MoveToGoal: 
                MoveToGoal(); 
                break; 
            case State.FollowWall: 
                FollowWall(); 
                break;
            case State.ToWallBestPoint: 
                ToWallBestPoint(); 
                break;
        } 
    } 
 
    void MoveToGoal() 
    { 
        Vector3 direction = goal.position - transform.position; 
 
        direction.y = 0f; 
 
        if (direction.sqrMagnitude < 0.01f) 
            return; 
 
        direction.Normalize();
 
        transform.position += direction * speed * Time.deltaTime; 
 
        Vector3 pos = transform.position;
        pos.y = 0f;
        transform.position = pos;
    }
 
    void FollowWall() 
    { 
        if (!HasObstacles()) 
        { 
            state = State.MoveToGoal; 
            return; 
        } 
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
        if (normal.sqrMagnitude < 0.0001f){normal = Vector3.right;} 
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

        //이탈지점 갱신
        float distanceToGoal = DistanceXZ(transform.position, goal.position); 
        if (distanceToGoal < bestDistance) 
        { 
            bestDistance = distanceToGoal; 
            bestPoint = transform.position; 
        } 

        //충돌점 도달 판정
        wallTravelDistance += movement.magnitude;
        if (wallTravelDistance > 1f) 
        {
            float distanceFromStart = DistanceXZ(transform.position, ContactPoint);
            bool hasReturnedToStart = distanceFromStart < returnDistance;
            bool hasSameTravelDirection = Vector3.Dot(clockwise, initialTangent) > 0f;
            if (hasReturnedToStart && hasSameTravelDirection) 
            {
                state = State.ToWallBestPoint;
            } 
        } 
    } 
    //이탈 지점으로
    void ToWallBestPoint() 
    { 
        if (!HasObstacles()) 
        { 
            state = State.MoveToGoal; 
            return; 
        }
        // 현재 위치
        Vector3 currentPosition = transform.position;
        currentPosition.y = 0f;
        // 다음 예상 위치
        Vector3 closestPoint = ClosestObstaclePoint(currentPosition);
        closestPoint.y = 0f;
        // 다음 에상 위치로의 벡터
        Vector3 normal = currentPosition - closestPoint;
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.0001f){normal = Vector3.right;}
        normal.Normalize();
        // 반시계방향
        Vector3 anitclockwise = new Vector3(-normal.z, 0f, normal.x).normalized;
        transform.position += anitclockwise * wallFollowSpeed * Time.deltaTime;//이동
        //거리 유지
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
        transform.position = newPosition;

        if (DistanceXZ(transform.position, bestPoint) < 0.05f)
        {
            state = State.MoveToGoal;
            obstacles.Clear();
        }
    } 

    //충돌
    private void OnCollisionEnter(Collision collision) 
    { 
        if (state != State.MoveToGoal || collision == null || collision.collider == null) 
            return; 
 
        obstacles.Clear();
        Transform obstacleRoot = collision.collider.transform.parent != null
            ? collision.collider.transform.parent
            : collision.collider.transform;
        obstacles.AddRange(obstacleRoot.GetComponentsInChildren<Collider>());
 
        if (goal == null || !HasObstacles()) 
            return; 
 
        Vector3 contactPoint = 
            collision.GetContact(0).point; 
 
        Vector3 contactNormal = 
            collision.GetContact(0).normal; 
 
        contactPoint.y = 0f; 
        contactNormal.y = 0f; 
 
        ContactPoint = contactPoint; 
 
        if (contactNormal.sqrMagnitude < 0.0001f) 
            contactNormal = transform.position - contactPoint; 
 
        if (contactNormal.sqrMagnitude < 0.0001f) 
            contactNormal = Vector3.right; 
 
        contactNormal.Normalize();

        // 충돌 초기값
        initialTangent = new Vector3(contactNormal.z, 0f, -contactNormal.x).normalized;
        bestPoint = contactPoint; 
        bestDistance = DistanceXZ(contactPoint, goal.position); 
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
 
    float DistanceXZ(Vector3 a, Vector3 b) 
    { 
        a.y = 0f; 
        b.y = 0f; 
        return Vector3.Distance(a, b); 
    } 
}