using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MainMovement : MonoBehaviour
{
    #region Beginning Setting
	//Check the conditions at the beginning.
    [SerializeField] private bool m_AirControl = false;             // Whether or not a player can steer while jumping;
	private bool m_Grounded;                                        // Whether or not the player is grounded.
	private bool m_FacingRight = true;                              // For determining which way the player is currently facing.
	private bool wasGrounded;                                       // Check if the avatar stay on ground in last frame.
    #endregion

    #region Check Collisions
	//Check collisions like walls, ground...
    [SerializeField] private LayerMask m_WhatIsGround;              // A mask determining what is ground to the character
	[SerializeField] private Transform m_GroundCheck;               // A position marking where to check if the player is grounded.
	[SerializeField] private Transform m_CeilingCheck;              // A position marking where to check for ceilings
	[SerializeField] private Collider2D m_CrouchDisableCollider;    // A collider that will be disabled when crouching
	const float k_GroundedRadius = .2f;                             // Radius of the overlap circle to determine if grounded
	const float k_CeilingRadius = .1f;                              // Radius of the overlap circle to determine if the player can stand up
    #endregion

    #region Get Components
	//Get components like RigidBody2D, material... and then using in Awake()
    private Rigidbody2D m_Rigidbody2D;
    #endregion

    #region Horizontal Movement Conditions
	//Horizontal movement (Also using smoooth merhod but not customized function).
    [Range(0f, .5f)] [SerializeField] private float m_MoveSmoothing = 0.1f;				// Amount of speed of movement smoothing.
	[Range(0f, 20f)] [SerializeField] private float m_RunSpeed = 2f;					// Amount of run speed.
	[Range(0f, .5f)] [SerializeField] private float squatMovementMultiplyer = 0.2f;     // A multiplyer to decrease the move speed.
	private float m_OriginalRunSpeed;
	private Vector2 m_RefVelocity = Vector2.zero;										// Reference velocity of smoothing method.(May use in very SmoothDamp method.)
    #endregion

    #region Dashing Conditions
	//Dashing addition
    [SerializeField] private float m_XDashSpeed = 50f;                              // Dashing speed as activating dash.
	[Range(0f, 1f)] [SerializeField] private float m_StartDashTime = .5f;           // Total duration time of dashing.
	[Range(0f, .3f)] [SerializeField] private float m_DashSmoothing = 0.1f;         // Amount of speed of dsahing smoothing.
	private float m_DashTime;														// Indentify processing time of dashing.
	private int m_DashDirection = 0;                                                // Directions of dash (8 ways).
	private int m_DashTimesNumber;										            // Dash times in the air.
	#endregion

	#region Squat Conditions
	// To see wether avatar is Squat or not.
	private bool m_wasSquating = false;
	#endregion

	#region Jump Conditions
	//Adjust jumping movement:
	[Range(0f, 40f)] [SerializeField] private float m_JumpVelocity = 20f;                // Amount of jump velocity that can give your avatar
	[Range(1f, 3f)] [SerializeField] private float m_FallMultiplyer = 2.5f;             // Amount of additional force give to avatar as falling
	[Range(1f, 3f)] [SerializeField] private float m_LowJumpMultiplyer = 2f;            // Amount of additional force give to avatar as releasing jump button
	//[Range(1f, 3f)] [SerializeField] private float m_SpeedUpFallingMultiplyer = 2f;	// Amount of additional force give to avatar as pressing crouch button
	[Range(0f, 0.15f)] [SerializeField] private float m_coyoteJumpDelayTime = 0.1f;     // Amount of coyote tiime that avatar can have.
	[Range(0f, .2f)] [SerializeField] private float m_JumpSmoothing = 0.1f;             // Amount of speed of jumping smoothing.
	private int m_JumpTimesNumber;														// Jump times (Even in the air, avatar can still jump)
	private bool m_coyoteJump;                                                          // Give coyote time to jump
	const float airControllTopSpeed = 9f;                                               // Top speed in air(!! But there still has some problem !!)
    #endregion

    #region Events
    // Using as animation events activate.
    [Header("Events")]
	[Space]

	public UnityEvent OnLandEvent;
	public UnityEvent InAirFallEvent;
	public UnityEvent OnSquatEvent;
	#endregion

	#region Awake()
	void Awake()
	{
		m_Rigidbody2D = GetComponent<Rigidbody2D>();
		m_DashTime = m_StartDashTime;
		m_OriginalRunSpeed = m_RunSpeed;

		if (OnLandEvent == null)
			OnLandEvent = new UnityEvent();

		if (OnSquatEvent == null)
			OnSquatEvent = new UnityEvent();

		if (InAirFallEvent == null)
			InAirFallEvent = new UnityEvent();
	}
	#endregion

	#region FixedUpdate()
	// Update is called once per frame (Fixed)
	// Attention, all physical movement should use in FixUpdate(), so the delta time is Time.fixdDeltaTime
	void FixedUpdate()
	{
		wasGrounded = m_Grounded;
		m_Grounded = false;

		// The player is grounded if a circlecast to the groundcheck position hits anything designated as ground
		// This can be done using layers instead but Sample Assets will not overwrite your project settings.
		Collider2D[] colliders = Physics2D.OverlapCircleAll(m_GroundCheck.position, k_GroundedRadius, m_WhatIsGround);

		if (colliders.Length > 0)
		{
			m_Grounded = true;
			m_JumpTimesNumber = 1;		// Double jump( = 1 ). If the avatar needs more jump times, just change the value of m_AnotherJumpTimes to 2,3...
			m_DashTimesNumber = 1;      // Dash in the air( = 1 ).

			if (!wasGrounded)
            {
				OnLandEvent.Invoke();
            }
		}
		else
		{
			if (wasGrounded)
            {
				StartCoroutine(CoyoteJumpDelay());
            }
		}

		//Detail axis-y move speed in the air.
		if (Mathf.Abs(m_Rigidbody2D.velocity.y) <= airControllTopSpeed)
		{
			//Falling
			if (m_Rigidbody2D.velocity.y <= 0)
			{
				m_Rigidbody2D.velocity += Vector2.up * Physics2D.gravity.y * (m_FallMultiplyer - 1) * Time.fixedDeltaTime;
			}
		}
	}
    #endregion

    #region Move
    // Button[moveLR] setting as button(left and right arrow). And move also means horizontal direction of the avatar.
    // moveLR can only be -1,0,1. (Input.GetAxisRaw("Horizontal"))
    // A method that maybe can help improve smooth is give m_RunSpeed a function of time.
    public void Move(float moveLR, bool squat)
	{
		if (m_Grounded || m_AirControl)
		{
			if (squat)
			{
				m_RunSpeed *= squatMovementMultiplyer;
			}
            else
            {
				m_RunSpeed = m_OriginalRunSpeed;

			}

			Vector2 targetVelocityMove = new Vector2(moveLR * m_RunSpeed, m_Rigidbody2D.velocity.y);
			m_Rigidbody2D.velocity = Vector2.SmoothDamp(m_Rigidbody2D.velocity, targetVelocityMove, ref m_RefVelocity, m_MoveSmoothing);

			// If the input is moving the player right and the player is facing left...
			if (moveLR == 1 && !m_FacingRight)
			{
				// ... flip the player.
				Flip();
			}
			// Otherwise if the input is moving the player left and the player is facing right...
			else if (moveLR == -1 && m_FacingRight)
			{
				// ... flip the player.
				Flip();
			}
		}
	}
    #endregion

    #region Dash
    // Button[dash] setting as button(x). (Set at Update() not FixedUpdate())
    // No Running, just DASH! 
    // Button[moveLR, moveUD] setting as button(left and right arrow, up and down arrow). Determind the directions of dash.
    // moveLR and moveUD can only be -1,0,1. (Input.GetAxisRaw("Horizontal") and Input.GetAxisRaw("Vertical"))
    public void Dash(bool dash, bool squat, float moveLR)
    {
		Vector2 targetVelocityDash = new Vector2(m_Rigidbody2D.velocity.x, m_Rigidbody2D.velocity.y);

		if (m_DashDirection == 0)
		{
			if (dash && m_DashTimesNumber > 0)
            {
				switch (moveLR)
				{
					case 1:
						m_DashDirection = 1;
						break;
					case -1:
						m_DashDirection = 2;
						break;
				}
                if (!wasGrounded)
                {
					m_DashTimesNumber--;
				}
            }
		}
		else
		{
			if (m_DashTime <= 0)
			{
				m_DashDirection = 0;
				m_DashTime = m_StartDashTime;
				m_Rigidbody2D.velocity = Vector2.zero;
			}
			else
			{
				m_DashTime -= Time.deltaTime;

				switch (m_DashDirection)
				{
					case 1:
						targetVelocityDash = Vector2.right * m_XDashSpeed;
						break;
					case 2:
						targetVelocityDash = Vector2.left * m_XDashSpeed;
						break;
				}
			}
		}
		m_Rigidbody2D.velocity = Vector2.SmoothDamp(m_Rigidbody2D.velocity, targetVelocityDash, ref m_RefVelocity, m_DashSmoothing);
	}
    #endregion

    #region Squat
    public void Squat(bool squat)
	{
		// If crouching, check to see if the character can stand up
		if (!squat)
		{
			// If the character has a ceiling preventing them from standing up, keep them crouching
			if (Physics2D.OverlapCircle(m_CeilingCheck.position, k_CeilingRadius, m_WhatIsGround))
			{
				squat = true;
			}
		}

		// If squating
		if (squat && m_Grounded)
		{
			if (!m_wasSquating)
			{
				m_wasSquating = true;
				OnSquatEvent.Invoke();
			}

			// Disable one of the colliders when crouching
			if (m_CrouchDisableCollider != null)
				m_CrouchDisableCollider.enabled = false;
		}
		else
		{
			// Enable the collider when not crouching
			if (m_CrouchDisableCollider != null)
				m_CrouchDisableCollider.enabled = true;

			if (m_wasSquating)
			{
				m_wasSquating = false;
				OnSquatEvent.Invoke();
			}
		}
	}
    #endregion

    #region Jump
    // Button[jump] setting as button(c), [jump: Input.Getbutton("Jump") amd moreJump: Input.GetButtonDown("Jump")]
    public void Jump(bool jump, bool moreJump, bool squat)
    {
		Vector2 targetVelocityJump = new Vector2(m_Rigidbody2D.velocity.x, m_Rigidbody2D.velocity.y);
		// If the player start to jump..
		if (jump)
		{
			if (m_Grounded && !squat)
			{
				m_Grounded = false;
				targetVelocityJump = new Vector2(m_Rigidbody2D.velocity.x, m_JumpVelocity);
			}
			else if (m_coyoteJump)
			{
				targetVelocityJump = new Vector2(m_Rigidbody2D.velocity.x, m_JumpVelocity);
			}
		}

        //Detail axis-y move speed in the air.
        if (Mathf.Abs(targetVelocityJump.y) <= airControllTopSpeed)
        {
            //Falling
            //if (targetVelocityJump.y <= 0)
            //{
            //    targetVelocityJump += Vector2.up * Physics2D.gravity.y * (m_FallMultiplyer - 1) * Time.fixedDeltaTime;
            //}
            //Rising up but releasing jump button. Remark: It's ok to write customized button setting here.
            if (!jump)
            {
                targetVelocityJump += Vector2.up * Physics2D.gravity.y * (m_LowJumpMultiplyer - 1) * Time.fixedDeltaTime;
            }
        }

        // Speed up as falling and double jump(=1)
        if (!m_Grounded && !wasGrounded)
		{
			if (moreJump && m_JumpTimesNumber > 0)
			{
				targetVelocityJump = new Vector2(m_Rigidbody2D.velocity.x, m_JumpVelocity);
				m_JumpTimesNumber--;
			}
			//else if (squat)
			//{
			//	targetVelocityJump += Vector2.up * Physics2D.gravity.y * (m_SpeedUpFallingMultiplyer - 1) * Time.fixedDeltaTime;
			//	InAirFallEvent.Invoke();
			//}
		}
		m_Rigidbody2D.velocity = Vector2.SmoothDamp(m_Rigidbody2D.velocity, targetVelocityJump, ref m_RefVelocity, m_JumpSmoothing);
	}
    #endregion

    #region Other Functions
    // Using System.Collections.Generic but not System.Collections would cause IEnumerator need one datatype<T>.
    private IEnumerator CoyoteJumpDelay()
	{
		m_coyoteJump = true;
		yield return new WaitForSeconds(m_coyoteJumpDelayTime);
		m_coyoteJump = false;
	}

	private void Flip()
	{
		// Switch the way the player is labelled as facing.
		m_FacingRight = !m_FacingRight;

		// Multiply the player's x local scale by -1.
		Vector3 theScale = transform.localScale;
		theScale.x *= -1;
		transform.localScale = theScale;
	}
    #endregion
}
