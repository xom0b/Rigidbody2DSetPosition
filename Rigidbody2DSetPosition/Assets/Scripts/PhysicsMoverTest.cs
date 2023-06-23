using UnityEngine;

/// <summary>
/// See: https://dicegame.substack.com/p/rigidbody2d-set-position for a write up of the findings implemented here. 
/// 
/// This physics mover test allows you to try different ways of "Setting a Dyanmic Rigidbody2D's position" 
/// The unity documentation suggests you use AddForce, or set a velocity to move it. However, in cases where we need to "instantly" set the position of a rigidbody,
/// these solutions are not favorable. 
/// 
/// This script tests 5 different ways of moving objects
/// 
/// VELOCITY: 
///     - Calculate the velocity that would cause the object to move from point A to point B in one physics step. 
/// POSITION:
///     - Sets rigidbody.position directly 
/// DYNAMIC MOVE POSITION:
///     - Moves rigidbody with the MovePosition function - a function the unity documentation suggests should mostly be used with Kinematic bodies
/// KINEMATIC MOVE POSITION:
///     - Changes the rigidbody to kinematic, then uses MovePosition
/// KINEMATIC POSITION:
///     - Changes the rigidbody to kinematic, sets rigidbody.position directly
/// FREEZE:
///     - Freezes all position and rotation of the rigidbody via constraints then sets rigidbody.position directly. 
///
/// Each function is listed below with a description of their ISSUES.
/// FREEZE is the fastest and most precise method i've tested. It accrues no error on the end position, is not effected by attached bodies, and does not need to change body types. 
/// </summary>
public class PhysicsMoverTest : MonoBehaviour
{
    public Transform target;
    public Rigidbody2D physicsBody;
    public Collider2D collider2d;
    public PhysicsMoverMode physicsMoverMode;

    public enum PhysicsMoverMode
    {
        Velocity,
        Position,
        DynamicMovePosition,
        KinematicMovePosition,
        KinematicPosition,
        Freeze
    }

    private bool tickOne = false;
    private bool tickTwo = false;

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            SetRandomAnchorPosition();
        }
    }

    private void FixedUpdate()
    {
        switch (physicsMoverMode)
        {
            case PhysicsMoverMode.Velocity:
                SetVelocity();
                break;
            case PhysicsMoverMode.Position:
                SetPosition();
                break;
            case PhysicsMoverMode.DynamicMovePosition:
                DynamicMovePosition();
                break;
            case PhysicsMoverMode.KinematicMovePosition:
                KinematicMovePosition();
                break;
            case PhysicsMoverMode.KinematicPosition:
                KinematicPosition();
                break;
            case PhysicsMoverMode.Freeze:
                Freeze();
                break;
        }
    }

    /// <summary>
    /// Sets the velocity so it covers the distance between the target position and the physics position in one tick.
    /// ISSUES: 
    /// - This does not account for forces affecting the body including:
    ///     - Linear drag
    ///     - Effectors in the scenes
    ///     - Bodies attached via Joints or Springs
    /// - Can hit objects inbetween (unless 
    /// </summary>
    private void SetVelocity()
    {
        if (tickTwo)
        {

            LogPositionDifference();
            // collider must be enabled / disabled before moving or else there is a possibility a collision will be detected by an object between the physicsBody and the target position
            collider2d.enabled = true;
            physicsBody.velocity = Vector2.zero;
            tickTwo = false;
        }

        if (tickOne)
        {
            // collider must be enabled / disabled before moving or else there is a possibility a collision will be detected by an object between the physicsBody and the target position
            collider2d.enabled = false;
            physicsBody.velocity = ((Vector2)target.position - physicsBody.position) * 1 / Time.fixedDeltaTime;
            tickOne = false;
            tickTwo = true;
        }
    }

    /// <summary>
    /// Sets the physicsBody.position directly
    /// ISSUES: 
    /// - This method does not account for forces on the body including:
    ///     - Effectors
    ///     - Bodies attached via Joints or Springs
    ///     
    /// NOTE:
    /// - Linear drag does NOT effect this movement
    /// - This does NOT need colliders to be enabled / disabled
    /// </summary>
    private void SetPosition()
    {
        if (tickTwo)
        {
            LogPositionDifference();
            physicsBody.velocity = Vector2.zero;
            tickTwo = false;
        }

        if (tickOne)
        {
            physicsBody.velocity = Vector2.zero;
            physicsBody.position = target.position;
            tickOne = false;
            tickTwo = true;
        }
    }

    /// <summary>
    /// Sets position by using MovePosition - a method intended to be used by a kinematic body: https://docs.unity3d.com/ScriptReference/Rigidbody2D.MovePosition.html
    /// This method shares the same issues as setting physicsBody.position as far as I can tell under this physics test.
    /// 
    /// ISSUES: 
    /// - This method does not account for forces on the body including:
    ///     - Effectors
    ///     - Bodies attached via Joints or Springs
    /// - Because of the way MovePosition works, it is possible that objects between the physics body and the target position could be affected by collision, UNLESS you disable colliders
    /// 
    /// NOTE:
    /// - Linear drag does NOT effect this movement
    /// </summary>
    private void DynamicMovePosition()
    {
        if (tickTwo)
        {
            LogPositionDifference();
            // collider must be enabled / disabled before moving or else there is a possibility a collision will be detected by an object between the physicsBody and the target position
            //collider2d.enabled = true;
            physicsBody.velocity = Vector2.zero;
            tickTwo = false;
        }

        if (tickOne)
        {
            tickOne = false;
            // collider must be enabled / disabled before moving or else there is a possibility a collision will be detected by an object between the physicsBody and the target position
            //collider2d.enabled = false;
            physicsBody.velocity = Vector2.zero;
            physicsBody.MovePosition(target.position);
            tickTwo = true;
        }
    }

    /// <summary>
    /// Sets the physicsBody position by first setting isKinematic to true, then using MovePosition.
    /// 
    /// ISSUES:
    /// - Changing a physicsBody to and from kinematic accrues a performance cost: https://docs.unity3d.com/Manual/class-Rigidbody2D.html
    ///   "Changing the Body Type of a Rigidbody 2D can be a tricky process. 
    ///     When a Body Type changes, various mass-related internal properties 
    ///     are recalculated immediately, and all existing contacts for the 
    ///     Collider 2Ds attached to the Rigidbody 2D need to be re-evaluated 
    ///     during the GameObject’s next FixedUpdate. 
    ///     Depending on how many contacts and Collider 2Ds are attached 
    ///     to the body, changing the Body Type can cause variations in performance."
    ///     
    /// - This method accrues a TINY floating point error - less than 0.000001f but enough to fail the == and Mathf.Approximately operator
    /// - Because MovePosition calculates a trajectory between point A and point B to move it WILL register collisions with other dynamic bodies in the scene https://docs.unity3d.com/ScriptReference/Rigidbody2D.MovePosition.html
    ///   UNLESS: you disable colliders
    /// </summary>
    private void KinematicMovePosition()
    {
        if (tickTwo)
        { 
            LogPositionDifference();
            // collider must be enabled / disabled before moving or else there is a possibility a collision will be detected by an object between the physicsBody and the target position
            collider2d.enabled = true;
            physicsBody.isKinematic = false;
            tickTwo = false;
        }

        if (tickOne)
        {
            physicsBody.velocity = Vector2.zero;
            // collider must be enabled / disabled before moving or else there is a possibility a collision will be detected by an object between the physicsBody and the target position
            collider2d.enabled = false;
            physicsBody.isKinematic = true;
            physicsBody.MovePosition(target.position);
            tickOne = false;
            tickTwo = true;
        }
    }

    /// <summary>
    /// Sets the physicsBody position by first setting isKinematic to true, then setting the position of the rigidbody directly.
    /// This accrues no error, is not effected by attached bodies, and does not collide with objects between point A and point B.
    /// ISSUES:
    /// - As stated above, this accrues a performance cost in setting your object to and from kinematic
    /// </summary>
    private void KinematicPosition()
    {
        if (tickTwo)
        {
            LogPositionDifference();
            physicsBody.isKinematic = false;
            tickTwo = false;
        }

        if (tickOne)
        {
            physicsBody.velocity = Vector2.zero;
            physicsBody.isKinematic = true;
            physicsBody.position = target.position;
            tickOne = false;
            tickTwo = true;
        }
    }


    private RigidbodyConstraints2D cachedConstraints;
    
    /// <summary>
    /// Utilizing the rigidbody constraints, we can "Freeze" all movement and rotation on the rigidbody, set its position, and then release it back into the simulation.
    /// This accrues NO error, is not effected by attached bodies, does not collide with objects between point A and point B. This is the solution I will be moving forward with. 
    /// </summary>
    private void Freeze()
    {
        if (tickTwo)
        {
            LogPositionDifference();
            physicsBody.constraints = cachedConstraints;
            tickTwo = false;
        }

        if (tickOne)
        {
            cachedConstraints = physicsBody.constraints;
            physicsBody.constraints = RigidbodyConstraints2D.FreezeAll;
            physicsBody.position = target.position;
            tickOne = false;
            tickTwo = true;
        }
    }

    private void SetRandomAnchorPosition()
    {
        target.position = Random.insideUnitCircle * 3;
        tickOne = true;
    }

    private void LogPositionDifference()
    {
        Vector2 physicsBodyPosition = physicsBody.position;
        Vector2 targetPosition = target.position;
        float distance = Vector2.Distance(physicsBodyPosition, targetPosition);

        if (distance == 0f)
        {
            Debug.Log(string.Format("{0}body: {1}, target: {2}, distance: {3}{4}", "<color=green>", physicsBodyPosition.ToString("F9"), targetPosition.ToString("F9"), distance.ToString("F9"), "</color>"));
        }
        else
        {
            Debug.Log(string.Format("{0}body: {1}, target: {2}, distance: {3}{4}", "<color=yellow>", physicsBodyPosition.ToString("F9"), targetPosition.ToString("F9"), distance.ToString("F9"), "</color>"));
        }
    }
}
