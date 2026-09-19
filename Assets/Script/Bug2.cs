using System.Collections.Generic;
using UnityEngine;

public class Bug2 : MonoBehaviour
{
    [Header("Movement")]
    public Transform goal;
    public float speed = 5f;

    [Header("Wall Following")]
    public float wallDistance = 0.25f;
    public float wallFollowSpeed = 5f;

    [Header("Bug2")]
    public float mLineTolerance = 0.08f;
    public float minimumWallTravel = 0.25f;

    private enum State
    {
        MoveToGoal,
        FollowWall,
        Stop
    }

    private readonly List<Collider> obstacles = new List<Collider>();
    private State state = State.MoveToGoal;
    private Vector3 startPoint;
    private Vector3 mLineDirection;
    private Vector3 contactPoint;
    private float hitDistanceToGoal;
    private float wallTravelDistance;
    private Vector3 wallFollowDirection;

    private void Start()
    {
        startPoint = Flatten(transform.position);
        UpdateMLineDirection();
    }

    private void Update()
    {
        if (goal == null)
            return;

        transform.position = Flatten(transform.position);

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

    private void MoveToGoal()
    {
        Vector3 direction = Flatten(goal.position) - Flatten(transform.position);
        if (direction.sqrMagnitude < 0.01f)
        {
            state = State.Stop;
            return;
        }

        transform.position += direction.normalized * speed * Time.deltaTime;
        transform.position = Flatten(transform.position);
    }

    private void FollowWall()
    {
        if (!HasObstacles())
        {
            state = State.MoveToGoal;
            return;
        }

        Vector3 currentPosition = Flatten(transform.position);
        Vector3 closestPoint = Flatten(ClosestObstaclePoint(currentPosition));
        Vector3 outward = currentPosition - closestPoint;
        if (outward.sqrMagnitude < 0.0001f)
            outward = -wallFollowDirection;

        outward.Normalize();
        Vector3 tangent = new Vector3(outward.z, 0f, -outward.x).normalized;
        if (Vector3.Dot(tangent, wallFollowDirection) < 0f)
            tangent = -tangent;

        wallFollowDirection = tangent;
        Vector3 movement = tangent * wallFollowSpeed * Time.deltaTime;
        transform.position = Flatten(transform.position + movement);
        wallTravelDistance += movement.magnitude;

        MaintainWallDistance();

        if (wallTravelDistance >= minimumWallTravel && IsValidMLineHit())
        {
            state = State.MoveToGoal;
            obstacles.Clear();
        }
    }

    private void MaintainWallDistance()
    {
        Vector3 position = Flatten(transform.position);
        Vector3 closestPoint = Flatten(ClosestObstaclePoint(position));
        Vector3 outward = position - closestPoint;
        if (outward.sqrMagnitude < 0.0001f)
            return;

        transform.position = Flatten(closestPoint + outward.normalized * wallDistance);
    }

    private bool IsValidMLineHit()
    {
        Vector3 currentPosition = Flatten(transform.position);
        Vector3 fromStart = currentPosition - startPoint;
        float lineDistance = Vector3.Cross(mLineDirection, fromStart).magnitude;
        float progress = Vector3.Dot(fromStart, mLineDirection);
        bool isOnForwardMLine = lineDistance <= mLineTolerance && progress > 0f;
        bool isCloserToGoal = DistanceXZ(currentPosition, goal.position) < hitDistanceToGoal;
        return isOnForwardMLine && isCloserToGoal;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (state != State.MoveToGoal || collision == null || collision.collider == null || goal == null)
            return;

        obstacles.Clear();
        Transform obstacleRoot = collision.collider.transform.parent != null
            ? collision.collider.transform.parent
            : collision.collider.transform;
        obstacles.AddRange(obstacleRoot.GetComponentsInChildren<Collider>());

        if (!HasObstacles() || collision.contactCount == 0)
            return;

        ContactPoint contact = collision.GetContact(0);
        contactPoint = Flatten(contact.point);
        Vector3 normal = Flatten(contact.normal);
        if (normal.sqrMagnitude < 0.0001f)
            normal = Flatten(transform.position) - contactPoint;
        if (normal.sqrMagnitude < 0.0001f)
            normal = Vector3.right;

        UpdateMLineDirection();
        hitDistanceToGoal = DistanceXZ(contactPoint, goal.position);
        wallTravelDistance = 0f;

        normal.Normalize();
        wallFollowDirection = new Vector3(normal.z, 0f, -normal.x).normalized;
        state = State.FollowWall;
    }

    private void UpdateMLineDirection()
    {
        if (goal == null)
            return;

        Vector3 direction = Flatten(goal.position) - startPoint;
        if (direction.sqrMagnitude > 0.0001f)
            mLineDirection = direction.normalized;
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
