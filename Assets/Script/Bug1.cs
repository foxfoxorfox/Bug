using UnityEngine; 
 
public class Bug1 : MonoBehaviour 
{ 
    [Header("Movement")] 
    public Transform goal; 
    public float speed = 5f; 
 
    [Header("Wall Following")] 
    public float wallDistance = 0.2f; 
    public float rayDistance = 1.0f; 
    public float wallFollowSpeed = 5f; 
 
    private enum State 
    { 
        MoveToGoal, 
        FollowWall, 
        ToWallBestPoint 
    } 
 
    private State state = State.MoveToGoal; 
 
    private Collider obstacle;//충돌 대상
    private Vector3 ContactPoint;//충돌 위치
    private Vector3 bestPoint;//이탈 지점
 
    // 목표와 bestPoint 사이의 거리 
    private float bestDistance; 
 
    // 외벽을 얼마나 돌았는지 판단하기 위한 누적 거리 
    private float wallTravelDistance; 
 
    // 충돌 지점에서 외벽을 따라 이동하기 시작한 방향 
    private Vector3 initialTangent;
 
    // 처음 충돌한 위치로부터 얼마나 가까워졌는지 
    private float returnDistance = 0.3f; 

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
        if (obstacle == null) 
        { 
            state = State.MoveToGoal; 
            return; 
        } 
 
        Vector3 currentPosition = transform.position; 
        currentPosition.y = 0f; 
 
        // -------------------------------------------------------- 
        // 현재 위치에서 장애물의 가장 가까운 표면을 찾는다. 
        // -------------------------------------------------------- 
 
        Vector3 closestPoint =  obstacle.ClosestPoint(currentPosition); 
 
        closestPoint.y = 0f; 
 
 
        // -------------------------------------------------------- 
        // 장애물 표면에서 바깥쪽으로 향하는 방향 
        // -------------------------------------------------------- 
 
        Vector3 normal = currentPosition - closestPoint; 
 
        normal.y = 0f; 
 
        if (normal.sqrMagnitude < 0.0001f) 
        { 
            // 너무 가까우면 임시 방향 
            normal = Vector3.right; 
        } 
 
        normal.Normalize(); 
 
 
        // -------------------------------------------------------- 
        // 외벽을 따라가는 방향 
        // 
        // Y축 기준으로 시계방향 
        // -------------------------------------------------------- 
 
        Vector3 tangent = new Vector3( 
            normal.z, 
            0f, 
            -normal.x 
        ); 
 
        tangent.Normalize(); 
 
 
        // -------------------------------------------------------- 
        // 이동 
        // -------------------------------------------------------- 
 
        Vector3 movement =  tangent * wallFollowSpeed * Time.deltaTime; 
 
        transform.position += movement; 
 
 
        // Y 고정 
        Vector3 newPosition = transform.position; 
        newPosition.y = 0f; 
 
 
        // -------------------------------------------------------- 
        // 장애물에서 일정한 거리 유지 
        // -------------------------------------------------------- 
 
        Vector3 newClosest = 
            obstacle.ClosestPoint(newPosition); 
 
        newClosest.y = 0f; 
 
        Vector3 outward = 
            newPosition - newClosest; 
 
        outward.y = 0f; 
 
        if (outward.sqrMagnitude > 0.0001f) 
        { 
            outward.Normalize(); 
 
            newPosition = 
                newClosest + outward * wallDistance; 
        } 
 
        newPosition.y = 0f; 
 
        transform.position = newPosition; 
 
 
        // -------------------------------------------------------- 
        // 이동 거리 누적 
        // -------------------------------------------------------- 
 
        wallTravelDistance += movement.magnitude; 
 
 
        // -------------------------------------------------------- 
        // 현재 위치가 목표에 얼마나 가까운지 검사 
        // -------------------------------------------------------- 
 
        float distanceToGoal = 
            DistanceXZ(transform.position, goal.position); 
 
        if (distanceToGoal < bestDistance) 
        { 
            bestDistance = distanceToGoal; 
            bestPoint = transform.position; 
        } 
 
 
        // -------------------------------------------------------- 
        // BUG1: 
        // 충분히 이동한 뒤 처음 충돌 지점 근처로 돌아오면 
        // 한 바퀴 돌았다고 판단 
        // -------------------------------------------------------- 
 
        if (wallTravelDistance > 1f) 
        { 
            float distanceFromStart = 
                DistanceXZ(transform.position, ContactPoint); 
 
            bool hasReturnedToStart = distanceFromStart < returnDistance; 
            bool hasSameTravelDirection = 
                Vector3.Dot(tangent, initialTangent) > 0f; 
 
            if (hasReturnedToStart && hasSameTravelDirection) 
            { 
                state = State.ToWallBestPoint; 
            } 
        } 
    } 
 
 
    // ============================================================ 
    // 장애물 외벽에서 찾은 최적 지점으로 이동 
    // ============================================================ 
 
    void ToWallBestPoint() 
    { 
        Vector3 direction = bestPoint - transform.position;  
        direction.y = 0f; 
 
        float distance = direction.magnitude; 
 
        if (distance < 0.05f) 
        { 
            state = State.MoveToGoal; 
            obstacle = null; 
            return; 
        } 
 
        direction.Normalize(); 
 
        transform.position += 
            direction * speed * Time.deltaTime; 
 
        Vector3 pos = transform.position; 
        pos.y = 0f; 
 
        transform.position = pos; 
    } 
    //충돌
    private void OnCollisionEnter(Collision collision) 
    { 
        if (state != State.MoveToGoal) 
            return; 
 
        obstacle = collision.collider; 
 
        if (goal == null || obstacle == null) 
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
        initialTangent = new Vector3( 
            contactNormal.z, 
            0f, 
            -contactNormal.x 
        ).normalized; 
 
        bestPoint = contactPoint; 
 
        bestDistance = 
            DistanceXZ(contactPoint, goal.position); 
 
        wallTravelDistance = 0f; 
 
        state = State.FollowWall; 
    } 
 
 
    float DistanceXZ(Vector3 a, Vector3 b) 
    { 
        a.y = 0f; 
        b.y = 0f; 
 
        return Vector3.Distance(a, b); 
    } 
}